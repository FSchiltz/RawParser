using System;
using System.IO;

namespace RawNet
{

    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.

    public class BitPumpMSB
    {

        int BITS_PER_LONG = (8 * sizeof(UInt32));
        int MIN_GET_BITS; /* max value for long getBuffer */

        public UInt32 getOffset()
        {
            return (uint)(off - (mLeft >> 3));
        }

        public void checkPos()
        {
            if (mStuffed > 8)
                throw new FileIOException("Out of buffer read");
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        public void fill()
        {
            if (mLeft < 25) _fill();
        }

        //get the nbits as an int32
        public UInt32 peekBitsNoFill(UInt32 nbits)
        {
            int shift = (int)(mLeft - nbits);
            UInt32 ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            /*
            for (int i = 0; i < shift >> 3; i++)
            {
                ret <<= 3;
                ret += current_buffer[i];
            }
            ret >>= shift & 7;
            */
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
            mLeft -= (byte)nbits;
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
            UInt32 ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
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

        public void skipBits(uint nbits)
        {
            int skipn = (int)nbits;
            while (skipn != 0)
            {
                fill();
                checkPos();
                int n = Math.Min(skipn, mLeft);
                mLeft -= (byte)n;
                skipn -= n;
            }
        }

        public void skipBitsNoFill(uint nbits)
        {
            mLeft -= (byte)nbits;
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

        byte[] current_buffer;
        byte[] buffer;
        UInt32 size = 0;            // This if the end of buffer.
        public byte mLeft = 0;
        UInt32 off;                  // Offset in bytes
        int mStuffed = 0;


        /*** Used for entropy encoded sections ***/
        public BitPumpMSB(ref TIFFBinaryReader s) : this(ref s, (uint)s.Position, (uint)s.BaseStream.Length)
        {

        }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB(ref TIFFBinaryReader s, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = count + sizeof(UInt32);
            buffer = new byte[size];
            s.BaseStream.Position = offset;
            s.BaseStream.Read(buffer, 0, (int)count);
            init();
        }

        public BitPumpMSB(ref byte[] _buffer, UInt32 _size)
        {
            buffer = (_buffer);
            size = (_size + sizeof(UInt32));
            init();
        }

        public void init()
        {
            current_buffer = new byte[24];
            //Common.memset<byte>(current_buffer, 0, 16); //not needed
            fill();
        }

        public void _fill()
        {
            // Fill in 96 bits
            //uint[] b = Common.convertByteToUInt(current_buffer);
            if ((off + 12) > size)
            {
                while (mLeft <= 64 && off < size)
                {
                    for (int i = (mLeft >> 3); i >= 0; i--)
                        current_buffer[i + 1] = current_buffer[i];
                    current_buffer[0] = buffer[off++];
                    mLeft += 8;
                }
                while (mLeft <= 64)
                {
                    current_buffer[15] = current_buffer[11];
                    current_buffer[14] = current_buffer[10];
                    current_buffer[13] = current_buffer[9];
                    current_buffer[12] = current_buffer[8];

                    current_buffer[11] = current_buffer[7];
                    current_buffer[10] = current_buffer[6];
                    current_buffer[9] = current_buffer[5];
                    current_buffer[8] = current_buffer[4];

                    current_buffer[7] = current_buffer[3];
                    current_buffer[6] = current_buffer[2];
                    current_buffer[5] = current_buffer[1];
                    current_buffer[4] = current_buffer[0];

                    current_buffer[3] = 0;
                    current_buffer[2] = 0;
                    current_buffer[1] = 0;
                    current_buffer[0] = 0;
                    mLeft += 32;
                    mStuffed += 4;
                }
                return;
            }

            current_buffer[15] = current_buffer[3];
            current_buffer[14] = current_buffer[2];
            current_buffer[13] = current_buffer[1];
            current_buffer[12] = current_buffer[0];

            current_buffer[11] = buffer[off];
            current_buffer[10] = buffer[off + 1];
            current_buffer[9] = buffer[off + 2];
            current_buffer[8] = buffer[off + 3];
            off += 4;
            current_buffer[7] = buffer[off];
            current_buffer[6] = buffer[off + 1];
            current_buffer[5] = buffer[off + 2];
            current_buffer[4] = buffer[off + 3];
            off += 4;
            current_buffer[3] = buffer[off];
            current_buffer[2] = buffer[off + 1];
            current_buffer[1] = buffer[off + 2];
            current_buffer[0] = buffer[off + 3];
            off += 4;

            //convert back b to the current_buffer            
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
                throw new FileIOException("Too many bits requested");

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
                throw new FileIOException("Offset set out of buffer");

            mLeft = 0;
            mStuffed = 0;
            off = offset;
            fill();
        }
    }
}


