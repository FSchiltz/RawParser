using RawNet.Decoder.Decompressor;
using RawNet.Format.TIFF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet.Decoder
{
    class PEFDecoder : TIFFDecoder
    {
        public PEFDecoder(Stream stream) : base(stream) { }

        public override void DecodeRaw()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Decoder: No image data found");

            IFD raw = data[0];

            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);

            if (1 == compression || compression == 32773)
            {
                DecodeUncompressed(raw, BitOrder.Jpeg);
                return;
            }

            if (65535 != compression)
                throw new RawDecoderException("PEF Decoder: Unsupported compression");

            Tag offsets = raw.GetEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.GetEntry(TagType.STRIPBYTECOUNTS);

            if (offsets.dataCount != 1)
            {
                throw new RawDecoderException("PEF Decoder: Multiple Strips found: " + offsets.dataCount);
            }
            if (counts.dataCount != offsets.dataCount)
            {
                throw new RawDecoderException("PEF Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
            }
            if (!reader.IsValid(offsets.GetUInt(0), counts.GetUInt(0)))
                throw new RawDecoderException("PEF Decoder: Truncated file.");

            rawImage.raw.dim = new Point2D(raw.GetEntry(TagType.IMAGEWIDTH).GetUInt(0), raw.GetEntry(TagType.IMAGELENGTH).GetUInt(0));
            rawImage.Init();
            try
            {
                PentaxDecompressor l = new PentaxDecompressor(reader, rawImage);
                l.DecodePentax(ifd, offsets.GetUInt(0), counts.GetUInt(0));
                reader.Dispose();
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }
        }

        public override void DecodeMetadata()
        {
            base.DecodeMetadata();
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

            SetMetadata(rawImage.metadata.Model);

            // Read black level
            Tag black = ifd.GetEntryRecursive((TagType)0x200);
            if (black != null)
            {
                if (black.dataCount == 4)
                {
                    for (int i = 0; i < 4; i++)
                        rawImage.blackLevelSeparate[i] = black.GetInt(i);
                }
            }

            // Set the whitebalance
            Tag wb = ifd.GetEntryRecursive((TagType)0x0201);
            if (wb != null)
            {
                if (wb.dataCount == 4)
                {
                    rawImage.metadata.WbCoeffs[0] = wb.GetInt(0);
                    rawImage.metadata.WbCoeffs[1] = wb.GetInt(1);
                    rawImage.metadata.WbCoeffs[2] = wb.GetInt(3);

                    rawImage.metadata.WbCoeffs[0] /= rawImage.metadata.WbCoeffs[1];
                    rawImage.metadata.WbCoeffs[2] /= rawImage.metadata.WbCoeffs[1];
                    rawImage.metadata.WbCoeffs[1] /= rawImage.metadata.WbCoeffs[1];
                }

            }
        }

        protected void SetMetadata(string model)
        {
            if (rawImage.raw.dim.height == 2624 && rawImage.raw.dim.width == 3936)    /* Pentax K10D */
            {
                rawImage.raw.dim.height = 2616;
                rawImage.raw.dim.width = 3896;
            }
            else if (rawImage.raw.dim.height == 3136 && rawImage.raw.dim.width == 4864)  /* Pentax K20D 0 */
            {
                rawImage.raw.dim.height = 3124;
                rawImage.raw.dim.width = 4688;
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
            }
            else if (rawImage.raw.dim.width == 4352 && (model == "K-r" || model == "K-x"))
            {
                rawImage.raw.dim.width = 4309;
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
            }
            else if (rawImage.raw.dim.width >= 4960 && model.Contains("K-5"))
            {
                rawImage.raw.offset.height = 10;
                rawImage.raw.dim.width = 4950;
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
            }
            else if (rawImage.raw.dim.width == 4736 && model == "K-7")
            {
                rawImage.raw.dim.height = 3122;
                rawImage.raw.dim.width = 4684;
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
                rawImage.raw.offset.width = 2;
            }
            else if (rawImage.raw.dim.width == 6080 && model == "K-3")
            {
                rawImage.raw.offset.height = 4;
                rawImage.raw.dim.width = 6040;
            }
            else if (rawImage.raw.dim.width == 7424 && model == "645D")
            {
                rawImage.raw.dim.height = 5502;
                rawImage.raw.dim.width = 7328;
                rawImage.colorFilter.SetCFA(new Point2D(2, 2), CFAColor.GREEN, CFAColor.RED, CFAColor.BLUE, CFAColor.GREEN);
                rawImage.raw.offset.width = 30;
                rawImage.raw.offset.height = 48;
            }
        }

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

            reader.Position = thumb.GetUInt(0);
            Thumbnail temp = new Thumbnail()
            {
                data = reader.ReadBytes(size.GetInt(0)),
                Type = ThumbnailType.JPEG,
                dim = new Point2D()
            };
            return temp;
        }
        /*
        private CamRGB[] colorM = {
    { "Pentax *ist DL2", 0, 0,
    { 10504,-2438,-1189,-8603,16207,2531,-1022,863,12242 } },
    { "Pentax *ist DL", 0, 0,
    { 10829,-2838,-1115,-8339,15817,2696,-837,680,11939 } },
    { "Pentax *ist DS2", 0, 0,
    { 10504,-2438,-1189,-8603,16207,2531,-1022,863,12242 } },
    { "Pentax *ist DS", 0, 0,
    { 10371,-2333,-1206,-8688,16231,2602,-1230,1116,11282 } },
    { "Pentax *ist D", 0, 0,
    { 9651,-2059,-1189,-8881,16512,2487,-1460,1345,10687 } },
    { "Pentax K10D", 0, 0,
    { 9566,-2863,-803,-7170,15172,2112,-818,803,9705 } },
    { "Pentax K1", 0, 0,
    { 11095,-3157,-1324,-8377,15834,2720,-1108,947,11688 } },
    { "Pentax K20D", 0, 0,
    { 9427,-2714,-868,-7493,16092,1373,-2199,3264,7180 } },
    { "Pentax K200D", 0, 0,
    { 9186,-2678,-907,-8693,16517,2260,-1129,1094,8524 } },
    { "Pentax K2000", 0, 0,
    { 11057,-3604,-1155,-5152,13046,2329,-282,375,8104 } },
    { "Pentax K-m", 0, 0,
    { 11057,-3604,-1155,-5152,13046,2329,-282,375,8104 } },
    { "Pentax K-x", 0, 0,
    { 8843,-2837,-625,-5025,12644,2668,-411,1234,7410 } },
    { "Pentax K-r", 0, 0,
    { 9895,-3077,-850,-5304,13035,2521,-883,1768,6936 } },
    { "Pentax K-1", 0, 0,
    { 8566,-2746,-1201,-3612,12204,1550,-893,1680,6264 } },
    { "Pentax K-30", 0, 0,
    { 8710,-2632,-1167,-3995,12301,1881,-981,1719,6535 } },
    { "Pentax K-3 II", 0, 0,
    { 8626,-2607,-1155,-3995,12301,1881,-1039,1822,6925 } },
    { "Pentax K-3", 0, 0,
    { 7415,-2052,-721,-5186,12788,2682,-1446,2157,6773 } },
    { "Pentax K-5 II", 0, 0,
    { 8170,-2725,-639,-4440,12017,2744,-771,1465,6599 } },
    { "Pentax K-5", 0, 0,
    { 8713,-2833,-743,-4342,11900,2772,-722,1543,6247 } },
    { "Pentax K-7", 0, 0,
    { 9142,-2947,-678,-8648,16967,1663,-2224,2898,8615 } },
    { "Pentax K-S1", 0, 0,
    { 8512,-3211,-787,-4167,11966,2487,-638,1288,6054 } },
    { "Pentax K-S2", 0, 0,
    { 8662,-3280,-798,-3928,11771,2444,-586,1232,6054 } },
    { "Pentax Q-S1", 0, 0,
    { 12995,-5593,-1107,-1879,10139,2027,-64,1233,4919 } },
    { "Pentax 645D", 0, 0x3e00,
    { 10646,-3593,-1158,-3329,11699,1831,-667,2874,6287 } }};*/
    }
}