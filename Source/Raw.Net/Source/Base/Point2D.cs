using System;

namespace RawNet
{
    public class Point2D
    {
        public int width, height;

        public override bool Equals(object obj)
        {
            if (obj is Point2D) return this == (Point2D)obj;
            return base.Equals(obj);
        }

        //TODO check
        public override int GetHashCode()
        {
            return width + height;
        }

        public Point2D() { width = height = 0; }
        public Point2D(int x, int y) { this.width = x; this.height = y; }
        public Point2D(Point2D pt) { width = pt.width; height = pt.height; }
        static public Point2D operator -(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return null;
            if (a is null) return a;
            if (b is null) return b;
            return new Point2D(a.width - b.width, a.height - b.height);
        }
        static public Point2D operator +(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return null;
            if (a is null) return a;
            if (b is null) return b;
            return new Point2D(a.width + b.width, a.height + b.height);
        }
        public static bool operator ==(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return true;
            if ((a is null) || (b is null)) return false;
            return a.width == b.width && a.height == b.height;
        }
        public static bool operator !=(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return false;
            if ((a is null) || (b is null)) return true;
            return a.width != b.width || a.height != b.height;
        }

        public UInt32 Area()
        {
            return (uint)Math.Abs(width * height);
        }
        public bool IsThisInside(Point2D otherPoint) { return (width <= otherPoint.width && height <= otherPoint.height); }
        public Point2D GetSmallest(Point2D otherPoint) { return new Point2D(Math.Min(width, otherPoint.width), Math.Min(height, otherPoint.height)); }

        public void Flip()
        {
            var tmp = width;
            width = height;
            height = tmp;
        }
    };
}
