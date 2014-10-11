using RawParser.Model.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format.Image.IFD
{
    class NikonMakerNote : MakerNote
    {
        protected NEFIFD ifd;
        protected NEFIFD preview;
        public string stringMagic { set; get; }
        public ushort version { set; get; }
        public ushort unknow { set; get; }

        public NikonMakerNote(BinaryReader fileStream, BinaryReader headerBuffer, uint offset, bool compression): base(fileStream, offset, compression)
        {
            //read the header
            stringMagic = "";
            for (int i = 0; i < 6; i++)
                stringMagic += headerBuffer.ReadChar();
            version = headerBuffer.ReadUInt16();
            unknow = headerBuffer.ReadUInt16();

            ifd = new NEFIFD(fileStream, header.TIFFoffset, true);

            Tag previewOffsetTag;
            ifd.tags.TryGetValue(17, out previewOffsetTag);
            preview = new NEFIFD(fileStream, (uint)previewOffsetTag.data[0], true);
        }

        public Tag[] parseToStandardExifTag()
        {
            return null;
        }
    }
}
