using System;

namespace RawNet
{
    public class BlackArea
    {
        public Int32 offset { get; internal set; } // Offset in bayer pixels.
        public Int32 size { get; internal set; }   // Size in bayer pixels.
        public bool isVertical { get; internal set; }  // Otherwise horizontal

        public BlackArea(int _offset, int _size, bool _isVertical)
        {
            offset = (_offset);
            size = (_size);
            isVertical = (_isVertical);
        }
    }
}
