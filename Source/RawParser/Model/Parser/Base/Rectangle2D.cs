using System;

namespace RawNet
{
    /* Helper class for managing a rectangle in 2D space. */
    public class Rectangle2D
    {
        public Point2D Position { get; set; }
        public Point2D Dimension { get; set; }
        public uint Top { get { return Position.Height; } }
        public uint Bottom { get { return Position.Height + Dimension.Height; } }
        public uint Left { get { return Position.Width; } }
        public uint Right { get { return Position.Width + Dimension.Width; } }
        public uint Width { get { return Dimension.Width; } }
        public uint Height { get { return Dimension.Height; } }
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
            return Position.Width >= otherPoint.Position.Width && Position.Height >= otherPoint.Position.Height && br1.Width <= br2.Width && br1.Height <= br2.Height;
        }

        public bool IsPointInside(Point2D check)
        {
            Point2D br1 = BottomRight;
            return Position.Width <= check.Width && Position.Height <= check.Height && br1.Width >= check.Width && br1.Height >= check.Height;
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
        public bool HasPositiveArea() { return (Dimension.Width > 0) && (Dimension.Height > 0); }

        /* Crop, so area is positive, and return true, if there is any area left */
        /* This will ensure that bottom right is never on the left/top of the offset */
        public bool CropArea()
        {
            Dimension.Width = Math.Max(0, Dimension.Width);
            Dimension.Height = Math.Max(0, Dimension.Height);
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
