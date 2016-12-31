using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RawNet
{
    public class Image
    {
        public ushort[] data;
        public Point2D dim, offset = new Point2D(), uncroppedDim;

    }
    public class RawImage
    {
        public Image preview = new Image(), thumb, raw = new Image();
        public ColorFilterArray cfa = new ColorFilterArray();
        public double[] camMul, black, curve;
        public int rotation = 0, blackLevel, saturation, dark;
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public bool DitherScale { get; set; }          // Should upscaling be done with dither to mimize banding?
        public ushort ColorDepth { get; set; }

        public ImageMetadata metadata = new ImageMetadata();
        public uint cpp, pitch;
        public int whitePoint;
        public int[] blackLevelSeparate = new int[4];
        public List<String> errors = new List<string>();
        public bool isCFA = true;
        internal TableLookUp table;
        public ColorFilterArray UncroppedCfa;
        public int bpp { get { return (int)Math.Ceiling(ColorDepth / 8.0); } }

        public RawImage()
        {
            //Set for 16bit image non demos           
            cpp = 1;
            ColorDepth = 16;
        }

        internal void Init()
        {
            if (raw.dim.width > 65535 || raw.dim.height > 65535)
                throw new RawDecoderException("RawImageData: Dimensions too large for allocation.");
            if (raw.dim.width <= 0 || raw.dim.height <= 0)
                throw new RawDecoderException("RawImageData: Dimension of one sides is less than 1 - cannot allocate image.");
            pitch = (uint)(((raw.dim.width * bpp) + 15) / 16) * 16;
            raw.data = new ushort[raw.dim.width * raw.dim.height * cpp];
            if (raw.data == null)
                throw new RawDecoderException("RawImageData::createData: Memory Allocation failed.");
            raw.uncroppedDim = new Point2D(raw.dim.width, raw.dim.height);
        }

        /*
         * Should not be used if possible
         * not efficient but allows more concise code
         * for demos
         */
        public ushort this[int row, int col]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var a = (row * raw.uncroppedDim.width) + col;
                if (row < 0 || row >= raw.dim.height || col < 0 || col >= raw.dim.width)
                {
                    return 0;
                }
                else return raw.data[a];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                raw.data[(row * raw.dim.width) + col] = value;
            }
        }

        public void SetTable(ushort[] table, int nfilled, bool dither)
        {
            TableLookUp t = new TableLookUp(1, dither);
            t.SetTable(0, table, nfilled);
            this.table = (t);
        }

        public void Crop(Rectangle2D crop)
        {
            if (!crop.Dim.IsThisInside(raw.dim - crop.Pos))
            {
                Debug.WriteLine("WARNING: RawImageData::subFrame - Attempted to create new subframe larger than original size. Crop skipped.");
                return;
            }
            if (crop.Pos.width < 0 || crop.Pos.height < 0 || !crop.HasPositiveArea())
            {
                Debug.WriteLine("WARNING: RawImageData::subFrame - Negative crop raw.offset. Crop skipped.");
                return;
            }

            raw.offset += crop.Pos;

            raw.dim = crop.Dim;

            if ((crop.Pos.width & 1) != 0)
                cfa.ShiftLeft(0);
            if ((crop.Pos.height & 1) != 0)
                cfa.ShiftDown(0);
        }

        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        internal void SetWithLookUp(UInt16 value, ushort[] dst, int offset, ref uint random)
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
            if (whitePoint == 0)
            {
                whitePoint = (1 << ColorDepth) - 1;
                Debug.WriteLine("Whitepoint incorrect");
            }

            if (blackLevel == 0 && blackLevelSeparate[0] != 0)
            {
                for (int i = 0; i < blackLevelSeparate.Length; i++)
                {
                    blackLevel += blackLevelSeparate[i];
                }
                blackLevel /= blackLevelSeparate.Length;
            }
            if (blackLevel != 0 || whitePoint != ((1 << ColorDepth) - 1))
            {
                double factor = ((1 << ColorDepth) - 1.0) / (((1 << ColorDepth) - 1.0) - blackLevel);
                Parallel.For(raw.offset.height, raw.dim.height + raw.offset.height, y =>
                {
                    long v = y * raw.uncroppedDim.width * cpp;
                    for (int x = raw.offset.width; x < (raw.offset.width + raw.dim.width) * cpp; x++)
                    {
                        raw.data[x + v] = (ushort)((raw.data[x + v] - blackLevel) * factor);
                    }
                });
            }
        }

        public void ScaleBlackWhite()
        {
            const int skipBorder = 250;
            int gw = (int)((raw.dim.width - skipBorder + raw.offset.width) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && blackLevel < 0) || whitePoint >= 65536)
            {  // Estimate
                int b = 65536;
                int m = 0;
                for (int row = skipBorder; row < (raw.dim.height - skipBorder + raw.offset.height); row++)
                {
                    ushort[] pixel = raw.data.Skip(skipBorder + row * raw.dim.width).ToArray();
                    int pix = 0;
                    for (int col = skipBorder; col < gw; col++)
                    {
                        b = Math.Min(pixel[pix], b);
                        m = Math.Max(pixel[pix], m);
                        pix++;
                    }
                }
                if (blackLevel < 0)
                    blackLevel = b;
                if (whitePoint >= 65536)
                    whitePoint = m;
                //Debug.WriteLine("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + " Estimated white: " + whitePoint);
            }

            /* Skip, if not needed */
            if ((blackAreas.Count == 0 && blackLevel == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || raw.dim.Area() <= 0)
                return;

            /* If filter has not set separate blacklevel, compute or fetch it */
            if (blackLevelSeparate[0] < 0)
                CalculateBlackAreas();

            ScaleValues();
        }

        void CalculateBlackAreas()
        {
            int[] histogram = new int[4 * 65536 * sizeof(int)];
            //memset(histogram, 0, 4 * 65536 * sizeof(int));
            int totalpixels = 0;

            for (int i = 0; i < blackAreas.Count; i++)
            {
                BlackArea area = blackAreas[i];

                /* Make sure area sizes are multiple of two, 
                   so we have the same amount of pixels for each CFA group */
                area.Size = area.Size - (area.Size & 1);

                /* Process horizontal area */
                if (!area.IsVertical)
                {
                    if (area.Offset + area.Size > raw.uncroppedDim.height)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than height of image");
                    for (int y = area.Offset; y < area.Offset + area.Size; y++)
                    {
                        ushort[] pixel = preview.data.Skip(raw.offset.width + raw.dim.width * y).ToArray();
                        int[] localhist = histogram.Skip((y & 1) * (65536 * 2)).ToArray();
                        for (int x = raw.offset.width; x < raw.dim.width + raw.offset.width; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * raw.dim.width;
                }

                /* Process vertical area */
                if (area.IsVertical)
                {
                    if (area.Offset + area.Size > raw.uncroppedDim.width)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than width of image");
                    for (int y = raw.offset.height; y < raw.dim.height + raw.offset.height; y++)
                    {
                        ushort[] pixel = preview.data.Skip(area.Offset + raw.dim.width * y).ToArray();
                        int[] localhist = histogram.Skip((y & 1) * (65536 * 2)).ToArray();
                        for (int x = area.Offset; x < area.Size + area.Offset; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * raw.dim.height;
                }
            }

            if (totalpixels == 0)
            {
                for (int i = 0; i < 4; i++)
                    blackLevelSeparate[i] = blackLevel;
                return;
            }

            /* Calculate median value of black areas for each component */
            /* Adjust the number of total pixels so it is the same as the median of each histogram */
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

            /* If this is not a CFA image, we do not use separate blacklevels, use average */
            if (!isCFA)
            {
                int total = 0;
                for (int i = 0; i < 4; i++)
                    total += blackLevelSeparate[i];
                for (int i = 0; i < 4; i++)
                    blackLevelSeparate[i] = (total + 2) >> 2;
            }
        }

        /**
         * Create a preview of the raw image using the scaling factor
         * The X and Y dimension will be both divided by the image
         * 
         */
        public void CreatePreview(int previewFactor)
        {
            preview.dim = new Point2D(raw.dim.width / previewFactor, raw.dim.height / previewFactor);
            preview.uncroppedDim = new Point2D(raw.dim.width / previewFactor, raw.dim.height / previewFactor);
            Debug.WriteLine("Preview of size w:" + preview.dim.width + "y:" + preview.dim.height);
            preview.data = new ushort[preview.dim.height * preview.dim.width * cpp];
            int doubleFactor = previewFactor * previewFactor;
            ushort maxValue = (ushort)((1 << ColorDepth) - 1);
            //loop over each block
            Parallel.For(0, preview.dim.height, y =>
             {
                 for (int x = 0; x < preview.dim.width; x++)
                 {
                     //find the mean of each block
                     long r = 0, g = 0, b = 0;
                     int xk = 0, yk = 0;
                     for (int i = 0; i < previewFactor; i++)
                     {
                         int realY = raw.dim.width * ((y * previewFactor) + i);
                         yk++;
                         for (int k = 0; k < previewFactor; k++)
                         {
                             xk++;
                             UInt64 realX = (UInt64)(realY + (x * previewFactor + k)) * cpp;
                             r += raw.data[realX];
                             g += raw.data[realX + 1];
                             b += raw.data[realX + 2];
                         }
                     }

                     if (xk != doubleFactor || yk != previewFactor)
                     {
                         Debug.WriteLine("yk :" + yk + " xk: " + xk + " doubleFactor:" + doubleFactor);
                     }
                     r = (ushort)(r / doubleFactor);
                     g = (ushort)(g / doubleFactor);
                     b = (ushort)(b / doubleFactor);
                     if (r < 0) r = 0; else if (r > maxValue) r = maxValue;
                     if (g < 0) g = 0; else if (g > maxValue) g = maxValue;
                     if (b < 0) b = 0; else if (b > maxValue) b = maxValue;
                     preview.data[((y * preview.dim.width) + x) * cpp] = (ushort)r;
                     preview.data[(((y * preview.dim.width) + x) * cpp) + 1] = (ushort)g;
                     preview.data[(((y * preview.dim.width) + x) * cpp) + 2] = (ushort)b;
                 }
             });
        }

        public void Rotate(bool v)
        {
            throw new NotImplementedException();
        }
    }
}

