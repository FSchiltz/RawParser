using PhotoNet.Common;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PhotoNet
{
    static class Luminance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clip(ref double red, ref double green, ref double blue, uint maxValue)
        {
            if (red > maxValue)
                red = maxValue;
            else if (red < 0)
                red = 0;

            if (green > maxValue)
                green = maxValue;
            if (green < 0)
                green = 0;

            if (blue > maxValue)
                blue = maxValue;
            else if (blue < 0)
                blue = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Clip(ref double l)
        {
            if (l > 1) l = 1;
            else if (l < 0) l = 0;
        }

        internal static void Clip(ImageComponent<int> image)
        {
            var maxValue = (1 << image.ColorDepth) - 1;
            Parallel.For(0, image.dim.height, y =>
            {
                long realY = y * image.dim.width;
                for (int x = 0; x < image.dim.width; x++)
                {
                    long realPix = realY + x;
                    var red = image.red[realPix];
                    var green = image.green[realPix];
                    var blue = image.blue[realPix];
                    if (red < 0) red = 0;
                    else if (red > maxValue) red = maxValue;

                    if (green < 0) green = 0;
                    else if (green > maxValue) green = maxValue;

                    if (blue < 0) blue = 0;
                    else if (blue > maxValue) blue = maxValue;

                    image.red[realPix] = red;
                    image.green[realPix] = green;
                    image.blue[realPix] = blue;
                }
            });
        }

        internal static void Clip(ImageComponent<int> image, ushort colorDepth)
        {
            var maxValue = (1 << colorDepth) - 1;
            var shift = image.ColorDepth - colorDepth;
            image.ColorDepth = colorDepth;
            Parallel.For(0, image.dim.height, y =>
            {
                long realY = y * image.dim.width;
                for (int x = 0; x < image.dim.width; x++)
                {
                    long realPix = realY + x;
                    var red = image.red[realPix] >> shift;
                    var green = image.green[realPix] >> shift;
                    var blue = image.blue[realPix] >> shift;
                    if (red < 0) red = 0;
                    else if (red > maxValue) red = maxValue;

                    if (green < 0) green = 0;
                    else if (green > maxValue) green = maxValue;

                    if (blue < 0) blue = 0;
                    else if (blue > maxValue) blue = maxValue;

                    image.red[realPix] = red;
                    image.green[realPix] = green;
                    image.blue[realPix] = blue;
                }
            });
        }
    }
}
