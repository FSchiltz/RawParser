using System;

namespace RawEditor.Effect
{
    class Luminance
    {
        private Luminance() { }

        /*
            value = Math.Pow(2, exposure as stop);
        */
        public static void Exposure(ref double r, ref double g, ref double b, double value)
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

        internal static void Brightness(ref double red, ref double green, ref double blue, double brightness)
        {
            red += brightness;
            green += brightness;
            blue += brightness;
        }

        internal static void Clip(ref double l)
        {
            if (l > 1)
                l = 1;
            else if (l < 0)
                l = 0;
        }
    }
}
