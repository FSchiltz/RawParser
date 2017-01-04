using System;

namespace RawNet
{
    internal class FujiMakerNote : Makernote
    {
        public FujiMakerNote(byte[] data, Endianness endian,int depth):base(endian, depth)
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
            file.BaseStream.Position = 12;
            RelativeOffset = 0;
            Parse(file);
            file.Dispose();
        }
    }
}