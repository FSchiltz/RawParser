namespace RawNet.Format.Tiff
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
            TiffBinaryReader file;
            if (endian == Endianness.Little)
            {
                file = new TiffBinaryReader(data);
            }
            else if (endian == Endianness.Big)
            {
                file = new TiffBinaryReaderBigEndian(data);
            }
            else
            {
                throw new RawDecoderException("Endianness not correct " + endian);
            }

            file.BaseStream.Position = offset;
            RelativeOffset = -parentOffset;
            Parse(file);
            file.Dispose();
        }
    }
}
