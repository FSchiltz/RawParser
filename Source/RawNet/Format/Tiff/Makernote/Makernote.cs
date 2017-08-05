using PhotoNet.Common;

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
            type = IFDType.Makernote;
            ImageBinaryReader file;
            if (endian == Endianness.Little)
            {
                file = new ImageBinaryReader(data);
            }
            else if (endian == Endianness.Big)
            {
                file = new ImageBinaryReaderBigEndian(data);
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
