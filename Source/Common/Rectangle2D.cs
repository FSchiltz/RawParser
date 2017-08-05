using System;

namespace PhotoNet.Common
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
        public Rectangle2D(uint width, uint height) { Dimension = new Point2D(width, height); }
        public Rectangle2D(uint xPos, uint yPos, uint width, uint height) { Dimension = new Point2D(width, height); Position = new Point2D(xPos, yPos); }
        public Rectangle2D(Rectangle2D rectangle)
        {
            Dimension = new Point2D(rectangle.Dimension); Position = new Point2D(rectangle.Position);
        }
        public Rectangle2D(Point2D position, Point2D size)
        {
            Dimension = size;
            Position = position;
        }

        public UInt32 Area { get { return Dimension.Area; } }
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
             if (Position.Width < 0)
             {
                 crop_pixels.Width = -(Position.Width);
                 Position.Width = 0;
             }
             if (Position.Height < 0)
             {
                 crop_pixels.Height = -Position.Height;
                 Position.Height = 0;
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
            overlap.SetAbsolute(Math.Max(Pos.Width, other.Pos.Width), Math.Max(Pos.Height, other.Pos.Height), Math.Min(br1.Width, br2.Width), Math.Min(br1.Height, br2.Height));
            return overlap;
        }

        Rectangle2D Combine(Rectangle2D other)
        {
            Rectangle2D combined = new Rectangle2D();
            Point2D br1 = BottomRight;
            Point2D br2 = other.BottomRight;
            combined.SetAbsolute(Math.Min(Pos.Width, other.Pos.Width), Math.Min(Pos.Height, other.Pos.Height), Math.Max(br1.Width, br2.Width), Math.Max(br2.Height, br2.Height));
            return combined;
        }
        */
    };
}
