using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RawNet
{
    public class RawImage
    {
        public ushort[] previewData, rawData;

        public Point2D dim, offset = new Point2D(), previewDim, previewOffset = new Point2D(), uncroppedDim, uncroppedPreviewDim;
        public ColorFilterArray cfa = new ColorFilterArray();
        public double[] camMul, black, curve;
        public int rotation = 0, blackLevel, saturation, dark;
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public bool DitherScale { get; set; }          // Should upscaling be done with dither to mimize banding?
        public ushort ColorDepth { get; set; }

        public ImageMetadata metadata = new ImageMetadata();
        public uint cpp, bpp, whitePoint, pitch;
        public int[] blackLevelSeparate = new int[4];
        public List<String> errors = new List<string>();
        public bool isCFA = true;
        internal TableLookUp table;
        public ColorFilterArray UncroppedCfa;

        public RawImage()
        {
            //Set for 16bit image non demos           
            cpp = 1;
            bpp = 2;
            ColorDepth = 16;
        }

        internal void Init()
        {
            if (dim.width > 65535 || dim.height > 65535)
                throw new RawDecoderException("RawImageData: Dimensions too large for allocation.");
            if (dim.width <= 0 || dim.height <= 0)
                throw new RawDecoderException("RawImageData: Dimension of one sides is less than 1 - cannot allocate image.");
            if (rawData != null)
                throw new RawDecoderException("RawImageData: Duplicate data allocation in createData.");
            pitch = (uint)(((dim.width * bpp) + 15) / 16) * 16;
            rawData = new ushort[dim.width * dim.height * cpp];
            if (rawData == null)
                throw new RawDecoderException("RawImageData::createData: Memory Allocation failed.");
            uncroppedDim = new Point2D(dim.width, dim.height);
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
                var a = (row * uncroppedDim.width) + col;
                if (row < 0 || row >= dim.height || col < 0 || col >= dim.width)
                {
                    return 0;
                }
                else return rawData[a];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                rawData[(row * dim.width) + col] = value;
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
            if (!crop.Dim.IsThisInside(dim - crop.Pos))
            {
                Debug.WriteLine("WARNING: RawImageData::subFrame - Attempted to create new subframe larger than original size. Crop skipped.");
                return;
            }
            if (crop.Pos.width < 0 || crop.Pos.height < 0 || !crop.HasPositiveArea())
            {
                Debug.WriteLine("WARNING: RawImageData::subFrame - Negative crop offset. Crop skipped.");
                return;
            }

            offset += crop.Pos;

            dim = crop.Dim;

            if ((crop.Pos.width & 1) != 0)
                cfa.ShiftLeft(0);
            if ((crop.Pos.height & 1) != 0)
                cfa.ShiftDown(0);
        }

        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        internal void SetWithLookUp(UInt16 value, ref ushort[] dst, int offset, ref uint random)
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
            //skip 250 pixel to reduce calculation
            //TODO fix the condiftion
            const int skipBorder = 250;
            int gw = (int)((dim.width - skipBorder) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && blackLevel < 0) || whitePoint >= 65536)
            {  // Estimate
                int b = 65536;
                uint m = 0;
                for (int row = skipBorder; row < (dim.height - skipBorder + offset.height); row++)
                {
                    for (int col = skipBorder; col < gw; col++)
                    {
                        b = Math.Min(rawData[row * dim.width + col], b);
                        m = Math.Min(rawData[row * dim.width + col], m);
                    }
                }
                if (blackLevel < 0)
                    blackLevel = b;
                if (whitePoint >= 65536)
                    whitePoint = m;
                Debug.WriteLine("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + ", Estimated white:" + whitePoint);
            }

            /* Skip, if not needed */
            if ((blackAreas.Count == 0 && blackLevel == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || dim.Area() <= 0)
                return;

            /* If filter has not set separate blacklevel, compute or fetch it */
            if (blackLevelSeparate[0] < 0)
                CalculateBlackAreas();
            gw = (int)(dim.width * cpp) + offset.width;
            int[] mul = new int[4];
            int[] sub = new int[4];
            int depth_values = (int)(whitePoint - blackLevelSeparate[0]);
            float app_scale = 65535.0f / depth_values;

            // Scale in 30.2 fp
            int full_scale_fp = (int)(app_scale * 4.0f);
            // Half Scale in 18.14 fp
            int half_scale_fp = (int)(app_scale * 4095.0f);

            for (int i = 0; i < 4; i++)
            {
                int v = i;
                if ((offset.width & 1) != 0)
                    v ^= 1;
                if ((offset.height & 1) != 0)
                    v ^= 2;
                mul[i] = (int)(16384.0f * 65535.0f / (whitePoint - blackLevelSeparate[v]));
                sub[i] = blackLevelSeparate[v];
            }

            Parallel.For(offset.height, dim.height + offset.height, y =>
            //for (int y = mOffset.y; y < dim.y + mOffset.y; y++)
            {
                int v = dim.width + y * 36969;
                for (int x = offset.width; x < gw; x++)
                {
                    int rand;
                    if (DitherScale)
                    {
                        v = 18000 * (v & 65535) + (v >> 16);
                        rand = half_scale_fp - (full_scale_fp * (v & 2047));
                    }
                    else
                    {
                        rand = 0;
                    }
                    rawData[x + (y * dim.width * cpp)] = (ushort)Common.Clampbits(((rawData[(y * dim.width * cpp) + x] - sub[(2 * (y & 1)) + (x & 1)]) * mul[(2 * (y & 1)) + (x & 1)] + 8192 + rand) >> 14, 16);
                }

            });
            ColorDepth = 16;
            bpp = 2;
        }

        /*
         * return the n byte of the image
         */
        internal byte GetByteAt(uint n)
        {
            //find the index of the short
            int index = (int)n / 2;
            int reste = (int)n % 2;
            ushort value = rawData[index];
            return (reste == 0) ? (byte)(value >> 8) : (byte)value;
        }

        public void ScaleBlackWhite()
        {
            const int skipBorder = 250;
            int gw = (int)((dim.width - skipBorder + offset.width) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && blackLevel < 0) || whitePoint >= 65536)
            {  // Estimate
                int b = 65536;
                int m = 0;
                for (int row = skipBorder; row < (dim.height - skipBorder + offset.height); row++)
                {
                    ushort[] pixel = rawData.Skip(skipBorder + row * dim.width).ToArray();
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
                    whitePoint = (uint)m;
                //Debug.WriteLine("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + " Estimated white: " + whitePoint);
            }

            /* Skip, if not needed */
            if ((blackAreas.Count == 0 && blackLevel == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || dim.Area() <= 0)
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
                    if (area.Offset + area.Size > uncroppedDim.height)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than height of image");
                    for (int y = area.Offset; y < area.Offset + area.Size; y++)
                    {
                        ushort[] pixel = previewData.Skip(offset.width + dim.width * y).ToArray();
                        int[] localhist = histogram.Skip((y & 1) * (65536 * 2)).ToArray();
                        for (int x = offset.width; x < dim.width + offset.width; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * dim.width;
                }

                /* Process vertical area */
                if (area.IsVertical)
                {
                    if (area.Offset + area.Size > uncroppedDim.width)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than width of image");
                    for (int y = offset.height; y < dim.height + offset.height; y++)
                    {
                        ushort[] pixel = previewData.Skip(area.Offset + dim.width * y).ToArray();
                        int[] localhist = histogram.Skip((y & 1) * (65536 * 2)).ToArray();
                        for (int x = area.Offset; x < area.Size + area.Offset; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * dim.height;
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
            previewDim = new Point2D(dim.width / previewFactor, dim.height / previewFactor);
            uncroppedPreviewDim = new Point2D(dim.width / previewFactor, dim.height / previewFactor);
            Debug.WriteLine("Preview of size w:" + previewDim.width + "y:" + previewDim.height);
            previewData = new ushort[previewDim.height * previewDim.width * cpp];
            int doubleFactor = previewFactor * previewFactor;
            ushort maxValue = (ushort)((1 << ColorDepth) - 1);
            //loop over each block
            Parallel.For(0, previewDim.height, y =>
            {
                for (int x = 0; x < previewDim.width; x++)
                {
                    //find the mean of each block
                    long r = 0, g = 0, b = 0;
                    int xk = 0, yk = 0;
                    for (int i = 0; i < previewFactor; i++)
                    {
                        int realY = dim.width * ((y * previewFactor) + i);
                        yk++;
                        for (int k = 0; k < previewFactor; k++)
                        {
                            xk++;
                            UInt64 realX = (UInt64)(realY + (x * previewFactor + k)) * cpp;
                            r += rawData[realX];
                            g += rawData[realX + 1];
                            b += rawData[realX + 2];
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
                    previewData[((y * previewDim.width) + x) * cpp] = (ushort)r;
                    previewData[(((y * previewDim.width) + x) * cpp) + 1] = (ushort)g;
                    previewData[(((y * previewDim.width) + x) * cpp) + 2] = (ushort)b;
                }
            });
        }

        public void Rotate(bool v)
        {
            throw new NotImplementedException();
        }
    }
}

