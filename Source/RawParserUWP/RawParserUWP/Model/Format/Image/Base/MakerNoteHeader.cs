using RawParser.Model.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format.Image.Base
{
    class MakerNoteHeader : Header
    {


        public MakerNoteHeader(BinaryReader filestream, uint offset): base(filestream, offset)
        {
           
        }
    }
}
