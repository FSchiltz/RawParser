using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    /*** Used for entropy encoded sections, for now only Nikon Coolpix ***/
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    class BitPumpMSB16: BitPump
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

        public override void Init()
        {
            mStuffed = 0;
            Fill();
        }

        public override void Fill()
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

        public override uint GetOffset()
        {
            return off - (mLeft >> 3);
        }

        public override void CheckPos()
        {
            if (mStuffed > 3)
                throw new IOException("Out of buffer read");
        }

        // Fill the buffer with at least 24 bits
        public override void FillCheck() { if (mLeft < MIN_GET_BITS) Fill(); }

        public override uint GetBit()
        {
            if (mLeft == 0) Fill();
            return (uint)((mCurr >> (int)(--mLeft)) & 1);
        }

        public override uint GetBitNoFill()
        {
            return (uint)((mCurr >> (int)(--mLeft)) & 1);
        }

        public override uint GetBits(uint nbits)
        {
            if (mLeft < nbits)
            {
                Fill();
            }
            return (uint)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public override uint GetBitsNoFill(uint nbits)
        {
            return (uint)((int)(mCurr >> (int)(mLeft -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public override void SkipBits(uint nbits)
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

        public override uint PeekByteNoFill()
        {
            return (uint)((mCurr >> (int)(mLeft - 8)) & 0xff);
        }

        public override void SkipBitsNoFill(uint nbits)
        {
            mLeft -= nbits;
        }

        public override uint GetBitsSafe(uint nbits)
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

        public override void SetAbsoluteOffset(uint offset)
        {
            if (offset >= size)
                throw new IOException("Offset set out of buffer");

            mLeft = 0;
            mCurr = 0;
            off = offset;
            mStuffed = 0;
            Fill();
        }

        public override uint GetBitSafe()
        {
            throw new NotImplementedException();
        }

        public override byte GetByte()
        {
            throw new NotImplementedException();
        }

        public override byte GetByteSafe()
        {
            throw new NotImplementedException();
        }

        public override uint PeekBit()
        {
            throw new NotImplementedException();
        }

        public override uint PeekBits(uint nbits)
        {
            throw new NotImplementedException();
        }

        public override uint PeekBitsNoFill(uint nbits)
        {
            throw new NotImplementedException();
        }

        public override uint PeekByte()
        {
            throw new NotImplementedException();
        }
    }
}

