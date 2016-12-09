﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RawNet
{
    public class RawImage
    {
        public byte[] Thumbnail { get; set; }
        public ushort[] previewData, rawData;

        public Point2D dim, mOffset = new Point2D(), previewDim, previewOffset = new Point2D(), uncroppedDim;
        public ColorFilterArray cfa = new ColorFilterArray();
        public double[] camMul, black, curve;
        public int rotation = 0, blackLevel, saturation, dark;
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public bool mDitherScale { get; set; }          // Should upscaling be done with dither to mimize banding?
        public ushort ColorDepth { get; set; }

        public ImageMetaData metadata = new ImageMetaData();
        public uint pitch, cpp, bpp, whitePoint;
        public int[] blackLevelSeparate = new int[4];
        public List<String> errors;
        internal bool isCFA;
        internal TableLookUp table;
        public ColorFilterArray UncroppedCfa;

        public RawImage()
        {
            //Set for 16bit image non demos
            uint _cpp = 1; uint _bpc = 2;
            cpp = (_cpp);
            bpp = (_bpc * _cpp);
        }

        internal void Init()
        {
            if (dim.x > 65535 || dim.y > 65535)
                throw new RawDecoderException("RawImageData: Dimensions too large for allocation.");
            if (dim.x <= 0 || dim.y <= 0)
                throw new RawDecoderException("RawImageData: Dimension of one sides is less than 1 - cannot allocate image.");
            if (rawData != null)
                throw new RawDecoderException("RawImageData: Duplicate data allocation in createData.");
            pitch = (uint)(((dim.x * bpp) + 15) / 16) * 16;
            rawData = new ushort[dim.x * dim.y * cpp];
            if (rawData == null)
                throw new RawDecoderException("RawImageData::createData: Memory Allocation failed.");
            uncroppedDim = dim;
        }

        /*
         * Should be allows if possible
         * not efficient but allows more concise code
         * 
         */
        public ushort this[int row, int col]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var a = (row * uncroppedDim.x) + col;
                if (row < 0 || row >= dim.y || col < 0 || col >= dim.x)
                {
                    return 0;
                }
                else return rawData[a];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                rawData[(row * uncroppedDim.x) + col] = value;
            }
        }

        public void setTable(ushort[] table, int nfilled, bool dither)
        {
            TableLookUp t = new TableLookUp(1, dither);
            t.setTable(0, table, nfilled);
            this.table = (t);
        }

        public void subFrame(Rectangle2D crop)
        {
            if (!crop.dim.isThisInside(dim - crop.pos))
            {
                Debug.WriteLine("WARNING: RawImageData::subFrame - Attempted to create new subframe larger than original size. Crop skipped.");
                return;
            }
            if (crop.pos.x < 0 || crop.pos.y < 0 || !crop.hasPositiveArea())
            {
                Debug.WriteLine("WARNING: RawImageData::subFrame - Negative crop offset. Crop skipped.");
                return;
            }

            mOffset += crop.pos;

            dim = crop.dim;
        }
        /*
         * For testing
         */
        internal ushort[] GetImageAsByteArray()
        {
            ushort[] tempByteArray = new ushort[dim.x * dim.y];
            for (int i = 0; i < tempByteArray.Length; i++)
            {
                //get the pixel
                ushort temp = rawData[(i * ColorDepth)];
                /*
            for (int k = 0; k < 8; k++)
            {
                bool xy = rawData[(i * (int)colorDepth) + k];
                if (xy)
                {
                    temp |= (ushort)(1 << k);
                }
            }*/
                tempByteArray[i] = temp;
            }
            return tempByteArray;
        }

        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        internal void setWithLookUp(UInt16 value, ref ushort[] dst, uint offset, ref uint random)
        {
            if (table == null)
            {
                dst[offset] = value;
                return;
            }
            if (table.dither)
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
        internal unsafe void setWithLookUp(ushort value, byte* dst, ref uint random)
        {
            ushort* dest = (ushort*)dst;
            if (table == null)
            {
                *dest = value;
                return;
            }
            if (table.dither)
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

        public void scaleValues()
        {
            //skip 250 pixel to reduce calculation
            //TODO fix the condiftion
            const int skipBorder = 250;
            int gw = (int)((dim.x - skipBorder) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && blackLevel < 0) || whitePoint >= 65536)
            {  // Estimate
                int b = 65536;
                uint m = 0;
                for (int row = skipBorder; row < (dim.y - skipBorder + mOffset.y); row++)
                {
                    for (int col = skipBorder; col < gw; col++)
                    {
                        b = Math.Min(rawData[row * dim.x + col], b);
                        m = Math.Min(rawData[row * dim.x + col], m);
                    }
                }
                if (blackLevel < 0)
                    blackLevel = b;
                if (whitePoint >= 65536)
                    whitePoint = m;
                Debug.WriteLine("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + ", Estimated white:" + whitePoint);
            }

            /* Skip, if not needed */
            if ((blackAreas.Count == 0 && blackLevel == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || dim.area() <= 0)
                return;

            /* If filter has not set separate blacklevel, compute or fetch it */
            if (blackLevelSeparate[0] < 0)
                calculateBlackAreas();
            gw = (int)(dim.x * cpp) + mOffset.x;
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
                if ((mOffset.x & 1) != 0)
                    v ^= 1;
                if ((mOffset.y & 1) != 0)
                    v ^= 2;
                mul[i] = (int)(16384.0f * 65535.0f / (whitePoint - blackLevelSeparate[v]));
                sub[i] = blackLevelSeparate[v];
            }

            Parallel.For(mOffset.y, dim.y + mOffset.y, y =>
            //for (int y = mOffset.y; y < dim.y + mOffset.y; y++)
            {
                int v = dim.x + y * 36969;
                for (int x = mOffset.x; x < gw; x++)
                {
                    int rand;
                    if (mDitherScale)
                    {
                        v = 18000 * (v & 65535) + (v >> 16);
                        rand = half_scale_fp - (full_scale_fp * (v & 2047));
                    }
                    else
                    {
                        rand = 0;
                    }
                    rawData[x + (y * dim.x * cpp)] = (ushort)Common.clampbits(((rawData[(y * dim.x * cpp) + x] - sub[(2 * (y & 1)) + (x & 1)]) * mul[(2 * (y & 1)) + (x & 1)] + 8192 + rand) >> 14, 16);
                }

            });
            ColorDepth = 16;
            bpp = 2;
        }

        /*
         * return the n byte of the image
         */
        internal byte getByteAt(uint n)
        {
            //find the index of the short
            int index = (int)n / 2;
            int reste = (int)n % 2;
            ushort value = rawData[index];
            return (reste == 0) ? (byte)(value >> 8) : (byte)value;
        }

        public void scaleBlackWhite()
        {
            const int skipBorder = 250;
            int gw = (int)((dim.x - skipBorder + mOffset.x) * cpp);
            if ((blackAreas.Count == 0 && blackLevelSeparate[0] < 0 && blackLevel < 0) || whitePoint >= 65536)
            {  // Estimate
                int b = 65536;
                int m = 0;
                for (int row = skipBorder; row < (dim.y - skipBorder + mOffset.y); row++)
                {
                    ushort[] pixel = rawData.Skip(skipBorder + row * dim.x).ToArray();
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
                Debug.WriteLine("ISO:" + metadata.isoSpeed + ", Estimated black:" + blackLevel + " Estimated white: " + whitePoint);
            }

            /* Skip, if not needed */
            if ((blackAreas.Count == 0 && blackLevel == 0 && whitePoint == 65535 && blackLevelSeparate[0] < 0) || dim.area() <= 0)
                return;

            /* If filter has not set separate blacklevel, compute or fetch it */
            if (blackLevelSeparate[0] < 0)
                calculateBlackAreas();

            scaleValues();
        }

        void calculateBlackAreas()
        {
            int[] histogram = new int[4 * 65536 * sizeof(int)];
            //memset(histogram, 0, 4 * 65536 * sizeof(int));
            int totalpixels = 0;

            for (int i = 0; i < blackAreas.Count; i++)
            {
                BlackArea area = blackAreas[i];

                /* Make sure area sizes are multiple of two, 
                   so we have the same amount of pixels for each CFA group */
                area.size = area.size - (area.size & 1);

                /* Process horizontal area */
                if (!area.isVertical)
                {
                    if (area.offset + area.size > uncroppedDim.y)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than height of image");
                    for (int y = area.offset; y < area.offset + area.size; y++)
                    {
                        ushort[] pixel = previewData.Skip(mOffset.x + dim.x * y).ToArray();
                        int[] localhist = histogram.Skip((y & 1) * (65536 * 2)).ToArray();
                        for (int x = mOffset.x; x < dim.x + mOffset.x; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.size * dim.x;
                }

                /* Process vertical area */
                if (area.isVertical)
                {
                    if (area.offset + area.size > uncroppedDim.x)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than width of image");
                    for (int y = mOffset.y; y < dim.y + mOffset.y; y++)
                    {
                        ushort[] pixel = previewData.Skip(area.offset + dim.x * y).ToArray();
                        int[] localhist = histogram.Skip((y & 1) * (65536 * 2)).ToArray();
                        for (int x = area.offset; x < area.size + area.offset; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.size * dim.y;
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
            previewDim = new Point2D(dim.x / previewFactor, dim.y / previewFactor);
            previewData = new ushort[previewDim.y * previewDim.x * cpp];
            int doubleFactor = previewFactor * previewFactor;
            ushort maxValue = (ushort)((1 << ColorDepth) - 1);
            //loop over each block
            Parallel.For(0, previewDim.y, y =>
            {
                for (int x = 0; x < previewDim.x; x++)
                {
                    //find the mean of each block
                    ushort r = 0, g = 0, b = 0;

                    for (int i = 0; i < previewFactor; i++)
                    {
                        int realY = dim.x * ((y * previewFactor) + i);
                        for (int k = 0; k < previewFactor; k++)
                        {
                            long realX = (realY + (x * previewFactor + k)) * cpp;
                            r += rawData[realX];
                            g += rawData[realX + 1];
                            b += rawData[realX + 2];
                        }
                    }
                    r = (ushort)(r / doubleFactor);
                    g = (ushort)(g / doubleFactor);
                    b = (ushort)(b / doubleFactor);
                    if (r < 0) r = 0; else if (r > maxValue) r = maxValue;
                    if (g < 0) g = 0; else if (g > maxValue) g = maxValue;
                    if (b < 0) b = 0; else if (b > maxValue) b = maxValue;
                    previewData[((y * previewDim.x) + x) * cpp] = r;
                    previewData[(((y * previewDim.x) + x) * cpp) + 1] = g;
                    previewData[(((y * previewDim.x) + x) * cpp) + 2] = b;
                }
            });
        }


        /*protected void doLookup(int start_y, int end_y)
        {
            if (table.ntables == 1)
            {
                ushort[] t = table.getTable(0);
                if (table.dither)
                {
                    long g = uncropped_dim.x * cpp;
                    Common.ConvertArray(ref table.getTable(0), out int[]t2);
                    for (int y = start_y; y < end_y; y++)
                    {
                        int v = (uncropped_dim.x + y * 13) ^ 0x45694584;
                        ushort[] pixel = getDataUncropped(0, y);
                        for (int x = 0; x < g; x++)
                        {
                            ushort p = pixel;
                            uint lookup = t2[p];
                            uint b = lookup & 0xffff;
                            uint delta = lookup >> 16;
                            v = 15700 * (v & 65535) + (v >> 16);
                            uint pix = b + (((delta * (v & 2047) + 1024)) >> 12);
                            pixel = pix;
                            pixel++;
                        }
                    }
                    return;
                }

                long gw = uncropped_dim.x * cpp;
                for (int y = start_y; y < end_y; y++)
                {
                    ushort[] pixel = getDataUncropped(0, y);
                    for (int x = 0; x < gw; x++)
                    {
                        *pixel = t[*pixel];
                        pixel++;
                    }
                }
                return;
            }
            throw new RawDecoderException("Table lookup with multiple components not implemented");
        }*/
    }
}

