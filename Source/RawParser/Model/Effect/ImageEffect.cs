using RawEditor.View.UIHelper;
using RawNet;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        //public double gamma = 0;
        public double contrast = 0;
        //  public double brightness = 0;
        public double hightlight = 1;
        public double shadow = 1;
        public float[] mul;
        public bool cameraWB;
        public bool ReverseGamma = true;
        public uint maxValue;
        public double saturation = 1;
        //public double vibrance = 0;
        //public double[] camCurve;
        public double rMul;
        public double gMul;
        public double bMul;
        public int rotation;
        //internal double vignet;

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
            yCurve[2] = (maxValue + ((contrast - hightlight) * (maxValue / 200))) * exposure;
            maxValue--;

            var curve = Curve.CubicSpline(xCurve, yCurve);
            if (ReverseGamma)
            {
                double param = 1 / 1.8;

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
            /*
            {
                double param = 2.4;

                param += contrast;
                for (int i = 0; i < curve.Length; i++)
                {
                    double normal = curve[i] / maxValue;
                    if (normal <= 0.04045)
                    {
                        curve[i] = normal / 12.92;
                    }
                    else
                    {
                        curve[i] = Math.Pow((normal+0.055)/1.055, param);
                    }
                    curve[i] *= maxValue;
                }
            }*/
            return curve;
        }

        //ugly code
        public unsafe void ApplyModification(ushort[] image, Point2D dim, Point2D off, Point2D uncrop, int colorDepth, SoftwareBitmap bitmap)
        {
            int shift = colorDepth - 8;
            maxValue = (uint)(1 << colorDepth);
            using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                int startIndex = buffer.GetPlaneDescription(0).StartIndex;
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                if (!cameraWB)
                {
                    mul = new float[4];
                    mul[0] = (float)(rMul / 255);
                    mul[1] = (float)(gMul / 255);
                    mul[2] = (float)(bMul / 255);
                }
                double[] curve = CreateCurve();
                Parallel.For(0, dim.height, y =>
                {
                    long realY = (y + off.height) * uncrop.width * 3;
                    for (int x = 0; x < dim.width; x++)
                    {
                        long realPix = realY + (3 * (x + off.width));
                        long bufferPix = Rotate(x, y, dim.width, dim.height) * 4;
                        double red = image[realPix] * mul[0], green = image[realPix + 1] * mul[1], blue = image[realPix + 2] * mul[2];
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);
                        Color.RgbToHsl(red, green, blue, maxValue, out double h, out double s, out double l);
                        Luminance.Clip(ref l);
                        l = curve[(uint)(l * maxValue)] / maxValue;
                        Luminance.Clip(ref l);
                        s *= saturation;
                        Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);
                        temp[bufferPix] = (byte)((int)blue >> shift);
                        temp[bufferPix + 1] = (byte)((int)green >> shift);
                        temp[bufferPix + 2] = (byte)((int)red >> shift);
                        temp[bufferPix + 3] = 255; //set transparency to 255 else image will be blank
                    }
                });
            }
        }

        public unsafe HistoRaw ApplyModificationHisto(ushort[] image, Point2D dim, Point2D off, Point2D uncrop, int colorDepth, SoftwareBitmap bitmap)
        {
            int shift = colorDepth - 8;
            maxValue = (uint)(1 << colorDepth);
            using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                int startIndex = buffer.GetPlaneDescription(0).StartIndex;
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                if (!cameraWB)
                {
                    mul = new float[4];
                    mul[0] = (float)(rMul / 255);
                    mul[1] = (float)(gMul / 255);
                    mul[2] = (float)(bMul / 255);
                }
                double[] curve = CreateCurve();
                HistoRaw value = new HistoRaw()
                {
                    luma = new int[256],
                    red = new int[256],
                    blue = new int[256],
                    green = new int[256]
                };
                Parallel.For(0, dim.height, y =>
                {
                    long realY = (y + off.height) * uncrop.width * 3;
                    for (int x = 0; x < dim.width; x++)
                    {
                        long realPix = realY + (3 * (x + off.width));
                        long bufferPix = Rotate(x, y, dim.width, dim.height) * 4;
                        double red = image[realPix] * mul[0], green = image[realPix + 1] * mul[1], blue = image[realPix + 2] * mul[2];
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);
                        // Luminance.Clip(ref red, ref green, ref blue, maxValue);                           
                        Color.RgbToHsl(red, green, blue, maxValue, out double h, out double s, out double l);
                        //vignet correction
                        //int xV = (x + off.width);
                        //int yV = (y + off.height);
                        //var v = Math.Abs(xV - (uncrop.width / 2.0)) / uncrop.width;
                        //l *= 1 + (vignet * Math.Sin((xV - uncrop.width / 2) / uncrop.width) + Math.Sin((yV - uncrop.height / 2) / uncrop.width));
                        Luminance.Clip(ref l);
                        if (Double.IsNaN(l)) {
                            Debug.Write("error");
                        }
                        l = curve[(uint)(l * maxValue)] / maxValue;
                        Luminance.Clip(ref l);
                        s *= saturation;
                        // s += vibrance;
                        Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);

                        temp[bufferPix] = (byte)((int)blue >> shift);
                        temp[bufferPix + 1] = (byte)((int)green >> shift);
                        temp[bufferPix + 2] = (byte)((int)red >> shift);
                        temp[bufferPix + 3] = 255; //set transparency to 255 else image will be blank
                        Interlocked.Increment(ref value.red[(int)red >> shift]);
                        Interlocked.Increment(ref value.green[(int)green >> shift]);
                        Interlocked.Increment(ref value.blue[(int)blue >> shift]);
                        Interlocked.Increment(ref value.luma[(((int)red >> shift) + ((int)green >> shift) + ((int)blue >> shift)) / 3]);
                    }
                });
                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long Rotate(long x, long y, uint w, uint h)
        {
            switch (rotation)
            {
                //dest_buffer[c][m - r - 1] = source_buffer[r][c];
                case 2: return (h - y - 1) * w + w - x - 1;
                case 3: return (w - x - 1) * h + y;
                case 1: return x * h + h - y - 1;
                default: return y * w + x;
            }
        }
    }
}
