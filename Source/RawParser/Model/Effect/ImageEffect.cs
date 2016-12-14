using RawEditor.Effect;
using RawNet;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace RawEditor
{
    class ImageEffect
    {
        public double exposure = 0;
        //public double temperature = 1;
        //public double tint = 1;
        public double gamma = 0;
        public double contrast = 0;
        public double brightness = 0;
        public double hightlight = 1;
        public double shadow = 1;
        public float[] mul;
        public bool cameraWB;
        public uint maxValue;
        public double saturation = 1;
        public double vibrance = 0;
        public double[] camCurve;
        public double rMul;
        public double gMul;
        public double bMul;

        internal double[] CreateCurve()
        {
            //generate the curve            
            double[] xCurve = new double[5], yCurve = new double[5];
            //mid point
            xCurve[2] = maxValue / 2;
            yCurve[2] = maxValue / 2;
            //shadow
            xCurve[0] = 0;
            yCurve[0] = shadow * (maxValue / (200));
            //hightlight
            xCurve[4] = maxValue;
            yCurve[4] = maxValue + (hightlight * (maxValue / 200));
            //contrast
            xCurve[1] = maxValue / 4;
            yCurve[1] = ((yCurve[0] + yCurve[2]) / 2) - (maxValue / 200);
            xCurve[3] = maxValue * 3 / 4;
            yCurve[3] = ((yCurve[2] + yCurve[4]) / 2) + (maxValue / 200);
            maxValue--;

            //interpolate with spline
            //double[] contrastCurve = Balance.contrast_curve(shadow, hightlight, 1 << colorDepth);
            return Curve.CubicSpline(xCurve, yCurve);
        }

        public void ApplyModification(ushort[] image, Point2D dim, int colorDepth)
        {
            maxValue = (uint)(1 << colorDepth);
            if (!cameraWB)
            {
                mul = new float[4];
                //Balance.calculateRGB((int)temperature, out mul[0], out mul[1], out mul[2]);
                mul[0] = (float)(rMul / 255);
                mul[1] = (float)(gMul / 255);
                mul[2] = (float)(bMul / 255);
            }
            double[] contrastCurve = CreateCurve();

            //Change the gamma/No more gammaneeded here, the raw should be transformed to neutral gamma before demos
            //double[] gammaCurve = Balance.gamma_curve(0.45, 4.5, 2, 8192 << 3);

            //gammacurve from camera
            //double[] gammaCurve = Balance.gamma_curve(camCurve[0] / 100, camCurve[1] / 10, 2, 8192 << 3);

            Parallel.For(0, dim.height, y =>
            {
                int realY = y * dim.width * 3;
                for (int x = 0; x < dim.width; x++)
                {
                    int realPix = realY + (3 * x);
                    //get the RGB value
                    double red = image[realPix],
                    green = image[realPix + 1],
                    blue = image[realPix + 2];

                    //convert to linear rgb (not needed, the raw should be in linear already)
                    /*Balance.sRGBToRGB(ref red, maxValue - 1);
                    Balance.sRGBToRGB(ref green, maxValue - 1);
                    Balance.sRGBToRGB(ref blue, maxValue - 1);*/

                    //scale according to the white balance
                    //Balance.scaleColor(ref red, ref green, ref blue, mul);
                    red *= mul[0];
                    green *= mul[1];
                    blue *= mul[2];
                    //clip
                    Luminance.Clip(ref red, ref green, ref blue, maxValue);
                    double h = 0, s = 0, l = 0;
                    //transform to HSL value
                    Color.RgbToHsl(red, green, blue, maxValue, ref h, ref s, ref l);
                    //change brightness from curve
                    //add saturation
                    l = contrastCurve[(uint)(l * maxValue)] / maxValue;
                    s *= saturation;
                    s += vibrance;
                    l *= exposure;
                    l += brightness / 100;
                    //change back to RGB
                    Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);

                    //Luminance.Exposure(ref red, ref green, ref blue, exposure);
                    //Luminance.Brightness(ref red, ref green, ref blue, brightness);
                    //Balance.scaleGamma(ref red, ref green, ref blue, gamma, maxValue);               
                    Luminance.Contraste(ref red, ref green, ref blue, maxValue, contrast);

                    //clip
                    Luminance.Clip(ref red, ref green, ref blue, maxValue);
                    image[realPix] = (byte)red;
                    image[realPix + 1] = (byte)green;
                    image[realPix + 2] = (byte)blue;
                    //change gamma from curve 
                    /*
                    image[i * 3] = (ushort)gammaCurve[(int)red];
                    image[(i * 3) + 1] = (ushort)gammaCurve[(int)green];
                    image[(i * 3) + 2] = (ushort)gammaCurve[(int)blue];*/
                }
            });
        }

        public unsafe int[] ApplyModification(ushort[] image, Point2D dim, int colorDepth, ref SoftwareBitmap bitmap)
        {
            int[] value = new int[256];
            using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                    maxValue = (uint)(1 << colorDepth);
                    int shift = colorDepth - 8;
                    if (!cameraWB)
                    {
                        mul = new float[4];
                        //Balance.calculateRGB((int)temperature, out mul[0], out mul[1], out mul[2]);
                        mul[0] = (float)(rMul / 255);
                        mul[1] = (float)(gMul / 255);
                        mul[2] = (float)(bMul / 255);
                    }



                    //interpolate with spline
                    //double[] contrastCurve = Balance.contrast_curve(shadow, hightlight, 1 << colorDepth);
                    double[] contrastCurve = CreateCurve();

                    //Change the gamma/No more gammaneeded here, the raw should be transformed to neutral gamma before demos
                    //double[] gammaCurve = Balance.gamma_curve(0.45, 4.5, 2, 8192 << 3);

                    //gammacurve from camera
                    //double[] gammaCurve = Balance.gamma_curve(camCurve[0] / 100, camCurve[1] / 10, 2, 8192 << 3);

                    Parallel.For(0, dim.height, y =>
                   {
                       int realY = y * dim.width * 3;
                       int bufferY = y * dim.width * 4 + +bufferLayout.StartIndex;
                       for (int x = 0; x < dim.width; x++)
                       {
                           int realPix = realY + (3 * x);
                           int bufferPix = bufferY + (4 * x);
                           //get the RGB value
                           double red = image[realPix],
                          green = image[realPix + 1],
                          blue = image[realPix + 2];

                           //convert to linear rgb (not needed, the raw should be in linear already)
                           /*Balance.sRGBToRGB(ref red, maxValue - 1);
                           Balance.sRGBToRGB(ref green, maxValue - 1);
                           Balance.sRGBToRGB(ref blue, maxValue - 1);*/

                           //scale according to the white balance
                           red *= mul[0];
                           green *= mul[1];
                           blue *= mul[2];
                           //clip
                           Luminance.Clip(ref red, ref green, ref blue, maxValue);
                           double h = 0, s = 0, l = 0;
                           //transform to HSL value
                           Color.RgbToHsl(red, green, blue, maxValue, ref h, ref s, ref l);
                           //change brightness from curve
                           //add saturation
                           l = contrastCurve[(uint)(l * maxValue)] / maxValue;
                           s *= saturation;
                           s += vibrance;
                           l *= exposure;
                           l += brightness / 100;
                           //change back to RGB
                           Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);

                           //Luminance.Exposure(ref red, ref green, ref blue, exposure);
                           //Luminance.Brightness(ref red, ref green, ref blue, brightness);
                           //Balance.scaleGamma(ref red, ref green, ref blue, gamma, maxValue);               
                           Luminance.Contraste(ref red, ref green, ref blue, maxValue, contrast);

                           //clip
                           Luminance.Clip(ref red, ref green, ref blue, maxValue);

                           temp[bufferPix] = (byte)((int)blue >> shift);
                           temp[bufferPix + 1] = (byte)((int)green >> shift);
                           temp[bufferPix + 2] = (byte)((int)red >> shift);

                           Interlocked.Increment(ref value[(((int)red >> shift) + ((int)green >> shift) + ((int)blue >> shift)) / 3]);
                           //set transparency to 255 else image will be blank
                           temp[bufferPix + 3] = 255;
                           //change gamma from curve 
                           /*
                           image[i * 3] = (ushort)gammaCurve[(int)red];
                           image[(i * 3) + 1] = (ushort)gammaCurve[(int)green];
                           image[(i * 3) + 2] = (ushort)gammaCurve[(int)blue];*/
                       }
                   });
                }
            }
            return value;
        }
    }
}
