using PhotoNet.Common;
using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(uint) large.
    internal class BitPumpJPEG : BitPump
    {
        byte[] current_buffer = new byte[24];
        int stuffed = 0;

        public override int Offset
        {
            get
            {
                return off - (left >> 3) + stuffed;
            }
            set
            {
                if (value >= size)
                    throw new IOException("Offset set out of buffer");

                left = 0;
                off = value;
                Fill();
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpJPEG(ImageBinaryReader reader) : this(reader, (uint)reader.Position, (uint)reader.BaseStream.Length) { }
        public BitPumpJPEG(ImageBinaryReader reader, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = (uint)(reader.RemainingSize + sizeof(uint));
            buffer = new byte[size];
            reader.BaseStream.Position = offset;
            reader.Read(buffer, 0, (int)reader.RemainingSize);
            Init();
        }

        public BitPumpJPEG(byte[] _buffer, uint _size)
        {
            buffer = _buffer;
            size = _size + sizeof(uint);
            Init();
        }

        public void Init()
        {
            Fill();
        }

        // Fill the buffer with at least 24 bits
        public override void Fill()
        {
            if (left < 25)
            {  // Fill in 96 bits
               //int[] b = Common.convertByteToInt(current_buffer);
                if ((off + 12) >= size)
                {
                    while (left <= 64 && off < size)
                    {
                        for (int i = left >> 3; i >= 0; i--)
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
                        left += 8;
                    }
                    while (left < 64)
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
                        left += 32;
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
                left += 96;
            };
        }

        public override uint PeekBits(int nbits)
        {
            int shift = (left - nbits);
            uint ret = current_buffer[shift >> 3] | (uint)current_buffer[(shift >> 3) + 1] << 8 | (uint)current_buffer[(shift >> 3) + 2] << 16 | (uint)current_buffer[(shift >> 3) + 3] << 24;
            ret >>= shift & 7;
            return (uint)(ret & ((1 << nbits) - 1));
        }

        public override int GetBit()
        {
            var t = PeekBit();
            left--;
            return t;
        }

        public override int PeekBit()
        {
            if (left == 0) Fill();
            return (current_buffer[(left - 1) >> 3] >> ((left - 1) & 0x7)) & 1;
        }

        public override void SkipBits(int nbits)
        {
            int skipn = nbits;
            while (skipn != 0)
            {
                Fill();
                int n = Math.Min(skipn, left);
                left -= n;
                skipn -= n;
            }
        }

        public override uint GetBits(int nbits)
        {
            uint ret = PeekBits(nbits);
            left -= nbits;
            return ret;
        }
    }
}




