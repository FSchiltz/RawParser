using System;

namespace RawNet.Decoder.Decompressor
{
    // Note: Allocated buffer MUST be at least size+sizeof(int) large.
    internal class BitPumpPlain : BitPump
    {
        public override uint GetOffset() { return off >> 3; }
        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TIFFBinaryReader s) : this(s, (uint)s.Position, (uint)s.RemainingSize) { }


        /*** Used for entropy encoded sections ***/
        public BitPumpPlain(TIFFBinaryReader s, uint offset, uint count)
        {
            //MIN_GET_BITS = (BITS_PER_LONG - 7);
            size = 8 * count;
            buffer = new byte[count];
            s.BaseStream.Position = offset;
            s.BaseStream.Read(buffer, 0, (int)count);
        }

        public BitPumpPlain(byte[] _buffer, uint _size)
        {
            //MIN_GET_BITS = (BITS_PER_LONG - 7);
            buffer = _buffer;
            size = _size * 8;
        }

        unsafe public override uint GetBit()
        {
            uint v = PeekBit();
            off++;
            return v;
        }

        public override uint GetBits(uint nbits)
        {
            uint v = PeekBits(nbits);
            off += nbits;
            return v;
        }

        unsafe public override uint PeekBit()
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> (int)(off & 7) & 1);
            }
        }

        unsafe public override uint PeekBits(uint nbits)
        {
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(int*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public override uint PeekByte()
        {
            return (uint)(((buffer[off >> 3] << 8) | buffer[(off >> 3) + 1]) >> (int)(off & 7) & 0xff);
        }

        unsafe public override uint GetBitSafe()
        {
            CheckPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & 1);
            }
        }

        unsafe public override uint GetBitsSafe(uint nbits)
        {
            CheckPos();
            fixed (byte* t = &buffer[off >> 3])
            {
                return (uint)(*(Int32*)t >> ((int)off & 7) & ((1 << (int)nbits) - 1));
            }
        }

        public override void SkipBits(uint nbits)
        {
            off += nbits;
            CheckPos();
        }

        unsafe public override byte GetByte()
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

        public override void SetAbsoluteOffset(uint offset)
        {
            off = offset * 8;
            CheckPos();
        }

        #region abstract

        public override void CheckPos()
        {
            throw new NotImplementedException();
        }

        public override void Fill()
        {
            throw new NotImplementedException();
        }

        public override void FillCheck()
        {
            throw new NotImplementedException();
        }

        public override uint GetBitNoFill()
        {
            throw new NotImplementedException();
        }

        public override uint GetBitsNoFill(uint nbits)
        {
            throw new NotImplementedException();
        }

        public override void Init()
        {
            throw new NotImplementedException();
        }

        public override uint PeekBitsNoFill(uint nbits)
        {
            throw new NotImplementedException();
        }

        public override uint PeekByteNoFill()
        {
            throw new NotImplementedException();
        }

        public override void SkipBitsNoFill(uint nbits)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
