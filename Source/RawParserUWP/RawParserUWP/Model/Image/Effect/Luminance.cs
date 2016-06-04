using RawParserUWP.Model.Format.Image;
using System;

namespace RawParserUWP.Model.Image.Effect
{
    class Luminance
    {
        public static void Exposure(ref RawImage image, double value)
        {
            ushort maxValue = (ushort)(Math.Pow(2, image.colorDepth) - 1);
            if (value > 0)
            {
                for (int i = 0; i < image.height * image.width; ++i)
                {
                    image.imageData[(i * 3)] = (ushort)(image.imageData[(i * 3)] * value);
                }
            }
            else if (value < 0)
            {
                for (int i = 0; i < image.height * image.width; ++i)
                {
                    image.imageData[(i * 3)] = (ushort)(image.imageData[(i * 3)] / value);
                }
            }
        }

        public static void Clip(ref RawImage image, ushort maxValue)
        {
            for (int i = 0; i < image.height * image.width; ++i)
            {
                if (image.imageData[(i * 3)] > maxValue) image.imageData[(i * 3)] = maxValue;
            }
        }
    }
}
