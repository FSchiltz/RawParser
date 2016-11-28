
namespace RawNet
{
    class NikonMakerNote
    {
        public Header header { get; set; }
        public IFD ifd { get; set; }
        public IFD preview { get; set; }
        public string stringMagic { set; get; }
        public ushort version { set; get; }
        private uint offset;

        public NikonMakerNote(TIFFBinaryReader buffer, uint offset, bool compression)
        {
            //read the header
            buffer.Position = offset;
            stringMagic = "";
            this.offset = offset;
            for (int i = 0; i < 6; i++)
            {
                stringMagic += buffer.ReadChar();
            }
            Endianness endian = Endianness.little;
            version = buffer.ReadUInt16();
            buffer.Position = 2 + offset;//jump the padding

            header = new Header(buffer, 0); //0 car beggining of the stream

            if (header.byteOrder == 0x4D4D)
            {
                buffer = new TIFFBinaryReaderRE(buffer.BaseStream);
                endian = Endianness.big;
                //TODO see if need to move
            }
            ifd = new IFD(buffer, header.TIFFoffset + getOffset(), true, true, endian);
            //ifd = new IFD(buffer, (uint)buffer.Position, true, true);
            Tag previewOffsetTag;
            if (ifd.tags.TryGetValue(17, out previewOffsetTag))
            {
                preview = new IFD(buffer, (uint)previewOffsetTag.data[0] + getOffset(), true, false, endian);
            }
            else preview = null; //no preview in this file
        }

        internal uint getOffset()
        {
            return 10 + offset;
        }
    }
}
