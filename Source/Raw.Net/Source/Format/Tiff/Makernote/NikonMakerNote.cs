
using System.Linq;

namespace RawNet
{
    class NikonMakerNote : Makernote
    {
        public string StringMagic { set; get; }
        public ushort Version { set; get; }

        public NikonMakerNote(byte[] data)
        {
            //read the header
            // buffer.BaseStream.Position = offset;
            StringMagic = "";
            for (int i = 0; i < 6; i++)
            {
                StringMagic += (char)data[i];
            }

            Version = (ushort)(data[8] << 8 | data[7]);
            //buffer.BaseStream.Position = 2 + offset;//jump the padding
            data = data.Skip(10).ToArray();
            //header = new Header(buffer, 0); //0 car beggining of the stream
            TIFFBinaryReader buffer;
            if (data[0] == 0x4D && data[1] == 0x4D)
            {
                buffer = new TIFFBinaryReaderRE(TIFFBinaryReader.streamFromArray(data));
                endian = Endianness.big;
            }
            else if (data[0] == 0x49 && data[1] == 0x49)
            {
                buffer = new TIFFBinaryReader(TIFFBinaryReader.streamFromArray(data));
                endian = Endianness.little;
            }
            else throw new RawDecoderException("Makernote endianess unknown " + data[0]);
            buffer.BaseStream.Position = 2;
            ushort TIFFMagic = buffer.ReadUInt16();
            uint TIFFoffset = buffer.ReadUInt32();
            buffer.BaseStream.Position = TIFFoffset;
            Parse(buffer);
        }
    }
}
