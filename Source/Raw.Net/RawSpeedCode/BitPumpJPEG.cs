

namespace RawSpeed
{

    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.

    class BitPumpJPEG
    {
        int BITS_PER_LONG = (8 * sizeof(UInt32));
        int MIN_GET_BITS = (BITS_PER_LONG - 7);   /* max value for long getBuffer */
        UInt32 getOffset() { return off - (mLeft >> 3) + stuffed; }

        void checkPos()
        {
            if (off >= size || stuffed > (mLeft >> 3))
            {
                ThrowIOE("Out of buffer read");
            }
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        void fill()
        {
            if (mLeft < 25) _fill();
        }

        UInt32 peekBitsNoFill(UInt32 nbits)
        {
            int shift = mLeft - nbits;
            UInt32 ret = *(UInt32*)&current_buffer[shift >> 3];
            ret >>= shift & 7;
            return ret & ((1 << nbits) - 1);
        }


        UInt32 getBit()
        {
            if (!mLeft) _fill();
            mLeft--;
            UInt32 _byte = mLeft >> 3;
            return (current_buffer[_byte] >> (mLeft & 0x7)) & 1;
        }

        UInt32 getBitsNoFill(UInt32 nbits)
        {
            UInt32 ret = peekBitsNoFill(nbits);
            mLeft -= nbits;
            return ret;
        }
        UInt32 getBits(UInt32 nbits)
        {
            fill();
            return getBitsNoFill(nbits);
        }

        UInt32 peekBit()
        {
            if (!mLeft) _fill();
            return (current_buffer[(mLeft - 1) >> 3] >> ((mLeft - 1) & 0x7)) & 1;
        }
        UInt32 getBitNoFill()
        {
            mLeft--;
            UInt32 ret = (current_buffer[mLeft >> 3] >> (mLeft & 0x7)) & 1;
            return ret;
        }

        UInt32 peekByteNoFill()
        {
            int shift = mLeft - 8;
            UInt32 ret = *(UInt32*)&current_buffer[shift >> 3];
            ret >>= shift & 7;
            return ret & 0xff;
        }

        UInt32 peekBits(UInt32 nbits)
        {
            fill();
            return peekBitsNoFill(nbits);
        }

        UInt32 peekByte()
        {
            fill();

            if (off > size)
                throw IOException("Out of buffer read");

            return peekByteNoFill();
        }

        void skipBits(UInt nbits)
        {
            int skipn = nbits;
            while (skipn)
            {
                fill();
                checkPos();
                int n = Math.Math.Min(((skipn, mLeft);
                mLeft -= n;
                skipn -= n;
            }
        }

        void skipBitsNoFill(UInt nbits)
        {
            mLeft -= nbits;
        }

        byte getByte()
        {
            fill();
            mLeft -= 8;
            int shift = mLeft;
            UInt32 ret = *(UInt32*)&current_buffer[shift >> 3];
            ret >>= shift & 7;
            return ret & 0xff;
        }

        void init();
        void _fill();
        byte8[] buffer;
        byte8[] current_buffer;
        UInt32 size = 0;            // This if the end of buffer.
        int mLeft = 0;
        UInt32 off;                  // Offset in bytes
        int stuffed = 0;              // How many bytes has been stuffed?

        /*** Used for entropy encoded sections ***/


        BitPumpJPEG(ref TIFFBinaryReader s)
        {
            buffer = s.getData();
            size = s.getRemainSize() + sizeof(UInt32);
            init();
        }

        BitPumpJPEG(byte[] _buffer, UInt32 _size)
        {
            buffer = _buffer;
            size = _size + sizeof(UInt32);
            init();
        }

        void init()
        {
            memset(current_buffer, 0, 16);
            fill();
        }

        void _fill()
        {
            // Fill in 96 bits
            int* b = (int*)current_buffer;
            if ((off + 12) >= size)
            {
                while (mLeft <= 64 && off < size)
                {
                    for (int i = (mLeft >> 3); i >= 0; i--)
                        current_buffer[i + 1] = current_buffer[i];
                    byte val = buffer[off++];
                    if (val == 0xff)
                    {
                        if (buffer[off] == 0)
                            off++;
                        else
                        {
                            // We hit another marker - don't forward bitpump anymore
                            val = 0;
                            off--;
                            stuffed++;
                        }
                    }
                    current_buffer[0] = val;
                    mLeft += 8;
                }
                while (mLeft < 64)
                {
                    b[2] = b[1];
                    b[1] = b[0];
                    b[0] = 0;
                    mLeft += 32;
                    stuffed += 4;  //We are adding to mLeft without incrementing offset
                }
                return;
            }
            b[3] = b[0];
            for (int i = 0; i < 12; i++)
            {
                byte val = buffer[off++];
                if (val == 0xff)
                {
                    if (buffer[off] == 0)
                        off++;
                    else
                    {
                        val = 0;
                        off--;
                        stuffed++;
                    }
                }
                current_buffer[11 - i] = val;
            }
            mLeft += 96;
        }

        UInt32 getBitSafe()
        {
            fill();
            checkPos();

            return getBitNoFill();
        }

        UInt32 getBitsSafe(UInt nbits)
        {
            if (nbits > Math.Math.Min((_GET_BITS)
                throw new IOException("Too many bits requested");

            fill();
            checkPos();
            return getBitsNoFill(nbits);
        }


        byte getByteSafe()
        {
            fill();
            checkPos();
            return getBitsNoFill(8);
        }

        void setAbsoluteOffset(UInt offset)
        {
            if (offset >= size)
                throw IOException("Offset set out of buffer");

            mLeft = 0;
            off = offset;
            _fill();
        }
    }
}




