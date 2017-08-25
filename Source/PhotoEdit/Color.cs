using System;
using System.Runtime.CompilerServices;
using PhotoNet.Common;
using System.Threading.Tasks;

namespace PhotoNet
{
    static public class ColorManipulation
    {
        public static void SplitTone(ImageComponent<int> image, Pixel splitShadow, Pixel splitHighlight, double splitBalance, uint maxValue)
        {
            if (splitShadow.IsZero() && splitHighlight.IsZero()) return;
            //scale the value
            var coeff = maxValue / (double)byte.MaxValue;
            splitShadow.R *= coeff;
            splitShadow.G *= coeff;
            splitShadow.B *= coeff;
            splitHighlight.R *= coeff;
            splitHighlight.G *= coeff;
            splitHighlight.B *= coeff;


            //loop and apply
            Parallel.For(0, image.dim.height, y =>
            {
                long realY = (y + image.offset.height) * image.UncroppedDim.width;
                for (int x = 0; x < image.dim.width; x++)
                {
                    long realPix = realY + x + image.offset.width;
                    //compute luminance
                    var l = ((image.red[realPix] + image.green[realPix] + image.blue[realPix]) / 3.0 / maxValue);
                    double invL = (1 - l) * (1 - splitBalance);
                    l *= splitBalance;

                    image.red[realPix] += (int)(splitShadow.R * invL);
                    image.green[realPix] += (int)(splitShadow.G * invL);
                    image.blue[realPix] += (int)(splitShadow.B * invL);

                    image.red[realPix] += (int)(splitHighlight.R * l);
                    image.green[realPix] += (int)(splitHighlight.G * l);
                    image.blue[realPix] += (int)(splitHighlight.B * l);
                }
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RgbToHsv(double red, double green, double blue, uint maxValue, out double h, out double s, out double v)
        {
            red /= maxValue;
            green /= maxValue;
            blue /= maxValue;
            double max = Math.Max(red, green);
            max = Math.Max(max, blue);
            double min = Math.Min(red, green);
            min = Math.Min(min, blue);
            v = max;

            if (max == min)
            {
                h = s = 0; // achromatic
            }
            else
            {
                var d = max - min;
                s = d / max;
                if (red == max)
                {
                    h = (green - blue) / d + (green < blue ? 6 : 0);
                }
                else if (green == max)
                {
                    h = (blue - red) / d + 2.0;
                }
                else if (blue == max)
                {
                    h = (red - green) / d + 4.0;
                }
                else
                {
                    h = 0;
                }
            }
            h /= 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HsvToRgb(double h, double s, double v, uint maxValue, out double red, out double green, out double blue)
        {
            if (s == 0)
            {
                red = green = blue = v; // achromatic
            }
            else
            {
                h *= 6;
                if (h >= 6) h = 0;     //H must be < 1
                var var_i = (int)h;             //Or ... var_i = floor( var_h )
                var var_1 = v * (1 - s);
                var var_2 = v * (1 - s * (h - var_i));
                var var_3 = v * (1 - s * (1 - (h - var_i)));

                if (var_i == 0)
                {
                    red = v;
                    green = var_3;
                    blue = var_1;
                }
                else if (var_i == 1)
                {
                    red = var_2;
                    green = v;
                    blue = var_1;
                }
                else if (var_i == 2)
                {
                    red = var_1;
                    green = v;
                    blue = var_3;
                }
                else if (var_i == 3)
                {
                    red = var_1;
                    green = var_2;
                    blue = v;
                }
                else if (var_i == 4)
                {
                    red = var_3;
                    green = var_1;
                    blue = v;
                }
                else
                {
                    red = v;
                    green = var_1;
                    blue = var_2;
                }
            }
            red *= maxValue;
            green *= maxValue;
            blue *= maxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RgbToHsl(double red, double green, double blue, uint maxValue, out double h, out double s, out double l)
        {
            red /= maxValue;
            green /= maxValue;
            blue /= maxValue;
            double max = Math.Max(red, green);
            max = Math.Max(max, blue);
            double min = Math.Min(red, green);
            min = Math.Min(min, blue);
            l = (max + min) / 2.0;

            if (max == min)
            {
                h = s = 0; // achromatic
            }
            else
            {
                var d = max - min;
                s = l >= 0.5 ? d / (2.0 - max - min) : d / (max + min);
                if (red == max)
                {
                    h = (green - blue) / d + (green < blue ? 6 : 0);
                }
                else if (green == max)
                {
                    h = (blue - red) / d + 2.0;
                }
                else if (blue == max)
                {
                    h = (red - green) / d + 4.0;
                }
                else
                {
                    h = 0;
                }
            }
            h /= 6;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void HslToRgb(double h, double s, double l, uint maxValue, out double red, out double green, out double blue)
        {
            if (s == 0)
            {
                red = green = blue = l; // achromatic
            }
            else
            {
                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                red = Hue2Rgb(p, q, h + 1 / 3.0);
                green = Hue2Rgb(p, q, h);
                blue = Hue2Rgb(p, q, h - 1 / 3.0);
            }
            red *= maxValue;
            green *= maxValue;
            blue *= maxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Hue2Rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            else if (t > 1) t -= 1;

            if (t < 1 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 0.5) return q;
            if (t < 2 / 3.0) return p + (q - p) * (2 / 3.0 - t) * 6.0;
            return p;
        }
    }
}