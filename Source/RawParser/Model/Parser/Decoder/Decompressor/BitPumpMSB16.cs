using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    /*** Used for entropy encoded sections, for now only Nikon Coolpix ***/
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    class BitPumpMSB16 : BitPump
    {

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
                current = 0;
                off = value;
                stuffed = 0;
                Fill();
            }
        }

        public BitPumpMSB16(TIFFBinaryReader reader)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            reader.Read(buffer, (int)reader.Position, (int)reader.BaseStream.Length);
            size = (uint)(reader.RemainingSize + sizeof(uint));
            Init();
        }

        public BitPumpMSB16(byte[] _buffer, uint _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            buffer = (_buffer);
            size = (_size + sizeof(uint));
            Init();
        }

        public override void Init()
        {
            stuffed = 0;
            FillNoCheck();
        }

        private void FillNoCheck()
        {
            uint c, c2;
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
                    stuffed++;
                }
                return;
            }
            c = buffer[off++];
            c2 = buffer[off++];
            current <<= 16;
            current |= (c2 << 8) | c;
            left += 16;
        }

        public override void CheckPos()
        {
            if (stuffed > 3)
                throw new IOException("Out of buffer read");
        }        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        public override void Fill() { if (left < MIN_GET_BITS) Fill(); }

        public override uint GetBit()
        {
            if (left == 0) FillNoCheck();
            return (uint)((current >> --left) & 1);
        }

        public override uint GetBitNoFill()
        {
            return (uint)((current >> --left) & 1);
        }

        public override uint GetBits(int nbits)
        {
            if (left < nbits)
            {
                Fill();
            }
            return (uint)((int)(current >> (left -= (nbits))) & ((1 << nbits) - 1));
        }

        public override uint GetBitsNoFill(int nbits)
        {
            return (uint)((int)(current >> (left -= (nbits))) & ((1 << nbits) - 1));
        }

        public override void SkipBits(int nbits)
        {
            while (nbits != 0)
            {
                Fill();
                CheckPos();
                int n = Math.Min(nbits, left);
                left -= n;
                nbits -= n;
            }
        }

        public override uint PeekByteNoFill()
        {
            return (uint)((current >> left - 8) & 0xff);
        }

        public override void SkipBitsNoFill(int nbits)
        {
            left -= nbits;
        }

        public override uint GetBitsSafe(int nbits)
        {
            if (nbits > MIN_GET_BITS)
                throw new IOException("Too many bits requested");

            if (left < nbits)
            {
                Fill();
                CheckPos();
            }
            return (uint)((int)(current >> (left -= (nbits))) & ((1 << (int)nbits) - 1));
        }

        public override uint PeekBitsNoFill(int v)
        {
            throw new NotImplementedException();
        }

        public override uint GetBitSafe()
        {
            throw new NotImplementedException();
        }

        public override byte GetByteSafe()
        {
            throw new NotImplementedException();
        }

        public override byte GetByte()
        {
            throw new NotImplementedException();
        }

        public override uint PeekBits(int nbits)
        {
            throw new NotImplementedException();
        }

        public override uint PeekByte()
        {
            throw new NotImplementedException();
        }

        public override uint PeekBit()
        {
            throw new NotImplementedException();
        }
    }
}