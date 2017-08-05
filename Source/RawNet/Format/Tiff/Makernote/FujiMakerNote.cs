using PhotoNet.Common;

namespace RawNet.Format.Tiff
{
    internal class FujiMakerNote : Makernote
    {
        public FujiMakerNote(byte[] data, Endianness endian,int depth):base(endian, depth)
        {
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
            file.BaseStream.Position = 12;
            RelativeOffset = 0;
            Parse(file);
            file.Dispose();
        }
    }
}