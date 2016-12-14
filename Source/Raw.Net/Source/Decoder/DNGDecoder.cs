
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

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

    internal class DngDecoder : TiffDecoder
    {
        bool mFixLjpeg;

        internal DngDecoder(ref Stream file) : base(ref file)
        {
            List<IFD> data = ifd.getIFDsWithTag(TagType.DNGVERSION);
            /*
            if (data.Count != 0)
            {  // We have a dng image entry
                t.tags.TryGetValue(TagType.DNGVERSION, out Tag tag);
                object[] c = tag.data;
                if (Convert.ToInt32(c[0]) > 1)
                    throw new TiffParserException("DNG version too new.");            
            }*/
            var v = data[0].getEntry(TagType.DNGVERSION).data;

            if ((byte)v[0] != 1)
                throw new RawDecoderException("Not a supported DNG image format: " + (int)v[0] + (int)v[1] + (int)v[2] + (int)v[3]);
            if ((byte)v[1] > 4)
                throw new RawDecoderException("Not a supported DNG image format: " + (int)v[0] + (int)v[1] + (int)v[2] + (int)v[3]);

            if (((byte)v[0] <= 1) && ((byte)v[1] < 1))  // Prior to v1.1.xxx  fix LJPEG encoding bug
                mFixLjpeg = true;
            else
                mFixLjpeg = false;
        }

        protected override void DecodeMetaDataInternal()
        {
            // Set the make and model
            var t = ifd.getEntryRecursive(TagType.MAKE);
            var t2 = ifd.getEntryRecursive(TagType.MODEL);
            if (t != null && t != null)
            {
                string make = t.DataAsString;
                string model = t2.DataAsString;
                make = make.Trim();
                model = model.Trim();
                rawImage.metadata.make = make;
                rawImage.metadata.model = model;

                //get cfa
                var cfa = ifd.getEntryRecursive(TagType.CFAPATTERN);
                if (cfa == null)
                {
                    Debug.WriteLine("CFA pattern is not found");
                    rawImage.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
                }
                else
                {
                    rawImage.cfa.setCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
                }

                //more exifs
                var exposure = ifd.getEntryRecursive(TagType.EXPOSURETIME);
                var fn = ifd.getEntryRecursive(TagType.FNUMBER);
                var isoTag = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
                if (isoTag != null) rawImage.metadata.isoSpeed = isoTag.GetInt(0);
                if (exposure != null) rawImage.metadata.exposure = exposure.GetFloat(0);
                if (fn != null) rawImage.metadata.aperture = fn.GetFloat(0);

                var time = ifd.getEntryRecursive(TagType.DATETIMEORIGINAL);
                var timeModify = ifd.getEntryRecursive(TagType.DATETIMEDIGITIZED);
                if (time != null) rawImage.metadata.timeTake = time.DataAsString;
                if (timeModify != null) rawImage.metadata.timeModify = timeModify.DataAsString;
            }
        }

        /* Decodes DNG masked areas into blackareas in the image */
        bool DecodeMaskedAreas(IFD raw)
        {
            Tag masked = raw.getEntry(TagType.MASKEDAREAS);

            if (masked.dataType != TiffDataType.SHORT && masked.dataType != TiffDataType.LONG)
                return false;

            Int32 nrects = (int)masked.dataCount / 4;
            if (0 == nrects)
                return false;

            /* Since we may both have short or int, copy it to int array. */

            masked.GetIntArray(out Int32[] rects, nrects * 4);

            Point2D top = rawImage.offset;

            for (UInt32 i = 0; i < nrects; i++)
            {
                Point2D topleft = new Point2D(rects[i * 4 + 1], rects[i * 4]);
                Point2D bottomright = new Point2D(rects[i * 4 + 3], rects[i * 4 + 2]);
                // Is this a horizontal box, only add it if it covers the active width of the image
                if (topleft.x <= top.x && bottomright.x >= (rawImage.dim.x + top.x))
                    rawImage.blackAreas.Add(new BlackArea(topleft.y, bottomright.y - topleft.y, false));
                // Is it a vertical box, only add it if it covers the active height of the image
                else if (topleft.y <= top.y && bottomright.y >= (rawImage.dim.y + top.y))
                {
                    rawImage.blackAreas.Add(new BlackArea(topleft.x, bottomright.x - topleft.x, true));
                }
            }
            return rawImage.blackAreas.Count != 0;
        }

        bool DecodeBlackLevels(IFD raw)
        {
            Point2D blackdim = new Point2D(1, 1);

            Tag bleveldim = raw.getEntry(TagType.BLACKLEVELREPEATDIM);
            if (bleveldim != null)
            {
                if (bleveldim.dataCount != 2)
                    return false;
                blackdim = new Point2D(bleveldim.GetInt(0), bleveldim.GetInt(1));
            }

            if (blackdim.x == 0 || blackdim.y == 0)
                return false;

            if (raw.getEntry(TagType.BLACKLEVEL) == null)
                return true;

            if (rawImage.cpp != 1)
                return false;

            Tag black_entry = raw.getEntry(TagType.BLACKLEVEL);
            if ((int)black_entry.dataCount < blackdim.x * blackdim.y)
                throw new RawDecoderException("DNG: BLACKLEVEL entry is too small");

            if (blackdim.x < 2 || blackdim.y < 2)
            {
                // We so not have enough to fill all individually, read a single and copy it
                //TODO check if float
                float value = black_entry.GetFloat(0);
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        rawImage.blackLevelSeparate[y * 2 + x] = (int)value;
                }
            }
            else
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        rawImage.blackLevelSeparate[y * 2 + x] = (int)black_entry.GetFloat(y * blackdim.x + x);
                }
            }

            // DNG Spec says we must add black in deltav and deltah

            Tag blackleveldeltav = raw.getEntry(TagType.BLACKLEVELDELTAV);
            if (blackleveldeltav != null)
            {
                if ((int)blackleveldeltav.dataCount < rawImage.dim.y)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAV array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < rawImage.dim.y; i++)
                    black_sum[i & 1] += blackleveldeltav.GetFloat(i);

                for (int i = 0; i < 4; i++)
                    rawImage.blackLevelSeparate[i] += (int)(black_sum[i >> 1] / rawImage.dim.y * 2.0f);
            }


            Tag blackleveldeltah = raw.getEntry(TagType.BLACKLEVELDELTAH);
            if (blackleveldeltah != null)
            {
                if ((int)blackleveldeltah.dataCount < rawImage.dim.x)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAH array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < rawImage.dim.x; i++)
                    black_sum[i & 1] += blackleveldeltah.GetFloat(i);

                for (int i = 0; i < 4; i++)
                    rawImage.blackLevelSeparate[i] += (int)(black_sum[i & 1] / rawImage.dim.x * 2.0f);
            }
            return true;
        }

        void SetBlack(IFD raw)
        {
            if (raw.tags.ContainsKey(TagType.MASKEDAREAS))
                if (DecodeMaskedAreas(raw))
                    return;
            if (raw.getEntry(TagType.BLACKLEVEL) != null)
                DecodeBlackLevels(raw);
        }

        protected override void DecodeRawInternal()
        {
            List<IFD> data = ifd.getIFDsWithTag(TagType.COMPRESSION);

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No image data found");

            // Erase the ones not with JPEG compression
            for (int k = data.Count - 1; k >= 0; k--)
            {
                IFD i = data[k];
                int comp = i.getEntry(TagType.COMPRESSION).GetShort(0);
                bool isSubsampled = false;
                try
                {
                    isSubsampled = (i.getEntry(TagType.NEWSUBFILETYPE).GetInt(0) & 1) != 0; // bit 0 is on if image is subsampled
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
            UInt32 bps = raw.getEntry(TagType.BITSPERSAMPLE).GetUInt(0);

            if (raw.tags.ContainsKey(TagType.SAMPLEFORMAT))
                sample_format = raw.getEntry(TagType.SAMPLEFORMAT).GetUInt(0);

            if (sample_format != 1)
                throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported.");

            rawImage.isCFA = (raw.getEntry(TagType.PHOTOMETRICINTERPRETATION).GetUShort(0) == 32803);

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
                rawImage.dim = new Point2D();
                rawImage.dim.x = raw.getEntry(TagType.IMAGEWIDTH).GetInt(0);
                rawImage.dim.y = raw.getEntry(TagType.IMAGELENGTH).GetInt(0);
            }
            catch (TiffParserException)
            {
                throw new RawDecoderException("DNG Decoder: Could not read basic image information.");
            }

            //init the raw image
            rawImage.Init();
            rawImage.ColorDepth = (ushort)bps;
            int compression = -1;

            try
            {
                compression = raw.getEntry(TagType.COMPRESSION).GetShort(0);
                if (rawImage.isCFA)
                {
                    // Check if layout is OK, if present
                    if (raw.tags.ContainsKey(TagType.CFALAYOUT))
                        if (raw.getEntry(TagType.CFALAYOUT).GetShort(0) != 1)
                            throw new RawDecoderException("DNG Decoder: Unsupported CFA Layout.");

                    Tag cfadim = raw.getEntry(TagType.CFAREPEATPATTERNDIM);
                    if (cfadim.dataCount != 2)
                        throw new RawDecoderException("DNG Decoder: Couldn't read CFA pattern dimension");
                    Tag pDim = raw.getEntry(TagType.CFAREPEATPATTERNDIM); // Get the size
                    var cPat = raw.getEntry(TagType.CFAPATTERN).data;     // Does NOT contain dimensions as some documents state

                    Point2D cfaSize = new Point2D(pDim.GetInt(1), pDim.GetInt(0));
                    rawImage.cfa.setSize(cfaSize);
                    if (cfaSize.area() != raw.getEntry(TagType.CFAPATTERN).dataCount)
                        throw new RawDecoderException("DNG Decoder: CFA pattern dimension and pattern count does not match: " + raw.getEntry(TagType.CFAPATTERN).dataCount);

                    for (int y = 0; y < cfaSize.y; y++)
                    {
                        for (int x = 0; x < cfaSize.x; x++)
                        {
                            UInt32 c1 = Convert.ToUInt32(cPat[x + y * cfaSize.x]);
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
                            rawImage.cfa.setColorAt(new Point2D(x, y), c2);
                        }
                    }
                }

                // Now load the image
                if (compression == 1)
                {  // Uncompressed.
                    try
                    {
                        UInt32 cpp = raw.getEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                        if (cpp > 4)
                            throw new RawDecoderException("DNG Decoder: More than 4 samples per pixel is not supported.");
                        rawImage.cpp = cpp;

                        Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                        Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
                        UInt32 yPerSlice = raw.getEntry(TagType.ROWSPERSTRIP).GetUInt(0);
                        UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).GetUInt(0);
                        UInt32 height = raw.getEntry(TagType.IMAGELENGTH).GetUInt(0);

                        if (counts.dataCount != offsets.dataCount)
                        {
                            throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
                        }

                        UInt32 offY = 0;
                        List<DngStrip> slices = new List<DngStrip>();
                        for (UInt32 s = 0; s < offsets.dataCount; s++)
                        {
                            DngStrip slice = new DngStrip();
                            slice.offset = offsets.GetUInt(s);
                            slice.count = counts.GetUInt(s);
                            slice.offsetY = offY;
                            if (offY + yPerSlice > height)
                                slice.h = height - offY;
                            else
                                slice.h = yPerSlice;

                            offY += yPerSlice;

                            if (reader.isValid(slice.offset, slice.count)) // Only decode if size is valid
                                slices.Add(slice);
                        }

                        for (int i = 0; i < slices.Count; i++)
                        {
                            DngStrip slice = slices[i];
                            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, slice.offset, (uint)reader.BaseStream.Length);
                            Point2D size = new Point2D((int)width, (int)slice.h);
                            Point2D pos = new Point2D(0, (int)slice.offsetY);

                            bool big_endian = (raw.endian == Endianness.big);
                            // DNG spec says that if not 8 or 16 bit/sample, always use big endian
                            if (bps != 8 && bps != 16)
                                big_endian = true;
                            try
                            {
                                ReadUncompressedRaw(ref input, size, pos, (int)(rawImage.cpp * width * bps / 8), (int)bps, big_endian ? BitOrder.Jpeg : BitOrder.Plain);
                            }
                            catch (IOException ex)
                            {
                                if (i > 0)
                                    rawImage.errors.Add(ex.Message);
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

                        rawImage.cpp = (raw.getEntry(TagType.SAMPLESPERPIXEL).GetUInt(0));

                        if (sample_format != 1)
                            throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported for compressed data.");

                        DngDecoderSlices slices = new DngDecoderSlices(reader, rawImage, compression);
                        if (raw.tags.ContainsKey(TagType.TILEOFFSETS))
                        {
                            UInt32 tilew = raw.getEntry(TagType.TILEWIDTH).GetUInt(0);
                            UInt32 tileh = raw.getEntry(TagType.TILELENGTH).GetUInt(0);
                            if (tilew == 0 || tileh == 0)
                                throw new RawDecoderException("DNG Decoder: Invalid tile size");

                            UInt32 tilesX = (uint)(rawImage.dim.x + tilew - 1) / tilew;
                            UInt32 tilesY = (uint)(rawImage.dim.y + tileh - 1) / tileh;
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
                                    DngSliceElement e = new DngSliceElement(offsets.GetUInt(x + y * tilesX), counts.GetUInt(x + y * tilesX), tilew * x, tileh * y);
                                    e.mUseBigtable = tilew * tileh > 1024 * 1024;
                                    slices.addSlice(e);
                                }
                            }
                        }
                        else
                        {  // Strips
                            Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);

                            UInt32 yPerSlice = raw.getEntry(TagType.ROWSPERSTRIP).GetUInt(0);

                            if (counts.dataCount != offsets.dataCount)
                            {
                                throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", stips:" + offsets.dataCount);
                            }

                            if (yPerSlice == 0 || yPerSlice > (UInt32)rawImage.dim.y)
                                throw new RawDecoderException("DNG Decoder: Invalid y per slice");

                            UInt32 offY = 0;
                            for (UInt32 s = 0; s < counts.dataCount; s++)
                            {
                                DngSliceElement e = new DngSliceElement(offsets.GetUInt(s), counts.GetUInt(s), 0, offY);
                                e.mUseBigtable = yPerSlice * rawImage.dim.y > 1024 * 1024;
                                offY += yPerSlice;

                                if (reader.isValid(e.byteOffset, e.byteCount)) // Only decode if size is valid
                                    slices.addSlice(e);
                            }
                        }
                        UInt32 nSlices = (uint)slices.slices.Count;
                        if (nSlices == 0)
                            throw new RawDecoderException("DNG Decoder: No valid slices found.");

                        slices.decodeSlice();

                        if (rawImage.errors.Count >= nSlices)
                            throw new RawDecoderException("DNG Decoding: Too many errors encountered. Giving up.\nFirst Error:" + rawImage.errors[0]);
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

            Tag as_shot_neutral = ifd.getEntryRecursive(TagType.ASSHOTNEUTRAL);
            if (as_shot_neutral != null)
            {
                if (as_shot_neutral.dataCount == 3)
                {
                    for (UInt32 i = 0; i < 3; i++)
                        rawImage.metadata.wbCoeffs[i] = 1.0f / Convert.ToSingle(as_shot_neutral.data[i]);
                }
            }
            else
            {
                Tag as_shot_white_xy = ifd.getEntryRecursive(TagType.ASSHOTWHITEXY);
                if (as_shot_white_xy != null)
                {
                    if (as_shot_white_xy.dataCount == 2)
                    {
                        rawImage.metadata.wbCoeffs[0] = as_shot_white_xy.GetFloat(0);
                        rawImage.metadata.wbCoeffs[1] = as_shot_white_xy.GetFloat(1);
                        rawImage.metadata.wbCoeffs[2] = 1 - rawImage.metadata.wbCoeffs[0] - rawImage.metadata.wbCoeffs[1];

                        float[] d65_white = { 0.950456F, 1, 1.088754F };
                        for (UInt32 i = 0; i < 3; i++)
                            rawImage.metadata.wbCoeffs[i] /= d65_white[i];
                    }
                }
            }


            // Crop
            Tag active_area = raw.getEntry(TagType.ACTIVEAREA);
            if (active_area != null)
            {
                Point2D new_size = new Point2D(rawImage.dim.x, rawImage.dim.y);
                if (active_area.dataCount != 4)
                    throw new RawDecoderException("DNG: active area has " + active_area.dataCount + " values instead of 4");

                active_area.GetIntArray(out int[] corners, 4);
                if (new Point2D(corners[1], corners[0]).isThisInside(rawImage.dim))
                {
                    if (new Point2D(corners[3], corners[2]).isThisInside(rawImage.dim))
                    {
                        Rectangle2D crop = new Rectangle2D(corners[1], corners[0], corners[3] - corners[1], corners[2] - corners[0]);
                        rawImage.Crop(crop);
                    }
                }
            }



            Tag origin_entry = raw.getEntry(TagType.DEFAULTCROPORIGIN);
            Tag size_entry = raw.getEntry(TagType.DEFAULTCROPSIZE);
            if (origin_entry != null && size_entry != null)
            {
                Rectangle2D cropped = new Rectangle2D(0, 0, rawImage.dim.x, rawImage.dim.y);
                /* Read crop position (sometimes is rational so use float) */
                origin_entry.GetFloatArray(out float[] tl, 2);
                if (new Point2D((int)tl[0], (int)tl[1]).isThisInside(rawImage.dim))
                    cropped = new Rectangle2D((int)tl[0], (int)tl[1], 0, 0);

                cropped.dim = rawImage.dim - cropped.pos;
                /* Read size (sometimes is rational so use float) */

                size_entry.GetFloatArray(out float[] sz, 2);
                Point2D size = new Point2D((int)sz[0], (int)sz[1]);
                if ((size + cropped.pos).isThisInside(rawImage.dim))
                    cropped.dim = size;

                if (!cropped.hasPositiveArea())
                    throw new RawDecoderException("DNG Decoder: No positive crop area");

                rawImage.Crop(cropped);
                if (rawImage.isCFA && cropped.pos.x % 2 == 1)
                    rawImage.cfa.shiftLeft(1);
                if (rawImage.isCFA && cropped.pos.y % 2 == 1)
                    rawImage.cfa.shiftDown(1);
            }
            if (rawImage.dim.area() <= 0)
                throw new RawDecoderException("DNG Decoder: No image left after crop");


            // Apply stage 1 opcodes
            if (ApplyStage1DngOpcodes)
            {
                if (raw.tags.ContainsKey(TagType.OPCODELIST1))
                {
                    // Apply stage 1 codes
                    try
                    {
                        //DngOpcodes codes = new DngOpcodes(raw.getEntry(TagType.OPCODELIST1));
                        //mRaw = codes.applyOpCodes(mRaw);
                    }
                    catch (RawDecoderException e)
                    {
                        // We push back errors from the opcode parser, since the image may still be usable
                        rawImage.errors.Add(e.Message);
                    }
                }
            }

            // Linearization
            Tag lintable = raw.getEntry(TagType.LINEARIZATIONTABLE);
            if (lintable != null)
            {
                UInt32 len = lintable.dataCount;
                lintable.GetShortArray(out ushort[] table, (int)len);
                rawImage.SetTable(table, (int)len, true);

                //TODO Fix
                //mRaw.sixteenBitLookup();
                //mRaw.table = (null);

            }

            // Default white level is (2 ** BitsPerSample) - 1
            rawImage.whitePoint = (uint)(1 >> raw.getEntry(TagType.BITSPERSAMPLE).GetShort(0)) - 1;


            Tag whitelevel = raw.getEntry(TagType.WHITELEVEL);
            try
            {
                rawImage.whitePoint = whitelevel.GetUInt(0);
            }
            catch (Exception) { }

            // Set black
            SetBlack(raw);

            //convert to linear value
            //*
            //TODO optimize (super slow)
            double maxVal = Math.Pow(2, rawImage.ColorDepth);
            double coeff = maxVal / (rawImage.whitePoint - rawImage.blackLevelSeparate[0]);
            Parallel.For(rawImage.offset.y, rawImage.dim.y + rawImage.offset.y, y =>
            //for (int y = mRaw.mOffset.y; y < mRaw.dim.y + mRaw.mOffset.y; y++)
            {
                //int offset = ((y % 2) * 2);
                int realY = y * rawImage.dim.x;
                for (int x = rawImage.offset.x; x < rawImage.dim.x + rawImage.offset.x; x++)
                {
                    int pos = realY + x;
                    double val;
                    //Linearisation
                    if (rawImage.table != null)
                        val = rawImage.table.tables[rawImage.rawData[pos]];
                    else val = rawImage.rawData[pos];
                    //Black sub
                    //val -= mRaw.blackLevelSeparate[offset + x % 2];
                    val -= rawImage.blackLevelSeparate[0];
                    //Rescaling
                    //val /= (mRaw.whitePoint - mRaw.blackLevelSeparate[offset + x % 2]);
                    val *= coeff;//change to take into consideration each individual blacklevel
                                 //Clip
                    if (val > maxVal) val = maxVal;
                    else if (val < 0) val = 0;
                    //val *= maxVal;
                    //rescale to colordepth of the original                        
                    rawImage.rawData[pos] = (ushort)val;
                }
            });
            //*/
            // Apply opcodes to lossy DNG 
            if (compression == 0x884c)
            {
                /*
                if (raw.tags.ContainsKey(TagType.OPCODELIST2))
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
        }

        protected override Thumbnail DecodeThumbInternal()
        {
            //find the preview IFD (usually the first if any)
            try
            {
                List<IFD> potential = ifd.getIFDsWithTag(TagType.NEWSUBFILETYPE);
                if (potential != null || potential.Count != 0)
                {
                    IFD thumbIFD = null;
                    for (int i = 0; i < potential.Count; i++)
                    {
                        var subFile = potential[i].getEntry(TagType.NEWSUBFILETYPE);
                        if (subFile.GetInt(0) == 1)
                        {
                            thumbIFD = potential[i];
                            break;
                        }
                    }
                    if (thumbIFD != null)
                    {
                        //there is a thumbnail
                        UInt32 sample_format = 1;
                        UInt32 bps = thumbIFD.getEntry(TagType.BITSPERSAMPLE).GetUInt(0);
                        Point2D dim;
                        if (thumbIFD.tags.ContainsKey(TagType.SAMPLEFORMAT))
                            sample_format = thumbIFD.getEntry(TagType.SAMPLEFORMAT).GetUInt(0);
                        try
                        {
                            dim = new Point2D()
                            {
                                x = thumbIFD.getEntry(TagType.IMAGEWIDTH).GetInt(0),
                                y = thumbIFD.getEntry(TagType.IMAGELENGTH).GetInt(0)
                            };
                        }
                        catch (TiffParserException)
                        {
                            throw new RawDecoderException("DNG Decoder: Could not read basic image information.");
                        }

                        int compression = thumbIFD.getEntry(TagType.COMPRESSION).GetShort(0);
                        // Now load the image
                        if (compression == 1)
                        {  // Uncompressed.

                            UInt32 cpp = thumbIFD.getEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                            if (cpp > 4)
                                throw new RawDecoderException("DNG Decoder: More than 4 samples per pixel is not supported.");


                            Tag offsets = thumbIFD.getEntry(TagType.STRIPOFFSETS);
                            Tag counts = thumbIFD.getEntry(TagType.STRIPBYTECOUNTS);
                            UInt32 yPerSlice = thumbIFD.getEntry(TagType.ROWSPERSTRIP).GetUInt(0);
                            UInt32 width = thumbIFD.getEntry(TagType.IMAGEWIDTH).GetUInt(0);
                            UInt32 height = thumbIFD.getEntry(TagType.IMAGELENGTH).GetUInt(0);

                            if (counts.dataCount != offsets.dataCount)
                            {
                                throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
                            }

                            UInt32 offY = 0;
                            List<DngStrip> slices = new List<DngStrip>();
                            for (UInt32 s = 0; s < offsets.dataCount; s++)
                            {
                                DngStrip slice = new DngStrip();
                                slice.offset = offsets.GetUInt(s);
                                slice.count = counts.GetUInt(s);
                                slice.offsetY = offY;
                                if (offY + yPerSlice > height)
                                    slice.h = height - offY;
                                else
                                    slice.h = yPerSlice;

                                offY += yPerSlice;

                                if (reader.isValid(slice.offset, slice.count)) // Only decode if size is valid
                                    slices.Add(slice);
                            }

                            for (int i = 0; i < slices.Count; i++)
                            {
                                DngStrip slice = slices[i];
                                TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, slice.offset, (uint)reader.BaseStream.Length);
                                Point2D size = new Point2D((int)width, (int)slice.h);
                                Point2D pos = new Point2D(0, (int)slice.offsetY);

                                bool big_endian = (thumbIFD.endian == Endianness.big);
                                // DNG spec says that if not 8 or 16 bit/sample, always use big endian
                                if (bps != 8 && bps != 16)
                                    big_endian = true;
                                try
                                {
                                    // readUncompressedRaw(ref input, size, pos, (int)(mRaw.cpp * width * bps / 8), (int)bps, big_endian ? BitOrder.Jpeg : BitOrder.Plain);
                                }
                                catch (IOException ex)
                                {

                                    throw new RawDecoderException("DNG decoder: IO error occurred in first slice, unable to decode more. Error is: " + ex.Message);
                                }
                            }
                        }
                        else if (compression == 7 || compression == 0x884c)
                        {
                            /*
                            // Let's try loading it as tiles instead

                            uint cpp = (thumbIFD.getEntry(TagType.SAMPLESPERPIXEL).GetUInt(0));

                            if (sample_format != 1)
                                throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported for compressed data.");

                            DngDecoderSlices slices = new DngDecoderSlices(mFile, mRaw, compression);
                            if (thumbIFD.tags.ContainsKey(TagType.TILEOFFSETS))
                            {
                                UInt32 tilew = thumbIFD.getEntry(TagType.TILEWIDTH).GetUInt(0);
                                UInt32 tileh = thumbIFD.getEntry(TagType.TILELENGTH).GetUInt(0);
                                if (tilew == 0 || tileh == 0)
                                    throw new RawDecoderException("DNG Decoder: Invalid tile size");

                                UInt32 tilesX = (uint)(mRaw.dim.x + tilew - 1) / tilew;
                                UInt32 tilesY = (uint)(mRaw.dim.y + tileh - 1) / tileh;
                                UInt32 nTiles = tilesX * tilesY;

                                Tag offsets = thumbIFD.getEntry(TagType.TILEOFFSETS);
                                Tag counts = thumbIFD.getEntry(TagType.TILEBYTECOUNTS);
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
                                Tag offsets = thumbIFD.getEntry(TagType.STRIPOFFSETS);
                                Tag counts = thumbIFD.getEntry(TagType.STRIPBYTECOUNTS);

                                UInt32 yPerSlice = thumbIFD.getEntry(TagType.ROWSPERSTRIP).GetUInt(0);

                                if (counts.dataCount != offsets.dataCount)
                                {
                                    throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", stips:" + offsets.dataCount);
                                }

                                if (yPerSlice == 0 || yPerSlice > (UInt32)dim.y)
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
                            */
                        }
                    }
                }
            }
            catch (Exception)
            {
                //thumbnail are optional so ignore all exception
            }
            return null;
        }
    };

}
