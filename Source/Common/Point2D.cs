using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PhotoNet.Common
{
    public class Point2D : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public uint width, height;


        public UInt32 Area
        {
            get { return width * height; }
        }

        public override bool Equals(object obj)
        {
            if (obj is Point2D) return this == (Point2D)obj;
            return base.Equals(obj);
        }

        //TODO check
        public override int GetHashCode()
        {
            return (int)(width + height);
        }

        public Point2D() { width = height = 0; }
        public Point2D(uint width, uint height) { this.width = width; this.height = height; }
        public Point2D(Point2D point) { width = point.width; height = point.height; }

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

        public bool IsThisInside(Point2D otherPoint) { return (width <= otherPoint.width && height <= otherPoint.height); }
        public Point2D GetSmallest(Point2D otherPoint) { return new Point2D(Math.Min(width, otherPoint.width), Math.Min(height, otherPoint.height)); }

        public void Flip()
        {
            var tmp = width;
            width = height;
            height = tmp;
        }

        public override string ToString()
        {
            return "Width: " + width + "px, Height: " + height + "px";
        }
    };
}
