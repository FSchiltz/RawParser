using RawNet.Decoder.HuffmanCompressor;
using System;
using System.Diagnostics;

namespace RawNet.Decoder.Decompressor
{
    //Decompresses Lossless non subsampled JPEGs, with 2-4 components
    class LJPEGPlain : JPEGDecompressor
    {
        public bool CanonFlipDim { get; set; }    // Fix Canon 6D mRaw where width/height is flipped
        public bool CanonDoubleHeight { get; set; }  // Fix Canon double height on 4 components (EOS 5DS R)
        public bool WrappedCr2Slices { get; set; } // Fix Canon 80D mRaw where the slices are wrapped

        public LJPEGPlain(byte[] data, RawImage<ushort> img, bool UseBigTable, bool DNGCompatible) : this(new TiffBinaryReader(data), img, UseBigTable, DNGCompatible) { }
        public LJPEGPlain(TiffBinaryReader file, RawImage<ushort> img, bool DNGCompatible, bool UseBigTable) : base(file, img, DNGCompatible, UseBigTable)
        {
            CanonFlipDim = false;
            CanonDoubleHeight = false;
            huff = new HuffmanTable[4] {
                new HuffmanTable(UseBigTable, DNGCompatible),
                new HuffmanTable(UseBigTable, DNGCompatible) ,
                new HuffmanTable(UseBigTable, DNGCompatible) ,
                new HuffmanTable(UseBigTable, DNGCompatible)
            };
        }

        public override void DecodeScan()
        {
            // Fix for Canon 6D raw, which has flipped width & height for some part of the image
            // We temporarily swap width and height for cropping.
            if (CanonFlipDim)
            {
                uint w = frame.width;
                frame.width = frame.height;
                frame.height = w;
            }

            // If image attempts to decode beyond the image bounds, strip it.
            if ((frame.width * frame.numComponents + offX * raw.raw.cpp) > raw.raw.dim.width * raw.raw.cpp)
                skipX = ((frame.width * frame.numComponents + offX * raw.raw.cpp) - raw.raw.dim.width * raw.raw.cpp) / frame.numComponents;
            if (frame.height + offY > raw.raw.dim.height)
                skipY = frame.height + offY - raw.raw.dim.height;

            // Swap back (see above)
            if (CanonFlipDim)
            {
                uint w = frame.width;
                frame.width = frame.height;
                frame.height = w;
            }

            /* Correct wrong slice count (Canon G16) */
            if (slicesW.Count == 1)
                slicesW[0] = frame.width * frame.numComponents;

            if (slicesW.Count == 0)
                slicesW.Add(frame.width * frame.numComponents);

            if (0 == frame.height || 0 == frame.width)
                throw new RawDecoderException("decodeScan: Image width or height set to zero");

            for (UInt32 i = 0; i < frame.numComponents; i++)
            {
                if (frame.ComponentInfo[i].superH != 1 || frame.ComponentInfo[i].superV != 1)
                {
                    if (raw.isCFA)
                        throw new RawDecoderException("LJpegDecompressor::decodeScan: Cannot decode subsampled image to CFA data");

                    if (raw.raw.cpp != frame.numComponents)
                        throw new RawDecoderException("LJpegDecompressor::decodeScan: Subsampled component count does not match image.");

                    if (predictor == 1)
                    {
                        if (frame.ComponentInfo[0].superH == 2 && frame.ComponentInfo[0].superV == 2 &&
                            frame.ComponentInfo[1].superH == 1 && frame.ComponentInfo[1].superV == 1 &&
                            frame.ComponentInfo[2].superH == 1 && frame.ComponentInfo[2].superV == 1)
                        {
                            // Something like Cr2 sRaw1, use fast decoder
                            DecodeScanLeft4_2_0();
                            return;
                        }
                        else if (frame.ComponentInfo[0].superH == 2 && frame.ComponentInfo[0].superV == 1 &&
                                 frame.ComponentInfo[1].superH == 1 && frame.ComponentInfo[1].superV == 1 &&
                                 frame.ComponentInfo[2].superH == 1 && frame.ComponentInfo[2].superV == 1)
                        {
                            // Something like Cr2 sRaw2, use fast decoder
                            if (CanonFlipDim)
                                throw new RawDecoderException("LJpegDecompressor::decodeScan: Cannot flip non 4:2:2 subsampled images.");
                            DecodeScanLeft4_2_2();
                            return;
                        }
                        else
                        {
                            raw.errors.Add("LJpegDecompressor::decodeScan: Unsupported subsampling");
                            DecodeScanLeftGeneric();
                        }
                    }
                    else
                    {
                        throw new RawDecoderException("LJpegDecompressor::decodeScan: Unsupported prediction direction.");
                    }
                }
            }

            if (predictor == 1)
            {
                if (CanonFlipDim)
                    throw new RawDecoderException("LJpegDecompressor::decodeScan: Cannot flip non subsampled images.");
                /*if (raw.raw.dim.height * raw.pitch >= 1 << 28)
                {
                    DecodeScanLeftGeneric();
                    return;
                }*/
                if (frame.numComponents == 2)
                    DecodeScanLeft2Comps();
                else if (frame.numComponents == 3)
                    DecodeScanLeft3Comps();
                else if (frame.numComponents == 4)
                    DecodeScanLeft4Comps();
                else
                    throw new RawDecoderException("LJpegDecompressor::decodeScan: Unsupported component direction count.");
                return;
            }
            throw new RawDecoderException("LJpegDecompressor::decodeScan: Unsupported prediction direction.");
        }

        /**
        *  CR2 Slice handling:
        *  In the following code, canon slices are handled in-place, to avoid having to
        *  copy the entire frame afterwards.
        *  The "offset" array is created to easily map slice positions on to the output image.
        *  The offset array size is the number of slices multiplied by height.
        *  Each of these offsets are an offset into the destination image, and it also contains the
        *  slice number (shifted up 28 bits), so it is possible to retrieve the width of each slice.
        *  Every time "components" pixels has been processed the slice size is tested, and output offset
        *  is adjusted if needed. This makes slice handling very "light", since it involves a single
        *  counter, and a predictable branch.
        *  For unsliced images, add one slice with the width of the image.
        **/
        void DecodeScanLeftGeneric()
        {
            UInt32 comps = frame.numComponents;  // Components
            HuffmanTable[] dctbl = new HuffmanTable[4];   // Tables for up to 4 components         

            UInt32[] samplesH = new UInt32[4];
            UInt32[] samplesV = new uint[4];

            UInt32 maxSuperH = 1;
            UInt32 maxSuperV = 1;
            UInt32[] samplesComp = new UInt32[4]; // How many samples per group does this component have
            UInt32 pixGroup = 0;   // How many pixels per group.

            for (UInt32 i = 0; i < comps; i++)
            {
                dctbl[i] = huff[frame.ComponentInfo[i].dcTblNo];
                samplesH[i] = frame.ComponentInfo[i].superH;
                if (!Common.IsPowerOfTwo(samplesH[i]))
                    throw new RawDecoderException("decodeScanLeftGeneric: Horizontal sampling is not power of two.");
                maxSuperH = Math.Max(samplesH[i], maxSuperH);
                samplesV[i] = frame.ComponentInfo[i].superV;
                if (!Common.IsPowerOfTwo(samplesV[i]))
                    throw new RawDecoderException("decodeScanLeftGeneric: Vertical sampling is not power of two.");
                maxSuperV = Math.Max(samplesV[i], maxSuperV);
                samplesComp[i] = samplesV[i] * samplesH[i];
                pixGroup += samplesComp[i];
            }

            raw.metadata.Subsampling.width = maxSuperH;
            raw.metadata.Subsampling.height = maxSuperV;

            //Prepare slices (for CR2)
            int slices = slicesW.Count * (int)((frame.height - skipY) / maxSuperV);
            uint[] imagePos = new uint[(slices + 1)];
            uint[] sliceWidth = new uint[(slices + 1)];

            uint t_y = 0;
            uint t_x = 0;
            uint t_s = 0;
            uint slice = 0;
            uint[] slice_width = new uint[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (int i = 0; i < slicesW.Count; i++)
                slice_width[i] = slicesW[i] / frame.numComponents; // This is a guess, but works for sRaw1+2.

            if (skipX != 0 && (maxSuperV > 1 || maxSuperH > 1))
            {
                throw new RawDecoderException("decodeScanLeftGeneric: Cannot skip right border in subsampled mode");
            }
            if (skipX != 0)
            {
                slice_width[slicesW.Count - 1] -= skipX;
            }

            for (slice = 0; slice < slices; slice++)
            {
                imagePos[slice] = t_x + offX + ((offY + t_y) * raw.raw.dim.width);
                sliceWidth[slice] = slice_width[t_s];
                t_y += maxSuperV;
                if (t_y >= (frame.height - skipY))
                {
                    t_y = 0;
                    t_x += slice_width[t_s++];
                }
            }
            slice_width = null;

            // We check the final position. If bad slice sizes are given we risk writing outside the image
            if (imagePos[slices - 1] - imagePos[0] >= raw.raw.dim.width * raw.raw.dim.height)
            {
                throw new RawDecoderException("decodeScanLeft: Last slice out of bounds");
            }

            imagePos[slices] = imagePos[slices - 1];      // Extra offset to avoid branch in loop.
            sliceWidth[slices] = sliceWidth[slices - 1];        // Extra offset to avoid branch in loop.

            // Predictors for components
            long[] p = new long[4];
            long dest = imagePos[0];

            // Always points to next slice
            slice = 1;
            long pixInSlice = sliceWidth[0];

            // Initialize predictors and decode one group.pitch
            uint x = 0;
            long predict = dest;          // Prediction pointer
            for (uint i = 0; i < comps; i++)
            {
                for (uint y2 = 0; y2 < samplesV[i]; y2++)
                {
                    for (uint x2 = 0; x2 < samplesH[i]; x2++)
                    {
                        // First pixel is not predicted, all other are.
                        if (y2 == 0 && x2 == 0)
                        {
                            p[i] = (1 << (int)(frame.precision - Pt - 1)) + dctbl[i].Decode();
                            Debug.Assert(p[i] >= 0 && p[i] < 65536);
                            raw.raw.rawView[dest] = (ushort)p[i];
                        }
                        else
                        {
                            p[i] += dctbl[i].Decode();
                            Debug.Assert(p[i] >= 0 && p[i] < 65536);
                            raw.raw.rawView[dest + (x2 * comps) + (y2 * raw.raw.dim.width)] = (ushort)p[i];
                        }
                    }
                }
                // Set predictor for this component
                // Next component
                dest++;
            }

            // Increment destination to next group
            dest += (maxSuperH - 1) * comps;
            x = maxSuperH;
            pixInSlice -= maxSuperH;

            uint cw = frame.width - skipX;
            for (uint y = 0; y < (frame.height - skipY); y += maxSuperV)
            {
                for (; x < cw; x += maxSuperH)
                {

                    if (0 == pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("Ran out of slices");
                        pixInSlice = sliceWidth[slice];
                        dest = imagePos[slice];  // Adjust destination for next pixel
                        slice++;
                        // If new are at the start of a new line, also update predictors.
                        if (x == 0)
                            predict = dest;
                    }

                    for (int i = 0; i < comps; i++)
                    {
                        for (int y2 = 0; y2 < samplesV[i]; y2++)
                        {
                            for (int x2 = 0; x2 < samplesH[i]; x2++)
                            {
                                p[i] += dctbl[i].Decode();
                                Debug.Assert(p[i] >= 0 && p[i] < 65536);
                                raw.raw.rawView[dest + (x2 * comps) + (y2 * raw.raw.dim.width)] = (ushort)p[i];
                            }
                        }
                        dest++;
                    }
                    dest += (maxSuperH * comps) - comps;
                    pixInSlice -= maxSuperH;
                }

                if (skipX != 0)
                {
                    for (UInt32 sx = 0; sx < skipX; sx++)
                    {
                        for (UInt32 i = 0; i < comps; i++)
                        {
                            dctbl[i].Decode();
                        }
                    }
                }

                // Update predictors
                for (UInt32 i = 0; i < comps; i++)
                {
                    p[i] = raw.raw.rawView[predict + i];
                    // Ensure, that there is a slice shift at new line
                    if (!(pixInSlice == 0 || maxSuperV == 1))
                        throw new RawDecoderException("decodeScanLeftGeneric: Slice not placed at new line");
                }
                predict = dest;
                x = 0;
            }
        }


        //These are often used compression schemes, heavily optimized to decode
        unsafe void DecodeScanLeft4_2_0()
        {
            uint COMPS = 3;
            Debug.Assert(slicesW.Count < 16);  // We only have 4 bits for slice number.
            Debug.Assert(!(slicesW.Count > 1 && skipX != 0)); // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[0].superH == 2);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[0].superV == 2);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[1].superH == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[1].superV == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[2].superH == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[2].superV == 1);   // Check if this is a valid state
            Debug.Assert(frame.numComponents == COMPS);
            Debug.Assert(skipX == 0);

            HuffmanTable dctbl1 = huff[frame.ComponentInfo[0].dcTblNo];
            HuffmanTable dctbl2 = huff[frame.ComponentInfo[1].dcTblNo];
            HuffmanTable dctbl3 = huff[frame.ComponentInfo[2].dcTblNo];

            UInt16* predict;      // Prediction pointer

            raw.metadata.Subsampling.width = 2;
            raw.metadata.Subsampling.height = 2;
            fixed (ushort* d = raw.raw.rawView)
            {
                //TODO remove this hack
                byte* draw = (byte*)d;
                // Fix for Canon 6D raw, which has flipped width & height
                UInt32 real_h = CanonFlipDim ? frame.width : frame.height;

                //Prepare slices (for CR2)
                int slices = slicesW.Count * (int)(real_h - skipY) / 2;

                long[] offset = new long[(slices + 1)];

                UInt32 t_y = 0;
                UInt32 t_x = 0;
                UInt32 t_s = 0;
                UInt32 slice = 0;
                UInt32 pitch_s = raw.pitch / 2;  // Pitch in shorts

                uint[] slice_width = new uint[slices];

                // This is divided by comps, since comps pixels are processed at the time
                for (int i = 0; i < slicesW.Count; i++)
                    slice_width[i] = slicesW[i] / COMPS;

                for (slice = 0; slice < slices; slice++)
                {
                    offset[slice] = ((t_x + offX) * raw.Bpp + ((offY + t_y) * raw.pitch)) | (t_s << 28);
                    Debug.Assert((offset[slice] & 0x0fffffff) < raw.pitch * raw.raw.dim.height);
                    t_y += 2;
                    if (t_y >= (real_h - skipY))
                    {
                        t_y = 0;
                        t_x += slice_width[t_s++];
                    }
                }

                // We check the final position. If bad slice sizes are given we risk writing outside the image
                if ((offset[slices - 1] & 0x0fffffff) >= raw.pitch * raw.raw.dim.height)
                {
                    throw new RawDecoderException("decodeScanLeft: Last slice out of bounds");
                }

                offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

                if (skipX != 0)
                    slice_width[slicesW.Count - 1] -= skipX;

                // Predictors for components
                UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];

                // Always points to next slice
                slice = 1;
                UInt32 pixInSlice = slice_width[0];

                // Initialize predictors and decode one group.
                int x = 0, p1, p2, p3;
                // First pixel is not predicted, all other are.
                p1 = (1 << (int)(frame.precision - Pt - 1)) + dctbl1.Decode();
                *dest = (ushort)p1;
                p1 = dest[COMPS] = (ushort)(p1 + dctbl1.Decode());
                p1 = dest[pitch_s] = (ushort)(p1 + dctbl1.Decode());
                p1 = dest[COMPS + pitch_s] = (ushort)(p1 + dctbl1.Decode());
                predict = dest;
                p2 = (1 << (int)(frame.precision - Pt - 1)) + dctbl2.Decode();
                dest[1] = (ushort)p2;
                p3 = (1 << (int)(frame.precision - Pt - 1)) + dctbl3.Decode();
                dest[2] = (ushort)p3;

                // Skip next
                dest += COMPS * 2;

                x = 2;
                pixInSlice -= 2;

                UInt32 cw = frame.width - skipX;
                for (int y = 0; y < (frame.height - skipY); y += 2)
                {
                    for (; x < cw; x += 2)
                    {

                        if (0 == pixInSlice)
                        { // Next slice
                            if (slice > slices)
                                throw new RawDecoderException("decodeScanLeft: Ran out of slices");
                            long o = offset[slice++];
                            dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                            Debug.Assert((o & 0x0fffffff) < raw.pitch * raw.raw.dim.height);
                            if ((o & 0x0fffffff) > raw.pitch * raw.raw.dim.height)
                                throw new RawDecoderException("decodeScanLeft: Offset out of bounds");
                            pixInSlice = slice_width[o >> 28];

                            // If new are at the start of a new line, also update predictors.
                            if (x == 0)
                            {
                                predict = dest;
                            }
                        }
                        p1 += dctbl1.Decode();
                        *dest = (ushort)p1;
                        p1 += dctbl1.Decode();
                        dest[COMPS] = (ushort)p1;
                        p1 += dctbl1.Decode();
                        dest[pitch_s] = (ushort)p1;
                        p1 += dctbl1.Decode();
                        dest[pitch_s + COMPS] = (ushort)p1;

                        p2 = p2 + dctbl2.Decode();
                        dest[1] = (ushort)p2;
                        p3 = p3 + dctbl3.Decode();
                        dest[2] = (ushort)p2;

                        dest += COMPS * 2;
                        pixInSlice -= 2;
                    }

                    // Update predictors
                    p1 = predict[0];
                    p2 = predict[1];
                    p3 = predict[2];
                    Debug.Assert(pixInSlice == 0);  // Ensure, that there is a slice shift at new line           

                    x = 0;
                }
            }
        }

        unsafe void DecodeScanLeft4_2_2()
        {
            int COMPS = 3;
            Debug.Assert(slicesW.Count < 16);  // We only have 4 bits for slice number.
            Debug.Assert(!(slicesW.Count > 1 && skipX != 0)); // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[0].superH == 2);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[0].superV == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[1].superH == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[1].superV == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[2].superH == 1);   // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[2].superV == 1);   // Check if this is a valid state
            Debug.Assert(frame.numComponents == COMPS);
            Debug.Assert(skipX == 0);
            HuffmanTable dctbl1 = huff[frame.ComponentInfo[0].dcTblNo];
            HuffmanTable dctbl2 = huff[frame.ComponentInfo[1].dcTblNo];
            HuffmanTable dctbl3 = huff[frame.ComponentInfo[2].dcTblNo];

            raw.metadata.Subsampling.width = 2;
            raw.metadata.Subsampling.height = 1;

            UInt16* predict;      // Prediction pointer

            fixed (ushort* d = raw.raw.rawView)
            {
                //TODO remove this hack
                byte* draw = (byte*)d;

                //Prepare slices (for CR2)
                Int32 slices = slicesW.Count * (int)(frame.height - skipY);

                long[] offset = new long[(slices + 1)];

                UInt32 t_y = 0;
                UInt32 t_x = 0;
                UInt32 t_s = 0;
                UInt32 slice = 0;
                uint[] slice_width = new uint[slices];

                // This is divided by comps, since comps pixels are processed at the time
                for (int i = 0; i < slicesW.Count; i++)
                    slice_width[i] = slicesW[i] / 2;

                for (slice = 0; slice < slices; slice++)
                {
                    offset[slice] = ((t_x + offX) * raw.Bpp + ((offY + t_y) * raw.pitch)) | (t_s << 28);
                    Debug.Assert((offset[slice] & 0x0fffffff) < raw.pitch * raw.raw.dim.height);
                    t_y++;
                    if (t_y >= (frame.height - skipY))
                    {
                        t_y = 0;
                        t_x += slice_width[t_s++];
                    }
                }
                if ((offset[slices - 1] & 0x0fffffff) >= raw.pitch * raw.raw.dim.height)
                {
                    throw new RawDecoderException("decodeScanLeft: Last slice out of bounds");
                }

                offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

                if (skipX != 0)
                    slice_width[slicesW.Count - 1] -= skipX;

                // Predictors for components
                UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];

                // Always points to next slice
                slice = 1;
                UInt32 pixInSlice = slice_width[0];

                // Initialize predictors and decode one group.
                UInt32 x = 0;
                int p1;
                int p2;
                int p3;
                // First pixel is not predicted, all other are.
                p1 = (1 << (int)(frame.precision - Pt - 1)) + dctbl1.Decode();
                *dest = (ushort)p1;
                p1 = p1 + dctbl1.Decode();
                dest[COMPS] = (ushort)p1;
                predict = dest;
                p2 = (1 << (int)(frame.precision - Pt - 1)) + dctbl2.Decode();
                dest[1] = (ushort)p2;
                p3 = (1 << (int)(frame.precision - Pt - 1)) + dctbl3.Decode();
                dest[2] = (ushort)p3;

                // Skip to next
                dest += COMPS * 2;

                x = 2;
                pixInSlice -= 2;

                UInt32 cw = frame.width - skipX;
                for (int y = 0; y < (frame.height - skipY); y++)
                {
                    for (; x < cw; x += 2)
                    {

                        if (0 == pixInSlice)
                        { // Next slice
                            if (slice > slices)
                                throw new RawDecoderException("decodeScanLeft: Ran out of slices");
                            long o = offset[slice++];
                            dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                            if ((o & 0x0fffffff) > raw.pitch * raw.raw.dim.height)
                                throw new RawDecoderException("decodeScanLeft: Offset out of bounds");
                            pixInSlice = slice_width[o >> 28];

                            // If new are at the start of a new line, also update predictors.
                            if (x == 0)
                            {
                                predict = dest;
                            }
                        }
                        p1 += dctbl1.Decode();
                        *dest = (ushort)p1;
                        p1 += dctbl1.Decode();
                        dest[COMPS] = (ushort)p1;
                        p2 = p2 + dctbl2.Decode();
                        dest[1] = (ushort)p2;
                        p3 = p3 + dctbl3.Decode();
                        dest[2] = (ushort)p3;

                        dest += COMPS * 2;
                        pixInSlice -= 2;
                    }

                    // Update predictors
                    p1 = predict[0];
                    p2 = predict[1];
                    p3 = predict[2];
                    predict = dest;
                    x = 0;
                }
            }
        }

        unsafe void DecodeScanLeft2Comps()
        {
            uint COMPS = 2;
            Debug.Assert(slicesW.Count < 16);  // We only have 4 bits for slice number.
            Debug.Assert(!(slicesW.Count > 1 && skipX != 0)); // Check if this is a valid state
            fixed (ushort* draw = raw.raw.rawView)
            {
                // First line
                HuffmanTable dctbl1 = huff[frame.ComponentInfo[0].dcTblNo];
                HuffmanTable dctbl2 = huff[frame.ComponentInfo[1].dcTblNo];

                //Prepare slices (for CR2)
                Int32 slices = slicesW.Count * (int)(frame.height - skipY);
                long[] offset = new long[(slices + 1)];

                UInt32 t_y = 0;
                UInt32 t_x = 0;
                UInt32 t_s = 0;
                UInt32 slice = 0;
                UInt32 cw = frame.width - skipX;
                for (slice = 0; slice < slices; slice++)
                {
                    offset[slice] = (t_x + offX + ((offY + t_y) * raw.raw.dim.width)) | (t_s << 28);
                    Debug.Assert((offset[slice] & 0x0fffffff) < raw.raw.dim.width * raw.raw.dim.height);
                    t_y++;
                    if (t_y == (frame.height - skipY))
                    {
                        t_y = 0;
                        t_x += slicesW[(int)t_s++];
                    }
                }
                // We check the final position. If bad slice sizes are given we risk writing outside the image
                if ((offset[slices - 1] & 0x0fffffff) >= raw.raw.dim.width * raw.raw.dim.height)
                {
                    throw new RawDecoderException("decodeScanLeft: Last slice out of bounds");
                }
                offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

                uint[] slice_width = new uint[slices];

                // This is divided by comps, since comps pixels are processed at the time
                for (Int32 i = 0; i < slicesW.Count; i++)
                    slice_width[i] = slicesW[i] / COMPS;

                if (skipX != 0)
                    slice_width[slicesW.Count - 1] -= skipX;

                // First pixels are obviously not predicted
                int p1;
                int p2;
                UInt16* dest = &draw[offset[0] & 0x0fffffff];
                UInt16* predict = dest;
                p1 = (1 << (int)(frame.precision - Pt - 1)) + dctbl1.Decode();
                *dest++ = (ushort)p1;
                p2 = (1 << (int)(frame.precision - Pt - 1)) + dctbl2.Decode();
                *dest++ = (ushort)p2;

                slice = 1;    // Always points to next slice
                UInt32 pixInSlice = slice_width[0] - 1;  // Skip first pixel

                int x = 1;                            // Skip first pixels on first line.
                for (int y = 0; y < (frame.height - skipY); y++)
                {
                    for (; x < cw; x++)
                    {
                        p1 += dctbl1.Decode();
                        *dest++ = (ushort)p1;
                        //    Debug.Assert(p1 >= 0 && p1 < 65536);

                        p2 += dctbl2.Decode();
                        *dest++ = (ushort)p2;
                        //      Debug.Assert(p2 >= 0 && p2 < 65536);

                        if (0 == --pixInSlice)
                        { // Next slice
                            if (slice > slices)
                                throw new RawDecoderException("decodeScanLeft: Ran out of slices");
                            long o = offset[slice++];
                            dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                            if ((o & 0x0fffffff) > raw.pitch * raw.raw.dim.height)
                                throw new RawDecoderException("decodeScanLeft: Offset out of bounds");
                            pixInSlice = slice_width[o >> 28];
                        }
                    }

                    if (skipX != 0)
                    {
                        for (UInt32 i = 0; i < skipX; i++)
                        {
                            dctbl1.Decode();
                            dctbl2.Decode();
                        }
                    }

                    p1 = predict[0];  // Predictors for next row
                    p2 = predict[1];
                    predict = dest;  // Adjust destination for next prediction
                    x = 0;
                }
            }
        }

        unsafe void DecodeScanLeft3Comps()
        {
            uint COMPS = 3;
            fixed (ushort* d = raw.raw.rawView)
            {
                //TODO remove this hack
                byte* draw = (byte*)d;
                // First line
                HuffmanTable dctbl1 = huff[frame.ComponentInfo[0].dcTblNo];
                HuffmanTable dctbl2 = huff[frame.ComponentInfo[1].dcTblNo];
                HuffmanTable dctbl3 = huff[frame.ComponentInfo[2].dcTblNo];

                //Prepare slices (for CR2)
                Int32 slices = slicesW.Count * (int)(frame.height - skipY);
                long[] offset = new long[(slices + 1)];

                UInt32 t_y = 0;
                UInt32 t_x = 0;
                UInt32 t_s = 0;
                UInt32 slice = 0;
                for (slice = 0; slice < slices; slice++)
                {
                    offset[slice] = ((t_x + offX) * raw.Bpp + ((offY + t_y) * raw.pitch)) | (t_s << 28);
                    Debug.Assert((offset[slice] & 0x0fffffff) < raw.pitch * raw.raw.dim.height);
                    t_y++;
                    if (t_y == (frame.height - skipY))
                    {
                        t_y = 0;
                        t_x += slicesW[(int)t_s++];
                    }
                }
                // We check the final position. If bad slice sizes are given we risk writing outside the image
                if ((offset[slices - 1] & 0x0fffffff) >= raw.pitch * raw.raw.dim.height)
                {
                    throw new RawDecoderException("decodeScanLeft: Last slice out of bounds");
                }

                offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

                uint[] slice_width = new uint[slices];

                // This is divided by comps, since comps pixels are processed at the time
                for (Int32 i = 0; i < slicesW.Count; i++)
                    slice_width[i] = slicesW[i] / COMPS;

                if (skipX != 0)
                    slice_width[slicesW.Count - 1] -= skipX;

                // First pixels are obviously not predicted
                int p1;
                int p2;
                int p3;
                UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];
                UInt16* predict = dest;
                p1 = (1 << (int)(frame.precision - Pt - 1)) + dctbl1.Decode();
                *dest++ = (ushort)p1;
                p2 = (1 << (int)(frame.precision - Pt - 1)) + dctbl2.Decode();
                *dest++ = (ushort)p2;
                p3 = (1 << (int)(frame.precision - Pt - 1)) + dctbl3.Decode();
                *dest++ = (ushort)p3;

                slice = 1;
                UInt32 pixInSlice = slice_width[0] - 1;

                UInt32 cw = frame.width - skipX;
                UInt32 x = 1;                            // Skip first pixels on first line.

                for (int y = 0; y < (frame.height - skipY); y++)
                {
                    for (; x < cw; x++)
                    {
                        p1 += dctbl1.Decode();
                        *dest++ = (UInt16)p1;

                        p2 += dctbl2.Decode();
                        *dest++ = (UInt16)p2;

                        p3 += dctbl3.Decode();
                        *dest++ = (UInt16)p3;

                        if (0 == --pixInSlice)
                        { // Next slice
                            if (slice > slices)
                                throw new RawDecoderException("decodeScanLeft: Ran out of slices");
                            long o = offset[slice++];
                            dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                            if ((o & 0x0fffffff) > raw.pitch * raw.raw.dim.height)
                                throw new RawDecoderException("decodeScanLeft: Offset out of bounds");
                            Debug.Assert((o >> 28) < slicesW.Count);
                            pixInSlice = slice_width[o >> 28];
                        }
                    }

                    if (skipX != 0)
                    {
                        for (int i = 0; i < skipX; i++)
                        {
                            dctbl1.Decode();
                            dctbl2.Decode();
                            dctbl3.Decode();
                        }
                    }

                    p1 = predict[0];  // Predictors for next row
                    p2 = predict[1];
                    p3 = predict[2];  // Predictors for next row
                    predict = dest;  // Adjust destination for next prediction
                    x = 0;
                }
            }

        }

        unsafe void DecodeScanLeft4Comps()
        {
            uint COMPS = 4;
            // First line
            HuffmanTable dctbl1 = huff[frame.ComponentInfo[0].dcTblNo];
            HuffmanTable dctbl2 = huff[frame.ComponentInfo[1].dcTblNo];
            HuffmanTable dctbl3 = huff[frame.ComponentInfo[2].dcTblNo];
            HuffmanTable dctbl4 = huff[frame.ComponentInfo[3].dcTblNo];

            if (CanonDoubleHeight)
            {
                frame.height *= 2;
                raw.raw.dim = new Point2D(frame.width * 2, frame.height);
                raw.Init(false);
            }
            fixed (ushort* d = raw.raw.rawView)
            {
                //TODO remove this hack
                byte* draw = (byte*)d;

                //Prepare slices (for CR2)
                Int32 slices = slicesW.Count * (int)(frame.height - skipY);
                long[] offset = new long[(slices + 1)];

                UInt32 t_y = 0;
                UInt32 t_x = 0;
                UInt32 t_s = 0;
                UInt32 slice = 0;
                for (slice = 0; slice < slices; slice++)
                {
                    offset[slice] = ((t_x + offX) * raw.Bpp + ((offY + t_y) * raw.pitch)) | (t_s << 28);
                    Debug.Assert((offset[slice] & 0x0fffffff) < raw.pitch * raw.raw.dim.height);
                    t_y++;
                    if (t_y == (frame.height - skipY))
                    {
                        t_y = 0;
                        t_x += slicesW[(int)t_s++];
                    }
                }
                // We check the final position. If bad slice sizes are given we risk writing outside the image
                if ((offset[slices - 1] & 0x0fffffff) >= raw.pitch * raw.raw.dim.height)
                {
                    throw new RawDecoderException("decodeScanLeft: Last slice out of bounds");
                }
                offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

                uint[] slice_width = new uint[slices];

                // This is divided by comps, since comps pixels are processed at the time
                for (Int32 i = 0; i < slicesW.Count; i++)
                    slice_width[i] = slicesW[i] / COMPS;

                if (skipX != 0)
                    slice_width[slicesW.Count - 1] -= skipX;

                // First pixels are obviously not predicted
                int p1;
                int p2;
                int p3;
                int p4;
                UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];
                UInt16* predict = dest;
                p1 = (1 << (int)(frame.precision - Pt - 1)) + dctbl1.Decode();
                *dest++ = (ushort)p1;
                p2 = (1 << (int)(frame.precision - Pt - 1)) + dctbl2.Decode();
                *dest++ = (ushort)p2;
                p3 = (1 << (int)(frame.precision - Pt - 1)) + dctbl3.Decode();
                *dest++ = (ushort)p3;
                p4 = (1 << (int)(frame.precision - Pt - 1)) + dctbl4.Decode();
                *dest++ = (ushort)p4;

                slice = 1;
                UInt32 pixInSlice = slice_width[0] - 1;

                UInt32 cw = frame.width - skipX;
                UInt32 x = 1;                            // Skip first pixels on first line.

                if (CanonDoubleHeight)
                    skipY = frame.height >> 1;

                for (UInt32 y = 0; y < (frame.height - skipY); y++)
                {
                    for (; x < cw; x++)
                    {
                        p1 += dctbl1.Decode();
                        *dest++ = (UInt16)p1;

                        p2 += dctbl2.Decode();
                        *dest++ = (UInt16)p2;

                        p3 += dctbl3.Decode();
                        *dest++ = (UInt16)p3;

                        p4 += dctbl4.Decode();
                        *dest++ = (UInt16)p4;

                        if (0 == --pixInSlice)
                        { // Next slice
                            if (slice > slices)
                                throw new RawDecoderException("decodeScanLeft: Ran out of slices");
                            long o = offset[slice++];
                            dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                            if ((o & 0x0fffffff) > raw.pitch * raw.raw.dim.height)
                                throw new RawDecoderException("decodeScanLeft: Offset out of bounds");
                            pixInSlice = slice_width[o >> 28];
                        }
                    }
                    if (skipX != 0)
                    {
                        for (UInt32 i = 0; i < skipX; i++)
                        {
                            dctbl1.Decode();
                            dctbl2.Decode();
                            dctbl3.Decode();
                            dctbl4.Decode();
                        }
                    }
                    p1 = predict[0];  // Predictors for next row
                    p2 = predict[1];
                    p3 = predict[2];  // Predictors for next row
                    p4 = predict[3];
                    predict = dest;  // Adjust destination for next prediction
                    x = 0;
                }
            }
        }
    }
}
