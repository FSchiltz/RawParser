using System;
using System.Linq;

namespace RawNet
{
    internal class PentaxMakernote : Makernote
    {
        private byte[] data;

        public PentaxMakernote(byte[] data)
        {
            TIFFBinaryReader buffer;
            if (data[0] == 0x4D && data[1] == 0x4D)
            {
                buffer = new TIFFBinaryReaderRE(TIFFBinaryReader.streamFromArray(data));
                //TODO see if need to move
            }
            else if (data[0] == 0x49 && data[1] == 0x49)
            {
                buffer = new TIFFBinaryReaderRE(TIFFBinaryReader.streamFromArray(data));
            }
            else
            {
                throw new RawDecoderException("Makernote endianess unknown " + data[0]);
            }
            buffer.BaseStream.Position += 2;

            ushort TIFFMagic = buffer.ReadUInt16();
            uint TIFFoffset = buffer.ReadUInt32();
            buffer.BaseStream.Position = TIFFoffset;
            Parse(buffer);
        }
    }
}