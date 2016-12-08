using System;

namespace RawNet
{

    /* Helper class for managing a rectangle in 2D space. */
    public class Rectangle2D
    {
        public Rectangle2D()
        { }
        public Rectangle2D(int w, int h) { dim = new Point2D(w, h); }
        public Rectangle2D(int x_pos, int y_pos, int w, int h) { dim = new Point2D(w, h); pos = new Point2D(x_pos, y_pos); }
        public Rectangle2D(Rectangle2D r)
        {
            dim = new Point2D(r.dim); pos = new Point2D(r.pos);
        }
        public Rectangle2D(Point2D _pos, Point2D size)
        {
            dim = size;
            pos = _pos;
        }

        public UInt32 area() { return dim.area(); }
        public void offset(Point2D offset) { pos += offset; }
        public bool isThisInside(ref Rectangle2D otherPoint)
        {
            Point2D br1 = getBottomRight();
            Point2D br2 = otherPoint.getBottomRight();
            return pos.x >= otherPoint.pos.x && pos.y >= otherPoint.pos.y && br1.x <= br2.x && br1.y <= br2.y;
        }

        public bool isPointInside(Point2D checkPoint)
        {
            Point2D br1 = getBottomRight();
            return pos.x <= checkPoint.x && pos.y <= checkPoint.y && br1.x >= checkPoint.x && br1.y >= checkPoint.y;
        }

        public int getTop() { return pos.y; }
        public int getBottom() { return pos.y + dim.y; }
        public int getLeft() { return pos.x; }
        public int getRight() { return pos.x + dim.x; }
        public int getWidth() { return dim.x; }
        public int getHeight() { return dim.y; }
        public Point2D getTopLeft() { return pos; }
        public Point2D getBottomRight() { return dim + pos; }
        /* Retains size */
        public void setTopLeft(Point2D top_left) { pos = top_left; }
        /* Set BR  */
        public void setBottomRightAbsolute(Point2D bottom_right)
        {
            dim = new Point2D(bottom_right) - pos;
        }
        public void setAbsolute(int x1, int y1, int x2, int y2)
        {
            pos = new Point2D(x1, y1);
            dim = new Point2D(x2 - x1, y2 - y1);
        }
        public void setAbsolute(Point2D top_left, Point2D bottom_right)
        {
            pos = top_left;
            setBottomRightAbsolute(bottom_right);
        }
        public void setSize(Point2D size) { dim = size; }
        public bool hasPositiveArea() { return (dim.x > 0) && (dim.y > 0); }

        /* Crop, so area is positive, and return true, if there is any area left */
        /* This will ensure that bottom right is never on the left/top of the offset */
        public bool cropArea()
        {
            dim.x = Math.Max(0, dim.x);
            dim.y = Math.Max(0, dim.y);
            return hasPositiveArea();
        }

        /* This will make sure that offset is positive, and make the area smaller if needed */
        /* This will return true if there is any area left */
        public bool cropOffsetToZero()
        {
            Point2D crop_pixels = new Point2D();
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

        Rectangle2D getOverlap(ref Rectangle2D other)
        {
            Rectangle2D overlap = new Rectangle2D();
            Point2D br1 = getBottomRight();
            Point2D br2 = other.getBottomRight();
            overlap.setAbsolute(Math.Max(pos.x, other.pos.x), Math.Max(pos.y, other.pos.y), Math.Min(br1.x, br2.x), Math.Min(br1.y, br2.y));
            return overlap;
        }

        Rectangle2D combine(ref Rectangle2D other)
        {
            Rectangle2D combined = new Rectangle2D();
            Point2D br1 = getBottomRight();
            Point2D br2 = other.getBottomRight();
            combined.setAbsolute(Math.Min(pos.x, other.pos.x), Math.Min(pos.y, other.pos.y), Math.Max(br1.x, br2.x), Math.Max(br2.y, br2.y));
            return combined;
        }
        public Point2D pos { get; set; }
        public Point2D dim { get; set; }
    };
}
