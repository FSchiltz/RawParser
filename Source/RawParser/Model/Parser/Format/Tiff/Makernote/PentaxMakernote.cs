namespace RawNet.Format.TIFF
{
    internal class PentaxMakernote : Makernote
    {
        public PentaxMakernote(byte[] data, int offset, int parentOffset, Endianness endian, int depth) : base(endian, depth)
        {
            TIFFBinaryReader buffer;
            if (data[offset] == 0x4D && data[offset + 1] == 0x4D)
            {
                buffer = new TIFFBinaryReaderRE(data);
            }
            else if (data[offset] == 0x49 && data[offset + 1] == 0x49)
            {
                buffer = new TIFFBinaryReaderRE(data);
            }
            else
            {
                throw new RawDecoderException("Makernote endianess unknown " + data[0]);
            }
            buffer.BaseStream.Position += (offset + 2);
            RelativeOffset = -parentOffset;
            //offset are from the start of the tag
            Parse(buffer);
            buffer.Dispose();
        }
    }
}