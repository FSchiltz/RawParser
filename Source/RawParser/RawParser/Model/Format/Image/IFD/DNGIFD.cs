using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format
{
    class DNGIFD : IFD
    {
        public DNGIFD(BinaryReader fileStream, uint offset, bool compression): base(fileStream,offset,compression)
        {


        }
    }
}
