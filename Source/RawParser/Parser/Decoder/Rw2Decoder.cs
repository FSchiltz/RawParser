using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    class Rw2Decoder : TiffDecoder
    {
        UInt32 load_flags;
        TIFFBinaryReader input_start;

        internal Rw2Decoder(Stream reader) : base(reader) { }

        public override Thumbnail DecodeThumb()
        {
            //find the preview ifd Preview is in the rootIFD (smaller preview in subiFD use those)
            List<IFD> possible = ifd.GetIFDsWithTag(TagType.JPEGINTERCHANGEFORMAT);
            //no thumbnail
            if (possible == null || possible.Count == 0) return null;
            IFD preview = possible[possible.Count - 1];

            var thumb = preview.GetEntry(TagType.JPEGINTERCHANGEFORMAT);
            var size = preview.GetEntry(TagType.JPEGINTERCHANGEFORMATLENGTH);
            if (size == null || thumb == null) return null;

            reader.Position = (uint)(thumb.data[0]);
            Thumbnail temp = new Thumbnail()
            {
                data = reader.ReadBytes(Convert.ToInt32(size.data[0])),
                Type = ThumbnailType.JPEG,
                dim = new Point2D()
            };
            return temp;
        }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.PANASONIC_STRIPOFFSET);

            bool isOldPanasonic = false;

            if (data.Count == 0)
            {
                data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);
                if (data == null)
                    throw new RawDecoderException("RW2 Decoder: No image data found");
                isOldPanasonic = true;
            }

            IFD raw = data[0];
            Int32 height = raw.GetEntry((TagType)3).GetInt(0);
            Int32 width = raw.GetEntry((TagType)2).GetInt(0);

            if (isOldPanasonic)
            {
                Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);

                if (offsets.dataCount != 1)
                {
                    throw new RawDecoderException("RW2 Decoder: Multiple Strips found:" + offsets.dataCount);
                }
                uint off = offsets.GetUInt(0);
                if (!reader.IsValid(off))
                    throw new RawDecoderException("Panasonic RAW Decoder: Invalid image data offset, cannot decode.");

                rawImage.raw.dim = new Point2D(width, height);
                rawImage.Init();

                UInt32 size = (uint)(reader.BaseStream.Length - off);
                TIFFBinaryReader input_start = new TIFFBinaryReader(stream, off);

                if (size >= width * height * 2)
                {
                    // It's completely unpacked little-endian
                    Decode12BitRawUnpacked(input_start, width, height);
                    rawImage.ColorDepth = 12;
                }
                else if (size >= width * height * 3 / 2)
                {
                    // It's a packed format
                    Decode12BitRawWithControl(input_start, width, height);
                    rawImage.ColorDepth = 12;
                }
                else
                {
                    var colorTag = raw.GetEntry((TagType)5);
                    if (colorTag != null)
                    {
                        rawImage.ColorDepth = colorTag.GetUShort(0);
                    }
                    else
                    {
                        //try to load with 12bits colordepth
                    }
                    // It's using the new .RW2 decoding method
                    load_flags = 0;
                    DecodeRw2();
                }
            }
            else
            {

                rawImage.raw.dim = new Point2D(width, height);
                rawImage.Init();
                Tag offsets = raw.GetEntry(TagType.PANASONIC_STRIPOFFSET);

                if (offsets.dataCount != 1)
                {
                    throw new RawDecoderException("RW2 Decoder: Multiple Strips found:" + offsets.dataCount);
                }

                load_flags = 0x2008;
                uint off = offsets.GetUInt(0);

                if (!reader.IsValid(off))
                    throw new RawDecoderException("RW2 Decoder: Invalid image data offset, cannot decode.");

                input_start = new TIFFBinaryReader(stream, off);
                DecodeRw2();
            }
            // Read blacklevels
            var rTag = raw.GetEntry((TagType)0x1c);
            var gTag = raw.GetEntry((TagType)0x1d);
            var bTag = raw.GetEntry((TagType)0x1e);
            if (rTag != null && gTag != null && bTag != null)
            {
                rawImage.blackLevelSeparate[0] = rTag.GetInt(0) + 15;
                rawImage.blackLevelSeparate[1] = rawImage.blackLevelSeparate[2] = gTag.GetInt(0) + 15;
                rawImage.blackLevelSeparate[3] = bTag.GetInt(0) + 15;
            }

            // Read WB levels
            var rWBTag = raw.GetEntry((TagType)0x0024);
            var gWBTag = raw.GetEntry((TagType)0x0025);
            var bWBTag = raw.GetEntry((TagType)0x0026);
            if (rWBTag != null && gWBTag != null && bWBTag != null)
            {
                rawImage.metadata.WbCoeffs[0] = rWBTag.GetShort(0);
                rawImage.metadata.WbCoeffs[1] = gWBTag.GetShort(0);
                rawImage.metadata.WbCoeffs[2] = bWBTag.GetShort(0);
            }
            else
            {
                var wb1Tag = raw.GetEntry((TagType)0x0011);
                var wb2Tag = raw.GetEntry((TagType)0x0012);
                if (wb1Tag != null && wb2Tag != null)
                {
                    rawImage.metadata.WbCoeffs[0] = wb1Tag.GetShort(0);
                    rawImage.metadata.WbCoeffs[1] = 256.0f;
                    rawImage.metadata.WbCoeffs[2] = wb2Tag.GetShort(0);
                }
            }
            rawImage.metadata.WbCoeffs[0] /= rawImage.metadata.WbCoeffs[1];
            rawImage.metadata.WbCoeffs[2] /= rawImage.metadata.WbCoeffs[1];
            rawImage.metadata.WbCoeffs[1] /= rawImage.metadata.WbCoeffs[1];
        }

        unsafe void DecodeRw2()
        {
            int i, j, sh = 0;
            int[] pred = new int[2], nonz = new int[2];
            int w = rawImage.raw.dim.width / 14;

            bool zero_is_bad = true;
            if (hints.ContainsKey("zero_is_not_bad"))
                zero_is_bad = false;

            PanaBitpump bits = new PanaBitpump(input_start, load_flags);
            List<Int32> zero_pos = new List<int>();
            for (int y = 0; y < rawImage.raw.dim.height; y++)
            {
                fixed (UInt16* t = &rawImage.raw.data[y * rawImage.raw.dim.width])
                {
                    UInt16* dest = t;
                    for (int x = 0; x < w; x++)
                    {
                        pred[0] = pred[1] = nonz[0] = nonz[1] = 0;
                        int u = 0;
                        for (i = 0; i < 14; i++)
                        {
                            // Even pixels
                            if (u == 2)
                            {
                                sh = 4 >> (int)(3 - bits.GetBits(2));
                                u = -1;
                            }
                            if (nonz[0] != 0)
                            {
                                if (0 != (j = (int)bits.GetBits(8)))
                                {
                                    if ((pred[0] -= 0x80 << sh) < 0 || sh == 4)
                                        pred[0] &= ~(-1 << sh);
                                    pred[0] += j << sh;
                                }
                            }
                            else if ((nonz[0] = (int)bits.GetBits(8)) != 0 || i > 11)
                                pred[0] = (int)(nonz[0] << 4 | bits.GetBits(4));
                            *dest = (ushort)pred[0];
                            dest = dest + 1;
                            if (zero_is_bad && 0 == pred[0])
                                zero_pos.Add((y << 16) | (x * 14 + i));

                            // Odd pixels
                            i++;
                            u++;
                            if (u == 2)
                            {
                                sh = 4 >> (int)(3 - bits.GetBits(2));
                                u = -1;
                            }
                            if (nonz[1] != 0)
                            {
                                if ((j = (int)bits.GetBits(8)) != 0)
                                {
                                    if ((pred[1] -= 0x80 << sh) < 0 || sh == 4)
                                        pred[1] &= ~(-1 << sh);
                                    pred[1] += j << sh;
                                }
                            }
                            else if ((nonz[1] = (int)bits.GetBits(8)) != 0 || i > 11)
                                pred[1] = (int)(nonz[1] << 4 | bits.GetBits(4));
                            *dest = (ushort)pred[1];
                            dest++;
                            if (zero_is_bad && 0 == pred[1])
                                zero_pos.Add((y << 16) | (x * 14 + i));
                            u++;
                        }
                    }
                }
            }
            /*
            if (zero_is_bad && zero_pos.Count != 0)
            {
                rawImage.mBadPixelPositions.insert(rawImage.mBadPixelPositions.end(), zero_pos.begin(), zero_pos.end());
            }*/
        }

        public override void DecodeMetadata()
        {
            rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);

            base.DecodeMetadata();

            if (rawImage.metadata.Model == null)
                throw new RawDecoderException("RW2 Meta Decoder: Model name not found");
            if (rawImage.metadata.Make == null)
                throw new RawDecoderException("RW2 Support: Make name not found");

            string mode = GuessMode();

            SetMetadata(rawImage.metadata.Model);
            rawImage.metadata.Mode = mode;

            //panasonic iso is in a special tag
            if (rawImage.metadata.IsoSpeed == 0)
            {
                var t = ifd.GetEntryRecursive(TagType.PANASONIC_ISO_SPEED);
                if (t != null) rawImage.metadata.IsoSpeed = t.GetInt(0);
            }

        }

        private void SetMetadata(string model)
        {

        }

        string GuessMode()
        {
            float ratio = 3.0f / 2.0f;  // Default

            if (rawImage.raw.data == null)
                return "";

            ratio = rawImage.raw.dim.width / (float)rawImage.raw.dim.height;

            float min_diff = Math.Abs(ratio - 16.0f / 9.0f);
            string closest_match = "16:9";

            float t = Math.Abs(ratio - 3.0f / 2.0f);
            if (t < min_diff)
            {
                closest_match = "3:2";
                min_diff = t;
            }

            t = Math.Abs(ratio - 4.0f / 3.0f);
            if (t < min_diff)
            {
                closest_match = "4:3";
                min_diff = t;
            }

            t = Math.Abs(ratio - 1.0f);
            if (t < min_diff)
            {
                closest_match = "1:1";
                min_diff = t;
            }
            //_RPT1(0, "Mode guess: '%s'\n", closest_match.c_str());
            return closest_match;
        }



    }
}
