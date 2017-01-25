using System;
using System.IO;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    class BitPumpPlain : BitPump
    {
        public override int Offset
        {
            get { return off >> 3; }
            set
            {
                off = value * 8;
                CheckPos();
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TIFFBinaryReader reader) : this(reader, (uint)reader.Position, (uint)reader.RemainingSize) { }
        public BitPumpPlain(TIFFBinaryReader reader, uint offset, uint count)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = 8 * count;
            buffer = new byte[count];
            reader.BaseStream.Position = offset;
            reader.BaseStream.Read(buffer, 0, (int)count);
        }

        public BitPumpPlain(byte[] _buffer, uint _size)
        {
            MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = (_buffer);
            size = (_size * 8);
        }

        public override void CheckPos() { if (off >= size) throw new IOException("Out of buffer read"); }        // Check if we have a valid position

        unsafe override public uint GetBit()
        {
            uint v = PeekBit();
            off++;
            return v;
        }

        public override uint GetBits(int nbits)
        {
            uint v = PeekBits(nbits);
            off += nbits;
            return v;
        }

        unsafe override public uint PeekBit()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> (int)(off & 7) & 1);
            }
        }

        unsafe override public uint PeekBits(int nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(int*)t >> ((int)off & 7) & ((1 << nbits) - 1));
            }
        }

        public override uint PeekByte()
        {
            return (uint)(((buffer[off >> 3] << 8) | buffer[(off >> 3) + 1]) >> (int)(off & 7) & 0xff);
        }

        unsafe override public uint GetBitSafe()
        {
            CheckPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & 1);
            }
        }

        unsafe override public uint GetBitsSafe(int nbits)
        {
            CheckPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & ((1 << nbits) - 1));
            }
        }

        public override void SkipBits(int nbits)
        {
            off += nbits;
            CheckPos();
        }

        unsafe override public byte GetByte()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                uint v = (uint)(*(Int32*)t >> ((int)off & 7) & 0xff);
                off += 8;
                return (byte)v;
            }
        }

        public override byte GetByteSafe()
        {
            var v = GetByte();
            CheckPos();
            return v;
        }

        public override uint PeekBitsNoFill(int v)
        {
            throw new NotImplementedException();
        }

        public override uint GetBitNoFill()
        {
            throw new NotImplementedException();
        }

        public override uint GetBitsNoFill(int nbits)
        {
            throw new NotImplementedException();
        }

        public override void Init()
        {
            throw new NotImplementedException();
        }

        public override uint PeekByteNoFill()
        {
            throw new NotImplementedException();
        }

        public override void SkipBitsNoFill(int nbits)
        {
            throw new NotImplementedException();
        }

        public override void Fill()
        {
            throw new NotImplementedException();
        }
    }
}
