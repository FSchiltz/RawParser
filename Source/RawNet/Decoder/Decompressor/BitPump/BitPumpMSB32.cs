using PhotoNet.Common;
using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    internal class BitPumpMSB32 : BitPump
    {
        public override int Offset
        {
            get { return off - (left >> 3); }
            set
            {
                if (value >= size)
                    throw new IOException("Offset set out of buffer");

                left = 0;
                current = 0;
                off = value;
                Fill();
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB32(ImageBinaryReader reader) : this(reader, (uint)reader.Position, (uint)(reader.BaseStream.Length - reader.Position)) { }
        public BitPumpMSB32(ImageBinaryReader reader, long offset, long count)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            size = count + sizeof(uint);
            buffer = new byte[size];
            reader.BaseStream.Position = offset;
            reader.BaseStream.Read(buffer, 0, (int)count);
            Init();
        }

        public BitPumpMSB32(byte[] _buffer, uint _size)
        {
            buffer = _buffer;
            size = _size + sizeof(uint);
            MIN_GET_BITS = BITS_PER_LONG_LONG - 33;
            Init();
        }

        public void Init()
        {
            Fill();
        }

        public override void Fill()
        {
            // Fill the buffer with at least 24 bits
            if (left < MIN_GET_BITS)
            {
                int c, c2, c3, c4;
                if ((off + 4) > size)
                {
                    while (off < size)
                    {
                        current <<= 8;
                        c = buffer[off++];
                        current |= c;
                        left += 8;
                    }
                    while (left < MIN_GET_BITS)
                    {
                        current <<= 8;
                        left += 8;
                    }
                    return;
                }
                c = buffer[off++];
                c2 = buffer[off++];
                c3 = buffer[off++];
                c4 = buffer[off++];
                current <<= 32;
                current |= (c4 << 24) | (c3 << 16) | (c2 << 8) | c;
                left += 32;
            }
        }

        public override int GetBit()
        {
            if (left == 0) Fill();
            return (current >> --left) & 1;
        }

        public override uint GetBits(int nbits)
        {
            if (left < nbits)
            {
                Fill();
            }
            return (uint)(current >> (left -= (nbits)) & ((1 << nbits) - 1));
        }

        public override void SkipBits(int nbits)
        {
            while (nbits != 0)
            {
                Fill();
                int n = Math.Min(nbits, left);
                left -= n;
                nbits -= n;
            }
        }

        public override uint PeekBits(int v)
        {
            return 0;
        }

        public override int PeekBit()
        {
            throw new NotImplementedException();
        }
    }
}
