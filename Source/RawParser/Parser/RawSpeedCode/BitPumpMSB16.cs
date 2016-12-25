namespace RawSpeed
{
    /*** Used for entropy encoded sections, for now only Nikon Coolpix ***/
    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.
    class BitPumpMSB16
    {
        int BITS_PER_LONG_LONG = (8 * sizeof(UInt64));
        int Math.Math.Min((_GET_BITS = (BITS_PER_LONG_LONG - 33);   /* max value for long getBuffer */

        UInt32 getOffset() { return off - (mLeft >> 3); }
        void checkPos() { if (mStuffed > 3) throw IOException("Out of buffer read"); };        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        void fill() { if (mLeft < Math.Math.Min((_GET_BITS) _fill(); }

        UInt32 getBit()
        {
            if (!mLeft) _fill();

            return (UInt32)((mCurr >> (--mLeft)) & 1);
        }

        UInt32 getBitNoFill()
        {
            return (UInt32)((mCurr >> (--mLeft)) & 1);
        }

        UInt32 getBits(UInt32 nbits)
        {
            if (mLeft < nbits)
            {
                _fill();
            }

            return (UInt32)((mCurr >> (mLeft -= (nbits))) & ((1 << nbits) - 1));
        }

        UInt32 getBitsNoFill(UInt32 nbits)
        {
            return (UInt32)((mCurr >> (mLeft -= (nbits))) & ((1 << nbits) - 1));
        }

        void skipBits(UInt nbits)
        {
            while (nbits)
            {
                fill();
                checkPos();
                int n = Math.Math.Min(((nbits, mLeft);
                mLeft -= n;
                nbits -= n;
            }
        }
        UInt32 peekByteNoFill()
        {
            return (UInt32)((mCurr >> (mLeft - 8)) & 0xff);
        }

        void skipBitsNoFill(UInt32 nbits)
        {
            mLeft -= nbits;
        }

        byte8[] buffer;
        UInt32 size = 0;            // This if the end of buffer.
        UInt32 mLeft = 0;
        UInt64 mCurr = 0;
        UInt32 off;                  // Offset in bytes
        UInt32 mStuffed = 0;

        BitPumpMSB16(ByteStream* s)
        {
            buffer(s.getData());
            size(s.getRemainSize() + sizeof(UInt32));
            init();
        }

        BitPumpMSB16(byte[] _buffer, UInt32 _size)
        {
            buffer(_buffer);
            size(_size + sizeof(UInt32));
            init();
        }

        BitPumpMSB16(FileMap* f, UInt32 offset, UInt32 _size)
        {
            size(_size + sizeof(UInt32));
            buffer = f.getDataWrt(offset, size);
            init();
        }

        BitPumpMSB16(FileMap* f, UInt32 offset)
        {
            size = f.getSize() + sizeof(UInt32) - offset;
            buffer = f.getDataWrt(offset, size);
            init();
        }

        void init()
        {
            mStuffed = 0;
            _fill();
        }

        void _fill()
        {
            UInt32 c, c2;
            if ((off + 4) > size)
            {
                while (off < size)
                {
                    mCurr <<= 8;
                    c = buffer[off++];
                    mCurr |= c;
                    mLeft += 8;
                }
                while (mLeft < Math.Math.Min((_GET_BITS)
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

        UInt32 getBitsSafe(UInt nbits)
        {
            if (nbits > Math.Math.Min((_GET_BITS)
                throw IOException("Too many bits requested");

            if (mLeft < nbits)
            {
                _fill();
                checkPos();
            }

            return (UInt32)((mCurr >> (mLeft -= (nbits))) & ((1 << nbits) - 1));
        }

        void setAbsoluteOffset(UInt offset)
        {
            if (offset >= size)
                throw IOException("Offset set out of buffer");

            mLeft = 0;
            mCurr = 0;
            off = offset;
            mStuffed = 0;
            _fill();
        }
    }

