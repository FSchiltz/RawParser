using System;
using System.IO;

namespace RawNet
{
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    class BitPumpPlain
    {
        public uint GetOffset() { return off >> 3; }
        public void CheckPos() { if (off >= size) throw new IOException("Out of buffer read"); }        // Check if we have a valid position

        byte[] buffer;
        uint size;            // This if the end of buffer.
        uint off;                  // Offset in bytes

        //int BITS_PER_LONG = (8 * sizeof(uint));
        //int MIN_GET_BITS;// = (BITS_PER_LONG - 7);  /* max value for long getBuffer */

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TIFFBinaryReader s) : this(s, (uint)s.Position, (uint)s.RemainingSize) { }


        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TIFFBinaryReader s, uint offset, uint count)
        {
            //MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = 8 * count;
            buffer = new byte[count];
            s.BaseStream.Position = offset;
            s.BaseStream.Read(buffer, 0, (int)count);
        }

        public BitPumpPlain(byte[] _buffer, uint _size)
        {
            //MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = (_buffer);
            size = (_size * 8);
        }

        unsafe public uint GetBit()
        {
            uint v = PeekBit();
            off++;
            return v;
        }

        public uint GetBits(uint nbits)
        {
            uint v = PeekBits(nbits);
            off += nbits;
            return v;
        }

        unsafe public uint PeekBit()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> (int)(off & 7) & 1);
            }
        }

        unsafe public uint PeekBits(uint nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(int*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public uint PeekByte()
        {
            return (uint)(((buffer[off >> 3] << 8) | buffer[(off >> 3) + 1]) >> (int)(off & 7) & 0xff);
        }

        unsafe public uint GetBitSafe()
        {
            CheckPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & 1);
            }
        }

        unsafe public uint GetBitsSafe(uint nbits)
        {
            CheckPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public void SkipBits(uint nbits)
        {
            off += nbits;
            CheckPos();
        }

        unsafe public byte GetByte()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                uint v = (uint)(*(Int32*)t >> ((int)off & 7) & 0xff);
                off += 8;
                return (byte)v;
            }
        }

        public byte GetByteSafe()
        {
            var v = GetByte();
            CheckPos();
            return v;
        }

        public void SetAbsoluteOffset(uint offset)
        {
            off = offset * 8;
            CheckPos();
        }
    }
}
