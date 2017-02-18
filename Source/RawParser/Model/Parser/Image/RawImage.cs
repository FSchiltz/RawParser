using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

namespace RawNet
{
    public class RawImage<T>
    {
        public ImageComponent<ushort> preview = new ImageComponent<ushort>();
        public ImageComponent<byte> thumb;
        public ImageComponent<T> raw = new ImageComponent<T>();
        public ColorFilterArray colorFilter = new ColorFilterArray();
        public double[,] convertionM;
        public double[] camMul, curve;
        public int saturation, dark;
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public bool DitherScale { get; set; }          // Should upscaling be done with dither to mimize banding?

        public ImageMetadata metadata = new ImageMetadata();
        public uint pitch;
        public long whitePoint;
        public long black;

        public List<String> errors = new List<string>();
        public bool isCFA = true;
        public bool isFujiTrans = false;
        internal TableLookUp table;
        public ColorFilterArray UncroppedColorFilter;
        public int Bpp { get { return (int)Math.Ceiling(raw.ColorDepth / 8.0); } }
        public bool IsGammaCorrected { get; set; } = true;

        public RawImage(uint width, uint height) : this()
        {
            raw.dim = new Point2D(width, height);
        }

        public RawImage()
        {
            //Set for 16bit image non demos           
            raw.cpp = 1;
            raw.ColorDepth = 16;
        }

        internal void Init(bool RGB)
        {
            if (raw.dim.Width > 65535 || raw.dim.Height > 65535)
                throw new RawDecoderException("Dimensions too large for allocation.");
            if (raw.dim.Width <= 0 || raw.dim.Height <= 0)
                throw new RawDecoderException("Dimension of one sides is less than 1 - cannot allocate image.");
            pitch = (uint)(((raw.dim.Width * Bpp) + 15) / 16) * 16;
            if (RGB)
            {
                raw.red = new T[raw.dim.Width * raw.dim.Height];
                raw.green = new T[raw.dim.Width * raw.dim.Height];
                raw.blue = new T[raw.dim.Width * raw.dim.Height];
            }
            else
                raw.rawView = new T[raw.dim.Width * raw.dim.Height * raw.cpp];
            raw.UncroppedDim = new Point2D(raw.dim.Width, raw.dim.Height);
        }

        public void Crop(Rectangle2D crop)
        {
            if (!crop.Dimension.IsThisInside(raw.dim - crop.Position))
            {
                return;
            }
            if (crop.Position.Width < 0 || crop.Position.Height < 0 || !crop.HasPositiveArea())
            {
                return;
            }

            raw.offset += crop.Position;

            raw.dim = crop.Dimension;

            if ((crop.Position.Width & 1) != 0)
                colorFilter.ShiftLeft(0);
            if ((crop.Position.Height & 1) != 0)
                colorFilter.ShiftDown(0);
        }

        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        internal void SetWithLookUp(UInt16 value, ushort[] dst, long offset, ref uint random)
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

        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        internal unsafe void SetWithLookUp(ushort value, ushort* dest, ref uint random)
        {
            if (table == null)
            {
                *dest = value;
                return;
            }
            if (table.Dither)
            {
                int basevalue = table.tables[value * 2];
                uint delta = table.tables[value * 2 + 1];

                uint r = random;
                uint pix = (uint)basevalue + ((delta * (r & 2047) + 1024) >> 12);
                random = 15700 * (r & 65535) + (r >> 16);
                *dest = (ushort)pix;
                return;
            }
            *dest = table.tables[value];
        }

        internal void ConvertRGB()
        {
            Debug.Assert(convertionM?.Length == 9);
            //the matrice is cxyz to cam
            //interpolate the cam to rgb
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
                    /*
                    double[] rgb = { raw.data[realX] / maxValue, raw.data[realX + 1] / maxValue, raw.data[realY + 2] / maxValue };
                    //convert to XYZ
                    double[] result = Mult3by1(convertionM, rgb);
                    //convert back to rgb
                    double[] rgbConv = Mult3by1(xyzToRGB, result);
                    raw.data[realX] = (ushort)(rgb[0] * maxValue);
                    raw.data[realX + 1] = (ushort)(rgb[1] * maxValue);
                    raw.data[realY + 2] = (ushort)(rgb[2] * maxValue);*/
                }
            });
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
     
        public List<ExifValue> ParseExif()
        {
            List<ExifValue> exif = new List<ExifValue>
            {
                new ExifValue("File", metadata.FileNameComplete, ExifGroup.Parser),
                new ExifValue("Parsing time", metadata.ParsingTimeAsString, ExifGroup.Parser),
                new ExifValue("Size", "" + ((raw.dim.Width * raw.dim.Height) / 1000000.0).ToString("F") + " MPixels", ExifGroup.Camera),
                new ExifValue("Dimension", "" + raw.dim.Width + " x " + raw.dim.Height, ExifGroup.Camera),
                new ExifValue("Sensor size", "" + ((metadata.RawDim.Width * metadata.RawDim.Height) / 1000000.0).ToString("F") + " MPixels", ExifGroup.Camera),
                new ExifValue("Sensor dimension", "" + metadata.RawDim.Width + " x " + metadata.RawDim.Height, ExifGroup.Camera),
                new ExifValue("Black level", "" + black, ExifGroup.Image),
                new ExifValue("White level", "" + whitePoint, ExifGroup.Image),
                new ExifValue("Color depth", "" + raw.ColorDepth + " bits", ExifGroup.Image),
                new ExifValue("Color space", "" + metadata.ColorSpace.ToString(), ExifGroup.Shot)
            };
            //Camera
            if (!string.IsNullOrEmpty(metadata.Make))
                exif.Add(new ExifValue("Maker", metadata.Make, ExifGroup.Camera));
            if (!string.IsNullOrEmpty(metadata.Model))
                exif.Add(new ExifValue("Model", metadata.Model, ExifGroup.Camera));

            if (isCFA)
            {
                exif.Add(new ExifValue("CFA pattern", colorFilter.ToString(), ExifGroup.Camera));
            }

            //Image
            if (!string.IsNullOrEmpty(metadata.Mode))
                exif.Add(new ExifValue("Image mode", metadata.Mode, ExifGroup.Image));

            //Shot settings
            if (metadata.IsoSpeed > 0)
                exif.Add(new ExifValue("ISO", "" + (int)metadata.IsoSpeed, ExifGroup.Shot));
            if (metadata.Exposure > 0)
                exif.Add(new ExifValue("Exposure time", "" + metadata.ExposureAsString, ExifGroup.Shot));
            //Lens
            if (!string.IsNullOrEmpty(metadata.Lens))
                exif.Add(new ExifValue("Lense", metadata.Lens, ExifGroup.Lens));
            if (metadata.Focal > 0)
                exif.Add(new ExifValue("Focal", "" + (int)metadata.Focal + " mm", ExifGroup.Lens));
            if (metadata.Aperture > 0)
                exif.Add(new ExifValue("Aperture", "" + metadata.Aperture.ToString("F"), ExifGroup.Lens));

            //Various
            if (!string.IsNullOrEmpty(metadata.TimeTake))
                exif.Add(new ExifValue("Time of capture", "" + metadata.TimeTake, ExifGroup.Various));
            if (!string.IsNullOrEmpty(metadata.TimeModify))
                exif.Add(new ExifValue("Time modified", "" + metadata.TimeModify, ExifGroup.Various));
            if (!string.IsNullOrEmpty(metadata.Comment))
                exif.Add(new ExifValue("Comment", "" + metadata.Comment, ExifGroup.Various));

            //GPS
            if (metadata.Gps != null)
            {
                exif.Add(new ExifValue("Longitude", metadata.Gps.LongitudeAsString, ExifGroup.GPS));
                exif.Add(new ExifValue("lattitude", metadata.Gps.LattitudeAsString, ExifGroup.GPS));
                exif.Add(new ExifValue("altitude", metadata.Gps.AltitudeAsString, ExifGroup.GPS));
            }

            return exif;
        }
    }
}