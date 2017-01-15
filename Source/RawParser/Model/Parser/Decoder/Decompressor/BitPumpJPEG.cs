using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(uint) large.
    internal class BitPumpJPEG : BitPump
    {
        int BITS_PER_LONG = (8 * sizeof(uint));
        int MIN_GET_BITS;   /* max value for long getBuffer */
        byte[] current_buffer = new byte[24];
        int mLeft = 0;
        int stuffed = 0;              // How many bytes has been stuffed?

        /*** Used for entropy encoded sections ***/
        public BitPumpJPEG(TIFFBinaryReader reader) : this(reader, (uint)reader.Position, (uint)reader.BaseStream.Length) { }

        /*** Used for entropy encoded sections ***/
        public BitPumpJPEG(TIFFBinaryReader reader, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = count + sizeof(uint);
            buffer = new byte[size];
            reader.BaseStream.Position = offset;
            reader.Read(buffer, 0, reader.RemainingSize);
            Init();
        }

        public BitPumpJPEG(byte[] _buffer, uint _size)
        {
            buffer = _buffer;
            size = _size + sizeof(uint);
            Init();
        }

        public override void Init()
        {
            Fill();
        }

        public override void Fill()
        {
            // Fill in 96 bits
            //int[] b = Common.convertByteToInt(current_buffer);
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
                    stuffed += 4;  //We are adding to mLeft without incrementing offset
                }
                return;
            }
            current_buffer[15] = current_buffer[3];
            current_buffer[14] = current_buffer[2];
            current_buffer[13] = current_buffer[1];
            current_buffer[12] = current_buffer[0];

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

        public override uint GetOffset()
        {
            return (uint)(off - (mLeft >> 3) + stuffed);
        }

        public override void CheckPos()
        {
            if (off >= size || stuffed > (mLeft >> 3))
            {
                throw new IOException("Out of buffer read");
            }
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        public override void FillCheck()
        {
            if (mLeft < 25) Fill();
        }

        public override uint PeekBitsNoFill(uint nbits)
        {
            int shift = (int)(mLeft - nbits);
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return (uint)(ret & ((1 << (int)nbits) - 1));
        }

        public override uint GetBit()
        {
            if (mLeft == 0) Fill();
            mLeft--;
            uint _byte = (uint)(mLeft >> 3);
            return (uint)(current_buffer[_byte] >> (mLeft & 0x7)) & 1;
        }

        public override uint GetBitsNoFill(uint nbits)
        {
            uint ret = PeekBitsNoFill(nbits);
            mLeft -= (int)nbits;
            return ret;
        }

        public override uint GetBits(uint nbits)
        {
            Fill();
            return GetBitsNoFill(nbits);
        }

        public override uint PeekBit()
        {
            if (mLeft == 0) Fill();
            return (uint)(current_buffer[(mLeft - 1) >> 3] >> ((mLeft - 1) & 0x7)) & 1;
        }

        public override uint GetBitNoFill()
        {
            mLeft--;
            uint ret = (uint)(current_buffer[mLeft >> 3] >> (mLeft & 0x7)) & 1;
            return ret;
        }

        public override uint PeekByteNoFill()
        {
            int shift = mLeft - 8;
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return ret & 0xff;
        }

        public override uint PeekBits(uint nbits)
        {
            Fill();
            return PeekBitsNoFill(nbits);
        }

        public override uint PeekByte()
        {
            Fill();
            if (off > size)
                throw new IOException("Out of buffer read");

            return PeekByteNoFill();
        }

        public override void SkipBits(uint nbits)
        {
            int skipn = (int)nbits;
            while (skipn != 0)
            {
                Fill();
                CheckPos();
                int n = Math.Min(skipn, mLeft);
                mLeft -= n;
                skipn -= n;
            }
        }

        public override void SkipBitsNoFill(uint nbits)
        {
            mLeft -= (int)nbits;
        }

        public override byte GetByte()
        {
            Fill();
            mLeft -= 8;
            int shift = mLeft;
            uint ret = current_buffer[shift >> 3];
            ret >>= shift & 7;
            return (byte)(ret & 0xff);
        }

        public override uint GetBitSafe()
        {
            Fill();
            CheckPos();
            return GetBitNoFill();
        }

        public override uint GetBitsSafe(uint nbits)
        {
            if (nbits > MIN_GET_BITS)
                throw new IOException("Too many bits requested");

            Fill();
            CheckPos();
            return GetBitsNoFill(nbits);
        }

        public override byte GetByteSafe()
        {
            Fill();
            CheckPos();
            return (byte)GetBitsNoFill(8);
        }

        public override void SetAbsoluteOffset(uint offset)
        {
            if (offset >= size)
                throw new IOException("Offset set out of buffer");

            mLeft = 0;
            off = offset;
            Fill();
        }
    }
}




