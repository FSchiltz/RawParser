﻿using RawEditor.View.UIHelper;
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
        //public double temperature = 1;
        //public double tint = 1;
        //public double gamma = 0;
        //public double brightness = 0;
        //public double vibrance = 0;
        //public double vignet;
        //public double[] camCurve;

        public double contrast = 0;
        public double hightlight = 1;
        public double shadow = 1;
        public bool ReverseGamma = true;
        public uint maxValue;
        public double saturation = 1;
        public double exposure = 0;
        public double rMul;
        public double gMul;
        public double bMul;
        public int rotation;
        public double gamma;
        public bool histoEqual;

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
            yCurve[2] = (maxValue + ((contrast + hightlight) * (maxValue / 200))) * exposure;
            maxValue--;

            var curve = Curve.CubicSpline(xCurve, yCurve);
            if (ReverseGamma)
            {
                double param = 1 / gamma;
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

        public unsafe HistoRaw Apply(ImageComponent image, SoftwareBitmap bitmap)
        {
            int shift = image.ColorDepth - 8;
            maxValue = (uint)(1 << image.ColorDepth);
            using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                int startIndex = buffer.GetPlaneDescription(0).StartIndex;
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                double[] curve = CreateCurve();
                HistoRaw histogram = new HistoRaw()
                {
                    luma = new int[256],
                    red = new int[256],
                    blue = new int[256],
                    green = new int[256]
                };
                Parallel.For(0, image.dim.height, y =>
                {
                    long realY = (y + image.offset.height) * image.uncroppedDim.width;
                    for (int x = 0; x < image.dim.width; x++)
                    {
                        long realPix = realY + x;
                        long bufferPix = Rotate(x, y, image.dim.width, image.dim.height) * 4;
                        double red = image.red[realPix] * rMul, green = image.green[realPix] * gMul, blue = image.blue[realPix] * bMul;
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);
                        Color.RgbToHsl(red, green, blue, maxValue, out double h, out double s, out double l);
                        //vignet correction
                        //int xV = (x + off.width);
                        //int yV = (y + off.height);
                        //var v = Math.Abs(xV - (uncrop.width / 2.0)) / uncrop.width;
                        //l *= 1 + (vignet * Math.Sin((xV - uncrop.width / 2) / uncrop.width) + Math.Sin((yV - uncrop.height / 2) / uncrop.width));
                        Luminance.Clip(ref l);
                        l = curve[(uint)(l * maxValue)] / maxValue;
                        Luminance.Clip(ref l);
                        Interlocked.Increment(ref histogram.luma[(int)(l * 255)]);
                        s *= saturation;
                        // s += vibrance;
                        Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);

                        temp[bufferPix] = (byte)((int)blue >> shift);
                        temp[bufferPix + 1] = (byte)((int)green >> shift);
                        temp[bufferPix + 2] = (byte)((int)red >> shift);
                        temp[bufferPix + 3] = 255; //set transparency to 255 else image will be blank
                        Interlocked.Increment(ref histogram.red[(int)red >> shift]);
                        Interlocked.Increment(ref histogram.green[(int)green >> shift]);
                        Interlocked.Increment(ref histogram.blue[(int)blue >> shift]);
                    }
                });

                if (histoEqual)
                {
                    //apply histogram equalisation if needed using the histogram
                    //create a lookup table
                    byte[] lut = new byte[256];
                    double pixelCount = image.dim.height * image.dim.width;

                    int sum = 0;
                    // build a LUT containing scale factor
                    for (int i = 0; i < 256; ++i)
                    {
                        sum += histogram.luma[i];
                        lut[i] = (byte)(sum * 255 / pixelCount);
                        /*
                        double val = (byte)(value.luma[i] / pixelCount);
                        if (val > 255) val = 255;
                        lut[i] = (byte)val;*/

                    }
                    //reset the histogram
                    histogram = new HistoRaw()
                    {
                        luma = new int[256],
                        red = new int[256],
                        blue = new int[256],
                        green = new int[256]
                    };

                    // transform image using sum histogram as a LUT
                    Parallel.For(0, buffer.GetPlaneDescription(0).Height, y =>
                     {
                         int realY = y * buffer.GetPlaneDescription(0).Width;
                         for (int x = 0; x < buffer.GetPlaneDescription(0).Width; x++)
                         {
                             int realX = (realY + x) * 4;
                             double red = temp[realX], green = temp[realX + 1], blue = temp[realX + 2];
                             Color.RgbToHsl(red, green, blue, maxValue, out double h, out double s, out double l);
                             l = lut[(int)(l * 255.0)] / 255.0;
                             Interlocked.Increment(ref histogram.luma[(int)(l * 255)]);
                             Color.HslToRgb(h, s, l, maxValue, ref red, ref green, ref blue);
                             Luminance.Clip(ref red, ref green, ref blue, 255);
                             temp[realX] = (byte)(red);
                             temp[realX + 1] = (byte)(green);
                             temp[realX + 2] = (byte)(blue);
                             //update the histogram
                             Interlocked.Increment(ref histogram.red[(int)red]);
                             Interlocked.Increment(ref histogram.green[(int)green]);
                             Interlocked.Increment(ref histogram.blue[(int)blue]);
                         }
                     });
                }
                return histogram;
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
