using System.Runtime.CompilerServices;

namespace RawNet
{
    public class ImageComponent
    {
        public ushort[] red, blue, green, rawView;
        public bool IsLumaOnly { get; set; }//if is true,only green is filled
        public Point2D dim, offset = new Point2D(), uncroppedDim;


        public ImageComponent() { }
        public ImageComponent(ImageComponent image)
        {
            red = image.red;
            green = image.green;
            blue = image.blue;
            uncroppedDim = image.uncroppedDim;
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