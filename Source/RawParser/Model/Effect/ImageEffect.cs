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
        internal int rotation;

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

        public unsafe HistoRaw ApplyModification(ushort[] image, Point2D dim, Point2D off, Point2D uncrop, int colorDepth, ref SoftwareBitmap bitmap, bool histo)
        {
            HistoRaw value = new HistoRaw();
            if (histo)
            {
                value.luma = new int[256];
                value.red = new int[256];
                value.blue = new int[256];
                value.green = new int[256];
            }
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
                        mul[0] = (float)(rMul / 255);
                        mul[1] = (float)(gMul / 255);
                        mul[2] = (float)(bMul / 255);
                    }
                    double[] contrastCurve = CreateCurve();
                    Parallel.For(0, dim.height, y =>
                    {
                        int realY = (y+off.height) * uncrop.width * 3 ;
                        int bufferY = y * dim.width * 4 + bufferLayout.StartIndex;
                        for (int x = 0; x < dim.width; x++)
                        {
                            int realPix = realY + (3 * (x+off.width));
                            int bufferPix;
                            switch (rotation)
                            {
                                //dest_buffer[c][m - r - 1] = source_buffer[r][c];
                                case 2:
                                    bufferPix = (dim.height - y - 1) * dim.width + dim.width - x - 1;
                                    break;
                                case 3:
                                    bufferPix = (dim.width - x - 1) * dim.height + y;
                                    break;
                                case 1:
                                    bufferPix = x * dim.height + dim.height - y - 1;
                                    break;
                                default:
                                    bufferPix = y * dim.width + x;
                                    break;
                            }
                            bufferPix = bufferPix * 4 + bufferLayout.StartIndex;
                            //get the RGB value
                            double red = image[realPix], green = image[realPix + 1], blue = image[realPix + 2];
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
                            Luminance.Contraste(ref red, ref green, ref blue, maxValue, contrast);
                            Luminance.Clip(ref red, ref green, ref blue, maxValue);

                            temp[bufferPix] = (byte)((int)blue >> shift);
                            temp[bufferPix + 1] = (byte)((int)green >> shift);
                            temp[bufferPix + 2] = (byte)((int)red >> shift);
                            if (histo)
                            {
                                Interlocked.Increment(ref value.red[(int)red >> shift]);
                                Interlocked.Increment(ref value.green[(int)green >> shift]);
                                Interlocked.Increment(ref value.blue[(int)blue >> shift]);
                                Interlocked.Increment(ref value.luma[(((int)red >> shift) + ((int)green >> shift) + ((int)blue >> shift)) / 3]);
                            }
                            //set transparency to 255 else image will be blank
                            temp[bufferPix + 3] = 255;
                        }
                    });
                }
            }
            return value;
        }
    }
}
