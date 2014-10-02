using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParser.Model.Parser
{
    class BinaryReaderBE : BinaryReader
    {
        public BinaryReaderBE(Stream s) : base(s)
        {

        }

        public BinaryReaderBE(Stream s, Encoding e) : base (s,e)
        {

        }

        public override ushort ReadUInt16()
        {
            byte[] temp = BitConverter.GetBytes(base.ReadUInt16());
            Array.Reverse(temp);
            return BitConverter.ToUInt16(temp, 0);
        }

        public override uint ReadUInt32()
        {
            byte[] temp = BitConverter.GetBytes(base.ReadUInt32());
            Array.Reverse(temp);
            return BitConverter.ToUInt32(temp, 0);
        }
    }
}
