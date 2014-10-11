using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Format
{
    class Header
    {
        public ushort byteOrder { set; get; }
        public ushort TIFFMagic { set; get; }
        public uint TIFFoffset { set; get; }

        public Header(BinaryReader fileStream, uint offset)
        {
            fileStream.BaseStream.Seek(offset, SeekOrigin.Begin);
            //read the header
            byteOrder = fileStream.ReadUInt16();
            TIFFMagic = fileStream.ReadUInt16();
            TIFFoffset = fileStream.ReadUInt32();
        } 
    }
}
