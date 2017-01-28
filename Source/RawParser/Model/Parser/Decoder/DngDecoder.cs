using RawNet.DNG;
using RawNet.Format.TIFF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RawNet.Decoder
{
    class DngStrip
    {
        public DngStrip() { offset = count = offsetY = 0; h = 0; }
        public uint h;
        public uint offset; // Offset in bytes
        public uint count;
        public uint offsetY;
    };

    internal class DNGDecoder : TIFFDecoder
    {
        bool mFixLjpeg;

        //DNG thumbnail are tiff so no need to override 
        internal DNGDecoder(Stream file) : base(file)
        {
            ScaleValue = true;
            List<IFD> data = ifd.GetIFDsWithTag(TagType.DNGVERSION);
            /*
            if (data.Count != 0)
            {  // We have a dng image entry
                t.tags.TryGetValue(TagType.DNGVERSION, out Tag tag);
                object[] c = tag.data;
                if (Convert.ToInt32(c[0]) > 1)
                    throw new RawDecoderException("DNG version too new.");            
            }*/
            var v = data[0].GetEntry(TagType.DNGVERSION).GetIntArray();

            if (v[0] != 1)
                throw new RawDecoderException("Not a supported DNG image format: " + v[0] + v[1] + v[2] + v[3]);
            if (v[1] > 4)
                throw new RawDecoderException("Not a supported DNG image format: " + v[0] + v[1] + v[2] + v[3]);

            if ((v[0] <= 1) && (v[1] < 1))  // Prior to v1.1.xxx  fix LJPEG encoding bug
                mFixLjpeg = true;
            else
                mFixLjpeg = false;
        }

        public override void DecodeMetadata()
        {
            base.DecodeMetadata();
            //get transform matrix
            var matrix = ifd.GetEntryRecursive(TagType.FORWARDMATRIX1);
            if (matrix == null) matrix = ifd.GetEntryRecursive(TagType.FORWARDMATRIX2);
            if (matrix != null)
            {
                rawImage.convertionM = new double[3, 3];
                for (int i = 0; i < 3; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        rawImage.convertionM[i, k] = matrix.GetDouble(i * 3 + k);
                    }
                }
            }
            //get cfa
            var cfa = ifd.GetEntryRecursive(TagType.CFAPATTERN);
            if (cfa == null)
            {
                Debug.WriteLine("CFA pattern is not found");
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            }
            else
            {
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.COMPRESSION);

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No image data found");

            // Erase the ones not with JPEG compression
            for (int k = data.Count - 1; k >= 0; k--)
            {
                IFD i = data[k];
                int comp = i.GetEntry(TagType.COMPRESSION).GetInt(0);
                bool isSubsampled = false;
                try
                {
                    isSubsampled = (i.GetEntry(TagType.NEWSUBFILETYPE).GetInt(0) & 1) != 0; // bit 0 is on if image is subsampled
                }
                catch (RawDecoderException) { }
                if ((comp != 7 && comp != 1 && comp != 0x884c) || isSubsampled)
                {  // Erase if subsampled, or not JPEG or uncompressed
                    data.Remove(i);
                }
            }

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No RAW chunks found");

            IFD raw = data[0];
            int sampleFormat = 1;
            int bps = raw.GetEntry(TagType.BITSPERSAMPLE).GetInt(0);

            if (raw.tags.ContainsKey(TagType.SAMPLEFORMAT))
                sampleFormat = raw.GetEntry(TagType.SAMPLEFORMAT).GetInt(0);

            if (sampleFormat != 1)
                throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported.");

            rawImage.isCFA = (raw.GetEntry(TagType.PHOTOMETRICINTERPRETATION).GetUShort(0) == 32803);

            if (sampleFormat == 1 && bps > 16)
                throw new RawDecoderException("DNG Decoder: Integer precision larger than 16 bits currently not supported.");

            if (sampleFormat == 3 && bps != 32)
                throw new RawDecoderException("DNG Decoder: Float point must be 32 bits per sample.");
            try
            {
                rawImage.raw.dim = new Point2D()
                {
                    width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0),
                    height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0)
                };
            }
            catch (RawDecoderException)
            {
                throw new RawDecoderException("DNG Decoder: Could not read basic image information.");
            }
            rawImage.Init(false);
            rawImage.raw.ColorDepth = (ushort)bps;
            int compression = -1;

            try
            {
                compression = raw.GetEntry(TagType.COMPRESSION).GetShort(0);
                if (rawImage.isCFA)
                {
                    ReadCFA(raw);
                }
                // Now load the image
                if (compression == 1)
                {  // Uncompressed.
                    try
                    {
                        uint cpp = raw.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                        if (cpp > 4)
                            throw new RawDecoderException("DNG Decoder: More than 4 samples per pixel is not supported.");
                        rawImage.cpp = cpp;

                        Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
                        Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);
                        uint yPerSlice = raw.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);
                        uint width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0);
                        uint height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0);

                        if (counts.dataCount != offsets.dataCount)
                        {
                            throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
                        }

                        uint offY = 0;
                        List<DngStrip> slices = new List<DngStrip>();
                        for (int s = 0; s < offsets.dataCount; s++)
                        {
                            DngStrip slice = new DngStrip()
                            {
                                offset = offsets.GetUInt(s),
                                count = counts.GetUInt(s),
                                offsetY = offY
                            };
                            if (offY + yPerSlice > height)
                                slice.h = height - offY;
                            else
                                slice.h = yPerSlice;

                            offY += yPerSlice;

                            if (reader.IsValid(slice.offset, slice.count)) // Only decode if size is valid
                                slices.Add(slice);
                        }

                        for (int i = 0; i < slices.Count; i++)
                        {
                            DngStrip slice = slices[i];
                            TIFFBinaryReader input = new TIFFBinaryReader(reader.BaseStream, slice.offset);
                            Point2D size = new Point2D(width, slice.h);
                            Point2D pos = new Point2D(0, slice.offsetY);

                            bool big_endian = (raw.endian == Endianness.Big);
                            // DNG spec says that if not 8 or 16 bit/sample, always use big endian
                            if (bps != 8 && bps != 16)
                                big_endian = true;
                            try
                            {
                                ReadUncompressedRaw(input, size, pos, (int)(rawImage.cpp * width * bps / 8), bps, big_endian ? BitOrder.Jpeg : BitOrder.Plain);
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
                    catch (RawDecoderException)
                    {
                        throw new RawDecoderException("DNG Decoder: Unsupported format, uncompressed with no strips.");
                    }
                }
                else if (compression == 7 || compression == 0x884c)
                {
                    try
                    {
                        // Let's try loading it as tiles instead

                        rawImage.cpp = raw.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);

                        if (sampleFormat != 1)
                            throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported for compressed data.");

                        DngDecoderSlices slices = new DngDecoderSlices(reader, rawImage, compression);
                        if (raw.tags.ContainsKey(TagType.TILEOFFSETS))
                        {
                            int tilew = raw.GetEntry(TagType.TILEWIDTH).GetInt(0);
                            int tileh = raw.GetEntry(TagType.TILELENGTH).GetInt(0);
                            if (tilew == 0 || tileh == 0)
                                throw new RawDecoderException("DNG Decoder: Invalid tile size");

                            long tilesX = (rawImage.raw.dim.width + tilew - 1) / tilew;
                            long tilesY = (rawImage.raw.dim.height + tileh - 1) / tileh;
                            long nTiles = tilesX * tilesY;

                            Tag offsets = raw.GetEntry(TagType.TILEOFFSETS);
                            Tag counts = raw.GetEntry(TagType.TILEBYTECOUNTS);
                            if (offsets.dataCount != counts.dataCount || offsets.dataCount != nTiles)
                                throw new RawDecoderException("DNG Decoder: Tile count mismatch: offsets:" + offsets.dataCount + " count:" + counts.dataCount + ", calculated:" + nTiles);

                            slices.FixLjpeg = mFixLjpeg;

                            for (int y = 0; y < tilesY; y++)
                            {
                                for (int x = 0; x < tilesX; x++)
                                {
                                    DngSliceElement e = new DngSliceElement(offsets.GetUInt((int)(x + y * tilesX)), counts.GetUInt((int)(x + y * tilesX)), (uint)(tilew * x), (uint)(tileh * y))
                                    {
                                        mUseBigtable = tilew * tileh > 1024 * 1024
                                    };
                                    slices.slices.Add(e);
                                }
                            }
                        }
                        else
                        {  // Strips
                            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
                            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);

                            uint yPerSlice = raw.GetEntry(TagType.ROWSPERSTRIP).GetUInt(0);

                            if (counts.dataCount != offsets.dataCount)
                            {
                                throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", stips:" + offsets.dataCount);
                            }

                            if (yPerSlice == 0 || yPerSlice > rawImage.raw.dim.height)
                                throw new RawDecoderException("DNG Decoder: Invalid y per slice");

                            uint offY = 0;
                            for (int s = 0; s < counts.dataCount; s++)
                            {
                                DngSliceElement e = new DngSliceElement(offsets.GetUInt(s), counts.GetUInt(s), 0, offY)
                                {
                                    mUseBigtable = yPerSlice * rawImage.raw.dim.height > 1024 * 1024
                                };
                                offY += yPerSlice;

                                if (reader.IsValid(e.byteOffset, e.byteCount)) // Only decode if size is valid
                                    slices.slices.Add(e);
                            }
                        }
                        if (slices.slices.Count == 0)
                            throw new RawDecoderException("DNG Decoder: No valid slices found.");

                        slices.DecodeSlice();

                        if (rawImage.errors.Count >= slices.slices.Count)
                            throw new RawDecoderException("DNG Decoding: Too many errors encountered. Giving up.\nFirst Error:" + rawImage.errors[0]);
                    }
                    catch (RawDecoderException)
                    {
                        throw;
                    }
                }
                else
                {
                    throw new RawDecoderException("DNG Decoder: Unknown compression: " + compression);
                }
            }
            catch (RawDecoderException)
            {
                throw;
            }

            Tag as_shot_neutral = ifd.GetEntryRecursive(TagType.ASSHOTNEUTRAL);
            if (as_shot_neutral != null)
            {
                if (as_shot_neutral.dataCount == 3)
                {
                    for (int i = 0; i < 3; i++)
                        rawImage.metadata.WbCoeffs[i] = 1.0f / as_shot_neutral.GetFloat(i);
                }
            }
            else
            {
                Tag as_shot_white_xy = ifd.GetEntryRecursive(TagType.ASSHOTWHITEXY);
                if (as_shot_white_xy != null)
                {
                    if (as_shot_white_xy.dataCount == 2)
                    {
                        rawImage.metadata.WbCoeffs[0] = as_shot_white_xy.GetFloat(0);
                        rawImage.metadata.WbCoeffs[1] = as_shot_white_xy.GetFloat(1);
                        rawImage.metadata.WbCoeffs[2] = 1 - rawImage.metadata.WbCoeffs[0] - rawImage.metadata.WbCoeffs[1];

                        float[] d65_white = { 0.950456F, 1, 1.088754F };
                        for (int i = 0; i < 3; i++)
                            rawImage.metadata.WbCoeffs[i] /= d65_white[i];
                    }
                }
            }


            // Crop
            Tag active_area = raw.GetEntry(TagType.ACTIVEAREA);
            if (active_area != null)
            {
                if (active_area.dataCount != 4)
                    throw new RawDecoderException("DNG: active area has " + active_area.dataCount + " values instead of 4");

                //active_area.GetIntArray(out int[] corners, 4);
                if (new Point2D(active_area.GetUInt(1), active_area.GetUInt(0)).IsThisInside(rawImage.raw.dim))
                {
                    if (new Point2D(active_area.GetUInt(3), active_area.GetUInt(2)).IsThisInside(rawImage.raw.dim))
                    {
                        Rectangle2D crop = new Rectangle2D(active_area.GetUInt(1), active_area.GetUInt(0),
                            active_area.GetUInt(3) - active_area.GetUInt(1), active_area.GetUInt(2) - active_area.GetUInt(0));
                        rawImage.Crop(crop);
                    }
                }
            }

            Tag origin_entry = raw.GetEntry(TagType.DEFAULTCROPORIGIN);
            Tag size_entry = raw.GetEntry(TagType.DEFAULTCROPSIZE);
            if (origin_entry != null && size_entry != null)
            {
                Rectangle2D cropped = new Rectangle2D(0, 0, rawImage.raw.dim.width, rawImage.raw.dim.height);
                /* Read crop position (sometimes is rational so use float) */

                if (new Point2D(origin_entry.GetUInt(0), origin_entry.GetUInt(1)).IsThisInside(rawImage.raw.dim))
                    cropped = new Rectangle2D(origin_entry.GetUInt(0), origin_entry.GetUInt(1), 0, 0);

                cropped.Dimension = rawImage.raw.dim - cropped.Position;
                /* Read size (sometimes is rational so use float) */

                Point2D size = new Point2D(size_entry.GetUInt(0), size_entry.GetUInt(1));
                if ((size + cropped.Position).IsThisInside(rawImage.raw.dim))
                    cropped.Dimension = size;

                if (!cropped.HasPositiveArea())
                    throw new RawDecoderException("DNG Decoder: No positive crop area");

                rawImage.Crop(cropped);
                if (rawImage.isCFA && cropped.Position.width % 2 == 1)
                    rawImage.colorFilter.ShiftLeft(1);
                if (rawImage.isCFA && cropped.Position.height % 2 == 1)
                    rawImage.colorFilter.ShiftDown(1);
            }
            if (rawImage.raw.dim.Area() <= 0)
                throw new RawDecoderException("DNG Decoder: No image left after crop");


            // Apply stage 1 opcodes
            var opcodes = ifd.GetEntryRecursive(TagType.OPCODELIST1);
            if (opcodes != null)
            {
                // Apply stage 1 codes
                try
                {
                    DngOpcodes codes = new DngOpcodes(opcodes);
                    rawImage = codes.ApplyOpCodes(rawImage);
                }
                catch (RawDecoderException e)
                {
                    // We push back errors from the opcode parser, since the image may still be usable
                    rawImage.errors.Add(e.Message);
                }
            }

            // Linearization
            Tag lintable = raw.GetEntry(TagType.LINEARIZATIONTABLE);
            if (lintable != null)
            {
                var table = lintable.GetUShortArray();
                rawImage.SetTable(table, (int)lintable.dataCount, true);

                //mRaw.sixteenBitLookup();
                //mRaw.table = (null);

            }

            // Default white level is (2 ** BitsPerSample) - 1
            rawImage.whitePoint = (1 >> raw.GetEntry(TagType.BITSPERSAMPLE).GetInt(0) - 1);

            Tag whitelevel = raw.GetEntry(TagType.WHITELEVEL);
            if (whitelevel != null)
            {
                rawImage.whitePoint = whitelevel.GetInt(0);
            }

            // Set black
            SetBlack(raw);

            //convert to linear value
            double maxVal = Math.Pow(2, rawImage.raw.ColorDepth);
            double coeff = maxVal / (rawImage.whitePoint - rawImage.blackLevelSeparate[0]);
            Parallel.For(rawImage.raw.offset.height, rawImage.raw.dim.height + rawImage.raw.offset.height, y =>
            {
                long realY = y * rawImage.raw.dim.width;
                for (uint x = rawImage.raw.offset.width; x < rawImage.raw.dim.width + rawImage.raw.offset.width; x++)
                {
                    long pos = realY + x;
                    double val;
                    //Linearisation
                    if (rawImage.table != null)
                        val = rawImage.table.tables[rawImage.raw.rawView[pos]];
                    else val = rawImage.raw.rawView[pos];
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
                    rawImage.raw.rawView[pos] = (ushort)val;
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
                }
                
            }*/
                var opcodes2 = ifd.GetEntryRecursive(TagType.OPCODELIST2);
                if (opcodes2 != null)
                {
                    // Apply stage 2 codes
                    try
                    {
                        DngOpcodes codes = new DngOpcodes(opcodes2);
                        rawImage = codes.ApplyOpCodes(rawImage);
                    }
                    catch (RawDecoderException e)
                    {
                        // We push back errors from the opcode parser, since the image may still be usable
                        rawImage.errors.Add(e.Message);
                    }
                }
            }
        }

        /* Decodes DNG masked areas into blackareas in the image */
        bool DecodeMaskedAreas(IFD raw)
        {
            Tag masked = raw.GetEntry(TagType.MASKEDAREAS);

            if (masked.dataType != TiffDataType.SHORT && masked.dataType != TiffDataType.LONG)
                return false;

            Int32 nrects = (int)masked.dataCount / 4;
            if (0 == nrects)
                return false;

            /* Since we may both have short or int, copy it to int array. */
            var rects = masked.GetUIntArray();

            Point2D top = rawImage.raw.offset;

            for (int i = 0; i < nrects; i++)
            {
                Point2D topleft = new Point2D(rects[i * 4 + 1], rects[i * 4]);
                Point2D bottomright = new Point2D(rects[i * 4 + 3], rects[i * 4 + 2]);
                // Is this a horizontal box, only add it if it covers the active width of the image
                if (topleft.width <= top.width && bottomright.width >= (rawImage.raw.dim.width + top.width))
                    rawImage.blackAreas.Add(new BlackArea(topleft.height, bottomright.height - topleft.height, false));
                // Is it a vertical box, only add it if it covers the active height of the image
                else if (topleft.height <= top.height && bottomright.height >= (rawImage.raw.dim.height + top.height))
                {
                    rawImage.blackAreas.Add(new BlackArea(topleft.width, bottomright.width - topleft.width, true));
                }
            }
            return rawImage.blackAreas.Count != 0;
        }

        bool DecodeBlackLevels(IFD raw)
        {
            Point2D blackdim = new Point2D(1, 1);

            Tag bleveldim = raw.GetEntry(TagType.BLACKLEVELREPEATDIM);
            if (bleveldim != null)
            {
                if (bleveldim.dataCount != 2)
                    return false;
                blackdim = new Point2D(bleveldim.GetUInt(0), bleveldim.GetUInt(1));
            }

            if (blackdim.width == 0 || blackdim.height == 0)
                return false;

            if (raw.GetEntry(TagType.BLACKLEVEL) == null)
                return true;

            if (rawImage.cpp != 1)
                return false;

            Tag black_entry = raw.GetEntry(TagType.BLACKLEVEL);
            if ((int)black_entry.dataCount < blackdim.width * blackdim.height)
                throw new RawDecoderException("DNG: BLACKLEVEL entry is too small");

            if (blackdim.width < 2 || blackdim.height < 2)
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
                        rawImage.blackLevelSeparate[y * 2 + x] = (int)black_entry.GetFloat((int)(y * blackdim.width + x));
                }
            }

            // DNG Spec says we must add black in deltav and deltah

            Tag blackleveldeltav = raw.GetEntry(TagType.BLACKLEVELDELTAV);
            if (blackleveldeltav != null)
            {
                if ((int)blackleveldeltav.dataCount < rawImage.raw.dim.height)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAV array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < rawImage.raw.dim.height; i++)
                    black_sum[i & 1] += blackleveldeltav.GetFloat(i);

                for (int i = 0; i < 4; i++)
                    rawImage.blackLevelSeparate[i] += (int)(black_sum[i >> 1] / rawImage.raw.dim.height * 2.0f);
            }


            Tag blackleveldeltah = raw.GetEntry(TagType.BLACKLEVELDELTAH);
            if (blackleveldeltah != null)
            {
                if ((int)blackleveldeltah.dataCount < rawImage.raw.dim.width)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAH array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < rawImage.raw.dim.width; i++)
                    black_sum[i & 1] += blackleveldeltah.GetFloat(i);

                for (int i = 0; i < 4; i++)
                    rawImage.blackLevelSeparate[i] += (int)(black_sum[i & 1] / rawImage.raw.dim.width * 2.0f);
            }
            return true;
        }

        void SetBlack(IFD raw)
        {
            if (raw.tags.ContainsKey(TagType.MASKEDAREAS))
                if (DecodeMaskedAreas(raw))
                    return;
            if (raw.GetEntry(TagType.BLACKLEVEL) != null)
                DecodeBlackLevels(raw);
        }

        protected void ReadCFA(IFD raw)
        {
            // Check if layout is OK, if present
            if (raw.tags.ContainsKey(TagType.CFALAYOUT))
                if (raw.GetEntry(TagType.CFALAYOUT).GetShort(0) != 1)
                    throw new RawDecoderException("DNG Decoder: Unsupported CFA Layout.");

            Tag cfadim = raw.GetEntry(TagType.CFAREPEATPATTERNDIM);
            if (cfadim.dataCount != 2)
                throw new RawDecoderException("DNG Decoder: Couldn't read CFA pattern dimension");
            Tag pDim = raw.GetEntry(TagType.CFAREPEATPATTERNDIM); // Get the size
            var cPat = raw.GetEntry(TagType.CFAPATTERN).GetIntArray();     // Does NOT contain dimensions as some documents state

            Point2D cfaSize = new Point2D(pDim.GetUInt(1), pDim.GetUInt(0));
            rawImage.colorFilter.SetSize(cfaSize);
            if (cfaSize.Area() != raw.GetEntry(TagType.CFAPATTERN).dataCount)
                throw new RawDecoderException("DNG Decoder: CFA pattern dimension and pattern count does not match: " + raw.GetEntry(TagType.CFAPATTERN).dataCount);

            for (uint y = 0; y < cfaSize.height; y++)
            {
                for (uint x = 0; x < cfaSize.width; x++)
                {
                    int c1 = cPat[x + y * cfaSize.width];
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
                    rawImage.colorFilter.SetColorAt(new Point2D(x, y), c2);
                }
            }
        }
    };
}
