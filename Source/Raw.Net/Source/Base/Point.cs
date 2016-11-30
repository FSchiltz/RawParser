using System;

namespace RawNet
{
    public class iPoint2D
    {
        public override bool Equals(object obj)
        {
            if (obj is iPoint2D) return this == (iPoint2D)obj;
            return base.Equals(obj);
        }

        //TODO check
        public override int GetHashCode()
        {
            return x + y;
        }

        public iPoint2D() { x = y = 0; }
        public iPoint2D(int x, int y) { this.x = x; this.y = y; }
        public iPoint2D(iPoint2D pt) { x = pt.x; y = pt.y; }
        static public iPoint2D operator -(iPoint2D a, iPoint2D b)
        {
            return new iPoint2D(a.x - b.x, a.y - b.y);
        }
        static public iPoint2D operator +(iPoint2D a, iPoint2D b)
        {
            return new iPoint2D(a.x + b.x, a.y + b.y);
        }
        public static bool operator ==(iPoint2D a, iPoint2D rhs) { return a.x == rhs.x && a.y == rhs.y; }
        public static bool operator !=(iPoint2D a, iPoint2D rhs) { return a.x != rhs.x || a.y != rhs.y; }

        public UInt32 area()
        {
            return (uint)Math.Abs(x * y);
        }
        public bool isThisInside(iPoint2D otherPoint) { return (x <= otherPoint.x && y <= otherPoint.y); }
        public iPoint2D getSmallest(iPoint2D otherPoint) { return new iPoint2D(Math.Min(x, otherPoint.x), Math.Min(y, otherPoint.y)); }
        public int x, y;
    };

    /* Helper class for managing a rectangle in 2D space. */
    public class iRectangle2D
    {
        public iRectangle2D()
        { }
        public iRectangle2D(int w, int h) { dim = new iPoint2D(w, h); }
        public iRectangle2D(int x_pos, int y_pos, int w, int h) { dim = new iPoint2D(w, h); pos = new iPoint2D(x_pos, y_pos); }
        public iRectangle2D(iRectangle2D r)
        {
            dim = new iPoint2D(r.dim); pos = new iPoint2D(r.pos);
        }
        public iRectangle2D(iPoint2D _pos, iPoint2D size)
        {
            dim = size;
            pos = _pos;
        }

        public UInt32 area() { return dim.area(); }
        public void offset(iPoint2D offset) { pos += offset; }
        public bool isThisInside(ref iRectangle2D otherPoint)
        {
            iPoint2D br1 = getBottomRight();
            iPoint2D br2 = otherPoint.getBottomRight();
            return pos.x >= otherPoint.pos.x && pos.y >= otherPoint.pos.y && br1.x <= br2.x && br1.y <= br2.y;
        }

        public bool isPointInside(iPoint2D checkPoint)
        {
            iPoint2D br1 = getBottomRight();
            return pos.x <= checkPoint.x && pos.y <= checkPoint.y && br1.x >= checkPoint.x && br1.y >= checkPoint.y;
        }

        public int getTop() { return pos.y; }
        public int getBottom() { return pos.y + dim.y; }
        public int getLeft() { return pos.x; }
        public int getRight() { return pos.x + dim.x; }
        public int getWidth() { return dim.x; }
        public int getHeight() { return dim.y; }
        iPoint2D getTopLeft() { return pos; }
        iPoint2D getBottomRight() { return dim + pos; }
        /* Retains size */
        public void setTopLeft(iPoint2D top_left) { pos = top_left; }
        /* Set BR  */
        public void setBottomRightAbsolute(iPoint2D bottom_right) { dim = new iPoint2D(bottom_right) - pos; }
        public void setAbsolute(int x1, int y1, int x2, int y2) { pos = new iPoint2D(x1, y1); dim = new iPoint2D(x2 - x1, y2 - y1); }
        public void setAbsolute(iPoint2D top_left, iPoint2D bottom_right) { pos = top_left; setBottomRightAbsolute(bottom_right); }
        public void setSize(iPoint2D size) { dim = size; }
        public bool hasPositiveArea() { return (dim.x > 0) && (dim.y > 0); }
        /* Crop, so area is positive, and return true, if there is any area left */
        /* This will ensure that bottom right is never on the left/top of the offset */
        bool cropArea() { dim.x = Math.Max(0, dim.x); dim.y = Math.Max(0, dim.y); return hasPositiveArea(); }
        /* This will make sure that offset is positive, and make the area smaller if needed */
        /* This will return true if there is any area left */
        bool cropOffsetToZero()
        {
            iPoint2D crop_pixels = new iPoint2D();
            if (pos.x < 0)
            {
                crop_pixels.x = -(pos.x);
                pos.x = 0;
            }
            if (pos.y < 0)
            {
                crop_pixels.y = -pos.y;
                pos.y = 0;
            }
            dim -= crop_pixels;
            return cropArea();
        }

        iRectangle2D getOverlap(ref iRectangle2D other)
        {
            iRectangle2D overlap = new iRectangle2D();
            iPoint2D br1 = getBottomRight();
            iPoint2D br2 = other.getBottomRight();
            overlap.setAbsolute(Math.Max(pos.x, other.pos.x), Math.Max(pos.y, other.pos.y), Math.Min(br1.x, br2.x), Math.Min(br1.y, br2.y));
            return overlap;
        }

        iRectangle2D combine(ref iRectangle2D other)
        {
            iRectangle2D combined = new iRectangle2D();
            iPoint2D br1 = getBottomRight();
            iPoint2D br2 = other.getBottomRight();
            combined.setAbsolute(Math.Min(pos.x, other.pos.x), Math.Min(pos.y, other.pos.y), Math.Max(br1.x, br2.x), Math.Max(br2.y, br2.y));
            return combined;
        }
        public iPoint2D pos;
        public iPoint2D dim;
    };
}
