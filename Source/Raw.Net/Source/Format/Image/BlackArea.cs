using System;

namespace RawNet
{
    public class BlackArea
    {
        public Int32 offset; // Offset in bayer pixels.
        public Int32 size;   // Size in bayer pixels.
        public bool isVertical;  // Otherwise horizontal

        public BlackArea(int _offset, int _size, bool _isVertical)
        {
            offset = (_offset);
            size = (_size);
            isVertical = (_isVertical);
        }
    }
}
