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
            List<IFD> data = ifd.getIFDsWithTag(TagType.STRIPOFFSETS);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Decoder: No image data found");

            IFD raw = data[0];

            int compression = raw.getEntry(TagType.COMPRESSION).getInt();

            if (1 == compression || compression == 32773)
            {
                DecodeUncompressed(ref raw, BitOrder.Jpeg);
                return;
            }

            if (65535 != compression)
                throw new RawDecoderException("PEF Decoder: Unsupported compression");

            Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
            Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);

            if (offsets.dataCount != 1)
            {
                throw new RawDecoderException("PEF Decoder: Multiple Strips found: " + offsets.dataCount);
            }
            if (counts.dataCount != offsets.dataCount)
            {
                throw new RawDecoderException("PEF Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
            }
            if (!reader.isValid(offsets.getUInt(), counts.getUInt()))
                throw new RawDecoderException("PEF Decoder: Truncated file.");

            Int32 width = raw.getEntry(TagType.IMAGEWIDTH).getInt();
            Int32 height = raw.getEntry(TagType.IMAGELENGTH).getInt();

            rawImage.dim = new Point2D(width, height);
            rawImage.Init();
            try
            {
                PentaxDecompressor l = new PentaxDecompressor(reader, rawImage);
                l.decodePentax(ifd, offsets.getUInt(), counts.getUInt());
            }
            catch (IOException e)
            {
                rawImage.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }
        }

        protected override void DecodeMetaDataInternal()
        {
            List<IFD> data = ifd.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("PEF Meta Decoder: Model name found");

            IFD raw = data[0];

            string make = raw.getEntry(TagType.MAKE).DataAsString;
            string model = raw.getEntry(TagType.MODEL).DataAsString;

            Tag isoTag = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                rawImage.metadata.isoSpeed = isoTag.getInt();

            SetMetaData(model);

            //get cfa
            var cfa = ifd.getEntryRecursive(TagType.CFAPATTERN);
            if (cfa == null)
            {
                Debug.WriteLine("CFA pattern is not found");
                rawImage.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            }
            else
            {
                rawImage.cfa.setCFA(new Point2D(2, 2), (CFAColor)cfa.getInt(0), (CFAColor)cfa.getInt(1), (CFAColor)cfa.getInt(2), (CFAColor)cfa.getInt(3));
            }

            // Read black level
            Tag black = ifd.getEntryRecursive((TagType)0x200);
            if (black != null)
            {
                if (black.dataCount == 4)
                {
                    for (int i = 0; i < 4; i++)
                        rawImage.blackLevelSeparate[i] = black.getInt(i);
                }
            }

            // Set the whitebalance
            Tag wb = ifd.getEntryRecursive((TagType)0x0201);
            if (wb != null)
            {
                if (wb.dataCount == 4)
                {
                    rawImage.metadata.wbCoeffs[0] = wb.getInt(0);
                    rawImage.metadata.wbCoeffs[1] = wb.getInt(1);
                    rawImage.metadata.wbCoeffs[2] = wb.getInt(3);
                }
            }
        }

        protected override void SetMetaData(string model)
        {
            throw new NotImplementedException();
        }
    }
}