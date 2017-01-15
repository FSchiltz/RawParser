using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    internal class BitPumpMSB32 : BitPump
    {
        int BITS_PER_LONG_LONG = (8 * sizeof(UInt64));
        int MIN_GET_BITS;   /* max value for long getBuffer */
        byte[] buffer;
        uint size;            // This if the end of buffer.
        uint mLeft;
        UInt64 mCurr;
        uint off;                  // Offset in bytes
        uint mStuffed;
        public override uint GetOffset() { return off - (mLeft >> 3); }
        public override void CheckPos() { if (mStuffed > 3) throw new IOException("Out of buffer read"); }       // Check if we have a valid position

        /*** Used for entropy encoded sections, for now only Nikon Coolpix ***/
        public BitPumpMSB32(TIFFBinaryReader reader)
        {
            reader.Read(buffer, (int)reader.Position, (int)reader.BaseStream.Length);
            size = (uint)(reader.RemainingSize + sizeof(uint));
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            Init();
        }

        public BitPumpMSB32(byte[] _buffer, uint _size)
        {
            buffer = (_buffer);
            size = (_size + sizeof(uint));
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            Init();
        }

        public override void Init()
        {
            mStuffed = 0;
            Fill();
        }

        public override void Fill()
        {
            uint c, c2, c3, c4;
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
