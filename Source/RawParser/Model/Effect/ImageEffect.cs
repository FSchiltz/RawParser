using RawEditor.View.UIHelper;
using RawNet;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using RawEditor.Settings;
using System.ComponentModel;

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

    public class ImageEffect : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private uint maxValue;

        #region Binding
        private bool histoEqual = false;
        public bool HistoEqualisation
        {
            get { return histoEqual; }
            set
            {
                if (histoEqual != value)
                {
                    histoEqual = value;
                    OnPropertyChanged();
                }
            }
        }

        private int rotation = 0;
        public int Rotation
        {
            get { return rotation; }
            set
            {
                if (rotation != value)
                {
                    if (value < 0) rotation = 4 + (value % 4);
                    else rotation = value % 4;
                    OnPropertyChanged();
                }
            }
        }

        private double brightness = 0;
        public double Brightness
        {
            get { return brightness; }
            set
            {
                if (brightness != value)
                {
                    brightness = value;
                    OnPropertyChanged();
                }
            }
        }

        private double vibrance = 1;
        public double Vibrance
        {
            get { return vibrance; }
            set
            {
                if (vibrance != value)
                {
                    vibrance = value;
                    OnPropertyChanged();
                }
            }
        }

        private double vignet;
        public double Vignet
        {
            get { return vignet; }
            set
            {
                if (vignet != value)
                {
                    vignet = value;
                    OnPropertyChanged();
                }
            }
        }

        private double sharpness = 0;
        public double Sharpness
        {
            get { return sharpness; }
            set
            {
                if (sharpness != value)
                {
                    sharpness = value;
                    OnPropertyChanged();
                }
            }
        }

        private double denoise = 0;
        public double Denoise
        {
            get { return denoise; }
            set
            {
                if (denoise != value)
                {
                    denoise = value;
                    OnPropertyChanged();
                }
            }
        }

        private double contrast = 0;
        public double Contrast
        {
            get { return contrast; }
            set
            {
                if (contrast != value)
                {
                    contrast = value;
                    OnPropertyChanged();
                }
            }
        }

        private double hightlight = 0;
        public double Hightlight
        {
            get { return hightlight; }
            set
            {
                if (hightlight != value)
                {
                    hightlight = value;
                    OnPropertyChanged();
                }
            }
        }

        private double shadow = 0;
        public double Shadow
        {
            get { return shadow; }
            set
            {
                if (shadow != value)
                {
                    shadow = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool reverseGamma = false;
        public bool ReverseGamma
        {
            get { return reverseGamma; }
            set
            {
                if (reverseGamma != value)
                {
                    reverseGamma = value;
                    OnPropertyChanged();
                }
            }
        }

        private double saturation = 1;
        public double Saturation
        {
            get { return saturation; }
            set
            {
                if (saturation != value)
                {
                    saturation = value;
                    OnPropertyChanged();
                }
            }
        }

        private double exposure = 1;
        public double Exposure
        {
            set
            {
                exposure = Math.Pow(2, value);
                OnPropertyChanged();
            }
            get { return Math.Log(exposure, 2); }
        }

        private double rMul = 1;
        public double RMul
        {
            get { return rMul; }
            set
            {
                if (rMul != value)
                {
                    rMul = value;
                    OnPropertyChanged();
                }
            }
        }

        private double gMul = 1;
        public double GMul
        {
            get { return gMul; }
            set
            {
                if (gMul != value)
                {
                    gMul = value;
                    OnPropertyChanged();
                }
            }
        }

        private double bMul = 1;
        public double BMul
        {
            get { return bMul; }
            set
            {
                if (bMul != value)
                {
                    bMul = value;
                    OnPropertyChanged();
                }
            }
        }

        private double gamma = 2.2;
        public double Gamma
        {
            get { return gamma; }
            set
            {
                if (gamma != value)
                {
                    gamma = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        internal ImageEffect GetCopy()
        {
            return (ImageEffect)MemberwiseClone();
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        internal double[] CreateCurve()
        {
            //generate the curve            
            double[] xCurve = new double[3], yCurve = new double[3];
            //mid point
            xCurve[1] = maxValue / 2;
            yCurve[1] = (maxValue / 2) * exposure;
            //shadow
            xCurve[0] = 0;
            yCurve[0] = ((shadow - contrast) * (maxValue / 200)) * exposure;
            //hightlight
            xCurve[2] = maxValue;
            yCurve[2] = (maxValue + ((contrast + hightlight) * (maxValue / 200))) * exposure;

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
            maxValue = (uint)(1 << image.ColorDepth) - 1;
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
                Parallel.For(0, image.dim.Height, y =>
                {
                    long realY = (y + image.offset.Height) * image.uncroppedDim.Width;
                    for (int x = 0; x < image.dim.Width; x++)
                    {
                        long realPix = realY + x + image.offset.Width;
                        long bufferPix = Rotate(x, y, image.dim.Width, image.dim.Height) * 4;
                        double red = image.red[realPix] * rMul, green = image.green[realPix] * gMul, blue = image.blue[realPix] * bMul;
                        Luminance.Clip(ref red, ref green, ref blue, maxValue);
                        Color.RgbToHsl(red, green, blue, maxValue, out double h, out double s, out double l);
                        //vignet correction
                        //int xV = (x + off.Width);
                        //int yV = (y + off.Height);
                        //var v = Math.Abs(xV - (uncrop.Width / 2.0)) / uncrop.Width;
                        //l *= 1 + (vignet * Math.Sin((xV - uncrop.Width / 2) / uncrop.Width) + Math.Sin((yV - uncrop.Height / 2) / uncrop.Width));
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
                    double pixelCount = image.dim.Height * image.dim.Width;

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

                //denoise
                if (Denoise != 0) {

                }

                //sharpening
                /*
                if (Sharpness != 0)
                    Parallel.For(1, buffer.GetPlaneDescription(0).Height - 1, y =>
                    {
                        int realY = y * buffer.GetPlaneDescription(0).Width;
                        for (int x = 1; x < buffer.GetPlaneDescription(0).Width - 1; x++)
                        {
                            int realX = (realY + x) * 4;
                            temp[realX + 1] = (byte)((9 * temp[realX + 1])
                            + (-1 * temp[(realY + x + 1) * 4 + 1])
                            + (-1 * temp[(realY + x - 1) * 4 + 1])
                            + (-1 * temp[(((y + 1) * buffer.GetPlaneDescription(0).Width) + x + 1) * 4 + 1])
                            + (-1 * temp[(((y - 1) * buffer.GetPlaneDescription(0).Width) + x + 1) * 4 + 1])
                            + (-1 * temp[(((y + 1) * buffer.GetPlaneDescription(0).Width) + x + 1) * 4 + 1])
                            + (-1 * temp[(((y + 1) * buffer.GetPlaneDescription(0).Width) + x - 1) * 4 + 1])
                            + (-1 * temp[(((y - 1) * buffer.GetPlaneDescription(0).Width) + x + 1) * 4 + 1])
                            + (-1 * temp[(((y - 1) * buffer.GetPlaneDescription(0).Width) + x - 1) * 4 + 1]));
                        }
                    });*/
                return histogram;
            }
        }

        public void Copy(ImageEffect effect)
        {
            Exposure = effect.Exposure;
            RMul = effect.RMul;
            GMul = effect.GMul;
            BMul = effect.BMul;
            Contrast = effect.Contrast;
            Shadow = effect.Shadow;
            Hightlight = effect.Hightlight;
            Saturation = effect.Saturation;
            ReverseGamma = effect.ReverseGamma;
            HistoEqualisation = effect.HistoEqualisation;
            Rotation = effect.Rotation;
            ReverseGamma = effect.ReverseGamma;
            Gamma = effect.Gamma;
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

        internal HistoryObject GetHistory(ImageEffect effect)
        {
            //gettge first change
            HistoryObject history = new HistoryObject(EffectType.Unkown, effect.GetCopy());
            if (Exposure != effect.Exposure)
            {
                history.target = EffectType.Exposure;
                history.oldValue = Exposure;
                history.value = effect.Exposure;
            }
            else if (RMul != effect.RMul)
            {
                history.target = EffectType.Red;
                history.oldValue = RMul;
                history.value = effect.RMul;
            }
            else if (GMul != effect.GMul)
            {
                history.target = EffectType.Green;
                history.oldValue = GMul;
                history.value = effect.GMul;
            }
            else if (BMul != effect.BMul)
            {
                history.target = EffectType.Blue;
                history.oldValue = BMul;
                history.value = effect.BMul;
            }
            else if (Contrast != effect.Contrast)
            {
                history.target = EffectType.Contrast;
                history.oldValue = Contrast;
                history.value = effect.Contrast;
            }
            else if (Shadow != effect.Shadow)
            {
                history.target = EffectType.Shadow;
                history.oldValue = Shadow;
                history.value = effect.Shadow;
            }
            else if (Hightlight != effect.Hightlight)
            {
                history.target = EffectType.Hightlight;
                history.oldValue = Hightlight;
                history.value = effect.Hightlight;
            }
            else if (Saturation != effect.Saturation)
            {
                history.target = EffectType.Saturation;
                history.oldValue = Saturation;
                history.value = effect.Saturation;
            }
            else if (ReverseGamma != effect.ReverseGamma)
            {
                history.target = EffectType.ReverseGamma;
                history.oldValue = ReverseGamma;
                history.value = effect.ReverseGamma;
            }
            else if (HistoEqualisation != effect.HistoEqualisation)
            {
                history.target = EffectType.HistoEqualisation;
                history.oldValue = HistoEqualisation;
                history.value = effect.HistoEqualisation;
            }
            else if (Rotation != effect.Rotation)
            {
                history.target = EffectType.Rotate;
                history.oldValue = Rotation;
                history.value = effect.Rotation;
            }
            else if (Gamma != effect.Gamma) { }
            return history;
        }
    }
}
