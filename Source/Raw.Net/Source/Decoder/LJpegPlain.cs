using System;

namespace RawNet
{

    /******************
     * Decompresses Lossless non subsampled JPEGs, with 2-4 components
     *****************/

    public class LJpegPlain : LJpegDecompressor
    {
        UInt32[] offset;
        int[] slice_width;


        LJpegPlain(TIFFBinaryReader file, RawImage img) :
    base(file, img)
        {
            offset = 0;
            slice_width = 0;
        }


        void decodeScan()
        {

            // Fix for Canon 6D mRaw, which has flipped width & height for some part of the image
            // We temporarily swap width and height for cropping.
            if (mCanonFlipDim)
            {
                UInt32 w = frame.w;
                frame.w = frame.h;
                frame.h = w;
            }

            // If image attempts to decode beyond the image bounds, strip it.
            if ((frame.w * frame.cps + offX * mRaw.cpp) > mRaw.dim.x * mRaw.cpp)
                skipX = ((frame.w * frame.cps + offX * mRaw.cpp) - mRaw.dim.x * mRaw.cpp) / frame.cps;
            if (frame.h + offY > (UInt32)mRaw.dim.y)
                skipY = frame.h + offY - mRaw.dim.y;

            // Swap back (see above)
            if (mCanonFlipDim)
            {
                UInt32 w = frame.w;
                frame.w = frame.h;
                frame.h = w;
            }

            /* Correct wrong slice count (Canon G16) */
            if (slicesW.size() == 1)
                slicesW[0] = frame.w * frame.cps;

            if (slicesW.empty())
                slicesW.push_back(frame.w * frame.cps);

            if (0 == frame.h || 0 == frame.w)
                throw new RawDecoderException("LJpegPlain::decodeScan: Image width or height set to zero");

            for (UInt32 i = 0; i < frame.cps; i++)
            {
                if (frame.compInfo[i].superH != 1 || frame.compInfo[i].superV != 1)
                {
                    if (mRaw.isCFA)
                        throw new RawDecoderException("LJpegDecompressor::decodeScan: Cannot decode subsampled image to CFA data");

                    if (mRaw.getCpp() != frame.cps)
                        throw new RawDecoderException("LJpegDecompressor::decodeScan: Subsampled component count does not match image.");

                    if (pred == 1)
                    {
                        if (frame.compInfo[0].superH == 2 && frame.compInfo[0].superV == 2 &&
                            frame.compInfo[1].superH == 1 && frame.compInfo[1].superV == 1 &&
                            frame.compInfo[2].superH == 1 && frame.compInfo[2].superV == 1)
                        {
                            // Something like Cr2 sRaw1, use fast decoder
                            decodeScanLeft4_2_0();
                            return;
                        }
                        else if (frame.compInfo[0].superH == 2 && frame.compInfo[0].superV == 1 &&
                                 frame.compInfo[1].superH == 1 && frame.compInfo[1].superV == 1 &&
                                 frame.compInfo[2].superH == 1 && frame.compInfo[2].superV == 1)
                        {
                            // Something like Cr2 sRaw2, use fast decoder
                            if (mCanonFlipDim)
                                throw new RawDecoderException("LJpegDecompressor::decodeScan: Cannot flip non 4:2:2 subsampled images.");
                            decodeScanLeft4_2_2();
                            return;
                        }
                        else
                        {
                            throw new RawDecoderException("LJpegDecompressor::decodeScan: Unsupported subsampling");
                            decodeScanLeftGeneric();
                            return;
                        }
                    }
                    else
                    {
                        throw new RawDecoderException("LJpegDecompressor::decodeScan: Unsupported prediction direction.");
                    }
                }
            }

            if (pred == 1)
            {
                if (mCanonFlipDim)
                    throw new RawDecoderException("LJpegDecompressor::decodeScan: Cannot flip non subsampled images.");
                if (mRaw.dim.y * mRaw.pitch >= 1 << 28)
                {
                    decodeScanLeftGeneric();
                    return;
                }
                if (frame.cps == 2)
                    decodeScanLeft2Comps();
                else if (frame.cps == 3)
                    decodeScanLeft3Comps();
                else if (frame.cps == 4)
                    decodeScanLeft4Comps();
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

        void decodeScanLeftGeneric()
        {
            _ASSERTE(slicesW.size() < 16);  // We only have 4 bits for slice number.
            _ASSERTE(!(slicesW.size() > 1 && skipX)); // Check if this is a valid state

            UInt32 comps = frame.cps;  // Components
            HuffmanTable[] dctbl = new HuffmanTable[4];   // Tables for up to 4 components
            UInt16[] predict;         // Prediction pointer
                                      /* Fast access to supersampling component settings
                                      * this is the number of components in a given block.
                                      */
            UInt32[] samplesH = new UInt32[4];
            UInt32[] samplesV = new UInt32[4];

            byte[] draw = mRaw.rawData;
            UInt32 maxSuperH = 1;
            UInt32 maxSuperV = 1;
            UInt32[] samplesComp = new UInt32[4]; // How many samples per group does this component have
            UInt32 pixGroup = 0;   // How many pixels per group.

            for (UInt32 i = 0; i < comps; i++)
            {
                dctbl[i] = &huff[frame.compInfo[i].dcTblNo];
                samplesH[i] = frame.compInfo[i].superH;
                if (!isPowerOfTwo(samplesH[i]))
                    throw new RawDecoderException("LJpegPlain::decodeScanLeftGeneric: Horizontal sampling is not power of two.");
                maxSuperH = Math.Max(samplesH[i], maxSuperH);
                samplesV[i] = frame.compInfo[i].superV;
                if (!isPowerOfTwo(samplesV[i]))
                    throw new RawDecoderException("LJpegPlain::decodeScanLeftGeneric: Vertical sampling is not power of two.");
                maxSuperV = Math.Max(samplesV[i], maxSuperV);
                samplesComp[i] = samplesV[i] * samplesH[i];
                pixGroup += samplesComp[i];
            }

            mRaw.metadata.subsampling.x = maxSuperH;
            mRaw.metadata.subsampling.y = maxSuperV;

            //Prepare slices (for CR2)
            UInt32 slices = (UInt32)slicesW.size() * (frame.h - skipY) / maxSuperV;
            UInt16** imagePos = new UInt16*[slices + 1];
            int* sliceWidth = new int[slices + 1];

            UInt32 t_y = 0;
            UInt32 t_x = 0;
            UInt32 t_s = 0;
            UInt32 slice = 0;
            UInt32 pitch_s = mRaw.pitch / 2;  // Pitch in shorts
            slice_width = new int[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (UInt32 i = 0; i < slicesW.size(); i++)
                slice_width[i] = slicesW[i] / pixGroup / maxSuperH; // This is a guess, but works for sRaw1+2.

            if (skipX && (maxSuperV > 1 || maxSuperH > 1))
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeftGeneric: Cannot skip right border in subsampled mode");
            }
            if (skipX)
            {
                slice_width[slicesW.size() - 1] -= skipX;
            }

            for (slice = 0; slice < slices; slice++)
            {
                imagePos[slice] = (UInt16*)&draw[(t_x + offX) * mRaw.getBpp() + ((offY + t_y) * mRaw.pitch)];
                sliceWidth[slice] = slice_width[t_s];
                t_y += maxSuperV;
                if (t_y >= (frame.h - skipY))
                {
                    t_y = 0;
                    t_x += slice_width[t_s++];
                }
            }
            slice_width = null;

            // We check the final position. If bad slice sizes are given we risk writing outside the image
            if (imagePos[slices - 1] >= (UInt16*)&mRaw.getData()[mRaw.pitch * mRaw.dim.y])
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeft: Last slice out of bounds");
            }
            imagePos[slices] = imagePos[slices - 1];      // Extra offset to avoid branch in loop.
            sliceWidth[slices] = sliceWidth[slices - 1];        // Extra offset to avoid branch in loop.

            // Predictors for components
            int p[4];
            UInt16[] dest = imagePos[0];

            // Always points to next slice
            slice = 1;
            UInt32 pixInSlice = sliceWidth[0];

            // Initialize predictors and decode one group.
            UInt32 x = 0;
            predict = dest;
            for (UInt32 i = 0; i < comps; i++)
            {
                for (UInt32 y2 = 0; y2 < samplesV[i]; y2++)
                {
                    for (UInt32 x2 = 0; x2 < samplesH[i]; x2++)
                    {
                        // First pixel is not predicted, all other are.
                        if (y2 == 0 && x2 == 0)
                        {
                            *dest = p[i] = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl[i]);
                        }
                        else
                        {
                            p[i] += HuffDecode(dctbl[i]);
                            _ASSERTE(p[i] >= 0 && p[i] < 65536);
                            dest[x2 * comps + y2 * pitch_s] = p[i];
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

            UInt32 cw = (frame.w - skipX);
            for (UInt32 y = 0; y < (frame.h - skipY); y += maxSuperV)
            {
                for (; x < cw; x += maxSuperH)
                {

                    if (0 == pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Ran out of slices");
                        pixInSlice = sliceWidth[slice];
                        dest = imagePos[slice];  // Adjust destination for next pixel

                        slice++;
                        // If new are at the start of a new line, also update predictors.
                        if (x == 0)
                            predict = dest;
                    }

                    for (UInt32 i = 0; i < comps; i++)
                    {
                        for (UInt32 y2 = 0; y2 < samplesV[i]; y2++)
                        {
                            for (UInt32 x2 = 0; x2 < samplesH[i]; x2++)
                            {
                                p[i] += HuffDecode(dctbl[i]);
                                _ASSERTE(p[i] >= 0 && p[i] < 65536);
                                dest[x2 * comps + y2 * pitch_s] = p[i];
                            }
                        }
                        dest++;
                    }
                    dest += (maxSuperH * comps) - comps;
                    pixInSlice -= maxSuperH;
                }

                if (skipX)
                {
                    for (UInt32 sx = 0; sx < skipX; sx++)
                    {
                        for (UInt32 i = 0; i < comps; i++)
                        {
                            HuffDecode(dctbl[i]);
                        }
                    }
                }

                // Update predictors
                for (UInt32 i = 0; i < comps; i++)
                {
                    p[i] = predict[i];
                    // Ensure, that there is a slice shift at new line
                    if (!(pixInSlice == 0 || maxSuperV == 1))
                        throw new RawDecoderException("LJpegPlain::decodeScanLeftGeneric: Slice not placed at new line");
                }
                // Check if we are still within the file.
                bits.checkPos();
                predict = dest;
                x = 0;
            }
            delete[] imagePos;
            delete[] sliceWidth;
        }

        int COMPS = 3;
        /*************************************************************************/
        /* These are often used compression schemes, heavily optimized to decode */
        /* that specfic kind of images.                                          */
        /*************************************************************************/

        void decodeScanLeft4_2_0()
        {
            _ASSERTE(slicesW.size() < 16);  // We only have 4 bits for slice number.
            _ASSERTE(!(slicesW.size() > 1 && skipX)); // Check if this is a valid state
            _ASSERTE(frame.compInfo[0].superH == 2);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[0].superV == 2);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[1].superH == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[1].superV == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[2].superH == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[2].superV == 1);   // Check if this is a valid state
            _ASSERTE(frame.cps == COMPS);
            _ASSERTE(skipX == 0);

            HuffmanTable[] dctbl1 = huff[frame.compInfo[0].dcTblNo];
            HuffmanTable[] dctbl2 = huff[frame.compInfo[1].dcTblNo];
            HuffmanTable[] dctbl3 = huff[frame.compInfo[2].dcTblNo];

            UInt16[] predict;      // Prediction pointer

            mRaw.metadata.subsampling.x = 2;
            mRaw.metadata.subsampling.y = 2;

            byte[] draw = mRaw.rawData;
            // Fix for Canon 6D mRaw, which has flipped width & height
            UInt32 real_h = mCanonFlipDim ? frame.w : frame.h;

            //Prepare slices (for CR2)
            UInt32 slices = (UInt32)slicesW.size() * (real_h - skipY) / 2;
            offset = new UInt32[slices + 1];

            UInt32 t_y = 0;
            UInt32 t_x = 0;
            UInt32 t_s = 0;
            UInt32 slice = 0;
            UInt32 pitch_s = mRaw.pitch / 2;  // Pitch in shorts
            slice_width = new int[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (UInt32 i = 0; i < slicesW.size(); i++)
                slice_width[i] = slicesW[i] / COMPS;

            for (slice = 0; slice < slices; slice++)
            {
                offset[slice] = ((t_x + offX) * mRaw.bpp + ((offY + t_y) * mRaw.pitch)) | (t_s << 28);
                _ASSERTE((offset[slice] & 0x0fffffff) < mRaw.pitch * mRaw.dim.y);
                t_y += 2;
                if (t_y >= (real_h - skipY))
                {
                    t_y = 0;
                    t_x += slice_width[t_s++];
                }
            }

            // We check the final position. If bad slice sizes are given we risk writing outside the image
            if ((offset[slices - 1] & 0x0fffffff) >= mRaw.pitch * mRaw.dim.y)
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeft: Last slice out of bounds");
            }

            offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

            if (skipX)
                slice_width[slicesW.size() - 1] -= skipX;

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
            *dest = p1 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl1);
            p1 = dest[COMPS] = p1 + HuffDecode(dctbl1);
            p1 = dest[pitch_s] = p1 + HuffDecode(dctbl1);
            p1 = dest[COMPS + pitch_s] = p1 + HuffDecode(dctbl1);
            predict = dest;

            dest[1] = p2 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl2);
            dest[2] = p3 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl3);

            // Skip next
            dest += COMPS * 2;

            x = 2;
            pixInSlice -= 2;

            UInt32 cw = (frame.w - skipX);
            for (UInt32 y = 0; y < (frame.h - skipY); y += 2)
            {
                for (; x < cw; x += 2)
                {

                    if (0 == pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Ran out of slices");
                        UInt32 o = offset[slice++];
                        dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                        _ASSERTE((o & 0x0fffffff) < mRaw.pitch * mRaw.dim.y);
                        if ((o & 0x0fffffff) > mRaw.pitch * mRaw.dim.y)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Offset out of bounds");
                        pixInSlice = slice_width[o >> 28];

                        // If new are at the start of a new line, also update predictors.
                        if (x == 0)
                        {
                            predict = dest;
                        }
                    }
                    p1 += HuffDecode(dctbl1);
                    *dest = p1;
                    p1 += HuffDecode(dctbl1);
                    dest[COMPS] = p1;
                    p1 += HuffDecode(dctbl1);
                    dest[pitch_s] = p1;
                    p1 += HuffDecode(dctbl1);
                    dest[pitch_s + COMPS] = p1;

                    dest[1] = p2 = p2 + HuffDecode(dctbl2);
                    dest[2] = p3 = p3 + HuffDecode(dctbl3);

                    dest += COMPS * 2;
                    pixInSlice -= 2;
                }

                // Update predictors
                p1 = predict[0];
                p2 = predict[1];
                p3 = predict[2];
                _ASSERTE(pixInSlice == 0);  // Ensure, that there is a slice shift at new line
                                            // Check if we are still within the file.
                bits.checkPos();

                x = 0;
            }
        }

        void decodeScanLeft4_2_2()
        {
            _ASSERTE(slicesW.size() < 16);  // We only have 4 bits for slice number.
            _ASSERTE(!(slicesW.size() > 1 && skipX)); // Check if this is a valid state
            _ASSERTE(frame.compInfo[0].superH == 2);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[0].superV == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[1].superH == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[1].superV == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[2].superH == 1);   // Check if this is a valid state
            _ASSERTE(frame.compInfo[2].superV == 1);   // Check if this is a valid state
            _ASSERTE(frame.cps == COMPS);
            _ASSERTE(skipX == 0);

            HuffmanTable* dctbl1 = &huff[frame.compInfo[0].dcTblNo];
            HuffmanTable* dctbl2 = &huff[frame.compInfo[1].dcTblNo];
            HuffmanTable* dctbl3 = &huff[frame.compInfo[2].dcTblNo];

            mRaw.metadata.subsampling.x = 2;
            mRaw.metadata.subsampling.y = 1;

            UInt16* predict;      // Prediction pointer

            byte* draw = mRaw.getData();

            //Prepare slices (for CR2)
            UInt32 slices = (UInt32)slicesW.size() * (frame.h - skipY);
            offset = new UInt32[slices + 1];

            UInt32 t_y = 0;
            UInt32 t_x = 0;
            UInt32 t_s = 0;
            UInt32 slice = 0;
            slice_width = new int[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (UInt32 i = 0; i < slicesW.size(); i++)
                slice_width[i] = slicesW[i] / 2;

            for (slice = 0; slice < slices; slice++)
            {
                offset[slice] = ((t_x + offX) * mRaw.getBpp() + ((offY + t_y) * mRaw.pitch)) | (t_s << 28);
                _ASSERTE((offset[slice] & 0x0fffffff) < mRaw.pitch * mRaw.dim.y);
                t_y++;
                if (t_y >= (frame.h - skipY))
                {
                    t_y = 0;
                    t_x += slice_width[t_s++];
                }
            }
            if ((offset[slices - 1] & 0x0fffffff) >= mRaw.pitch * mRaw.dim.y)
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeft: Last slice out of bounds");
            }

            offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

            if (skipX)
                slice_width[slicesW.size() - 1] -= skipX;

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
            *dest = p1 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl1);
            p1 = dest[COMPS] = p1 + HuffDecode(dctbl1);
            predict = dest;

            dest[1] = p2 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl2);
            dest[2] = p3 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl3);

            // Skip to next
            dest += COMPS * 2;

            x = 2;
            pixInSlice -= 2;

            UInt32 cw = (frame.w - skipX);
            for (UInt32 y = 0; y < (frame.h - skipY); y++)
            {
                for (; x < cw; x += 2)
                {

                    if (0 == pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Ran out of slices");
                        UInt32 o = offset[slice++];
                        dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                        if ((o & 0x0fffffff) > mRaw.pitch * mRaw.dim.y)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Offset out of bounds");
                        pixInSlice = slice_width[o >> 28];

                        // If new are at the start of a new line, also update predictors.
                        if (x == 0)
                        {
                            predict = dest;
                        }
                    }
                    p1 += HuffDecode(dctbl1);
                    *dest = p1;
                    p1 += HuffDecode(dctbl1);
                    dest[COMPS] = p1;

                    dest[1] = p2 = p2 + HuffDecode(dctbl2);
                    dest[2] = p3 = p3 + HuffDecode(dctbl3);

                    dest += COMPS * 2;
                    pixInSlice -= 2;
                }

                // Update predictors
                p1 = predict[0];
                p2 = predict[1];
                p3 = predict[2];
                predict = dest;
                x = 0;
                // Check if we are still within the file.
                bits.checkPos();
            }
        }

        void decodeScanLeft2Comps()
        {
            int COMPS = 2;
            _ASSERTE(slicesW.size() < 16);  // We only have 4 bits for slice number.
            _ASSERTE(!(slicesW.size() > 1 && skipX)); // Check if this is a valid state

            byte* draw = mRaw.getData();
            // First line
            HuffmanTable* dctbl1 = &huff[frame.compInfo[0].dcTblNo];
            HuffmanTable* dctbl2 = &huff[frame.compInfo[1].dcTblNo];

            //Prepare slices (for CR2)
            UInt32 slices = (UInt32)slicesW.size() * (frame.h - skipY);
            offset = new UInt32[slices + 1];

            UInt32 t_y = 0;
            UInt32 t_x = 0;
            UInt32 t_s = 0;
            UInt32 slice = 0;
            UInt32 cw = (frame.w - skipX);
            for (slice = 0; slice < slices; slice++)
            {
                offset[slice] = ((t_x + offX) * mRaw.getBpp() + ((offY + t_y) * mRaw.pitch)) | (t_s << 28);
                _ASSERTE((offset[slice] & 0x0fffffff) < mRaw.pitch * mRaw.dim.y);
                t_y++;
                if (t_y == (frame.h - skipY))
                {
                    t_y = 0;
                    t_x += slicesW[t_s++];
                }
            }
            // We check the final position. If bad slice sizes are given we risk writing outside the image
            if ((offset[slices - 1] & 0x0fffffff) >= mRaw.pitch * mRaw.dim.y)
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeft: Last slice out of bounds");
            }
            offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

            slice_width = new int[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (UInt32 i = 0; i < slicesW.size(); i++)
                slice_width[i] = slicesW[i] / COMPS;

            if (skipX)
                slice_width[slicesW.size() - 1] -= skipX;

            // First pixels are obviously not predicted
            int p1;
            int p2;
            UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];
            UInt16* predict = dest;
            *dest++ = p1 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl1);
            *dest++ = p2 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl2);

            slice = 1;    // Always points to next slice
            UInt32 pixInSlice = slice_width[0] - 1;  // Skip first pixel

            UInt32 x = 1;                            // Skip first pixels on first line.
            for (UInt32 y = 0; y < (frame.h - skipY); y++)
            {
                for (; x < cw; x++)
                {
                    int diff = HuffDecode(dctbl1);
                    p1 += diff;
                    *dest++ = (UInt16)p1;
                    //    _ASSERTE(p1 >= 0 && p1 < 65536);

                    diff = HuffDecode(dctbl2);
                    p2 += diff;
                    *dest++ = (UInt16)p2;
                    //      _ASSERTE(p2 >= 0 && p2 < 65536);

                    if (0 == --pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Ran out of slices");
                        UInt32 o = offset[slice++];
                        dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                        if ((o & 0x0fffffff) > mRaw.pitch * mRaw.dim.y)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Offset out of bounds");
                        pixInSlice = slice_width[o >> 28];
                    }
                }

                if (skipX)
                {
                    for (UInt32 i = 0; i < skipX; i++)
                    {
                        HuffDecode(dctbl1);
                        HuffDecode(dctbl2);
                    }
                }

                p1 = predict[0];  // Predictors for next row
                p2 = predict[1];
                predict = dest;  // Adjust destination for next prediction
                x = 0;
                bits.checkPos();
            }
        }



        void decodeScanLeft3Comps()
        {
            int COMPS = 3;
            byte* draw = mRaw.getData();
            // First line
            HuffmanTable* dctbl1 = &huff[frame.compInfo[0].dcTblNo];
            HuffmanTable* dctbl2 = &huff[frame.compInfo[1].dcTblNo];
            HuffmanTable* dctbl3 = &huff[frame.compInfo[2].dcTblNo];

            //Prepare slices (for CR2)
            UInt32 slices = (UInt32)slicesW.size() * (frame.h - skipY);
            offset = new UInt32[slices + 1];

            UInt32 t_y = 0;
            UInt32 t_x = 0;
            UInt32 t_s = 0;
            UInt32 slice = 0;
            for (slice = 0; slice < slices; slice++)
            {
                offset[slice] = ((t_x + offX) * mRaw.getBpp() + ((offY + t_y) * mRaw.pitch)) | (t_s << 28);
                _ASSERTE((offset[slice] & 0x0fffffff) < mRaw.pitch * mRaw.dim.y);
                t_y++;
                if (t_y == (frame.h - skipY))
                {
                    t_y = 0;
                    t_x += slicesW[t_s++];
                }
            }
            // We check the final position. If bad slice sizes are given we risk writing outside the image
            if ((offset[slices - 1] & 0x0fffffff) >= mRaw.pitch * mRaw.dim.y)
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeft: Last slice out of bounds");
            }

            offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

            slice_width = new int[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (UInt32 i = 0; i < slicesW.size(); i++)
                slice_width[i] = slicesW[i] / COMPS;

            if (skipX)
                slice_width[slicesW.size() - 1] -= skipX;

            // First pixels are obviously not predicted
            int p1;
            int p2;
            int p3;
            UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];
            UInt16* predict = dest;
            *dest++ = p1 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl1);
            *dest++ = p2 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl2);
            *dest++ = p3 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl3);

            slice = 1;
            UInt32 pixInSlice = slice_width[0] - 1;

            UInt32 cw = (frame.w - skipX);
            UInt32 x = 1;                            // Skip first pixels on first line.

            for (UInt32 y = 0; y < (frame.h - skipY); y++)
            {
                for (; x < cw; x++)
                {
                    p1 += HuffDecode(dctbl1);
                    *dest++ = (UInt16)p1;

                    p2 += HuffDecode(dctbl2);
                    *dest++ = (UInt16)p2;

                    p3 += HuffDecode(dctbl3);
                    *dest++ = (UInt16)p3;

                    if (0 == --pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Ran out of slices");
                        UInt32 o = offset[slice++];
                        dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                        if ((o & 0x0fffffff) > mRaw.pitch * mRaw.dim.y)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Offset out of bounds");
                        _ASSERTE((o >> 28) < slicesW.size());
                        pixInSlice = slice_width[o >> 28];
                    }
                }

                if (skipX)
                {
                    for (UInt32 i = 0; i < skipX; i++)
                    {
                        HuffDecode(dctbl1);
                        HuffDecode(dctbl2);
                        HuffDecode(dctbl3);
                    }
                }

                p1 = predict[0];  // Predictors for next row
                p2 = predict[1];
                p3 = predict[2];  // Predictors for next row
                predict = dest;  // Adjust destination for next prediction
                x = 0;
                bits.checkPos();
            }
        }


        void decodeScanLeft4Comps()
        {
            int COMPS = 4;
            // First line
            HuffmanTable* dctbl1 = &huff[frame.compInfo[0].dcTblNo];
            HuffmanTable* dctbl2 = &huff[frame.compInfo[1].dcTblNo];
            HuffmanTable* dctbl3 = &huff[frame.compInfo[2].dcTblNo];
            HuffmanTable* dctbl4 = &huff[frame.compInfo[3].dcTblNo];

            if (mCanonDoubleHeight)
            {
                frame.h *= 2;
                mRaw.dim = iPoint2D(frame.w * 2, frame.h);
                mRaw.destroyData();
                mRaw.createData();
            }
            byte* draw = mRaw.getData();

            //Prepare slices (for CR2)
            UInt32 slices = (UInt32)slicesW.size() * (frame.h - skipY);
            offset = new UInt32[slices + 1];

            UInt32 t_y = 0;
            UInt32 t_x = 0;
            UInt32 t_s = 0;
            UInt32 slice = 0;
            for (slice = 0; slice < slices; slice++)
            {
                offset[slice] = ((t_x + offX) * mRaw.getBpp() + ((offY + t_y) * mRaw.pitch)) | (t_s << 28);
                _ASSERTE((offset[slice] & 0x0fffffff) < mRaw.pitch * mRaw.dim.y);
                t_y++;
                if (t_y == (frame.h - skipY))
                {
                    t_y = 0;
                    t_x += slicesW[t_s++];
                }
            }
            // We check the final position. If bad slice sizes are given we risk writing outside the image
            if ((offset[slices - 1] & 0x0fffffff) >= mRaw.pitch * mRaw.dim.y)
            {
                throw new RawDecoderException("LJpegPlain::decodeScanLeft: Last slice out of bounds");
            }
            offset[slices] = offset[slices - 1];        // Extra offset to avoid branch in loop.

            slice_width = new int[slices];

            // This is divided by comps, since comps pixels are processed at the time
            for (UInt32 i = 0; i < slicesW.size(); i++)
                slice_width[i] = slicesW[i] / COMPS;

            if (skipX)
                slice_width[slicesW.size() - 1] -= skipX;

            // First pixels are obviously not predicted
            int p1;
            int p2;
            int p3;
            int p4;
            UInt16* dest = (UInt16*)&draw[offset[0] & 0x0fffffff];
            UInt16* predict = dest;
            *dest++ = p1 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl1);
            *dest++ = p2 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl2);
            *dest++ = p3 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl3);
            *dest++ = p4 = (1 << (frame.prec - Pt - 1)) + HuffDecode(dctbl4);

            slice = 1;
            UInt32 pixInSlice = slice_width[0] - 1;

            UInt32 cw = (frame.w - skipX);
            UInt32 x = 1;                            // Skip first pixels on first line.

            if (mCanonDoubleHeight)
                skipY = frame.h >> 1;

            for (UInt32 y = 0; y < (frame.h - skipY); y++)
            {
                for (; x < cw; x++)
                {
                    p1 += HuffDecode(dctbl1);
                    *dest++ = (UInt16)p1;

                    p2 += HuffDecode(dctbl2);
                    *dest++ = (UInt16)p2;

                    p3 += HuffDecode(dctbl3);
                    *dest++ = (UInt16)p3;

                    p4 += HuffDecode(dctbl4);
                    *dest++ = (UInt16)p4;

                    if (0 == --pixInSlice)
                    { // Next slice
                        if (slice > slices)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Ran out of slices");
                        UInt32 o = offset[slice++];
                        dest = (UInt16*)&draw[o & 0x0fffffff];  // Adjust destination for next pixel
                        if ((o & 0x0fffffff) > mRaw.pitch * mRaw.dim.y)
                            throw new RawDecoderException("LJpegPlain::decodeScanLeft: Offset out of bounds");
                        pixInSlice = slice_width[o >> 28];
                    }
                }
                if (skipX)
                {
                    for (UInt32 i = 0; i < skipX; i++)
                    {
                        HuffDecode(dctbl1);
                        HuffDecode(dctbl2);
                        HuffDecode(dctbl3);
                        HuffDecode(dctbl4);
                    }
                }
                bits.checkPos();
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
