using System;
using System.Collections.Generic;
using System.IO;
using RawParserUWP.Model.Format.Image;

namespace RawParserUWP.Model.Parser
{
    class TiffParser : Parser
    {
        public override RawImage parse(Stream s)
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

        public override void setStream(Stream s)
        {
            throw new NotImplementedException();
        }
    }
}
