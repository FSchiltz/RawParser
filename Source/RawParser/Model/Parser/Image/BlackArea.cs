using System;

namespace RawNet
{
    public class BlackArea
    {
        public uint Offset { get; internal set; } // Offset in bayer pixels.
        public uint Size { get; internal set; }   // Size in bayer pixels.
        public bool IsVertical { get; internal set; }  // Otherwise horizontal

        public BlackArea(uint offset, uint size, bool isVertical)
        {
            Offset = offset;
            Size = size;
            IsVertical = isVertical;
        }
    }
}
