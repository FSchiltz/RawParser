using System;

namespace RawNet
{

    /* Helper class for managing a rectangle in 2D space. */
    public class Rectangle2D
    {
        public Rectangle2D()
        { }
        public Rectangle2D(int w, int h) { Dim = new Point2D(w, h); }
        public Rectangle2D(int xPos, int yPos, int w, int h) { Dim = new Point2D(w, h); Pos = new Point2D(xPos, yPos); }
        public Rectangle2D(Rectangle2D r)
        {
            Dim = new Point2D(r.Dim); Pos = new Point2D(r.Pos);
        }
        public Rectangle2D(Point2D pos, Point2D size)
        {
            Dim = size;
            Pos = pos;
        }

        public UInt32 Area() { return Dim.Area(); }
        public void Offset(Point2D offset) { Pos += offset; }
        public bool IsThisInside(ref Rectangle2D otherPoint)
        {
            Point2D br1 = GetBottomRight();
            Point2D br2 = otherPoint.GetBottomRight();
            return Pos.width >= otherPoint.Pos.width && Pos.height >= otherPoint.Pos.height && br1.width <= br2.width && br1.height <= br2.height;
        }

        public bool IsPointInside(Point2D checkPoint)
        {
            Point2D br1 = GetBottomRight();
            return Pos.width <= checkPoint.width && Pos.height <= checkPoint.height && br1.width >= checkPoint.width && br1.height >= checkPoint.height;
        }

        public int GetTop() { return Pos.height; }
        public int GetBottom() { return Pos.height + Dim.height; }
        public int GetLeft() { return Pos.width; }
        public int GetRight() { return Pos.width + Dim.width; }
        public int GetWidth() { return Dim.width; }
        public int GetHeight() { return Dim.height; }
        public Point2D GetTopLeft() { return Pos; }
        public Point2D GetBottomRight() { return Dim + Pos; }
        /* Retains size */
        public void SetTopLeft(Point2D topLeft) { Pos = topLeft; }
        /* Set BR  */
        public void SetBottomRightAbsolute(Point2D bottomRight)
        {
            Dim = new Point2D(bottomRight) - Pos;
        }
        public void SetAbsolute(int x1, int y1, int x2, int y2)
        {
            Pos = new Point2D(x1, y1);
            Dim = new Point2D(x2 - x1, y2 - y1);
        }
        public void SetAbsolute(Point2D topLeft, Point2D bottomRight)
        {
            Pos = topLeft;
            SetBottomRightAbsolute(bottomRight);
        }
        public void SetSize(Point2D size) { Dim = size; }
        public bool HasPositiveArea() { return (Dim.width > 0) && (Dim.height > 0); }

        /* Crop, so area is positive, and return true, if there is any area left */
        /* This will ensure that bottom right is never on the left/top of the offset */
        public bool CropArea()
        {
            Dim.width = Math.Max(0, Dim.width);
            Dim.height = Math.Max(0, Dim.height);
            return HasPositiveArea();
        }

        /* This will make sure that offset is positive, and make the area smaller if needed */
        /* This will return true if there is any area left */
        public bool CropOffsetToZero()
        {
            Point2D crop_pixels = new Point2D();
            if (Pos.width < 0)
            {
                crop_pixels.width = -(Pos.width);
                Pos.width = 0;
            }
            if (Pos.height < 0)
            {
                crop_pixels.height = -Pos.height;
                Pos.height = 0;
            }
            Dim -= crop_pixels;
            return CropArea();
        }

        Rectangle2D GetOverlap(ref Rectangle2D other)
        {
            Rectangle2D overlap = new Rectangle2D();
            Point2D br1 = GetBottomRight();
            Point2D br2 = other.GetBottomRight();
            overlap.SetAbsolute(Math.Max(Pos.width, other.Pos.width), Math.Max(Pos.height, other.Pos.height), Math.Min(br1.width, br2.width), Math.Min(br1.height, br2.height));
            return overlap;
        }

        Rectangle2D Combine(ref Rectangle2D other)
        {
            Rectangle2D combined = new Rectangle2D();
            Point2D br1 = GetBottomRight();
            Point2D br2 = other.GetBottomRight();
            combined.SetAbsolute(Math.Min(Pos.width, other.Pos.width), Math.Min(Pos.height, other.Pos.height), Math.Max(br1.width, br2.width), Math.Max(br2.height, br2.height));
            return combined;
        }
        public Point2D Pos { get; set; }
        public Point2D Dim { get; set; }
    };
}
