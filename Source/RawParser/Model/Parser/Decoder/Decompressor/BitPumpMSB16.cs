using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    /*** Used for entropy encoded sections, for now only Nikon Coolpix ***/
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    class BitPumpMSB16
    {
        int BITS_PER_LONG_LONG = (8 * sizeof(UInt64));
        int MIN_GET_BITS;   /* max value for long getBuffer */

        byte[] buffer;
        uint size = 0;            // This if the end of buffer.
        uint mLeft = 0;
        UInt64 mCurr = 0;
        uint off;                  // Offset in bytes
        uint mStuffed = 0;

        public BitPumpMSB16(TIFFBinaryReader reader)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            reader.Read(buffer, (int)reader.Position, (int)reader.BaseStream.Length);
            size = (uint)(reader.RemainingSize + sizeof(uint));
            Init();
        }

        public BitPumpMSB16(byte[] _buffer, uint _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            buffer = (_buffer);
            size = (_size + sizeof(uint));
            Init();
        }

        public void Init()
        {
            mStuffed = 0;
            Fill();
        }

        public void Fill()
        {
            uint c, c2;
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
            mCurr <<= 16;
            mCurr |= (c2 << 8) | c;
            mLeft += 16;
        }

        public uint GetOffset()
        {
            return off - (mLeft >> 3);
        }
        public void CheckPos()
        {
            if (mStuffed > 3)
                throw new IOException("Out of buffer read");
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        void FillCheck() { if (mLeft < MIN_GET_BITS) Fill(); }

        public uint GetBit()
        {
            if (mLeft == 0) Fill();
            return (uint)((mCurr >> (int)(--mLeft)) & 1);
        }

        public uint GetBitNoFill()
        {
            return (uint)((mCurr >> (int)(--mLeft)) & 1);
        }

        public uint GetBits(uint nbits)
        {
            if (mLeft < nbits)
            {
                Fill();
            }
            return (uint)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public uint GetBitsNoFill(uint nbits)
        {
            return (uint)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public void SkipBits(uint nbits)
        {
            while (nbits != 0)
            {
                FillCheck();
                CheckPos();
                uint n = Math.Min(nbits, mLeft);
                mLeft -= n;
                nbits -= n;
            }
        }

        public uint PeekByteNoFill()
        {
            return (uint)((mCurr >> (int)(mLeft - 8)) & 0xff);
        }

        public void SkipBitsNoFill(uint nbits)
        {
            mLeft -= nbits;
        }

        public uint GetBitsSafe(uint nbits)
        {
            if (nbits > MIN_GET_BITS)
                throw new IOException("Too many bits requested");

            if (mLeft < nbits)
            {
                Fill();
                CheckPos();
            }
            return (uint)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public void SetAbsoluteOffset(uint offset)
        {
            if (offset >= size)
                throw new IOException("Offset set out of buffer");

            mLeft = 0;
            mCurr = 0;
            off = offset;
            mStuffed = 0;
            Fill();
        }
    }
}

