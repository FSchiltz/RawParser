using PhotoNet.Common;

namespace RawNet.Format.Tiff
{
    internal class PanasonicMakernote : Makernote
    {
        public PanasonicMakernote(byte[] data, Endianness endian, int depth):base(endian, depth)
        {
            //start wth a tiff headder

            this.type = IFDType.Makernote;
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

            file.BaseStream.Position = 8;
            Parse(file);
            file.Dispose();
        }
    }
}