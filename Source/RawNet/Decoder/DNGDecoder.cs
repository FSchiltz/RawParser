using PhotoNet.Common;
using RawNet.Dng;
using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet.Decoder
{
    internal class DNGDecoder : TIFFDecoder
    {
        bool fixLjpeg;
        IFD raw;

        //DNG thumbnail are tiff so no need to override 
        internal DNGDecoder(Stream file) : base(file)
        {
            ScaleValue = true;
            List<IFD> data = ifd.GetIFDsWithTag(TagType.DNGVERSION);

            var v = data[0].GetEntry(TagType.DNGVERSION).GetIntArray();

            if (v[0] != 1)
                throw new RawDecoderException("Not a supported DNG image format: " + v[0] + v[1] + v[2] + v[3]);
            if (v[1] > 4)
                throw new RawDecoderException("Not a supported DNG image format: " + v[0] + v[1] + v[2] + v[3]);

            if ((v[0] <= 1) && (v[1] < 1))  // Prior to v1.1.xxx  fix LJPEG encoding bug
                fixLjpeg = true;
            else
                fixLjpeg = false;
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

            // Linearization
            Tag lintable = raw.GetEntry(TagType.LINEARIZATIONTABLE);
            if (lintable != null)
            {
                rawImage.table = new TableLookUp(lintable.GetUShortArray(), (int)lintable.dataCount, true);
            }

            Tag as_shot_neutral = ifd.GetEntryRecursive(TagType.ASSHOTNEUTRAL);
            if (as_shot_neutral != null)
            {
                if (as_shot_neutral.dataCount == 3)
                {
                    rawImage.metadata.WbCoeffs = new WhiteBalance(1.0f / as_shot_neutral.GetFloat(0), 1.0f / as_shot_neutral.GetFloat(1), 1.0f / as_shot_neutral.GetFloat(2));
                }
            }
            else
            {
                Tag as_shot_white_xy = ifd.GetEntryRecursive(TagType.ASSHOTWHITEXY);
                if (as_shot_white_xy != null)
                {
                    if (as_shot_white_xy.dataCount == 2)
                    {
                        float[] d65_white = { 0.950456F, 1, 1.088754F };
                        rawImage.metadata.WbCoeffs = new WhiteBalance(
                            as_shot_white_xy.GetFloat(0) / d65_white[0],
                            as_shot_white_xy.GetFloat(1) / d65_white[1],
                            (1 - rawImage.metadata.WbCoeffs.Red - rawImage.metadata.WbCoeffs.Green) / d65_white[2]);
                    }
                }
            }

            // Crop
            Tag active_area = raw.GetEntry(TagType.ACTIVEAREA);
            if (active_area != null)
            {
                if (active_area.dataCount != 4)
                    throw new RawDecoderException("Active area has " + active_area.dataCount + " values instead of 4");

                if (new Point2D(active_area.GetUInt(1), active_area.GetUInt(0)).IsThisInside(rawImage.fullSize.dim))
                {
                    if (new Point2D(active_area.GetUInt(3), active_area.GetUInt(2)).IsThisInside(rawImage.fullSize.dim))
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
                Rectangle2D cropped = new Rectangle2D(0, 0, rawImage.fullSize.dim.width, rawImage.fullSize.dim.height);
                /* Read crop position (sometimes is rational so use float) */

                if (new Point2D(origin_entry.GetUInt(0), origin_entry.GetUInt(1)).IsThisInside(rawImage.fullSize.dim))
                    cropped = new Rectangle2D(origin_entry.GetUInt(0), origin_entry.GetUInt(1), 0, 0);

                cropped.Dimension = rawImage.fullSize.dim - cropped.Position;
                /* Read size (sometimes is rational so use float) */

                Point2D size = new Point2D(size_entry.GetUInt(0), size_entry.GetUInt(1));
                if ((size + cropped.Position).IsThisInside(rawImage.fullSize.dim))
                    cropped.Dimension = size;

                if (!cropped.HasPositiveArea())
                    throw new RawDecoderException("No positive crop area");

                rawImage.Crop(cropped);
                if (rawImage.isCFA && cropped.Position.width % 2 == 1)
                    rawImage.colorFilter.ShiftLeft(1);
                if (rawImage.isCFA && cropped.Position.height % 2 == 1)
                    rawImage.colorFilter.ShiftDown(1);
            }
            if (rawImage.fullSize.dim.Area <= 0)
                throw new RawDecoderException("No image left after crop");

            // Default white level is (2 ** BitsPerSample) - 1
            rawImage.whitePoint = (1 >> raw.GetEntry(TagType.BITSPERSAMPLE).GetInt(0) - 1);

            Tag whitelevel = raw.GetEntry(TagType.WHITELEVEL);
            if (whitelevel != null)
            {
                rawImage.whitePoint = whitelevel.GetInt(0);
            }

            // Set black
            SetBlack(raw);
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.COMPRESSION);

            if (data.Count == 0)
                throw new RawDecoderException("No image data found");

            // Erase the ones not with JPEG compression
            for (int k = data.Count - 1; k >= 0; k--)
            {
                IFD i = data[k];
                int comp = i.GetEntry(TagType.COMPRESSION).GetInt(0);
                bool isSubsampled = false;
                try
                {
                    isSubsampled = ((i.GetEntry(TagType.NEWSUBFILETYPE)?.GetInt(0) ?? 0) & 1) != 0; // bit 0 is on if image is subsampled
                }
                catch (RawDecoderException) { }
                if ((comp != 7 && comp != 1 && comp != 0x884c) || isSubsampled)
                {  // Erase if subsampled, or not JPEG or uncompressed
                    data.Remove(i);
                }
            }

            if (data.Count == 0)
                throw new RawDecoderException("No RAW chunks found");

            raw = data[0];
            int sampleFormat = 1;
            int bps = raw.GetEntry(TagType.BITSPERSAMPLE).GetInt(0);

            if (raw.tags.ContainsKey(TagType.SAMPLEFORMAT))
                sampleFormat = raw.GetEntry(TagType.SAMPLEFORMAT).GetInt(0);

            if (sampleFormat != 1)
                throw new RawDecoderException("Only 16 bit unsigned data supported.");

            rawImage.isCFA = (raw.GetEntry(TagType.PHOTOMETRICINTERPRETATION).GetUShort(0) == 32803);

            if (sampleFormat == 1 && bps > 16)
                throw new RawDecoderException("Integer precision larger than 16 bits currently not supported.");

            if (sampleFormat == 3 && bps != 32)
                throw new RawDecoderException("Float point must be 32 bits per sample.");

            rawImage.fullSize.dim = new Point2D()
            {
                width = raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0),
                height = raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0)
            };

            rawImage.Init(false);
            rawImage.fullSize.ColorDepth = (ushort)bps;
            int compression = raw.GetEntry(TagType.COMPRESSION).GetShort(0);

            // Now load the image
            switch (compression)
            {
                case 1:
                    uint cpp = raw.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);
                    if (cpp > 4)
                        throw new RawDecoderException("More than 4 samples per pixel is not supported.");
                    rawImage.fullSize.cpp = cpp;
                    bool big_endian = (raw.endian == Endianness.Big);
                    // DNG spec says that if not 8 or 16 bit/sample, always use big endian
                    if (bps != 8 && bps != 16)
                        big_endian = true;
                    DecodeUncompressed(raw, big_endian ? BitOrder.Jpeg : BitOrder.Plain);
                    break;
                case 7:
                case 0x884c:
                    rawImage.fullSize.cpp = raw.GetEntry(TagType.SAMPLESPERPIXEL).GetUInt(0);                    // Let's try loading it as tiles instead

                    if (sampleFormat != 1)
                        throw new RawDecoderException("Only 16 bit unsigned data supported for compressed data.");

                    DngDecoderSlices slices = new DngDecoderSlices(reader, rawImage);
                    if (raw.tags.ContainsKey(TagType.TILEOFFSETS))
                    {
                        int tilew = raw.GetEntry(TagType.TILEWIDTH).GetInt(0);
                        int tileh = raw.GetEntry(TagType.TILELENGTH).GetInt(0);
                        if (tilew == 0 || tileh == 0)
                            throw new RawDecoderException("Invalid tile size");

                        long tilesX = (rawImage.fullSize.dim.width + tilew - 1) / tilew;
                        long tilesY = (rawImage.fullSize.dim.height + tileh - 1) / tileh;
                        long nTiles = tilesX * tilesY;

                        Tag offsets = raw.GetEntry(TagType.TILEOFFSETS);
                        Tag counts = raw.GetEntry(TagType.TILEBYTECOUNTS);
                        if (offsets.dataCount != counts.dataCount || offsets.dataCount != nTiles)
                            throw new RawDecoderException("Tile count mismatch: offsets:" + offsets.dataCount + " count:" + counts.dataCount + ", calculated:" + nTiles);

                        slices.FixLjpeg = fixLjpeg;

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
                            throw new RawDecoderException("Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);

                        if (yPerSlice == 0 || yPerSlice > rawImage.fullSize.dim.height)
                            throw new RawDecoderException("Invalid y per slice");

                        uint offY = 0;
                        for (int s = 0; s < counts.dataCount; s++)
                        {
                            DngSliceElement e = new DngSliceElement(offsets.GetUInt(s), counts.GetUInt(s), 0, offY)
                            {
                                mUseBigtable = yPerSlice * rawImage.fullSize.dim.height > 1024 * 1024
                            };
                            offY += yPerSlice;

                            if (reader.IsValid(e.byteOffset, e.byteCount)) // Only decode if size is valid
                                slices.slices.Add(e);
                        }
                    }

                    if (slices.slices.Count == 0)
                        throw new RawDecoderException("No valid slices found.");

                    slices.DecodeSlice();
                    if (rawImage.errors.Count >= slices.slices.Count)
                        throw new RawDecoderException("Too many errors encountered. Giving up.\nFirst Error:" + rawImage.errors[0]);
                    break;
                default:
                    throw new RawDecoderException("Unknown compression: " + compression);
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

            Point2D top = rawImage.fullSize.offset;

            for (int i = 0; i < nrects; i++)
            {
                Point2D topleft = new Point2D(rects[i * 4 + 1], rects[i * 4]);
                Point2D bottomright = new Point2D(rects[i * 4 + 3], rects[i * 4 + 2]);
                // Is this a horizontal box, only add it if it covers the active width of the image
                if (topleft.width <= top.width && bottomright.width >= (rawImage.fullSize.dim.width + top.width))
                {
                    rawImage.blackAreas.Add(new BlackArea(topleft.height, bottomright.height - topleft.height, false));
                }
                else if (topleft.height <= top.height && bottomright.height >= (rawImage.fullSize.dim.height + top.height))
                {
                    // Is it a vertical box, only add it if it covers the active height of the image
                    rawImage.blackAreas.Add(new BlackArea(topleft.width, bottomright.width - topleft.width, true));
                }
            }
            return rawImage.blackAreas.Count != 0;
        }

        bool Decodeblacks(IFD raw)
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

            if (rawImage.fullSize.cpp != 1)
                return false;

            Tag black_entry = raw.GetEntry(TagType.BLACKLEVEL);
            if ((int)black_entry.dataCount < blackdim.width * blackdim.height)
                throw new RawDecoderException("Black level entry is too small");

            if (black_entry.dataCount > 1) Debug.Assert(black_entry.GetFloat(0) == black_entry.GetFloat(1));
            rawImage.black = (int)black_entry.GetFloat(0);
            /*
            if (blackdim.Width < 2 || blackdim.Height < 2)
            {
                // We so not have enough to fill all individually, read a single and copy it
                rawImage.black = (int)black_entry.GetFloat(0);
            }
            else
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        rawImage.blackLevelSeparate[y * 2 + x] = (int)black_entry.GetFloat((int)(y * blackdim.Width + x));
                }
            }*/

            // DNG Spec says we must add black in deltav and deltah
            Tag blackleveldeltav = raw.GetEntry(TagType.BLACKLEVELDELTAV);
            if (blackleveldeltav != null)
            {
                if ((int)blackleveldeltav.dataCount < rawImage.fullSize.dim.height) throw new RawDecoderException("BLACKLEVELDELTAV array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < rawImage.fullSize.dim.height; i++)
                {
                    black_sum[i & 1] += blackleveldeltav.GetFloat(i);
                }

                Debug.Assert(black_sum[0] == black_sum[1]);
                rawImage.black += (int)(black_sum[0] / rawImage.fullSize.dim.height * 2.0f);
                //for (int i = 0; i < 4; i++)                  rawImage.blackLevelSeparate[i] += (int)(black_sum[i >> 1] / rawImage.raw.dim.Height * 2.0f);
            }


            Tag blackleveldeltah = raw.GetEntry(TagType.BLACKLEVELDELTAH);
            if (blackleveldeltah != null)
            {
                if ((int)blackleveldeltah.dataCount < rawImage.fullSize.dim.width) throw new RawDecoderException("BLACKLEVELDELTAH array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < rawImage.fullSize.dim.width; i++)
                {
                    black_sum[i & 1] += blackleveldeltah.GetFloat(i);
                }

                Debug.Assert(black_sum[0] == black_sum[1]);
                rawImage.black += (int)(black_sum[0] / rawImage.fullSize.dim.width * 2.0f);
                //for (int i = 0; i < 4; i++)                    rawImage.blackLevelSeparate[i] += (int)(black_sum[i & 1] / rawImage.raw.dim.Width * 2.0f);
            }
            return true;
        }

        void SetBlack(IFD raw)
        {
            if (raw.tags.ContainsKey(TagType.MASKEDAREAS))
                if (DecodeMaskedAreas(raw))
                    return;
            if (raw.GetEntry(TagType.BLACKLEVEL) != null)
                Decodeblacks(raw);
        }
    };
}