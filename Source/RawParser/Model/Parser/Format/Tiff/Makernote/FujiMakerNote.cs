namespace RawNet.Format.TIFF
{
    internal class FujiMakerNote : Makernote
    {
        public FujiMakerNote(byte[] data, Endianness endian,int depth):base(endian, depth)
        {
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
                throw new RawDecoderException("Endianness not correct " + endian);
            }
            file.BaseStream.Position = 12;
            RelativeOffset = 0;
            Parse(file);
            file.Dispose();
        }
    }
}