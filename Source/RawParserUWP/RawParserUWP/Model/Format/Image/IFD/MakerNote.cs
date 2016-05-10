using RawParser.Model.Format.Image.Base;
using RawParser.Model.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format
{
    class MakerNote
    {
        protected MakerNoteHeader header;
       
        public MakerNote(BinaryReader fileStream, uint offset, bool compression)
        {
            header = new MakerNoteHeader(fileStream, offset);
            if (header.byteOrder == 0x4949)
            {
                //File is in reverse bit order
                fileStream = new BinaryReader(fileStream.BaseStream);
                header = new MakerNoteHeader(fileStream, 0);
            }
        }
    }
}
