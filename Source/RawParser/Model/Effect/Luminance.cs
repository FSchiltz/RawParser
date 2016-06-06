using System;

namespace RawParser.Effect
{
    class Luminance
    {
        private Luminance() { }
        public static void Exposure(ref ushort[] image, uint h, uint w, double value, int colorDepth)
        {
            double v = Math.Pow(2, value);
            uint maxValue = (uint)(1 << colorDepth);
            for (int i = 0; i < h * w * 3; ++i)
            {
                double t = (image[i] * v);
                if (t > maxValue) t = maxValue;
                image[i] = (ushort)t;
            }
        }

        public static void Clip(ref ushort[] image, uint h, uint w, ushort maxValue)
        {
            for (int i = 0; i < w * h * 3; ++i)
            {
                if (image[i] > maxValue) image[i] = maxValue;
            }
        }
    }
}
