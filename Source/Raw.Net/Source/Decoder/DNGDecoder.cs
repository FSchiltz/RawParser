
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RawNet
{


    class DngStrip
    {
        public DngStrip() { h = offset = count = offsetY = 0; }
        public UInt32 h;
        public UInt32 offset; // Offset in bytes
        public UInt32 count;
        public UInt32 offsetY;
    };

    class DngDecoder : RawDecoder
    {
        IFD mRootIFD;
        bool mFixLjpeg;

        public DngDecoder(IFD rootIFD, ref TIFFBinaryReader file) : base(ref file)
        {
            mRootIFD = (rootIFD);
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.DNGVERSION);
            var v = data[0].getEntry(TagType.DNGVERSION).data;

            if ((byte)v[0] != 1)
                throw new RawDecoderException("Not a supported DNG image format: " + (int)v[0] + (int)v[1] + (int)v[2] + (int)v[3]);
            //  if (v[1] > 4)
            //    throw new RawDecoderException("Not a supported DNG image format: v%u.%u.%u.%u", (int)v[0], (int)v[1], (int)v[2], (int)v[3]);

            if (((byte)v[0] <= 1) && ((byte)v[1] < 1))  // Prior to v1.1.xxx  fix LJPEG encoding bug
                mFixLjpeg = true;
            else
                mFixLjpeg = false;
        }

        protected override void decodeMetaDataInternal(CameraMetaData meta)
        {
            if (mRootIFD.hasEntryRecursive(TagType.ISOSPEEDRATINGS))
                mRaw.metadata.isoSpeed = mRootIFD.getEntryRecursive(TagType.ISOSPEEDRATINGS).getInt();

            // Set the make and model
            if (mRootIFD.hasEntryRecursive(TagType.MAKE) && mRootIFD.hasEntryRecursive(TagType.MODEL))
            {
                string make = mRootIFD.getEntryRecursive(TagType.MAKE).dataAsString;
                string model = mRootIFD.getEntryRecursive(TagType.MODEL).dataAsString;
                make = make.Trim();
                model = model.Trim();
                mRaw.metadata.make = make;
                mRaw.metadata.model = model;

                Camera cam = meta.getCamera(make, model, "dng");
                if (cam == null) //Also look for non-DNG cameras in case it's a converted file
                    cam = meta.getCamera(make, model, "");
                if (cam != null)
                {
                    mRaw.metadata.canonical_make = cam.canonical_make;
                    mRaw.metadata.canonical_model = cam.canonical_model;
                    mRaw.metadata.canonical_alias = cam.canonical_alias;
                    mRaw.metadata.canonical_id = cam.canonical_id;
                }
                else
                {
                    mRaw.metadata.canonical_make = make;
                    mRaw.metadata.canonical_model = mRaw.metadata.canonical_alias = model;
                    if (mRootIFD.hasEntryRecursive(TagType.UNIQUECAMERAMODEL))
                    {
                        mRaw.metadata.canonical_id = mRootIFD.getEntryRecursive(TagType.UNIQUECAMERAMODEL).dataAsString;
                    }
                    else
                    {
                        mRaw.metadata.canonical_id = make + " " + model;
                    }
                }
            }
        }

        /* DNG Images are assumed to be decodable unless explicitly set so */
        protected override void checkSupportInternal(CameraMetaData meta)
        {
            // We set this, since DNG's are not explicitly added.
            failOnUnknown = false;

            if (!(mRootIFD.hasEntryRecursive(TagType.MAKE) && mRootIFD.hasEntryRecursive(TagType.MODEL)))
            {
                // Check "Unique Camera Model" instead, uses this for both make + model.
                if (mRootIFD.hasEntryRecursive(TagType.UNIQUECAMERAMODEL))
                {
                    string unique = mRootIFD.getEntryRecursive(TagType.UNIQUECAMERAMODEL).dataAsString;
                    this.checkCameraSupported(meta, unique, unique, "dng");
                    return;
                }
                else
                {
                    // If we don't have make/model we cannot tell, but still assume yes.
                    return;
                }
            }

            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.MODEL);
            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;
            this.checkCameraSupported(meta, make, model, "dng");
        }

        /* Decodes DNG masked areas into blackareas in the image */
        bool decodeMaskedAreas(IFD raw)
        {
            Tag masked = raw.getEntry(TagType.MASKEDAREAS);

            if (masked.dataType != TiffDataType.SHORT && masked.dataType != TiffDataType.LONG)
                return false;

            Int32 nrects = (int)masked.dataCount / 4;
            if (0 == nrects)
                return false;

            /* Since we may both have short or int, copy it to int array. */

            masked.getIntArray(out Int32[] rects, nrects * 4);

            iPoint2D top = mRaw.mOffset;

            for (UInt32 i = 0; i < nrects; i++)
            {
                iPoint2D topleft = new iPoint2D(rects[i * 4 + 1], rects[i * 4]);
                iPoint2D bottomright = new iPoint2D(rects[i * 4 + 3], rects[i * 4 + 2]);
                // Is this a horizontal box, only add it if it covers the active width of the image
                if (topleft.x <= top.x && bottomright.x >= (mRaw.dim.x + top.x))
                    mRaw.blackAreas.Add(new BlackArea(topleft.y, bottomright.y - topleft.y, false));
                // Is it a vertical box, only add it if it covers the active height of the image
                else if (topleft.y <= top.y && bottomright.y >= (mRaw.dim.y + top.y))
                {
                    mRaw.blackAreas.Add(new BlackArea(topleft.x, bottomright.x - topleft.x, true));
                }
            }
            return mRaw.blackAreas.Count != 0;
        }

        bool decodeBlackLevels(IFD raw)
        {
            iPoint2D blackdim = new iPoint2D(1, 1);
            if (raw.hasEntry(TagType.BLACKLEVELREPEATDIM))
            {
                Tag bleveldim = raw.getEntry(TagType.BLACKLEVELREPEATDIM);
                if (bleveldim.dataCount != 2)
                    return false;
                blackdim = new iPoint2D(bleveldim.getInt(0), bleveldim.getInt(1));
            }

            if (blackdim.x == 0 || blackdim.y == 0)
                return false;

            if (!raw.hasEntry(TagType.BLACKLEVEL))
                return true;

            if (mRaw.cpp != 1)
                return false;

            Tag black_entry = raw.getEntry(TagType.BLACKLEVEL);
            if ((int)black_entry.dataCount < blackdim.x * blackdim.y)
                throw new RawDecoderException("DNG: BLACKLEVEL entry is too small");

            if (blackdim.x < 2 || blackdim.y < 2)
            {
                // We so not have enough to fill all individually, read a single and copy it
                //TODO check if float
                float value = black_entry.getFloat();
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        mRaw.blackLevelSeparate[y * 2 + x] = (int)value;
                }
            }
            else
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        mRaw.blackLevelSeparate[y * 2 + x] = (int)black_entry.getFloat(y * blackdim.x + x);
                }
            }

            //TODO remove hasEntry
            // DNG Spec says we must add black in deltav and deltah
            if (raw.hasEntry(TagType.BLACKLEVELDELTAV))
            {
                Tag blackleveldeltav = raw.getEntry(TagType.BLACKLEVELDELTAV);
                if ((int)blackleveldeltav.dataCount < mRaw.dim.y)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAV array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < mRaw.dim.y; i++)
                    black_sum[i & 1] += blackleveldeltav.getFloat(i);

                for (int i = 0; i < 4; i++)
                    mRaw.blackLevelSeparate[i] += (int)(black_sum[i >> 1] / mRaw.dim.y * 2.0f);
            }

            if (raw.hasEntry(TagType.BLACKLEVELDELTAH))
            {
                Tag blackleveldeltah = raw.getEntry(TagType.BLACKLEVELDELTAH);
                if ((int)blackleveldeltah.dataCount < mRaw.dim.x)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAH array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < mRaw.dim.x; i++)
                    black_sum[i & 1] += blackleveldeltah.getFloat(i);

                for (int i = 0; i < 4; i++)
                    mRaw.blackLevelSeparate[i] += (int)(black_sum[i & 1] / mRaw.dim.x * 2.0f);
            }
            return true;
        }

        void setBlack(IFD raw)
        {

            if (raw.hasEntry(TagType.MASKEDAREAS))
                if (decodeMaskedAreas(raw))
                    return;

            // Black defaults to 0
            // Common.memset(mRaw.blackLevelSeparate, 0, sizeof(mRaw.blackLevelSeparate));

            if (raw.hasEntry(TagType.BLACKLEVEL))
                decodeBlackLevels(raw);
        }

        protected override RawImage decodeRawInternal()
        {
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.COMPRESSION);

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No image data found");

            // Erase the ones not with JPEG compression
            for (int k = data.Count - 1; k >= 0; k--)
            {
                IFD i = data[k];
                int comp = i.getEntry(TagType.COMPRESSION).getShort(0);
                bool isSubsampled = false;
                try
                {
                    isSubsampled = (i.getEntry(TagType.NEWSUBFILETYPE).getInt() & 1) != 0; // bit 0 is on if image is subsampled
                }
                catch (TiffParserException) { }
                if ((comp != 7 && comp != 1 && comp != 0x884c) || isSubsampled)
                {  // Erase if subsampled, or not JPEG or uncompressed
                    data.Remove(i);
                }
            }

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No RAW chunks found");
            /*
            if (data.size() > 1)
            {
                _RPT0(0, "Multiple RAW chunks found - using first only!");
            }*/

            IFD raw = data[0];
            UInt32 sample_format = 1;
            UInt32 bps = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();

            if (raw.hasEntry(TagType.SAMPLEFORMAT))
                sample_format = raw.getEntry(TagType.SAMPLEFORMAT).getUInt();

            if (sample_format != 1)
                throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported.");

            mRaw.isCFA = (raw.getEntry(TagType.PHOTOMETRICINTERPRETATION).getUShort() == 32803);

            /*
            if (mRaw.isCFA)
                _RPT0(0, "This is a CFA image\n");
            else
                _RPT0(0, "This is NOT a CFA image\n");
*/

            if (sample_format == 1 && bps > 16)
                throw new RawDecoderException("DNG Decoder: Integer precision larger than 16 bits currently not supported.");

            if (sample_format == 3 && bps != 32)
                throw new RawDecoderException("DNG Decoder: Float point must be 32 bits per sample.");

            try
            {
                mRaw.dim = new iPoint2D();
                mRaw.dim.x = raw.getEntry(TagType.IMAGEWIDTH).getInt();
                mRaw.dim.y = raw.getEntry(TagType.IMAGELENGTH).getInt();
            }
            catch (TiffParserException)
            {
                throw new RawDecoderException("DNG Decoder: Could not read basic image information.");
            }

            int compression = -1;

            try
            {
                compression = raw.getEntry(TagType.COMPRESSION).getShort();
                if (mRaw.isCFA)
                {

                    // Check if layout is OK, if present
                    if (raw.hasEntry(TagType.CFALAYOUT))
                        if (raw.getEntry(TagType.CFALAYOUT).getShort() != 1)
                            throw new RawDecoderException("DNG Decoder: Unsupported CFA Layout.");

                    Tag cfadim = raw.getEntry(TagType.CFAREPEATPATTERNDIM);
                    if (cfadim.dataCount != 2)
                        throw new RawDecoderException("DNG Decoder: Couldn't read CFA pattern dimension");
                    Tag pDim = raw.getEntry(TagType.CFAREPEATPATTERNDIM); // Get the size
                    var cPat = raw.getEntry(TagType.CFAPATTERN).data;                 // Does NOT contain dimensions as some documents state
                                                                                      /*
                                                                                            if (raw.hasEntry(CFAPLANECOLOR)) {
                                                                                              Tag e = raw.getEntry(CFAPLANECOLOR);
                                                                                              unsigned stringcPlaneOrder = e.getData();       // Map from the order in the image, to the position in the CFA
                                                                                              printf("Planecolor: ");
                                                                                              for (UInt32 i = 0; i < e.count; i++) {
                                                                                                printf("%u,",cPlaneOrder[i]);
                                                                                              }
                                                                                              printf("\n");
                                                                                            }
                                                                                      */
                    iPoint2D cfaSize = new iPoint2D(pDim.getInt(1), pDim.getInt(0));
                    mRaw.cfa.setSize(cfaSize);
                    if (cfaSize.area() != raw.getEntry(TagType.CFAPATTERN).dataCount)
                        throw new RawDecoderException("DNG Decoder: CFA pattern dimension and pattern count does not match: " + raw.getEntry(TagType.CFAPATTERN).dataCount);

                    for (int y = 0; y < cfaSize.y; y++)
                    {
                        for (int x = 0; x < cfaSize.x; x++)
                        {
                            UInt32 c1 = (uint)cPat[x + y * cfaSize.x];
                            CFAColor c2;
                            switch (c1)
                            {
                                case 0:
                                    c2 = CFAColor.RED; break;
                                case 1:
                                    c2 = CFAColor.GREEN; break;
                                case 2:
                                    c2 = CFAColor.BLUE; break;
                                case 3:
                                    c2 = CFAColor.CYAN; break;
                                case 4:
                                    c2 = CFAColor.MAGENTA; break;
                                case 5:
                                    c2 = CFAColor.YELLOW; break;
                                case 6:
                                    c2 = CFAColor.WHITE; break;
                                default:
                                    c2 = CFAColor.UNKNOWN;
                                    throw new RawDecoderException("DNG Decoder: Unsupported CFA Color.");
                            }
                            mRaw.cfa.setColorAt(new iPoint2D(x, y), c2);
                        }
                    }
                }


                // Now load the image
                if (compression == 1)
                {  // Uncompressed.
                    try
                    {
                        UInt32 cpp = raw.getEntry(TagType.SAMPLESPERPIXEL).getUInt();
                        if (cpp > 4)
                            throw new RawDecoderException("DNG Decoder: More than 4 samples per pixel is not supported.");
                        mRaw.cpp = cpp;

                        Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                        Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
                        UInt32 yPerSlice = raw.getEntry(TagType.ROWSPERSTRIP).getUInt();
                        UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getUInt();
                        UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getUInt();

                        if (counts.dataCount != offsets.dataCount)
                        {
                            throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
                        }

                        UInt32 offY = 0;
                        List<DngStrip> slices = new List<DngStrip>();
                        for (UInt32 s = 0; s < offsets.dataCount; s++)
                        {
                            DngStrip slice = new DngStrip();
                            slice.offset = offsets.getUInt(s);
                            slice.count = counts.getUInt(s);
                            slice.offsetY = offY;
                            if (offY + yPerSlice > height)
                                slice.h = height - offY;
                            else
                                slice.h = yPerSlice;

                            offY += yPerSlice;

                            if (mFile.isValid(slice.offset, slice.count)) // Only decode if size is valid
                                slices.Add(slice);
                        }

                        for (int i = 0; i < slices.Count; i++)
                        {
                            DngStrip slice = slices[i];
                            TIFFBinaryReader input = new TIFFBinaryReader(mFile.BaseStream, slice.offset, (uint)mFile.BaseStream.Length);
                            iPoint2D size = new iPoint2D((int)width, (int)slice.h);
                            iPoint2D pos = new iPoint2D(0, (int)slice.offsetY);

                            bool big_endian = (raw.endian == Endianness.big);
                            // DNG spec says that if not 8 or 16 bit/sample, always use big endian
                            if (bps != 8 && bps != 16)
                                big_endian = true;
                            try
                            {
                                readUncompressedRaw(ref input, size, pos, (int)(mRaw.cpp * width * bps / 8), (int)bps, big_endian ? BitOrder.Jpeg : BitOrder.Plain);
                            }
                            catch (IOException ex)
                            {
                                if (i > 0)
                                    mRaw.errors.Add(ex.Message);
                                else
                                    throw new RawDecoderException("DNG decoder: IO error occurred in first slice, unable to decode more. Error is: " + ex.Message);
                            }
                        }

                    }
                    catch (TiffParserException)
                    {
                        throw new RawDecoderException("DNG Decoder: Unsupported format, uncompressed with no strips.");
                    }
                }
                else if (compression == 7 || compression == 0x884c)
                {
                    try
                    {
                        // Let's try loading it as tiles instead

                        mRaw.cpp = (raw.getEntry(TagType.SAMPLESPERPIXEL).getUInt());

                        if (sample_format != 1)
                            throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported for compressed data.");

                        DngDecoderSlices slices = new DngDecoderSlices(mFile, mRaw, compression);
                        if (raw.hasEntry(TagType.TILEOFFSETS))
                        {
                            UInt32 tilew = raw.getEntry(TagType.TILEWIDTH).getUInt();
                            UInt32 tileh = raw.getEntry(TagType.TILELENGTH).getUInt();
                            if (tilew == 0 || tileh == 0)
                                throw new RawDecoderException("DNG Decoder: Invalid tile size");

                            UInt32 tilesX = (uint)(mRaw.dim.x + tilew - 1) / tilew;
                            UInt32 tilesY = (uint)(mRaw.dim.y + tileh - 1) / tileh;
                            UInt32 nTiles = tilesX * tilesY;

                            Tag offsets = raw.getEntry(TagType.TILEOFFSETS);
                            Tag counts = raw.getEntry(TagType.TILEBYTECOUNTS);
                            if (offsets.dataCount != counts.dataCount || offsets.dataCount != nTiles)
                                throw new RawDecoderException("DNG Decoder: Tile count mismatch: offsets:" + offsets.dataCount + " count:" + counts.dataCount + ", calculated:" + nTiles);

                            slices.mFixLjpeg = mFixLjpeg;

                            for (UInt32 y = 0; y < tilesY; y++)
                            {
                                for (UInt32 x = 0; x < tilesX; x++)
                                {
                                    DngSliceElement e = new DngSliceElement(offsets.getUInt(x + y * tilesX), counts.getUInt(x + y * tilesX), tilew * x, tileh * y);
                                    e.mUseBigtable = tilew * tileh > 1024 * 1024;
                                    slices.addSlice(e);
                                }
                            }
                        }
                        else
                        {  // Strips
                            Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);

                            UInt32 yPerSlice = raw.getEntry(TagType.ROWSPERSTRIP).getUInt();

                            if (counts.dataCount != offsets.dataCount)
                            {
                                throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", stips:" + offsets.dataCount);
                            }

                            if (yPerSlice == 0 || yPerSlice > (UInt32)mRaw.dim.y)
                                throw new RawDecoderException("DNG Decoder: Invalid y per slice");

                            UInt32 offY = 0;
                            for (UInt32 s = 0; s < counts.dataCount; s++)
                            {
                                DngSliceElement e = new DngSliceElement(offsets.getUInt(s), counts.getUInt(s), 0, offY);
                                e.mUseBigtable = yPerSlice * mRaw.dim.y > 1024 * 1024;
                                offY += yPerSlice;

                                if (mFile.isValid(e.byteOffset, e.byteCount)) // Only decode if size is valid
                                    slices.addSlice(e);
                            }
                        }
                        UInt32 nSlices = (uint)slices.slices.Count;
                        if (nSlices == 0)
                            throw new RawDecoderException("DNG Decoder: No valid slices found.");

                        slices.decodeSlice();

                        if (mRaw.errors.Count >= nSlices)
                            throw new RawDecoderException("DNG Decoding: Too many errors encountered. Giving up.\nFirst Error:" + mRaw.errors[0]);
                    }
                    catch (TiffParserException e)
                    {
                        throw new RawDecoderException("DNG Decoder: Unsupported format, tried strips and tiles:" + e.Message);
                    }
                }
                else
                {
                    throw new RawDecoderException("DNG Decoder: Unknown compression: " + compression);
                }
            }
            catch (TiffParserException e)
            {
                throw new RawDecoderException("DNG Decoder: Image could not be read:" + e.Message);
            }

            // Fetch the white balance
            if (mRootIFD.hasEntryRecursive(TagType.ASSHOTNEUTRAL))
            {
                Tag as_shot_neutral = mRootIFD.getEntryRecursive(TagType.ASSHOTNEUTRAL);
                if (as_shot_neutral.dataCount == 3)
                {
                    for (UInt32 i = 0; i < 3; i++)
                        mRaw.metadata.wbCoeffs[i] = 1.0f / as_shot_neutral.getFloat(i);
                }
            }
            else if (mRootIFD.hasEntryRecursive(TagType.ASSHOTWHITEXY))
            {
                // Commented out because I didn't have an example file to verify it's correct
                /* TiffEntry *as_shot_white_xy = mRootIFD.getEntryRecursive(ASSHOTWHITEXY);
                if (as_shot_white_xy.count == 2) {
                  mRaw.metadata.wbCoeffs[0] = as_shot_white_xy.getFloat(0);
                  mRaw.metadata.wbCoeffs[1] = as_shot_white_xy.getFloat(1);
                  mRaw.metadata.wbCoeffs[2] = 1 - mRaw.metadata.wbCoeffs[0] - mRaw.metadata.wbCoeffs[1];

                  float d65_white[3] = { 0.950456, 1, 1.088754 };
                  for (UInt32 i=0; i<3; i++)
                      mRaw.metadata.wbCoeffs[i] /= d65_white[i];
                } */
            }

            // Crop
            if (raw.hasEntry(TagType.ACTIVEAREA))
            {
                iPoint2D new_size = new iPoint2D(mRaw.dim.x, mRaw.dim.y);

                Tag active_area = raw.getEntry(TagType.ACTIVEAREA);
                if (active_area.dataCount != 4)
                    throw new RawDecoderException("DNG: active area has " + active_area.dataCount + " values instead of 4");

                active_area.getIntArray(out int[] corners, 4);
                if (new iPoint2D(corners[1], corners[0]).isThisInside(mRaw.dim))
                {
                    if (new iPoint2D(corners[3], corners[2]).isThisInside(mRaw.dim))
                    {
                        iRectangle2D crop = new iRectangle2D(corners[1], corners[0], corners[3] - corners[1], corners[2] - corners[0]);
                        mRaw.subFrame(crop);
                    }
                }
            }

            if (raw.hasEntry(TagType.DEFAULTCROPORIGIN) && raw.hasEntry(TagType.DEFAULTCROPSIZE))
            {
                iRectangle2D cropped = new iRectangle2D(0, 0, mRaw.dim.x, mRaw.dim.y);
                Tag origin_entry = raw.getEntry(TagType.DEFAULTCROPORIGIN);
                Tag size_entry = raw.getEntry(TagType.DEFAULTCROPSIZE);

                /* Read crop position (sometimes is rational so use float) */
                origin_entry.getFloatArray(out float[] tl, 2);
                if (new iPoint2D((int)tl[0], (int)tl[1]).isThisInside(mRaw.dim))
                    cropped = new iRectangle2D((int)tl[0], (int)tl[1], 0, 0);

                cropped.dim = mRaw.dim - cropped.pos;
                /* Read size (sometimes is rational so use float) */

                size_entry.getFloatArray(out float[] sz, 2);
                iPoint2D size = new iPoint2D((int)sz[0], (int)sz[1]);
                if ((size + cropped.pos).isThisInside(mRaw.dim))
                    cropped.dim = size;

                if (!cropped.hasPositiveArea())
                    throw new RawDecoderException("DNG Decoder: No positive crop area");

                mRaw.subFrame(cropped);
                if (mRaw.isCFA && cropped.pos.x % 2 == 1)
                    mRaw.cfa.shiftLeft(1);
                if (mRaw.isCFA && cropped.pos.y % 2 == 1)
                    mRaw.cfa.shiftDown(1);
            }
            if (mRaw.dim.area() <= 0)
                throw new RawDecoderException("DNG Decoder: No image left after crop");

            /*
            // Apply stage 1 opcodes
            if (applyStage1DngOpcodes)
            {
                if (raw.hasEntry(TagType.OPCODELIST1))
                {
                    // Apply stage 1 codes
                    try
                    {
                        DngOpcodes codes = new DngOpcodes(raw.getEntry(TagType.OPCODELIST1));
                        mRaw = codes.applyOpCodes(mRaw);
                    }
                    catch (RawDecoderException e)
                    {
                        // We push back errors from the opcode parser, since the image may still be usable
                        mRaw.errors.Add(e.Message);
                    }
                }
            }
            */
            // Linearization
            if (raw.hasEntry(TagType.LINEARIZATIONTABLE))
            {
                Tag lintable = raw.getEntry(TagType.LINEARIZATIONTABLE);
                UInt32 len = lintable.dataCount;
                lintable.getShortArray(out ushort[] table, (int)len);
                mRaw.setTable(table, (int)len, !uncorrectedRawValues);
                if (!uncorrectedRawValues)
                {
                    //mRaw.sixteenBitLookup();
                    //mRaw.table = (null);
                }
                /*
                if (0)
                {
                    // Test average for bias
                    UInt32 cw = (uint)mRaw.dim.x * mRaw.cpp;
                    UInt16[] pixels = mRaw.rawData.Take(500).ToArray();
                    float avg = 0.0f;
                    for (UInt32 x = 0; x < cw; x++)
                    {
                        avg += (float)pixels[x];
                    }
                    Debug.WriteLine("Average:" + avg / (float)cw);
                }*/
            }

            // Default white level is (2 ** BitsPerSample) - 1
            mRaw.whitePoint = (uint)(1 >> raw.getEntry(TagType.BITSPERSAMPLE).getShort()) - 1;

            if (raw.hasEntry(TagType.WHITELEVEL))
            {
                Tag whitelevel = raw.getEntry(TagType.WHITELEVEL);
                try
                {
                    mRaw.whitePoint = whitelevel.getUInt();
                }
                catch (Exception) { }
            }
            // Set black
            setBlack(raw);

            // Apply opcodes to lossy DNG 
            if (compression == 0x884c && !uncorrectedRawValues)
            {
                /*
                if (raw.hasEntry(TagType.OPCODELIST2))
                {
                    // We must apply black/white scaling
                    mRaw.scaleBlackWhite();
                    // Apply stage 2 codes
                    try
                    {
                        DngOpcodes codes = new DngOpcodes(raw.getEntry(TagType.OPCODELIST2));
                        mRaw = codes.applyOpCodes(mRaw);
                    }
                    catch (RawDecoderException e)
                    {
                        // We push back errors from the opcode parser, since the image may still be usable
                        mRaw.errors.Add(e.Message);
                    }
                    mRaw.blackAreas.Clear();
                    mRaw.blackLevel = 0;
                    mRaw.blackLevelSeparate[0] = mRaw.blackLevelSeparate[1] = mRaw.blackLevelSeparate[2] = mRaw.blackLevelSeparate[3] = 0;
                    mRaw.whitePoint = 65535;
                }*/
            }

            return mRaw;
        }

        protected override byte[] decodeThumbInternal()
        {
            return null;
        }
    };

}
