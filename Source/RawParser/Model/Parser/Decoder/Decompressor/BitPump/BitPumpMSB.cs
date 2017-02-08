using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{

    // Note: Allocated buffer MUST be at least size+sizeof(uint) large.
    internal class BitPumpMSB : BitPump
    {
        byte[] current_buffer;

        public override int Offset
        {
            get
            {
                return off - (left >> 3);
            }

            set
            {
                if (value >= size)
                    throw new IOException("Offset set out of buffer");

                left = 0;
                stuffed = 0;
                off = value;
                Fill();
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB(TIFFBinaryReader reader) : this(reader, (uint)reader.Position, (uint)(reader.BaseStream.Length - reader.Position)) { }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB(TIFFBinaryReader reader, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = count + sizeof(uint);
            buffer = new byte[size];
            reader.BaseStream.Position = offset;
            reader.BaseStream.Read(buffer, 0, (int)count);
            Init();
        }

        public BitPumpMSB(byte[] _buffer, uint _size)
        {
            buffer = (_buffer);
            size = (_size + sizeof(uint));
            Init();
        }

        public override void Init()
        {
            current_buffer = new byte[24];
            Fill();
        }

        private void FillNoCheck()
        {
            // Fill in 96 bits
            //uint[] b = Common.convertByteToUInt(current_buffer);
            if ((off + 12) > size)
            {
                while (left <= 64 && off < size)
                {
                    for (int i = left >> 3; i >= 0; i--)
                        current_buffer[i + 1] = current_buffer[i];
                    current_buffer[0] = buffer[off++];
                    left += 8;
                }
                while (left <= 64)
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
                    left += 32;
                    stuffed += 4;
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
            left += 96;
        }

        public override void CheckPos()
        {
            if (stuffed > 8)
                throw new IOException("Out of buffer read");
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        public override void Fill()
        {
            if (left < 25) FillNoCheck();
        }

        //get the nbits as an int32
        public override uint PeekBitsNoFill(int nbits)
        {
            int shift = (int)(left - nbits);
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return (uint)(ret & ((1 << (int)nbits) - 1));
        }

        public override uint GetBit()
        {
            if (left == 0) Fill();
            left--;
            uint _byte = (uint)(left >> 3);
            return (uint)(current_buffer[_byte] >> (left & 0x7)) & 1;
        }

        public override uint GetBitsNoFill(int nbits)
        {
            uint ret = PeekBitsNoFill(nbits);
            left -= (byte)nbits;
            return ret;
        }

        public override uint GetBits(int nbits)
        {
            Fill();
            return GetBitsNoFill(nbits);
        }

        public override uint PeekBit()
        {
            if (left == 0) Fill();
            return (uint)(current_buffer[(left - 1) >> 3] >> ((left - 1) & 0x7)) & 1;
        }

        public override uint GetBitNoFill()
        {
            left--;
            uint ret = (uint)(current_buffer[left >> 3] >> (left & 0x7)) & 1;
            return ret;
        }

        public override uint PeekByteNoFill()
        {
            int shift = left - 8;
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return ret & 0xff;
        }

        public override uint PeekBits(int nbits)
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

        public override void SkipBits(int nbits)
        {
            int skipn = (int)nbits;
            while (skipn != 0)
            {
                Fill();
                CheckPos();
                int n = Math.Min(skipn, left);
                left -= (byte)n;
                skipn -= n;
            }
        }

        public override void SkipBitsNoFill(int nbits)
        {
            left -= (byte)nbits;
        }

        public override byte GetByte()
        {
            Fill();
            left -= 8;
            int shift = left;
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

        public override uint GetBitsSafe(int nbits)
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
    }
}