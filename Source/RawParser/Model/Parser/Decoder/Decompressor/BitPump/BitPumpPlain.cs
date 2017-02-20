using System;
using System.Diagnostics;
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
            }
        }

        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TiffBinaryReader reader) : this(reader, (uint)reader.Position, (uint)reader.RemainingSize) { }
        public BitPumpPlain(TiffBinaryReader reader, long offset, long count)
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

        unsafe override public uint PeekBit()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> (int)(off & 7) & 1);
            }
        }

        unsafe override public byte GetByte()
        {
            uint v = PeekByte();
            off += 8;
            return (byte)v;
        }

        unsafe public override uint PeekBits(int nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(int*)t >> ((int)off & 7) & ((1 << nbits) - 1));
            }
        }

        public override uint GetBit()
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

        public override byte PeekByte()
        {
            return (byte)(((buffer[off >> 3] << 8) | buffer[(off >> 3) + 1]) >> (off & 7) & 0xff);
        }

        public override void SkipBits(int nbits)
        {
            off += nbits;
        }

        public override void Fill()
        {
            Debug.Assert(false);
        }

        unsafe public override ushort GetLowBits(int nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                var v = (ushort)(*(int*)t >> (off & 7) & ((1 << nbits) - 1));
                off += nbits;
                return v;
            }
        }
    }
}
