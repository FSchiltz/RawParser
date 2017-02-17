namespace RawNet.Format.Tiff
{
    internal class FujiMakerNote : Makernote
    {
        public FujiMakerNote(byte[] data, Endianness endian,int depth):base(endian, depth)
        {
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
            file.BaseStream.Position = 12;
            RelativeOffset = 0;
            Parse(file);
            file.Dispose();
        }
    }
}