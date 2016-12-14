using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RawNet
{
    class PefDecoder : TiffDecoder
    {
        public PefDecoder(ref Stream stream) : base(ref stream) { }

        protected override void DecodeRawInternal()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.STRIPOFFSETS);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Decoder: No image data found");

            IFD raw = data[0];

            int compression = raw.GetEntry(TagType.COMPRESSION).GetInt(0);

            if (1 == compression || compression == 32773)
            {
                DecodeUncompressed(ref raw, BitOrder.Jpeg);
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

            Int32 width = raw.GetEntry(TagType.IMAGEWIDTH).GetInt(0);
            Int32 height = raw.GetEntry(TagType.IMAGELENGTH).GetInt(0);

            rawImage.dim = new Point2D(width, height);
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

        protected override void DecodeMetadataInternal()
        {
            List<IFD> data = ifd.GetIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("PEF Meta Decoder: Model name found");

            IFD raw = data[0];

            string model = raw.GetEntry(TagType.MODEL).DataAsString;
            rawImage.metadata.make = raw.GetEntry(TagType.MAKE).DataAsString;
            rawImage.metadata.model = model;

            //more exifs
            var exposure = ifd.GetEntryRecursive(TagType.EXPOSURETIME);
            var fn = ifd.GetEntryRecursive(TagType.FNUMBER);
            var t = ifd.GetEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (t != null) rawImage.metadata.isoSpeed = t.GetInt(0);
            if (exposure != null) rawImage.metadata.exposure = exposure.GetFloat(0);
            if (fn != null) rawImage.metadata.aperture = fn.GetFloat(0);

            var time = ifd.GetEntryRecursive(TagType.DATETIMEORIGINAL);
            var timeModify = ifd.GetEntryRecursive(TagType.DATETIMEDIGITIZED);
            if (time != null) rawImage.metadata.timeTake = time.DataAsString;
            if (timeModify != null) rawImage.metadata.timeModify = timeModify.DataAsString;

            SetMetadata(model);

            //get cfa
            var cfa = ifd.GetEntryRecursive(TagType.CFAPATTERN);
            if (cfa == null)
            {
                Debug.WriteLine("CFA pattern is not found");
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            }
            else
            {
                rawImage.cfa.SetCFA(new Point2D(2, 2), (CFAColor)cfa.GetInt(0), (CFAColor)cfa.GetInt(1), (CFAColor)cfa.GetInt(2), (CFAColor)cfa.GetInt(3));
            }

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
                    rawImage.metadata.wbCoeffs[0] = wb.GetInt(0);
                    rawImage.metadata.wbCoeffs[1] = wb.GetInt(1);
                    rawImage.metadata.wbCoeffs[2] = wb.GetInt(3);
                }
            }
        }

        protected override void SetMetadata(string model)
        {
            if (rawImage.dim.height == 2624 && rawImage.dim.width == 3936)    /* Pentax K10D */
            {
                rawImage.dim.height = 2616;
                rawImage.dim.width = 3896;
            }
            else if (rawImage.dim.height == 3136 && rawImage.dim.width == 4864)  /* Pentax K20D 0 */
            {
                rawImage.dim.height = 3124;
                rawImage.dim.width = 4688;
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
            }
            else if (rawImage.dim.width == 4352 && (model == "K-r" || model == "K-x"))
            {
                rawImage.dim.width = 4309;
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
            }
            else if (rawImage.dim.width >= 4960 && model.Contains("K-5"))
            {
                rawImage.offset.height = 10;
                rawImage.dim.width = 4950;
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
            }
            else if (rawImage.dim.width == 4736 && model == "K-7")
            {
                rawImage.dim.height = 3122;
                rawImage.dim.width = 4684;
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.BLUE, CFAColor.GREEN, CFAColor.GREEN, CFAColor.RED);
                rawImage.offset.width = 2;
            }
            else if (rawImage.dim.width == 6080 && model == "K-3")
            {
                rawImage.offset.height = 4;
                rawImage.dim.width = 6040;
            }
            else if (rawImage.dim.width == 7424 && model == "645D")
            {
                rawImage.dim.height = 5502;
                rawImage.dim.width = 7328;
                rawImage.cfa.SetCFA(new Point2D(2, 2), CFAColor.GREEN, CFAColor.RED, CFAColor.BLUE, CFAColor.GREEN);
                rawImage.offset.width = 29;
                rawImage.offset.height = 48;
            }
        }
    }
}