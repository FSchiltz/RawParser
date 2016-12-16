using System;
using System.IO;

namespace RawNet
{

    // Note: Allocated buffer MUST be at least size+sizeof(uint) large.
    internal class BitPumpMSB
    {

        int BITS_PER_LONG = (8 * sizeof(uint));
        int MIN_GET_BITS; /* max value for long getBuffer */
        byte[] current_buffer;
        byte[] buffer;
        uint size = 0;            // This if the end of buffer.
        public byte mLeft = 0;
        uint off;                  // Offset in bytes
        int mStuffed = 0;

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB(ref TIFFBinaryReader s) : this(ref s, (uint)s.Position, (uint)(s.BaseStream.Length - s.Position)) { }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB(ref TIFFBinaryReader s, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = count + sizeof(uint);
            buffer = new byte[size];
            s.BaseStream.Position = offset;
            s.BaseStream.Read(buffer, 0, (int)count);
            Init();
        }

        public BitPumpMSB(ref byte[] _buffer, uint _size)
        {
            buffer = (_buffer);
            size = (_size + sizeof(uint));
            Init();
        }

        public void Init()
        {
            current_buffer = new byte[24];
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

        public uint getOffset()
        {
            return (uint)(off - (mLeft >> 3));
        }

        public void checkPos()
        {
            if (mStuffed > 8)
                throw new IOException("Out of buffer read");
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        public void fill()
        {
            if (mLeft < 25) _fill();
        }

        //get the nbits as an int32
        public uint peekBitsNoFill(uint nbits)
        {
            int shift = (int)(mLeft - nbits);
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return (uint)(ret & ((1 << (int)nbits) - 1));
        }

        public uint getBit()
        {
            if (mLeft == 0) _fill();
            mLeft--;
            uint _byte = (uint)(mLeft >> 3);
            return (uint)(current_buffer[_byte] >> (mLeft & 0x7)) & 1;
        }

        public uint getBitsNoFill(uint nbits)
        {
            uint ret = peekBitsNoFill(nbits);
            mLeft -= (byte)nbits;
            return ret;
        }

        public uint getBits(uint nbits)
        {
            fill();
            return getBitsNoFill(nbits);
        }

        public uint peekBit()
        {
            if (mLeft == 0) _fill();
            return (uint)(current_buffer[(mLeft - 1) >> 3] >> ((mLeft - 1) & 0x7)) & 1;
        }

        public uint getBitNoFill()
        {
            mLeft--;
            uint ret = (uint)(current_buffer[mLeft >> 3] >> (mLeft & 0x7)) & 1;
            return ret;
        }

        public uint peekByteNoFill()
        {
            int shift = mLeft - 8;
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return ret & 0xff;
        }

        public uint peekBits(uint nbits)
        {
            fill();
            return peekBitsNoFill(nbits);
        }

        public uint peekByte()
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
            uint ret = current_buffer[shift >> 3];
            ret >>= shift & 7;
            return (byte)(ret & 0xff);
        }

        public uint getBitSafe()
        {
            fill();
            checkPos();

            return getBitNoFill();
        }

        public uint getBitsSafe(uint nbits)
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
            mStuffed = 0;
            off = offset;
            fill();
        }
    }
}


