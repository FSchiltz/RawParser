using RawParser.Effect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;

namespace RawParser.Image
{
    class ImageEffect
    {
        public double exposure = 0;
        public double temperature = 1;
        public double tint = 1;
        public double gamma = 0;
        public double contrast = 0;
        public double brightness = 0;
        public double hightlight = 1;
        public double shadow = 1;
        public double[] mul;
        public bool cameraWB;
        public uint maxValue;
        public double saturation = 1;
        public double vibrance = 0;
        public double[] camCurve;

        public void applyModification(ref ushort[] image, uint height, uint width, int colorDepth)
        {
            maxValue = (uint)(1 << colorDepth);
            if (!cameraWB)
            {
                mul = new double[4];
                //Balance.calculateRGB((int)temperature, out mul[0], out mul[1], out mul[2]);
                mul[0] = 255 / temperature;
                mul[1] = 255 / tint;
                mul[2] = 1;
            }
            //generate the curve
            /*
            double[] x = new double[5], y = new double[5];
            //mid point
            x[2] = maxValue / 2;
            y[2] = maxValue / 2;
            //shadow
            x[0] = 0;
            y[0] = shadow * (maxValue / 2 / 100) ;
            //hightlight
            x[4] = maxValue;
            y[4] = maxValue - (hightlight * (maxValue / 2 / 100)) ;
            //contrast
            x[1] = maxValue / 4;
            y[1] = ((y[0] + y[2]) / 2) - (maxValue / 2 / 100 * contrast) ;
            x[3] = maxValue * 3 / 4;
            y[3] = ((y[2] + y[4]) / 2) + (maxValue / 2 / 100 * contrast) ;
            maxValue--;*/
            //interpolate with spline
            double[] contrastCurve = Balance.contrast_curve(shadow, hightlight, 1 << colorDepth);
            //double[] contrastCurve = Curve.simpleInterpol(x, y);
            //double[] gammaCurve = Balance.gamma_curve(0.45, 4.5, 2, 8192 << 3);
            double[] gammaCurve = Balance.gamma_curve(camCurve[0] / 100, camCurve[1] / 10, 2, 8192 << 3);
            for (int i = 0; i < height * width; i++)
            {
                //get the RGB value
                double red = image[i * 3],
                green = image[(i * 3) + 1],
                blue = image[(i * 3) + 2];
                //transform to HSL value
                //double h = 0, s = 0, l = 0;
                //Color.rgbToHsl(red, green, blue, maxValue, ref h, ref s, ref l);
                //change brightness from curve
                //l = contrastCurve[(uint)l];
                //s *= saturation;
                //s += vibrance;
                Luminance.Exposure(ref red, ref green, ref blue, exposure);
                Luminance.Brightness(ref red, ref green, ref blue, brightness);
                //Balance.scaleGamma(ref red, ref green, ref blue, gamma, maxValue);                
                Luminance.Contraste(ref red, ref green, ref blue, maxValue, contrast);

                //change back to RGB
                //Color.hslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);

                Balance.scaleColor(ref red, ref green, ref blue, mul);

                if (red > maxValue) red = maxValue;
                if (green > maxValue) green = maxValue;
                if (blue > maxValue) blue = maxValue;
                if (red < 0) red = 0;
                if (green < 0) green = 0;
                if (blue < 0) blue = 0;
                //change gamma from curve
                red = gammaCurve[(int)red];
                green = gammaCurve[(int)green];
                blue = gammaCurve[(int)blue];

                image[i * 3] = (ushort)red;
                image[(i * 3) + 1] = (ushort)green;
                image[(i * 3) + 2] = (ushort)blue;
            }
        }
    }
}
