using System;
using System.IO;

namespace RawNet
{
    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.
    class BitPumpPlain
    {
        public UInt32 getOffset() { return off >> 3; }
        public void checkPos() { if (off >= size) throw new IOException("Out of buffer read"); }        // Check if we have a valid position

        byte[] buffer;
        UInt32 size;            // This if the end of buffer.
        UInt32 off;                  // Offset in bytes

        int BITS_PER_LONG = (8 * sizeof(UInt32));
        int MIN_GET_BITS;// = (BITS_PER_LONG - 7);  /* max value for long getBuffer */

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(ref TIFFBinaryReader s) : this(ref s, (uint)s.Position, (uint)s.getRemainSize()) { }


        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(ref TIFFBinaryReader s, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = 8*count ;
            buffer = new byte[count];
            s.BaseStream.Position = offset;
            s.BaseStream.Read(buffer, 0, (int)count);
        }

        public BitPumpPlain(byte[] _buffer, UInt32 _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = (_buffer);
            size = (_size * 8);
        }

        unsafe public UInt32 getBit()
        {
            uint v = peekBit();
            off++;
            return v;
        }

        public UInt32 getBits(UInt32 nbits)
        {
            uint v = peekBits(nbits);
            off += nbits;
            return v;
        }

        unsafe public UInt32 peekBit()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> (int)(off & 7) & 1);
            }
        }

        unsafe public UInt32 peekBits(UInt32 nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public UInt32 peekByte()
        {
            return (uint)(((buffer[off >> 3] << 8) | buffer[(off >> 3) + 1]) >> (int)(off & 7) & 0xff);
        }

        unsafe public UInt32 getBitSafe()
        {
            checkPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & 1);
            }
        }

        unsafe public UInt32 getBitsSafe(uint nbits)
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
                UInt32 v = (uint)(*(Int32*)t >> ((int)off & 7) & 0xff);
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
