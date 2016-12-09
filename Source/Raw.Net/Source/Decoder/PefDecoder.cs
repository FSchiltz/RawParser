using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    class PefDecoder : RawDecoder
    {
        IFD mRootIFD;

        public PefDecoder(IFD rootIFD, TIFFBinaryReader file, CameraMetaData meta) : base(ref file, meta)
        {
            mRootIFD = (rootIFD);
            decoderVersion = 3;
        }

        protected override RawImage decodeRawInternal()
        {
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.STRIPOFFSETS);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Decoder: No image data found");

            IFD raw = data[0];

            int compression = raw.getEntry(TagType.COMPRESSION).getInt();

            if (1 == compression || compression == 32773)
            {
                decodeUncompressed(ref raw, BitOrder.Jpeg);
                return mRaw;
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
            if (!file.isValid(offsets.getUInt(), counts.getUInt()))
                throw new RawDecoderException("PEF Decoder: Truncated file.");

            Int32 width = raw.getEntry(TagType.IMAGEWIDTH).getInt();
            Int32 height = raw.getEntry(TagType.IMAGELENGTH).getInt();

            mRaw.dim = new Point2D(width, height);
            mRaw.Init();
            try
            {
                PentaxDecompressor l = new PentaxDecompressor(file, mRaw);
                l.decodePentax(mRootIFD, offsets.getUInt(), counts.getUInt());
            }
            catch (IOException e)
            {
                mRaw.errors.Add(e.Message);
                // Let's ignore it, it may have delivered somewhat useful data.
            }

            return mRaw;
        }

        protected override void checkSupportInternal()
        {
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.MODEL);
            if (data.Count == 0)
                throw new RawDecoderException("PEF Support check: Model name found");
            if (!data[0].hasEntry(TagType.MAKE))
                throw new RawDecoderException("PEF Support: Make name not found");

            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;
            this.checkCameraSupported(metaData, make, model, "");
        }

        protected override void decodeMetaDataInternal()
        {
            int iso = 0;
            mRaw.cfa.setCFA(new Point2D(2, 2), CFAColor.RED, CFAColor.GREEN, CFAColor.GREEN, CFAColor.BLUE);
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.MODEL);

            if (data.Count == 0)
                throw new RawDecoderException("PEF Meta Decoder: Model name found");

            IFD raw = data[0];

            string make = raw.getEntry(TagType.MAKE).dataAsString;
            string model = raw.getEntry(TagType.MODEL).dataAsString;

            Tag isoTag = mRootIFD.getEntryRecursive(TagType.ISOSPEEDRATINGS);
            if (isoTag != null)
                iso = isoTag.getInt();

            setMetaData(metaData, make, model, "", iso);

            // Read black level

            Tag black = mRootIFD.getEntryRecursive((TagType)0x200);
            if (black != null)
            {
                if (black.dataCount == 4)
                {
                    for (int i = 0; i < 4; i++)
                        mRaw.blackLevelSeparate[i] = black.getInt(i);
                }
            }

            // Set the whitebalance

            Tag wb = mRootIFD.getEntryRecursive((TagType)0x0201);
            if (wb != null)
            {
                if (wb.dataCount == 4)
                {
                    mRaw.metadata.wbCoeffs[0] = wb.getInt(0);
                    mRaw.metadata.wbCoeffs[1] = wb.getInt(1);
                    mRaw.metadata.wbCoeffs[2] = wb.getInt(3);
                }
            }
        }

    }
}