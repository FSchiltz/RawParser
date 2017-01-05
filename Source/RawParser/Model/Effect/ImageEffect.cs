using RawNet;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace RawEditor.Effect
{
    // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

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
        public bool ReverseGamma = true;
        public uint maxValue;
        public double saturation = 1;
        public double vibrance = 0;
        //public double[] camCurve;
        public double rMul;
        public double gMul;
        public double bMul;
        internal int rotation;
        internal double vignet;

        internal double[] CreateCurve()
        {
            //generate the curve            
            double[] xCurve = new double[3], yCurve = new double[3];
            //todo add exposure in the curve
            //mid point
            xCurve[1] = maxValue / 2;
            yCurve[1] = (maxValue / 2) * exposure;
            //shadow
            xCurve[0] = 0;
            yCurve[0] = ((shadow - contrast) * (maxValue / 200)) * exposure;
            //hightlight
            xCurve[2] = maxValue;
            yCurve[2] = (maxValue + ((hightlight + contrast) * (maxValue / 200))) * exposure;
            //contrast
            /*
            xCurve[1] = maxValue / 4;
            yCurve[1] = ((yCurve[0] + yCurve[2]) / 2) - (maxValue / 200);
            xCurve[3] = maxValue * 3 / 4;
            yCurve[3] = ((yCurve[2] + yCurve[4]) / 2) + (maxValue / 200);*/
            maxValue--;

            var curve = Curve.CubicSpline(xCurve, yCurve);
            if (ReverseGamma)
            {
                double param = 1 / 2.4;

                param += contrast;
                for (int i = 0; i < curve.Length; i++)
                {
                    double normal = curve[i] / maxValue;
                    if (normal <= 0.0031308)
                    {
                        curve[i] = normal * 12.92;
                    }
                    else
                    {
                        curve[i] = Math.Pow(1.055 * normal, param) - 0.055;
                    }
                    curve[i] *= maxValue;
                }
            }

            return curve;
        }

        public unsafe HistoRaw ApplyModification(ushort[] image, Point2D dim, Point2D off, Point2D uncrop, int colorDepth, SoftwareBitmap bitmap, bool histo)
        {
            HistoRaw value = null;
            if (histo) value = new HistoRaw()
            {
                luma = new int[256],
                red = new int[256],
                blue = new int[256],
                green = new int[256]
            };
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
                        int realY = (y + off.height) * uncrop.width * 3;
                        int bufferY = y * dim.width * 4 + bufferLayout.StartIndex;
                        for (int x = 0; x < dim.width; x++)
                        {
                            int realPix = realY + (3 * (x + off.width));
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
                            //vignet correction
                            //int xV = (x + off.width);
                            //int yV = (y + off.height);

                            //var v = Math.Abs(xV - (uncrop.width / 2.0)) / uncrop.width;
                            //l *= 1 + (vignet * Math.Sin((xV - uncrop.width / 2) / uncrop.width) + Math.Sin((yV - uncrop.height / 2) / uncrop.width));

                            Luminance.Clip(ref l);

                            //change brightness from curve
                            //add saturation
                            l = contrastCurve[(uint)(l * maxValue)] / maxValue;

                            //Luminance.Contraste(ref l, contrast);

                            s *= saturation;
                            s += vibrance;

                            //change back to RGB
                            Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);

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
