using PhotoNet.Common;

namespace RawNet.Format.Tiff
{
    internal class PentaxMakernote : Makernote
    {
        public PentaxMakernote(byte[] data, int offset, int parentOffset, Endianness endian, int depth) : base(endian, depth)
        {
            ImageBinaryReader buffer;
            if (data[offset] == 0x4D && data[offset + 1] == 0x4D)
            {
                buffer = new ImageBinaryReaderBigEndian(data);
            }
            else if (data[offset] == 0x49 && data[offset + 1] == 0x49)
            {
                buffer = new ImageBinaryReaderBigEndian(data);
            }
            else
            {
                throw new RawDecoderException("Makernote endianness unknown " + data[0]);
            }
            buffer.BaseStream.Position += (offset + 2);
            RelativeOffset = -parentOffset;
            //offset are from the start of the tag
            Parse(buffer);
            buffer.Dispose();
        }
    }
}