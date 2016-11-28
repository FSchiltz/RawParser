using System;

namespace RawEditor.Effect
{
    class Color
    {
        public static void rgbToHsl(double r, double g, double b, uint maxValue, ref double h, ref double s, ref double l)
        {
            r /= maxValue;
            g /= maxValue;
            b /= maxValue;
            double max = Math.Max(r, g);
            max = Math.Max(max, b);
            double min = Math.Min(r, g);
            min = Math.Min(min, b);
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; // achromatic
            }
            else
            {
                var d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
                if (r == max)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (g == max)
                {
                    h = (b - r) / d + 2.0;
                }
                else if (b == max)
                {
                    h = (r - g) / d + 4.0;
                }
            }
            h /= 6;
        }


        public static void hslToRgb(double h, double s, double l, uint maxValue, ref double r, ref double g, ref double b)
        {
            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                r = hue2rgb(p, q, h + 1 / 3.0);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1 / 3.0);
            }
            r *= maxValue;
            g *= maxValue;
            b *= maxValue;
        }

        public static double hue2rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1 / 2.0) return q;
            if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6.0;
            return p;
        }
    }
}

