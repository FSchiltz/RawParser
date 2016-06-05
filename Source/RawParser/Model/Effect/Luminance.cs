using RawParser.Image;
using System;

namespace RawParser.Effect
{
    class Luminance
    {
        private Luminance() { }
        public static void Exposure(ref ushort[] image, uint h, uint w, double value)
        {
            double v = Math.Pow(2, value);
            for (int i = 0; i < h * w * 3; ++i)
            {
                image[i] = (ushort)(image[i] * v);
            }
        }

        public static void Clip(ref ushort[] image,uint h, uint w, ushort maxValue)
        {
            for (int i = 0; i < w * w*3; ++i)
            {
                if (image[i] > maxValue) image[i ] = maxValue;
            }
        }

        internal static void Exposure(ref uint[] image, uint h, uint w, double value)
        {
            double v = Math.Pow(2, value);
            for (int i = 0; i < h * w * 3; ++i)
            {
                image[i] = (uint)(image[i] * v);
            }
        }
    }
}
