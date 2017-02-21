using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(uint) large.
    internal class BitPumpPlain : BitPump
    {
        public override int Offset
        {
            get
            {
                return off - (left >> 3);
            }

            set
            {
                if (value >= size)
                    throw new IOException("Offset set out of buffer");

                left = 0;
                off = value;
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TiffBinaryReader reader) : this(reader, (uint)reader.Position, (uint)(reader.BaseStream.Length - reader.Position)) { }

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TiffBinaryReader reader, long offset, long count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = count + sizeof(uint);
            buffer = new byte[size];
            reader.BaseStream.Position = offset;
            reader.BaseStream.Read(buffer, 0, (int)count);
        }

        public BitPumpPlain(byte[] _buffer, uint _size)
        {
            buffer = _buffer;
            size = _size + sizeof(uint);
        }

        // Fill the buffer with at least 24 bits
        public override void Fill() { }

        //get the nbits as an int32
        public override uint PeekBits(int nbits)
        {
            int shift = left >> 3;
            uint ret = buffer[shift + 3] | (uint)buffer[shift + 2] << 8 | (uint)buffer[shift + 1] << 16 | (uint)buffer[shift] << 24;
            ret >>= 32 - nbits - (left & 7);
            return (uint)(ret & ((1 << nbits) - 1));
        }

        public override uint GetBits(int nbits)
        {
            uint ret = PeekBits(nbits);
            left += nbits;
            return ret;
        }

        public override uint PeekBit()
        {
            return (uint)(buffer[(left - 1) >> 3] >> ((left - 1) & 0x7)) & 1;
        }

        public override uint GetBit()
        {
            uint ret = PeekBit();
            left++;
            return ret;
        }

        public override void SkipBits(int nbits)
        {
            left += nbits;
        }
    }
}
