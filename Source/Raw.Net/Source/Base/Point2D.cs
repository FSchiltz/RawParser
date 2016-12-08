using System;

namespace RawNet
{
    public class Point2D
    {
        public override bool Equals(object obj)
        {
            if (obj is Point2D) return this == (Point2D)obj;
            return base.Equals(obj);
        }

        //TODO check
        public override int GetHashCode()
        {
            return x + y;
        }

        public Point2D() { x = y = 0; }
        public Point2D(int x, int y) { this.x = x; this.y = y; }
        public Point2D(Point2D pt) { x = pt.x; y = pt.y; }
        static public Point2D operator -(Point2D a, Point2D b)
        {
            return new Point2D(a.x - b.x, a.y - b.y);
        }
        static public Point2D operator +(Point2D a, Point2D b)
        {
            return new Point2D(a.x + b.x, a.y + b.y);
        }
        public static bool operator ==(Point2D a, Point2D b)
        {
            return a.x == b.x && a.y == b.y;
        }
        public static bool operator !=(Point2D a, Point2D b)
        {
            return a.x != b.x || a.y != b.y;
        }

        public UInt32 area()
        {
            return (uint)Math.Abs(x * y);
        }
        public bool isThisInside(Point2D otherPoint) { return (x <= otherPoint.x && y <= otherPoint.y); }
        public Point2D getSmallest(Point2D otherPoint) { return new Point2D(Math.Min(x, otherPoint.x), Math.Min(y, otherPoint.y)); }
        public int x, y;
    };
}
