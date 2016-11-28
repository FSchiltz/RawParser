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

        public BitPumpPlain(TIFFBinaryReader s)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            s.Read(buffer, (int)s.Position, (int)s.BaseStream.Length);
            size = (uint)(8 * s.getRemainSize());
        }

        public BitPumpPlain(byte[] _buffer, UInt32 _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = (_buffer);
            size = (_size * 8);
        }

        public UInt32 getBit()
        {
            UInt32 v = (uint)(buffer[off >> 3] >> (int)(off & 7) & 1);
            off++;
            return v;
        }

        public UInt32 getBits(UInt32 nbits)
        {
            UInt32 v = (uint)(buffer[off >> 3] >> (int)(off & 7) & ((1 << (int)nbits) - 1));
            off += nbits;
            return v;
        }

        public UInt32 peekBit()
        {
            return (uint)(buffer[off >> 3] >> (int)(off & 7) & 1);
        }

        public UInt32 peekBits(UInt32 nbits)
        {
            return (uint)(buffer[off >> 3] >> (int)(off & 7) & ((1 << (int)nbits) - 1));
        }

        public UInt32 peekByte()
        {
            return (uint)(buffer[off >> 3] >> (int)(off & 7) & 0xff);
        }

        public UInt32 getBitSafe()
        {
            checkPos();
            return (uint)(buffer[off >> 3] >> (int)(off & 7) & 1);
        }

        public UInt32 getBitsSafe(uint nbits)
        {
            checkPos();
            return (uint)(buffer[off >> 3] >> (int)(off & 7) & ((1 << (int)nbits) - 1));
        }

        public void skipBits(uint nbits)
        {
            off += nbits;
            checkPos();
        }

        public byte getByte()
        {
            UInt32 v = (uint)(buffer[off >> 3] >> (int)(off & 7) & 0xff);
            off += 8;
            return (byte)v;
        }

        public byte getByteSafe()
        {
            UInt32 v = (uint)(buffer[off >> 3] >> (int)(off & 7) & 0xff);
            off += 8;
            checkPos();
            return (byte)v;
        }

        public void setAbsoluteOffset(uint offset)
        {
            off = offset * 8;
            checkPos();
        }
    }
}
