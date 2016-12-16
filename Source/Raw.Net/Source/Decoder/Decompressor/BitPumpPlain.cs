using System;
using System.IO;

namespace RawNet
{
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    class BitPumpPlain
    {
        public uint getOffset() { return off >> 3; }
        public void checkPos() { if (off >= size) throw new IOException("Out of buffer read"); }        // Check if we have a valid position

        byte[] buffer;
        uint size;            // This if the end of buffer.
        uint off;                  // Offset in bytes

        int BITS_PER_LONG = (8 * sizeof(uint));
        int MIN_GET_BITS;// = (BITS_PER_LONG - 7);  /* max value for long getBuffer */

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(ref TIFFBinaryReader s) : this(ref s, (uint)s.Position, (uint)s.GetRemainSize()) { }


        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(ref TIFFBinaryReader s, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = 8 * count;
            buffer = new byte[count];
            s.BaseStream.Position = offset;
            s.BaseStream.Read(buffer, 0, (int)count);
        }

        public BitPumpPlain(byte[] _buffer, uint _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = (_buffer);
            size = (_size * 8);
        }

        unsafe public uint getBit()
        {
            uint v = peekBit();
            off++;
            return v;
        }

        public uint getBits(uint nbits)
        {
            uint v = peekBits(nbits);
            off += nbits;
            return v;
        }

        unsafe public uint peekBit()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> (int)(off & 7) & 1);
            }
        }

        unsafe public uint peekBits(uint nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(int*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public uint peekByte()
        {
            return (uint)(((buffer[off >> 3] << 8) | buffer[(off >> 3) + 1]) >> (int)(off & 7) & 0xff);
        }

        unsafe public uint getBitSafe()
        {
            checkPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & 1);
            }
        }

        unsafe public uint getBitsSafe(uint nbits)
        {
            checkPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public void skipBits(uint nbits)
        {
            off += nbits;
            checkPos();
        }

        unsafe public byte getByte()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                uint v = (uint)(*(Int32*)t >> ((int)off & 7) & 0xff);
                off += 8;
                return (byte)v;
            }
        }

        public byte getByteSafe()
        {
            var v = getByte();
            checkPos();
            return v;
        }

        public void setAbsoluteOffset(uint offset)
        {
            off = offset * 8;
            checkPos();
        }
    }
}
