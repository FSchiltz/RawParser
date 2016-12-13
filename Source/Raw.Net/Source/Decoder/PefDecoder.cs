using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    class PefDecoder : TiffDecoder
    {
        public PefDecoder(ref Stream file, CameraMetaData meta) : base(ref file, meta)
        {
            decoderVersion = 3;
        }

        protected override RawImage decodeRawInternal()
        {
            List<IFD> data = ifd.getIFDsWithTag(TagType.STRIPOFFSETS);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Decoder: No image data found");

            IFD raw = data[0];

            int compression = raw.getEntry(TagType.COMPRESSION).getInt();

            if (1 == compression || compression == 32773)
            {
                decodeUncompressed(ref raw, BitOrder.Jpeg);
                return rawImage;
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

            return rawImage;
        }

        protected override void checkSupportInternal()
        {
            List<IFD> data = ifd.getIFDsWithTag(TagType.MODEL);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Support check: Model name found");
            if (!data[0].hasEntry(TagType.MAKE))
                throw new RawDecoderException("PEF Support: Make name not found");

            string make = data[0].getEntry(TagType.MAKE).DataAsString;
            string model = data[0].getEntry(TagType.MODEL).DataAsString;
            this.checkCameraSupported(metaData, make, model, "");
        }

        protected override void decodeMetaDataInternal()
        {
            int iso = 0;
            rawImage.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            List<IFD> data = ifd.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("PEF Meta Decoder: Model name found");

            IFD raw = data[0];

            string make = raw.getEntry(TagType.MAKE).DataAsString;
            string model = raw.getEntry(TagType.MODEL).DataAsString;

            Tag isoTag = ifd.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                iso = isoTag.getInt();

            setMetaData(metaData, make, model, "", iso);

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

    }
}