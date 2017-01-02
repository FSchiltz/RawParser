namespace RawNet
{
    class Makernote : IFD
    {
        public Makernote(Endianness endian, int depth) : base(endian, depth) { }

        public Makernote(byte[] data, uint offset, Endianness endian, int depth, int parentOffset) : base(endian, depth)
        {
            TIFFBinaryReader file;

            if (endian == Endianness.little)
            {
                file = new TIFFBinaryReader(data);
            }
            else if (endian == Endianness.big)
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
