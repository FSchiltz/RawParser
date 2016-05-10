using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format
{
    class NEFIFD : IFD
    {
        public NEFIFD(BinaryReader fileStream, uint offset, bool compression): base(fileStream,offset,compression)
        {

        }
    }
}
