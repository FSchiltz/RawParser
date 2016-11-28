using System;
using System.IO;

namespace RawNet
{
    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.
    public class BitPumpJPEG
    {
        int BITS_PER_LONG = (8 * sizeof(UInt32));
        int MIN_GET_BITS;   /* max value for long getBuffer */
        public UInt32 getOffset() { return (uint)(off - (mLeft >> 3) + stuffed); }

        public void checkPos()
        {
            if (off >= size || stuffed > (mLeft >> 3))
            {
                throw new FileIOException("Out of buffer read");
            }
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        public void fill()
        {
            if (mLeft < 25) _fill();
        }

        public UInt32 peekBitsNoFill(UInt32 nbits)
        {
            int shift = (int)(mLeft - nbits);
            UInt32 ret = current_buffer[shift >> 3];
            ret >>= shift & 7;
            return (uint)(ret & ((1 << (int)nbits) - 1));
        }

        public UInt32 getBit()
        {
            if (mLeft == 0) _fill();
            mLeft--;
            UInt32 _byte = (uint)(mLeft >> 3);
            return (uint)(current_buffer[_byte] >> (mLeft & 0x7)) & 1;
        }

        public UInt32 getBitsNoFill(UInt32 nbits)
        {
            UInt32 ret = peekBitsNoFill(nbits);
            mLeft -= (int)nbits;
            return ret;
        }

        public UInt32 getBits(UInt32 nbits)
        {
            fill();
            return getBitsNoFill(nbits);
        }

        public UInt32 peekBit()
        {
            if (mLeft == 0) _fill();
            return (uint)(current_buffer[(mLeft - 1) >> 3] >> ((mLeft - 1) & 0x7)) & 1;
        }

        public UInt32 getBitNoFill()
        {
            mLeft--;
            UInt32 ret = (uint)(current_buffer[mLeft >> 3] >> (mLeft & 0x7)) & 1;
            return ret;
        }

        public UInt32 peekByteNoFill()
        {
            int shift = mLeft - 8;
            UInt32 ret = current_buffer[shift >> 3];
            ret >>= shift & 7;
            return ret & 0xff;
        }

        public UInt32 peekBits(UInt32 nbits)
        {
            fill();
            return peekBitsNoFill(nbits);
        }

        public UInt32 peekByte()
        {
            fill();

            if (off > size)
                throw new IOException("Out of buffer read");

            return peekByteNoFill();
        }

        public void skipBits(UInt32 nbits)
        {
            int skipn = (int)nbits;
            while (skipn != 0)
            {
                fill();
                checkPos();
                int n = Math.Min(skipn, mLeft);
                mLeft -= n;
                skipn -= n;
            }
        }

        public void skipBitsNoFill(uint nbits)
        {
            mLeft -= (int)nbits;
        }

        public byte getByte()
        {
            fill();
            mLeft -= 8;
            int shift = mLeft;
            UInt32 ret = current_buffer[shift >> 3];
            ret >>= shift & 7;
            return (byte)(ret & 0xff);
        }

        byte[] buffer;
        byte[] current_buffer;
        UInt32 size = 0;            // This if the end of buffer.
        int mLeft = 0;
        UInt32 off;                  // Offset in bytes
        int stuffed = 0;              // How many bytes has been stuffed?

        /*** Used for entropy encoded sections ***/
        public BitPumpJPEG(TIFFBinaryReader s)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            s.Read(buffer, (int)s.Position, (int)s.BaseStream.Length);
            size = (uint)(s.getRemainSize() + sizeof(UInt32));
            init();
        }

        public BitPumpJPEG(byte[] _buffer, UInt32 _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = _buffer;
            size = _size + sizeof(UInt32);
            init();
        }

        public void init()
        {
            Common.memset<byte>(current_buffer, 0, 16);
            fill();
        }

        public void _fill()
        {
            // Fill in 96 bits
            int[] b = Common.convertByteToInt(current_buffer);
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

        public UInt32 getBitSafe()
        {
            fill();
            checkPos();
            return getBitNoFill();
        }

        public UInt32 getBitsSafe(uint nbits)
        {
            if (nbits > MIN_GET_BITS)
                throw new IOException("Too many bits requested");

            fill();
            checkPos();
            return getBitsNoFill(nbits);
        }

        public byte getByteSafe()
        {
            fill();
            checkPos();
            return (byte)getBitsNoFill(8);
        }

        public void setAbsoluteOffset(uint offset)
        {
            if (offset >= size)
                throw new IOException("Offset set out of buffer");

            mLeft = 0;
            off = offset;
            _fill();
        }
    }
}




