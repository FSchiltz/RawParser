﻿using RawParser.Format.IFD;
using RawParser.Reader;
using System;

namespace RawParser.Parser.Nikon
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
            stringMagic = "";
            this.offset = offset;
            for (int i = 0; i < 6; i++)
            {
                stringMagic += buffer.ReadChar();
            }
           
            version = buffer.ReadUInt16();
            buffer.BaseStream.Position = 2 + offset;//jump the padding

            header = new Header(buffer,0); //0 car beggining of the stream

            if(header.byteOrder == 0x4D4D)
            {
                buffer = new TIFFBinaryReaderRE(buffer.BaseStream);
                //TODO see if need to move
            }
            ifd = new IFD(buffer, header.TIFFoffset + 10 + offset, true, true);

            Tag previewOffsetTag;
            if (!ifd.tags.TryGetValue(17, out previewOffsetTag))
            {
                throw new Exception("Preview Offset not found");
            }
            preview = new IFD(buffer, (uint)previewOffsetTag.data[0] + offset + 10, true, false);
        }

        internal uint getOffset()
        {
            return 10 + offset;
        }
    }
}