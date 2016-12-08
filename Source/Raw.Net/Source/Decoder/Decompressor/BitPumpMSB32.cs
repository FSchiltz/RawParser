using System;
using System.IO;

namespace RawNet
{
    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.
    internal class BitPumpMSB32
    {
        int BITS_PER_LONG_LONG = (8 * sizeof(UInt64));
        int MIN_GET_BITS;   /* max value for long getBuffer */
        public UInt32 getOffset() { return off - (mLeft >> 3); }
        public void checkPos() { if (mStuffed > 3) throw new IOException("Out of buffer read"); }       // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        void fill() { if (mLeft < MIN_GET_BITS) _fill(); }

        public UInt32 getBit()
        {
            if (mLeft == 0) _fill();

            return (UInt32)((mCurr >> (int)(--mLeft)) & 1);
        }

        public UInt32 getBitNoFill()
        {
            return (UInt32)((mCurr >> (int)(--mLeft)) & 1);
        }

        public UInt32 getBits(UInt32 nbits)
        {
            if (mLeft < nbits)
            {
                _fill();
            }
            return (UInt32)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public UInt32 getBitsNoFill(UInt32 nbits)
        {
            return (UInt32)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public void skipBits(uint nbits)
        {
            while (nbits != 0)
            {
                fill();
                checkPos();
                uint n = Math.Min(nbits, mLeft);
                mLeft -= n;
                nbits -= n;
            }
        }

        public UInt32 peekByteNoFill()
        {
            return (UInt32)((mCurr >> (int)(mLeft - 8)) & 0xff);
        }

        public void skipBitsNoFill(UInt32 nbits)
        {
            mLeft -= nbits;
        }

        byte[] buffer;
        UInt32 size;            // This if the end of buffer.
        UInt32 mLeft;
        UInt64 mCurr;
        UInt32 off;                  // Offset in bytes
        UInt32 mStuffed;


        /*** Used for entropy encoded sections, for now only Nikon Coolpix ***/
        public BitPumpMSB32(ref TIFFBinaryReader s)
        {
            s.Read(buffer, (int)s.Position, (int)s.BaseStream.Length);
            size = (uint)(s.getRemainSize() + sizeof(UInt32));
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            init();
        }

        public BitPumpMSB32(byte[] _buffer, UInt32 _size)
        {
            buffer = (_buffer);
            size = (_size + sizeof(UInt32));
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            init();
        }

        public void init()
        {
            mStuffed = 0;
            _fill();
        }

        public void _fill()
        {
            UInt32 c, c2, c3, c4;
            if ((off + 4) > size)
            {
                while (off < size)
                {
                    mCurr <<= 8;
                    c = buffer[off++];
                    mCurr |= c;
                    mLeft += 8;
                }
                while (mLeft < MIN_GET_BITS)
                {
                    mCurr <<= 8;
                    mLeft += 8;
                    mStuffed++;
                }
                return;
            }
            c = buffer[off++];
            c2 = buffer[off++];
            c3 = buffer[off++];
            c4 = buffer[off++];
            mCurr <<= 32;
            mCurr |= (c4 << 24) | (c3 << 16) | (c2 << 8) | c;
            mLeft += 32;
        }

        public UInt32 getBitsSafe(uint nbits)
        {
            if (nbits > MIN_GET_BITS)
                throw new IOException("Too many bits requested");

            if (mLeft < nbits)
            {
                _fill();
                checkPos();
            }
            return (UInt32)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public void setAbsoluteOffset(uint offset)
        {
            if (offset >= size)
                throw new IOException("Offset set out of buffer");

            mLeft = 0;
            mCurr = 0;
            off = offset;
            mStuffed = 0;
            _fill();
        }
    }
}
