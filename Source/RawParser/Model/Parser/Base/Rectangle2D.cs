using System;

namespace RawNet
{
    /* Helper class for managing a rectangle in 2D space. */
    public class Rectangle2D
    {
        public Point2D Position { get; set; }
        public Point2D Dimension { get; set; }
        public uint Top { get { return Position.height; } }
        public uint Bottom { get { return Position.height + Dimension.height; } }
        public uint Left { get { return Position.width; } }
        public uint Right { get { return Position.width + Dimension.width; } }
        public uint Width { get { return Dimension.width; } }
        public uint Height { get { return Dimension.height; } }
        public Point2D TopLeft { get { return Position; } set { Position = value; } }
        public Point2D BottomRight { get { return Dimension + Position; } set { Dimension = new Point2D(value) - Position; } }

        public Rectangle2D() { }
        public Rectangle2D(uint w, uint h) { Dimension = new Point2D(w, h); }
        public Rectangle2D(uint xPos, uint yPos, uint w, uint h) { Dimension = new Point2D(w, h); Position = new Point2D(xPos, yPos); }
        public Rectangle2D(Rectangle2D r)
        {
            Dimension = new Point2D(r.Dimension); Position = new Point2D(r.Position);
        }
        public Rectangle2D(Point2D pos, Point2D size)
        {
            Dimension = size;
            Position = pos;
        }

        public UInt32 Area() { return Dimension.Area(); }
        public void Offset(Point2D offset) { Position += offset; }
        public bool IsThisInside(Rectangle2D otherPoint)
        {
            Point2D br1 = BottomRight;
            Point2D br2 = otherPoint.BottomRight;
            return Position.width >= otherPoint.Position.width && Position.height >= otherPoint.Position.height && br1.width <= br2.width && br1.height <= br2.height;
        }

        public bool IsPointInside(Point2D check)
        {
            Point2D br1 = BottomRight;
            return Position.width <= check.width && Position.height <= check.height && br1.width >= check.width && br1.height >= check.height;
        }


        public void SetAbsolute(uint x1, uint y1, uint x2, uint y2)
        {
            Position = new Point2D(x1, y1);
            Dimension = new Point2D(x2 - x1, y2 - y1);
        }
        public void SetAbsolute(Point2D topLeft, Point2D bottomRight)
        {
            Position = topLeft;
            BottomRight = bottomRight;
        }
        public void SetSize(Point2D size) { Dimension = size; }
        public bool HasPositiveArea() { return (Dimension.width > 0) && (Dimension.height > 0); }

        /* Crop, so area is positive, and return true, if there is any area left */
        /* This will ensure that bottom right is never on the left/top of the offset */
        public bool CropArea()
        {
            Dimension.width = Math.Max(0, Dimension.width);
            Dimension.height = Math.Max(0, Dimension.height);
            return HasPositiveArea();
        }

        /* This will make sure that offset is positive, and make the area smaller if needed */
        /* This will return true if there is any area left */
       /* public bool CropOffsetToZero()
        {
            Point2D crop_pixels = new Point2D();
            if (Position.width < 0)
            {
                crop_pixels.width = -(Position.width);
                Position.width = 0;
            }
            if (Position.height < 0)
            {
                crop_pixels.height = -Position.height;
                Position.height = 0;
            }
            Dimension -= crop_pixels;
            return CropArea();
        }*/
        /*
        Rectangle2D GetOverlap(Rectangle2D other)
        {
            Rectangle2D overlap = new Rectangle2D();
            Point2D br1 = BottomRight;
            Point2D br2 = other.BottomRight;
            overlap.SetAbsolute(Math.Max(Pos.width, other.Pos.width), Math.Max(Pos.height, other.Pos.height), Math.Min(br1.width, br2.width), Math.Min(br1.height, br2.height));
            return overlap;
        }

        Rectangle2D Combine(Rectangle2D other)
        {
            Rectangle2D combined = new Rectangle2D();
            Point2D br1 = BottomRight;
            Point2D br2 = other.BottomRight;
            combined.SetAbsolute(Math.Min(Pos.width, other.Pos.width), Math.Min(Pos.height, other.Pos.height), Math.Max(br1.width, br2.width), Math.Max(br2.height, br2.height));
            return combined;
        }
        */
    };
}
