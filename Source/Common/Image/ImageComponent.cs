using PhotoNet.Common;
using System.Runtime.CompilerServices;

namespace PhotoNet.Common
{
    public class ImageComponent<T>
    {
        public T[] red, blue, green, rawView;
        public bool IsLumaOnly { get; set; }//if is true,only green is filled
        public Point2D dim, offset = new Point2D();
        public Point2D UncroppedDim { get; set; }
        public uint cpp;

        public ImageComponent() { }
        public ImageComponent(Point2D dim, ushort colorDepth)
        {
            var d = dim.Area;
            red = new T[d];
            blue = new T[d];
            green = new T[d];
            UncroppedDim = new Point2D(dim);
            this.dim = new Point2D(dim);
            ColorDepth = colorDepth;
        }

        public ImageComponent(ImageComponent<T> image)
        {
            red = image.red;
            green = image.green;
            blue = image.blue;
            offset = new Point2D(image.offset);
            dim = new Point2D(image.dim);
            UncroppedDim = image.UncroppedDim;
            ColorDepth = image.ColorDepth;
        }

        public ushort ColorDepth { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetSafeBound(long row, long col)
        {
            if (row < 0 || row >= dim.height || col < 0 || col >= dim.width)
            {
                return false;
            }
            else return true;
        }
    }
}