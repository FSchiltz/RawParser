namespace RawNet.Decoder.Decompressor
{
    internal abstract class BitPump
    {
        protected byte[] buffer;
        protected uint size;            // This if the end of buffer.
        protected uint off;

        abstract public void CheckPos();    
        abstract public void Fill();
        abstract public void FillCheck();
        abstract public uint GetBit();
        abstract public uint GetBitNoFill();
        abstract public uint GetBits(uint nbits);
        abstract public uint GetBitSafe();
        abstract public uint GetBitsNoFill(uint nbits);
        abstract public uint GetBitsSafe(uint nbits);
        abstract public byte GetByte();
        abstract public byte GetByteSafe();
        abstract public uint GetOffset();
        abstract public void Init();
        abstract public uint PeekBit();
        abstract public uint PeekBits(uint nbits);
        abstract public uint PeekBitsNoFill(uint nbits);
        abstract public uint PeekByte();
        abstract public uint PeekByteNoFill();
        abstract public void SetAbsoluteOffset(uint offset);
        abstract public void SkipBits(uint nbits);
        abstract public void SkipBitsNoFill(uint nbits);
    }
}