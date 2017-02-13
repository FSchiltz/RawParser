﻿namespace RawNet.Format.TIFF
{
    class Makernote : IFD
    {
        public Makernote(Endianness endian, int depth) : base(endian, depth)
        {
            this.type = IFDType.Makernote;
        }

        public Makernote(byte[] data, uint offset, Endianness endian, int depth, int parentOffset) : base(endian, depth)
        {
            this.type = IFDType.Makernote;
            TIFFBinaryReader file;
            if (endian == Endianness.Little)
            {
                file = new TIFFBinaryReader(data);
            }
            else if (endian == Endianness.Big)
            {
                file = new TIFFBinaryReaderRE(data);
            }
            else
            {
                throw new RawDecoderException("Endianess not correct " + endian);
            }

            file.BaseStream.Position = offset;
            RelativeOffset = -parentOffset;
            Parse(file);
            file.Dispose();
        }
    }
}
