namespace RawParser.Effect
{
    class Luminance
    {
        private Luminance() { }

        /*
            value = Math.Pow(2, exposure as stop);
        */
        public static void Exposure(ref double r,ref double g, ref double b, double value)
        {
            r *= value;
            g *= value;
            b *= value;
        }

        public static void Contraste(ref double r, ref double g, ref double b, uint maxValue, double value)
        {
            r /= maxValue;
            r -= 0.5;
            r *= value * 1.0;
            r += 0.5;
            r *= maxValue;

            g /= maxValue;
            g -= 0.5;
            g *= value * 1.0;
            g += 0.5;
            g *= maxValue;

            b /= maxValue;
            b -= 0.5;
            b *= value * 1.0;
            b += 0.5;
            b *= maxValue;
        }

        public static void Clip(ref ushort[] image, uint h, uint w, ushort maxValue)
        {
            for (int i = 0; i < w * h * 3; ++i)
            {
                if (image[i] > maxValue) image[i] = maxValue;
            }
        }

        internal static void Brightness(ref double red, ref double green, ref double blue, double brightness)
        {
            red += brightness;
            green += brightness;
            blue += brightness;
        }
    }
}
