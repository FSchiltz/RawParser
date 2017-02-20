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
                Fill();
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB16(TiffBinaryReader reader) : this(reader, (uint)reader.Position, (uint)(reader.BaseStream.Length - reader.Position)) { }

        /*** Used for entropy encoded sections ***/
        public BitPumpMSB16(TiffBinaryReader reader, long offset, long count)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            size = count + sizeof(uint);
            buffer = new byte[size];
            reader.BaseStream.Position = offset;
            reader.BaseStream.Read(buffer, 0, (int)count);
            Fill();
        }

        public BitPumpMSB16(byte[] _buffer, uint _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG_LONG - 33);
            buffer = (_buffer);
            size = (_size + sizeof(uint));
            Fill();
        }

        public override void Fill()
        {
            if (left < MIN_GET_BITS)
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
                    }
                    return;
                }
                c = buffer[off++];
                c2 = buffer[off++];
                current <<= 16;
                current |= (c2 << 8) | c;
                left += 16;
            }
        }

        public override uint GetBit()
        {
            if (left == 0) Fill();
            return (uint)((current >> --left) & 1);
        }

        public override uint GetBits(int nbits)
        {
            if (left < nbits) Fill();
            return (uint)((int)(current >> (left -= (nbits))) & ((1 << nbits) - 1));
        }

        public override void SkipBits(int nbits)
        {
            if (left < nbits) Fill();
            while (nbits != 0)
            {
                Fill();
                int n = Math.Min(nbits, left);
                left -= n;
                nbits -= n;
            }
        }

        public override byte PeekByte()
        {
            if (left < 8) Fill();
            return (byte)((current >> left - 8) & 0xff);
        }

        public override uint PeekBits(int v)
        {
            throw new NotImplementedException();
        }

        public override byte GetByte()
        {
            throw new NotImplementedException();
        }

        public override uint PeekBit()
        {
            throw new NotImplementedException();
        }

        public override ushort GetLowBits(int nbits)
        {
            if (left < nbits) Fill();
            return (ushort)((int)(current >> (left -= (nbits))) & ((1 << nbits) - 1));
        }
    }
}