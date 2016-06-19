using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RawParser.Format.IFD;

namespace RawParser.Parser
{
    class DcRawparser : AParser
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

        #region DCRAW Variable

        #endregion

        #region DCRawCode

        #endregion
    }
}
