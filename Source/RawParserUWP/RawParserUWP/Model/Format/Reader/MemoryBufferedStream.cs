using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawParserUWP.Model.Format.Reader
{
    class MemoryBufferedStream : BinaryReader
    {
        byte[] buffer;
        long bufferSize;
        long bufferPosition;

        public MemoryBufferedStream(Stream S, long b) : base(S)
        {
            bufferSize = b;

            buffer = new byte[b];
            bufferPosition = S.Position;
            readBlock();
        }

        public override byte ReadByte()
        {
            //Si dans buffer, return from buffer
            if (BaseStream.Position < (bufferPosition + buffer.Length) && BaseStream.Position > bufferPosition)
            {
                return buffer[BaseStream.Position++ - bufferPosition];
            }
            else
            {             
                return buffer[BaseStream.Position++ - bufferPosition];
            }
        }

        private void readBlock()
        {
            //else read new block
            bufferPosition = BaseStream.Position;
            if (buffer.Length > BaseStream.Length)
            {
                buffer = new byte[BaseStream.Length - BaseStream.Position];
                BaseStream.Read(buffer, (int)BaseStream.Position, buffer.Length);
            }
            else
            {
                BaseStream.Read(buffer, (int)BaseStream.Position, buffer.Length);
            }
        }
    }
}

