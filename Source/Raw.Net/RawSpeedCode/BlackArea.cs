using System;

namespace RawSpeed
{
    class BlackArea
    {
        Int32 offset; // Offset in bayer pixels.
        Int32 size;   // Size in bayer pixels.
        bool isVertical;  // Otherwise horizontal

        BlackArea(int _offset, int _size, bool _isVertical)
        {
            offset = (_offset); size = (_size); isVertical = (_isVertical);
        }
    }
}
