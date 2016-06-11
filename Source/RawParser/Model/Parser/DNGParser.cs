using System;
using System.Collections.Generic;
using System.IO;
using RawParser.Format.IFD;
using RawParser.Reader;
using RawParser.Image;

namespace RawParser.Parser
{
    class DNGParser : TiffParser
    {

        public override void Parse(Stream file)
        {
            readTiffBase(file);
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
            IFD ifd = new IFD(fileStream, header.TIFFoffset, false, false);
            return null;
        }
    }
}
