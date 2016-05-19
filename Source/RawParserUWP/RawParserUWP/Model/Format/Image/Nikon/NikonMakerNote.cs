using RawParser.Model.Parser;
using System.IO;

namespace RawParser.Model.Format.Nikon
{
    class NikonMakerNote
    {
        protected Header header;
        protected IFD ifd;
        public IFD preview { get; }
        public string stringMagic { set; get; }
        public ushort version { set; get; }

        public NikonMakerNote(BinaryReader buffer, uint offset, bool compression)
        {
            //read the header
            stringMagic = "";
            for (int i = 0; i < 6; i++)
            {
                stringMagic += buffer.ReadChar();
            }
            version = buffer.ReadUInt16();
            buffer.BaseStream.Seek(2, SeekOrigin.Current);//jump the padding

            header = new Header(buffer, offset);
            if(header.byteOrder == 0x4D4D)
            {
                buffer = new BinaryReaderBE(buffer.BaseStream);
                //TODO see if need to move
            }
            ifd = new IFD(buffer, header.TIFFoffset, true);

            Tag previewOffsetTag;
            ifd.tags.TryGetValue(17, out previewOffsetTag);
            preview = new IFD(buffer, (uint)previewOffsetTag.data[0], true);
        }
    }
}
