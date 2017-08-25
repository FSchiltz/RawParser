using PhotoNet.Common;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using System.ComponentModel;
using System.Diagnostics;

namespace PhotoNet
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
        private bool histogramEqual = false;
        public bool HistogramEqualisation
        {
            get { return histogramEqual; }
            set
            {
                if (histogramEqual != value)
                {
                    histogramEqual = value;
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

        public Pixel SplitShadow { get; set; }
        public Pixel SplitHighlight { get; set; }

        private double splitBalance = 0.5;
        public double SplitBalance
        {
            get { return splitBalance; }
            set
            {
                if (splitBalance != value)
                {
                    splitBalance = value;
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
                if (rMul != value && !Double.IsNaN(value) && !Double.IsInfinity(value))
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
                if (gMul != value && !Double.IsNaN(value) && !Double.IsInfinity(value))
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
                if (bMul != value && !Double.IsNaN(value) && !Double.IsInfinity(value))
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

        public ImageEffect GetCopy()
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

            double[] curve = Curve.CubicSpline(xCurve, yCurve);
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
                }
                for (int i = 0; i < curve.Length; i++)
                {
                    if (curve[i] > 1) curve[i] = 1;
                }
            }
            else
            {
                for (int i = 0; i < curve.Length; i++)
                {
                    if (curve[i] > maxValue) curve[i] = 1;
                    else curve[i] /= maxValue;
                }
            }
            return curve;
        }

        public unsafe HistoRaw ApplyTo16Bits(ImageComponent<ushort> image, SoftwareBitmap bitmap, bool histogram)
        {
            Debug.Assert(bitmap.BitmapPixelFormat == BitmapPixelFormat.Rgba16);

            var buffer = Apply(image);
            //Clip the image
            //Luminance.Clip(buffer, 16);

            HistoRaw histo = null;
            //calculate the new histogram (create a 8 bits histogram)
            if (histogram)
                histo = HistogramHelper.CalculateHistogram(buffer);

            //copy the buffer to the image with clipping
            //calculte the shift between colordepth input and output
            int shift = image.ColorDepth - 16;

            using (BitmapBuffer buff = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buff.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                Parallel.For(0, buffer.dim.height, y =>
                {
                    long realY = y * buffer.dim.width;
                    for (int x = 0; x < buffer.dim.width; x++)
                    {
                        long realPix = realY + x;
                        long bufferPix = realPix * 8;
                        temp[bufferPix] = (byte)(buffer.red[realPix] >> 8);
                        temp[bufferPix + 1] = (byte)(buffer.red[realPix]);

                        temp[bufferPix + 2] = (byte)(buffer.green[realPix] >> 8);
                        temp[bufferPix + 3] = (byte)(buffer.green[realPix]);

                        temp[bufferPix + 4] = (byte)(buffer.blue[realPix] >> 8);
                        temp[bufferPix + 5] = (byte)(buffer.blue[realPix]);

                        temp[bufferPix + 6] = 255; //set transparency to 255 else image will be blank
                        temp[bufferPix + 7] = 255; //set transparency to 255 else image will be blank
                    }
                });
            }
            return histo;
        }

        public unsafe HistoRaw ApplyTo8Bits(ImageComponent<ushort> image, SoftwareBitmap bitmap, bool histogram)
        {
            Debug.Assert(image.red != null);
            Debug.Assert(image.blue != null);
            Debug.Assert(image.green != null);
            Debug.Assert(image.dim.Area >= 4);
            Debug.Assert(bitmap != null);
            Debug.Assert(image.dim.Area == bitmap.PixelHeight * bitmap.PixelWidth);

            var buffer = Apply(image);
            //Clip the image
            Luminance.Clip(buffer, 8);

            //calculate the new histogram (create a 8 bits histogram)
            HistoRaw histo = null;
            if (histogram)
                histo = HistogramHelper.CalculateHistogram(buffer);

            //copy the buffer to the image with clipping
            using (BitmapBuffer buff = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buff.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var temp, out uint capacity);
                Parallel.For(0, buffer.dim.height, y =>
                {
                    long realY = y * buffer.dim.width;
                    for (int x = 0; x < buffer.dim.width; x++)
                    {
                        long realPix = realY + x;
                        long bufferPix = realPix * 4;
                        temp[bufferPix] = (byte)(buffer.blue[realPix]);
                        temp[bufferPix + 1] = (byte)(buffer.green[realPix]);
                        temp[bufferPix + 2] = (byte)(buffer.red[realPix]);
                        temp[bufferPix + 3] = 255; //set transparency to 255 else image will be blank
                    }
                });
            }
            return histo;
        }

        protected ImageComponent<int> Apply(ImageComponent<ushort> image)
        {
            Debug.Assert(image.red != null);
            Debug.Assert(image.blue != null);
            Debug.Assert(image.green != null);
            Debug.Assert(image.dim.Area >= 4);

            //calculate the max value for clip
            maxValue = (uint)(1 << image.ColorDepth) - 1;
            HistoRaw histo;

            //TODO cut the image in patch to reduce memory 

            var buffer = new ImageComponent<int>(image.dim, image.ColorDepth);

            //apply the single pixel processing 
            SinglePixelProcessing(image, buffer, CreateCurve());

            ColorManipulation.SplitTone(buffer, new Pixel(SplitShadow), new Pixel(SplitHighlight), SplitBalance, maxValue);

            if (Rotation == 1 || Rotation == 3)
            {
                buffer.dim.Flip();
                buffer.UncroppedDim.Flip();
            }

            //clip
            Luminance.Clip(buffer);

            //apply histogram equalisation if any
            if (histogramEqual)
            {
                //calculate the histogram
                histo = HistogramHelper.CalculateLumaHistogram(buffer);
                HistogramHelper.HistogramEqualisation(buffer, histo);
            }

            //apply denoising 
            if (denoise != 0)
                buffer = Denoising.Apply(buffer, (int)denoise);

            //apply sharpening (always last step)
            if (sharpness != 0)
                buffer = Sharpening.Apply(buffer, (int)sharpness);

            //return the final histogram
            return buffer;
        }

        protected void SinglePixelProcessing(ImageComponent<ushort> image, ImageComponent<int> buffer, double[] curve)
        {
            Debug.Assert(image.red != null);
            Debug.Assert(image.blue != null);
            Debug.Assert(image.green != null);
            Debug.Assert(image.dim.Area >= 4);

            Debug.Assert(buffer.red != null);
            Debug.Assert(buffer.blue != null);
            Debug.Assert(buffer.green != null);
            Debug.Assert(buffer.dim.Area == image.dim.Area);

            Parallel.For(0, image.dim.height, y =>
            {
                long realY = (y + image.offset.height) * image.UncroppedDim.width;
                for (int x = 0; x < image.dim.width; x++)
                {
                    long realPix = realY + x + image.offset.width;
                    double red = image.red[realPix] * rMul, green = image.green[realPix] * gMul, blue = image.blue[realPix] * bMul;
                    Luminance.Clip(ref red, ref green, ref blue, maxValue);
                    ColorManipulation.RgbToHsl(red, green, blue, maxValue, out double h, out double s, out double l);
                    l = curve[(int)(l * maxValue)];
                    s *= saturation;

                    ColorManipulation.HslToRgb(h, s, l, maxValue, out red, out green, out blue);

                    long bufferPix = Rotate(x, y, image.dim.width, image.dim.height);
                    buffer.red[bufferPix] = (int)red;
                    buffer.green[bufferPix] = (int)green;
                    buffer.blue[bufferPix] = (int)blue;
                }
            });
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
            HistogramEqualisation = effect.HistogramEqualisation;
            Rotation = effect.Rotation;
            ReverseGamma = effect.ReverseGamma;
            Gamma = effect.Gamma;
            Denoise = effect.Denoise;
            Sharpness = effect.Sharpness;
            SplitShadow = effect.SplitShadow;
            SplitHighlight = effect.SplitHighlight;
            SplitBalance = effect.SplitBalance;
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


        public HistoryObject GetHistory(ImageEffect effect)
        {
            //get the first change
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
            else if (HistogramEqualisation != effect.HistogramEqualisation)
            {
                history.target = EffectType.HistoEqualisation;
                history.oldValue = HistogramEqualisation;
                history.value = effect.HistogramEqualisation;
            }
            else if (Rotation != effect.Rotation)
            {
                history.target = EffectType.Rotate;
                history.oldValue = Rotation;
                history.value = effect.Rotation;
            }
            else if (Gamma != effect.Gamma)
            {
                history.target = EffectType.Gamma;
                history.oldValue = Gamma;
                history.value = effect.Gamma;
            }
            else if (Sharpness != effect.Sharpness)
            {
                history.target = EffectType.Sharpness;
                history.oldValue = Sharpness;
                history.value = effect.Sharpness;
            }
            else if (Denoise != effect.Denoise)
            {
                history.target = EffectType.Denoise;
                history.oldValue = Denoise;
                history.value = effect.Denoise;
            }
            return history;
        }
    }
}
