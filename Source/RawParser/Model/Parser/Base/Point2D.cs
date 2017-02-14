using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RawNet
{
    public class Point2D : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private uint width, height;
        public uint Width
        {
            get { return width; }
            set
            {
                if (width != value)
                {
                    width = value;
                    OnPropertyChanged();
                }
            }
        }
        public uint Height
        {
            get { return height; }
            set
            {
                if (height != value)
                {
                    height = value;
                    OnPropertyChanged();
                }
            }
        }

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
        public Point2D(uint width, uint height) { Width = width; Height = height; }
        public Point2D(Point2D point) { width = point.Width; height = point.Height; }

        static public Point2D operator -(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return null;
            if (a is null) return a;
            if (b is null) return b;
            return new Point2D(a.Width - b.Width, a.Height - b.Height);
        }
        static public Point2D operator +(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return null;
            if (a is null) return a;
            if (b is null) return b;
            return new Point2D(a.Width + b.Width, a.Height + b.Height);
        }
        public static bool operator ==(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return true;
            if ((a is null) || (b is null)) return false;
            return a.Width == b.Width && a.Height == b.Height;
        }
        public static bool operator !=(Point2D a, Point2D b)
        {
            if ((a is null) && (b is null)) return false;
            if ((a is null) || (b is null)) return true;
            return a.Width != b.Width || a.Height != b.Height;
        }

        public bool IsThisInside(Point2D otherPoint) { return (width <= otherPoint.Width && height <= otherPoint.Height); }
        public Point2D GetSmallest(Point2D otherPoint) { return new Point2D(Math.Min(width, otherPoint.Width), Math.Min(height, otherPoint.Height)); }

        public void Flip()
        {
            var tmp = width;
            Width = height;
            Height = tmp;
        }

        public override string ToString()
        {
            return "Width: " + Width + "px, Height: " + Height + "px";
        }
    };
}
