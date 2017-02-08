using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RawNet
{
    public class RawImage
    {
        public ImageComponent preview = new ImageComponent(), thumb, raw = new ImageComponent();
        public ColorFilterArray colorFilter = new ColorFilterArray();
        public double[,] convertionM;
        public double[] camMul, curve;
        public int saturation, dark;
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public bool DitherScale { get; set; }          // Should upscaling be done with dither to mimize banding?

        public ImageMetadata metadata = new ImageMetadata();
        public uint cpp, pitch;
        public int whitePoint;
        public int[] blackLevelSeparate = new int[4];
        private int black;
        public int BlackLevel
        {
            set { black = value; }
            get
            {
                if (blackLevelSeparate[0] != 0) return blackLevelSeparate[0];
                else return black;
            }
        }
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
            cpp = 1;
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
                raw.red = new ushort[raw.dim.Width * raw.dim.Height];
                raw.green = new ushort[raw.dim.Width * raw.dim.Height];
                raw.blue = new ushort[raw.dim.Width * raw.dim.Height];
            }
            else
                raw.rawView = new ushort[raw.dim.Width * raw.dim.Height * cpp];
            raw.uncroppedDim = new Point2D(raw.dim.Width, raw.dim.Height);
        }

        public void SetTable(ushort[] table, int nfilled, bool dither)
        {
            TableLookUp t = new TableLookUp(1, dither);
            t.SetTable(0, table, nfilled);
            this.table = (t);
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
            {/*
                uint lookup = (uint)(table.tables[value * 2] | table.tables[value * 2 + 1] << 16);
                uint basevalue = lookup & 0xffff;
                uint delta = lookup >> 16;*/

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

        public void ScaleValues()
        {
            ushort maxValue = (ushort)((1 << raw.ColorDepth) - 1);
            Debug.Assert(whitePoint > 0);
            if (whitePoint == 0)
            {
                whitePoint = (1 << raw.ColorDepth) - 1;
            }
            try
            {
                if (BlackLevel == 0 && blackLevelSeparate[0] != 0)
                {
                    for (int i = 0; i < blackLevelSeparate.Length; i++)
                    {
                        BlackLevel += blackLevelSeparate[i];
                    }
                    BlackLevel /= blackLevelSeparate.Length;
                }
                if (BlackLevel != 0 || whitePoint != maxValue)
                {
                    double factor = (double)maxValue / (maxValue - BlackLevel);
                    Parallel.For(raw.offset.Height, raw.dim.Height + raw.offset.Height, y =>
                    {
                        long v = y * raw.uncroppedDim.Width * cpp;
                        for (uint x = raw.offset.Width; x < (raw.offset.Width + raw.dim.Width) * cpp; x++)
                        {
                            ulong value = (ulong)((raw.rawView[x + v] - BlackLevel) * factor);
                            if (value > maxValue) value = maxValue;
                            raw.rawView[x + v] = (ushort)value;
                        }
                    });
                }
                /*
                if (table != null)
                {
                    Parallel.For(raw.offset.Height, raw.dim.Height + raw.offset.Height, y =>
                    {
                        long v = y * raw.uncroppedDim.Width * cpp;
                        for (uint x = raw.offset.Width; x < (raw.offset.Width + raw.dim.Width) * cpp; x++)
                        {
                            raw.rawView[x + v] = table.tables[raw.rawView[x + v]];
                        }
                    });
                }*/
            }
            catch (Exception ex)
            {
                errors.Add("Linearisation went wrong:" + ex.Message);
            }
        }

        /*
        internal void ConvertRGB()
        {
            double[,] xyzToRGB = { { 0.412453, 0.357580, 0.180423 }, { 0.212671, 0.715160, 0.072169 }, { 0.019334, 0.119193, 0.950227 } };
            int maxValue = (1 << ColorDepth) - 1;
            Parallel.For(0, raw.dim.Height, y =>
            {
                long realY = (y + raw.offset.Height) * raw.uncroppedDim.Width * 3;
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
        }*/

        /*
        public void ScaleBlackWhite()
        {
            const int skipBorder = 250;
            int gw = (int)((raw.dim.Width - skipBorder + raw.offset.Width) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && BlackLevel < 0) || whitePoint >= 65536)
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
                if (BlackLevel < 0)
                    BlackLevel = b;
                if (whitePoint >= 65536)
                    whitePoint = m;
                //ConsoleContent.Value +=("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + " Estimated white: " + whitePoint);
            }

            // Skip, if not needed 
            if ((blackAreas.Count == 0 && BlackLevel == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || raw.dim.Area() <= 0)
                return;

            // If filter has not set separate blacklevel, compute or fetch it 
            if (blackLevelSeparate[0] < 0)
                CalculateBlackAreas();

            ScaleValues();
        }*/

        /*
        void CalculateBlackAreas()
        {
            int[] histogram = new int[4 * 65536 * sizeof(int)];
            //memset(histogram, 0, 4 * 65536 * sizeof(int));
            uint totalpixels = 0;

            for (int i = 0; i < blackAreas.Count; i++)
            {
                BlackArea area = blackAreas[i];

                // Make sure area sizes are multiple of two, 
                 //  so we have the same amount of pixels for each CFA group 
                area.Size = area.Size - (area.Size & 1);

                // Process horizontal area 
                if (!area.IsVertical)
                {
                    if (area.Offset + area.Size > raw.uncroppedDim.Height)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than height of image");
                    for (uint y = area.Offset; y < area.Offset + area.Size; y++)
                    {
                        ushort[] pixel = preview.data.Skip((int)(raw.offset.Width + raw.dim.Width * y)).ToArray();
                        int[] localhist = histogram.Skip((int)(y & 1) * (65536 * 2)).ToArray();
                        for (uint x = raw.offset.Width; x < raw.dim.Width + raw.offset.Width; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * raw.dim.Width;
                }

                // Process vertical area 
                if (area.IsVertical)
                {
                    if (area.Offset + area.Size > raw.uncroppedDim.Width)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than width of image");
                    for (uint y = raw.offset.Height; y < raw.dim.Height + raw.offset.Height; y++)
                    {
                        ushort[] pixel = preview.data.Skip((int)(area.Offset + raw.dim.Width * y)).ToArray();
                        int[] localhist = histogram.Skip((int)(y & 1) * (65536 * 2)).ToArray();
                        for (uint x = area.Offset; x < area.Size + area.Offset; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * raw.dim.Height;
                }
            }

            if (totalpixels == 0)
            {
                for (int i = 0; i < 4; i++)
                    blackLevelSeparate[i] = BlackLevel;
                return;
            }

            //Calculate median value of black areas for each component 
            // Adjust the number of total pixels so it is the same as the median of each histogram 
            totalpixels /= 4 * 2;

            for (int i = 0; i < 4; i++)
            {
                int[] localhist = histogram.Skip(i * 65536).ToArray();
                int acc_pixels = localhist[0];
                int pixel_value = 0;
                while (acc_pixels <= totalpixels && pixel_value < 65535)
                {
                    pixel_value++;
                    acc_pixels += localhist[pixel_value];
                }
                blackLevelSeparate[i] = pixel_value;
            }

            //If this is not a CFA image, we do not use separate blacklevels, use average 
            if (!isCFA)
            {
                int total = 0;
                for (int i = 0; i < 4; i++)
                    total += blackLevelSeparate[i];
                for (int i = 0; i < 4; i++)
                    blackLevelSeparate[i] = (total + 2) >> 2;
            }
        }
    */

        /**
         * Create a preview of the raw image using the scaling factor
         * The X and Y dimension will be both divided by the image
         * 
         */
        public void CreatePreview(FactorValue factor, double viewHeight, double viewWidth)
        {
            //image will be size of windows
            uint previewFactor = 0;
            if (factor == FactorValue.Auto)
            {
                if (raw.dim.Height > raw.dim.Width)
                {
                    previewFactor = (uint)((raw.dim.Height / viewHeight) * 0.9);
                }
                else
                {
                    previewFactor = (uint)((raw.dim.Width / viewWidth) * 0.9);
                }
                if (previewFactor < 1)
                {
                    previewFactor = 1;
                }
            }
            else
            {
                previewFactor = (uint)factor;
            }

            preview.dim = new Point2D(raw.dim.Width / previewFactor, raw.dim.Height / previewFactor);
            preview.ColorDepth = raw.ColorDepth;
            preview.uncroppedDim = new Point2D(preview.dim.Width, preview.dim.Height);
            preview.green = new ushort[preview.dim.Height * preview.dim.Width];
            preview.red = new ushort[preview.dim.Height * preview.dim.Width];
            preview.blue = new ushort[preview.dim.Height * preview.dim.Width];

            uint doubleFactor = previewFactor * previewFactor;
            ushort maxValue = (ushort)((1 << raw.ColorDepth) - 1);
            //loop over each block
            Parallel.For(0, preview.dim.Height, y =>
             {
                 for (int x = 0; x < preview.dim.Width; x++)
                 {
                     //find the mean of each block
                     long r = 0, g = 0, b = 0;
                     int xk = 0, yk = 0;
                     for (int i = 0; i < previewFactor; i++)
                     {
                         long realY = raw.dim.Width * ((y * previewFactor) + i);
                         yk++;
                         for (int k = 0; k < previewFactor; k++)
                         {
                             xk++;
                             UInt64 realX = (UInt64)(realY + (x * previewFactor + k));
                             r += raw.red[realX];
                             g += raw.green[realX];
                             b += raw.blue[realX];
                         }
                     }
                     r = (ushort)(r / doubleFactor);
                     g = (ushort)(g / doubleFactor);
                     b = (ushort)(b / doubleFactor);
                     if (r < 0) r = 0; else if (r > maxValue) r = maxValue;
                     if (g < 0) g = 0; else if (g > maxValue) g = maxValue;
                     if (b < 0) b = 0; else if (b > maxValue) b = maxValue;
                     preview.red[(y * preview.dim.Width) + x] = (ushort)r;
                     preview.green[(y * preview.dim.Width) + x] = (ushort)g;
                     preview.blue[(y * preview.dim.Width) + x] = (ushort)b;
                 }
             });
        }

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
                new ExifValue("Black level", "" + BlackLevel, ExifGroup.Image),
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