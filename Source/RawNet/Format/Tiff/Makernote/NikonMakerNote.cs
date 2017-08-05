using PhotoNet.Common;
using System.Linq;

namespace RawNet.Format.Tiff
{
    //nikon makernote are not always self contained so use the file stream to parse
    class NikonMakerNote : Makernote
    {
        public string StringMagic { set; get; }
        public ushort Version { set; get; }
        public IFD Gps { get; set; }
        public NikonMakerNote(byte[] data, int depth) : base(Endianness.Little, depth)
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
            ImageBinaryReader buffer;
            if (data[0] == 0x4D && data[1] == 0x4D)
            {
                buffer = new ImageBinaryReaderBigEndian(data);
                endian = Endianness.Big;
            }
            else if (data[0] == 0x49 && data[1] == 0x49)
            {
                buffer = new ImageBinaryReader(data);
                endian = Endianness.Little;
            }
            else throw new RawDecoderException("Makernote endianness unknown " + data[0]);
            buffer.BaseStream.Position = 2;
            buffer.ReadUInt16();
            uint TIFFoffset = buffer.ReadUInt32();
            buffer.BaseStream.Position = TIFFoffset;
            Parse(buffer);
            //parse gps info
            buffer.Dispose();
        }
    }
}
