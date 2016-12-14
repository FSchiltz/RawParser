using System;

namespace RawNet
{
    public class BlackArea
    {
        public Int32 Offset { get; internal set; } // Offset in bayer pixels.
        public Int32 Size { get; internal set; }   // Size in bayer pixels.
        public bool IsVertical { get; internal set; }  // Otherwise horizontal

        public BlackArea(int offset, int size, bool isVertical)
        {
            Offset = offset;
            Size = size;
            IsVertical = isVertical;
        }
    }
}
