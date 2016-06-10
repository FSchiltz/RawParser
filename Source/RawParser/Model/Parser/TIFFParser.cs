using RawParser.Format.IFD;
using RawParser.Image;
using System;
using System.Collections.Generic;
using System.IO;

namespace RawParser.Parser
{
    class TiffParser : AParser
    {
        public override void Parse(Stream s)
        {
            throw new NotImplementedException();
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            throw new NotImplementedException();
        }

        public override byte[] parsePreview()
        {
            throw new NotImplementedException();
        }

        public override ushort[] parseRAWImage()
        {
            throw new NotImplementedException();
        }

        public override byte[] parseThumbnail()
        {
            throw new NotImplementedException();
        }
    }
}
