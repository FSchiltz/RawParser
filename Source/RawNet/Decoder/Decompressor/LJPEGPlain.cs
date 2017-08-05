using PhotoNet.Common;
using RawNet.Decoder.HuffmanCompressor;
using System;
using System.Diagnostics;

namespace RawNet.Decoder.Decompressor
{
    class LJPEGPlain : JPEGDecompressor
    {
        public LJPEGPlain(byte[] data, RawImage img, bool UseBigTable, bool DNGCompatible) : this(new ImageBinaryReader(data), img, UseBigTable, DNGCompatible) { }
        public LJPEGPlain(ImageBinaryReader file, RawImage img, bool DNGCompatible, bool UseBigTable) : base(file, img, DNGCompatible, UseBigTable)
        {
            huff = new HuffmanTable[4] {
                new HuffmanTable(UseBigTable, DNGCompatible),
                new HuffmanTable(UseBigTable, DNGCompatible) ,
                new HuffmanTable(UseBigTable, DNGCompatible) ,
                new HuffmanTable(UseBigTable, DNGCompatible)
            };
        }

        public override void DecodeScan()
        {
            if (predictor != 1)
                throw new RawDecoderException("Unsupported prediction direction.");

            if (frame.height == 0 || frame.width == 0)
                throw new RawDecoderException("Image width or height set to zero");

            if (slicesW.Count == 0)
                slicesW.Add(frame.width * frame.numComponents);

            bool isSubSampled = false;
            for (uint i = 0; i < frame.numComponents; i++)
                isSubSampled |= frame.ComponentInfo[i].superH != 1 || frame.ComponentInfo[i].superV != 1;

            if (isSubSampled)
            {
                if (raw.isCFA)
                    throw new RawDecoderException("Cannot decode subsampled image to CFA data");

                if (raw.fullSize.cpp != frame.numComponents)
                    throw new RawDecoderException("Subsampled component count does not match image.");

                if (frame.numComponents != 3 || frame.ComponentInfo[0].superH != 2 ||
                    (frame.ComponentInfo[0].superV != 2 && frame.ComponentInfo[0].superV != 1) ||
                    frame.ComponentInfo[1].superH != 1 || frame.ComponentInfo[1].superV != 1 ||
                    frame.ComponentInfo[2].superH != 1 || frame.ComponentInfo[2].superV != 1)
                    throw new RawDecoderException("Unsupported subsampling");

                if (frame.ComponentInfo[0].superV == 2)
                {
                    // Something like Cr2 sRaw1, use fast decoder
                    DecodeN_X_Y(3, 2, 2);
                }
                else // frame.compInfo[0].superV == 1
                {
                    // Something like Cr2 sRaw2, use fast decoder
                    DecodeN_X_Y(3, 2, 1);
                }
            }
            else
            {
                //this will be useful for optimisation
                if (frame.numComponents == 2)
                    Decode2_1_1();
                else /*if (frame.numComponents == 3)
                    decodeN_X_Y(3, 1, 1);
                else if (frame.numComponents == 4)
                    decodeN_X_Y(4, 1, 1);
                else
                    throw new RawDecoderException("LJpegDecompressor::decodeScan: Unsupported component direction count.");*/
                    DecodeN_X_Y((int)frame.numComponents, 1, 1);
            }
        }

        // N_COMP == number of components (2, 3 or 4)
        // X_S_F  == x/horizontal sampling factor (1 or 2)
        // Y_S_F  == y/vertical   sampling factor (1 or 2)
        void DecodeN_X_Y(int N_COMP, int X_S_F, int Y_S_F)
        {
            Debug.Assert(frame.ComponentInfo[0].superH == X_S_F);
            Debug.Assert(frame.ComponentInfo[0].superV == Y_S_F);
            Debug.Assert(frame.ComponentInfo[1].superH == 1);
            Debug.Assert(frame.ComponentInfo[1].superV == 1);
            Debug.Assert(frame.numComponents == N_COMP);

            HuffmanTable[] ht = new HuffmanTable[N_COMP];
            for (int i = 0; i < N_COMP; ++i)
                ht[i] = huff[frame.ComponentInfo[i].dcTblNo];

            // Initialize predictors
            long[] p = new long[N_COMP];
            for (int i = 0; i < N_COMP; ++i)
                p[i] = (1 << (frame.precision - Pt - 1));

            BitPumpJPEG bitStream = new BitPumpJPEG(input);
            uint pixelPitch = raw.pitch / 2; // Pitch in pixel
            if (frame.numComponents != 3 && frame.width * frame.numComponents > 2 * frame.height)
            {
                // Fix Canon double height issue where Canon doubled the width and halfed
                // the height (e.g. with 5Ds), ask Canon. frame.w needs to stay as is here
                // because the number of pixels after which the predictor gets updated is
                // still the doubled width.
                // see: FIX_CANON_HALF_HEIGHT_DOUBLE_WIDTH
                frame.height *= 2;
            }
            // Fix for Canon 6D raw, which has flipped width & height
            // see FIX_CANON_FLIPPED_WIDTH_AND_HEIGHT
            uint sliceH = frame.numComponents == 3 ? Math.Min(frame.width, frame.height) : frame.height;

            if (X_S_F == 2 && Y_S_F == 1)
            {
                // fix the inconsistent slice width in sRaw mode, ask Canon.
                for (int i = 0; i < slicesW.Count; i++)
                    slicesW[i] *= 3 / 2;
            }

            // To understand the CR2 slice handling and sampling factor behavior, see
            // https://github.com/lclevy/libcraw2/blob/master/docs/cr2_lossless.pdf?raw=true

            // inner loop decodes one group of pixels at a time
            //  * for <N,1,1>: N  = N*1*1 (full raw)
            //  * for <3,2,1>: 6  = 3*2*1
            //  * for <3,2,2>: 12 = 3*2*2
            // and advances x by N_COMP*X_S_F and y by Y_S_F
            int xStepSize = N_COMP * X_S_F;
            int yStepSize = Y_S_F;

            uint processedPixels = 0;
            uint processedLineSlices = 0;
            long nextPredictor = offX + (offY * raw.fullSize.dim.width);
            foreach (uint sliceW in slicesW)
            {
                for (uint y = 0; y < sliceH && y + offY < raw.fullSize.dim.height; y += (uint)yStepSize)
                {
                    // Fix for Canon 80D mraw format.
                    // In that format, `frame` is 4032x3402, while `raw` is 4536x3024.
                    // Consequently, the slices in `frame` wrap around plus there are few
                    // 'extra' sliced lines because sum(slicesW) * sliceH > raw.dim.area()
                    // Those would overflow, hence the break.
                    // see FIX_CANON_FRAME_VS_IMAGE_SIZE_MISMATCH
                    uint destX = processedLineSlices / raw.fullSize.dim.height * slicesW[0];
                    uint destY = processedLineSlices % raw.fullSize.dim.height;
                    if (destX + offX >= raw.fullSize.dim.width * raw.fullSize.cpp)
                        break;

                    long dest = (destX + offX) + (destY + offY) * raw.fullSize.dim.width;
                    for (uint x = 0; x < sliceW; x += (uint)xStepSize)
                    {
                        Debug.Assert((processedPixels <= frame.width));
                        // check if we processed one full raw row worth of pixels
                        if (processedPixels == frame.width)
                        {
                            // if yes . update predictor by going back exactly one row,
                            // no matter where we are right now.
                            // makes no sense from an image compression point of view, ask Canon.
                            for (int i = 0; i < N_COMP; i++)
                            {
                                p[i] = raw.fullSize.rawView[nextPredictor + i];
                            }
                            nextPredictor = dest;
                            processedPixels = 0;
                        }


                        if (X_S_F == 1)
                        {
                            for (int i = 0; i < N_COMP; i++)
                            {
                                p[i] += ht[i].Decode();
                                if (x + offX < raw.fullSize.dim.width)
                                {
                                    raw.fullSize.rawView[dest] = (ushort)(p[i]);
                                    dest++;
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < Y_S_F; i++)
                            {
                                p[0] += ht[0].Decode();
                                var t = p[0];
                                p[0] += ht[0].Decode();
                                if (x + offX < raw.fullSize.dim.width)
                                {
                                    if (x + offX < raw.fullSize.dim.width) raw.fullSize.rawView[dest + i * pixelPitch] = (ushort)(t);
                                    if (x + offX < raw.fullSize.dim.width) raw.fullSize.rawView[dest + 3 + i * pixelPitch] = (ushort)(p[0]);
                                }
                            }
                            p[1] += ht[1].Decode();
                            p[2] += ht[2].Decode();
                            if (x + offX < raw.fullSize.dim.width)
                            {
                                raw.fullSize.rawView[dest + 1] = (ushort)(p[1]);
                                raw.fullSize.rawView[dest + 2] = (ushort)(p[2]);
                            }
                            dest += xStepSize;
                        }
                        processedPixels += (uint)X_S_F;
                    }
                    processedLineSlices += (uint)yStepSize;
                }
            }
            //TODO Check
            //input.ReadBytes(bitStream.getBufferPosition());
        }

        // N_COMP == number of components (2, 3 or 4)
        // X_S_F  == x/horizontal sampling factor (1 or 2)
        // Y_S_F  == y/vertical   sampling factor (1 or 2)
        void Decode2_1_1()
        {
            Debug.Assert(slicesW.Count < 16);  // We only have 4 bits for slice number.
            Debug.Assert(!(slicesW.Count > 1 && skipX != 0)); // Check if this is a valid state
            Debug.Assert(frame.ComponentInfo[0].superH == 1);
            Debug.Assert(frame.ComponentInfo[0].superV == 1);
            Debug.Assert(frame.ComponentInfo[1].superH == 1);
            Debug.Assert(frame.ComponentInfo[1].superV == 1);
            Debug.Assert(frame.numComponents == 2);

            HuffmanTable[] ht = new HuffmanTable[2];
            for (int i = 0; i < 2; ++i)
                ht[i] = huff[frame.ComponentInfo[i].dcTblNo];

            // Initialize predictors
            long[] p = new long[2];
            for (int i = 0; i < 2; ++i)
                p[i] = (1 << (frame.precision - Pt - 1));

            BitPumpJPEG bitStream = new BitPumpJPEG(input);
            uint pixelPitch = raw.pitch / 2; // Pitch in pixel
            if (frame.numComponents != 3 && frame.width * frame.numComponents > 2 * frame.height)
            {
                // Fix Canon double height issue where Canon doubled the width and halfed
                // the height (e.g. with 5Ds), ask Canon. frame.w needs to stay as is here
                // because the number of pixels after which the predictor gets updated is
                // still the doubled width.
                // see: FIX_CANON_HALF_HEIGHT_DOUBLE_WIDTH
                frame.height *= 2;
            }
            // Fix for Canon 6D raw, which has flipped width & height
            // see FIX_CANON_FLIPPED_WIDTH_AND_HEIGHT
            long sliceH = frame.numComponents == 3 ? Math.Min(frame.width, frame.height) : frame.height;

            // inner loop decodes one group of pixels at a time
            uint processedPixels = 0;
            uint processedLineSlices = 0;
            long nextPredictor = offX + (offY * raw.fullSize.dim.width);
            foreach (uint sliceW in slicesW)
            {
                for (uint y = 0; y < sliceH && y + offY < raw.fullSize.dim.height; y += 1)
                {
                    uint destX = processedLineSlices / raw.fullSize.dim.height * slicesW[0];
                    uint destY = processedLineSlices % raw.fullSize.dim.height;
                    if (destX + offX >= raw.fullSize.dim.width * raw.fullSize.cpp)
                        break;

                    long dest = (destX + offX) + (destY + offY) * raw.fullSize.dim.width;
                    for (uint x = 0; x < sliceW; x += 2)
                    {
                        // check if we processed one full raw row worth of pixels
                        if (processedPixels == frame.width)
                        {
                            p[0] = raw.fullSize.rawView[nextPredictor];
                            p[1] = raw.fullSize.rawView[nextPredictor + 1];
                            nextPredictor = dest;
                            processedPixels = 0;
                        }

                        p[0] += ht[0].Decode();
                        Debug.Assert(p[0] >= 0 && p[0] < 65536);
                        p[1] += ht[1].Decode();
                        Debug.Assert(p[1] >= 0 && p[1] < 65536);
                        if (x + offX < raw.fullSize.dim.width)
                        {
                            raw.fullSize.rawView[dest] = (ushort)(p[0]);
                            dest++;
                            raw.fullSize.rawView[dest] = (ushort)(p[1]);
                            dest++;
                        }
                        processedPixels++;
                    }
                    processedLineSlices++;
                }
            }
        }
    }
}
