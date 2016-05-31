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
        byte[] _buffer;
        long _bufferSize;
        long _bufferPosition;

        public MemoryBufferedStream(Stream s, long b) : base(s)
        {
            _bufferSize = b;

            _buffer = new byte[b];
            _bufferPosition = s.Position;
            ReadBlock();
        }

        public override byte ReadByte()
        {
            //Si dans buffer, return from buffer
            if (BaseStream.Position < (_bufferPosition + _buffer.Length) && BaseStream.Position > _bufferPosition)
            {
                return _buffer[BaseStream.Position++ - _bufferPosition];
            }
            else
            {             
                return _buffer[BaseStream.Position++ - _bufferPosition];
            }
        }

        private void ReadBlock()
        {
            //else read new block
            _bufferPosition = BaseStream.Position;
            if (_buffer.Length > BaseStream.Length)
            {
                _buffer = new byte[BaseStream.Length - BaseStream.Position];
                BaseStream.Read(_buffer, (int)BaseStream.Position, _buffer.Length);
            }
            else
            {
                BaseStream.Read(_buffer, (int)BaseStream.Position, _buffer.Length);
            }
        }
    }
}

