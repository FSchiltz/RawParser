using PhotoNet.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RawNet
{
    public class RawImage : Image<ushort>
    {
        public double[,] convertionM;
        public double[] camMul, curve;
        public int saturation, dark;
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public bool DitherScale { get; set; }          // Should upscaling be done with dither to mimize banding?

        internal TableLookUp table;
        public int Bpp { get { return (int)Math.Ceiling(fullSize.ColorDepth / 8.0); } }
        public bool IsGammaCorrected { get; set; } = true;

        public uint pitch;
        public long whitePoint;
        public long black;

        public void Init(bool RGB)
        {
            if (fullSize.dim.width > 65535 || fullSize.dim.height > 65535)
                throw new RawDecoderException("Dimensions too large for allocation.");
            if (fullSize.dim.width <= 0 || fullSize.dim.height <= 0)
                throw new RawDecoderException("Dimension of one sides is less than 1 - cannot allocate image.");
            pitch = (uint)(((fullSize.dim.width * Bpp) + 15) / 16) * 16;
            if (RGB)
            {
                fullSize.red = new ushort[fullSize.dim.width * fullSize.dim.height];
                fullSize.green = new ushort[fullSize.dim.width * fullSize.dim.height];
                fullSize.blue = new ushort[fullSize.dim.width * fullSize.dim.height];
            }
            else
                fullSize.rawView = new ushort[fullSize.dim.width * fullSize.dim.height * fullSize.cpp];
            fullSize.UncroppedDim = new Point2D(fullSize.dim.width, fullSize.dim.height);
        }

        public new void Crop(Rectangle2D crop)
        {
            base.Crop(crop);

            if ((crop.Position.width & 1) != 0)
                colorFilter.ShiftLeft(0);
            if ((crop.Position.height & 1) != 0)
                colorFilter.ShiftDown(0);
        }

        public new List<ExifValue> ParseExif()
        {
            var exif = base.ParseExif();
            exif.Add(new ExifValue("Black level", "" + black, ExifGroup.Image));
            exif.Add(new ExifValue("White level", "" + whitePoint, ExifGroup.Image));
            if (isCFA)
            {
                exif.Add(new ExifValue("CFA pattern", colorFilter.ToString(), ExifGroup.Camera));
            }
            return exif;
        }

        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        public void SetWithLookUp(UInt16 value, ushort[] dst, long offset, ref uint random)
        {
            if (table == null)
            {
                dst[offset] = value;
                return;
            }
            if (table.Dither)
            {
                uint basevalue = table.tables[value * 2];
                uint delta = table.tables[value * 2 + 1];
                uint r = random;

                uint pix = basevalue + ((delta * (r & 2047) + 1024) >> 12);
                random = 15700 * (r & 65535) + (r >> 16);
                dst[offset] = (ushort)pix;
                return;
            }
            dst[offset] = table.tables[value];
        }

        public void ApplyTableLookUp()
        {
            if (table?.tables != null && table.ntables > 0)
            {
                Debug.Assert(fullSize?.rawView != null);
                Parallel.For(fullSize.offset.height, fullSize.dim.height + fullSize.offset.height, y =>
                {
                    long pos = y * fullSize.UncroppedDim.width * fullSize.cpp;
                    for (uint x = fullSize.offset.width; x < (fullSize.offset.width + fullSize.dim.width) * fullSize.cpp; x++)
                    {
                        fullSize.rawView[x + pos] = table.tables[Convert.ToInt32(fullSize.rawView[x + pos])];
                    }
                });
            }
            table = null;
        }

        public void ConvertRGB()
        {
            Debug.Assert(convertionM?.Length == 9);
            //the matrice is cxyz to cam
            //interpolate the cam to rgb
            /*
            for (int k = 0; k < 3; k++)
                for (int l = 0; l < 3; l++)
                {
                    convertionM[k, l] /= 1000;
                }
            double[,] xyzToRGB = { { 0.412453, 0.357580, 0.180423 }, { 0.212671, 0.715160, 0.072169 }, { 0.019334, 0.119193, 0.950227 } };
            int maxValue = (1 << raw.ColorDepth) - 1;
            Parallel.For(0, raw.dim.Height, y =>
            {
                long realY = (y + raw.offset.Height) * raw.UncroppedDim.Width * 3;
                for (int x = 0; x < raw.dim.Width; x++)
                {
                    long realX = y + 3 * (x + raw.offset.Width);
                    
                    double[] rgb = { raw.data[realX] / maxValue, raw.data[realX + 1] / maxValue, raw.data[realY + 2] / maxValue };
                    //convert to XYZ
                    double[] result = Mult3by1(convertionM, rgb);
                    //convert back to rgb
                    double[] rgbConv = Mult3by1(xyzToRGB, result);
                    raw.data[realX] = (ushort)(rgb[0] * maxValue);
                    raw.data[realX + 1] = (ushort)(rgb[1] * maxValue);
                    raw.data[realY + 2] = (ushort)(rgb[2] * maxValue);
                }
            });*/
        }

        public double[] Mult3by1(double[,] m1, double[] m2)
        {
            double[] resultMatrix = new double[3];
            for (int i = 0; i < 3; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    resultMatrix[i] += m1[i, k] * m2[k];
                }
            }
            return resultMatrix;
        }

        /*
        public void ScaleBlackWhite()
        {
            const int skipBorder = 250;
            int gw = (int)((raw.dim.Width - skipBorder + raw.offset.Width) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && black < 0) || whitePoint >= 65536)
            {  // Estimate
                int b = 65536;
                int m = 0;
                for (int row = skipBorder; row < (raw.dim.Height - skipBorder + raw.offset.Height); row++)
                {
                    ushort[] pixel = raw.green.Skip((int)(skipBorder + row * raw.dim.Width)).ToArray();
                    int pix = 0;
                    for (int col = skipBorder; col < gw; col++)
                    {
                        b = Math.Min(pixel[pix], b);
                        m = Math.Max(pixel[pix], m);
                        pix++;
                    }
                }
                if (black < 0)
                    black = b;
                if (whitePoint >= 65536)
                    whitePoint = m;
                //ConsoleContent.Value +=("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + " Estimated white: " + whitePoint);
            }

            // Skip, if not needed 
            if ((blackAreas.Count == 0 && black == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || raw.dim.Area <= 0)
                return;

            // If filter has not set separate blacklevel, compute or fetch it 
            if (blackLevelSeparate[0] < 0)
                CalculateBlackAreas();

            ScaleValues();
        }*/
    }
}
