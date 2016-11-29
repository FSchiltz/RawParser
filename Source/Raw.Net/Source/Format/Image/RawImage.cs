using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using System.Linq;

namespace RawNet
{

    public class RawImageWorker
    {
        public enum RawImageWorkerTask
        {
            SCALE_VALUES = 1, FIX_BAD_PIXELS = 2, APPLY_LOOKUP = 3 | 0x1000, FULL_IMAGE = 0x1000
        };

        RawImage data;
        RawImageWorkerTask task;
        int start_y;
        int end_y;
    };

    public class TableLookUp
    {

        static int TABLE_SIZE = 65536 * 2;
        public int ntables;
        public UInt16[] tables;
        public bool dither;

        // Creates n numre of tables.
        public TableLookUp(int _ntables, bool _dither)
        {
            ntables = (_ntables);
            dither = (_dither);
            tables = null;
            if (ntables < 1)
            {
                throw new RawDecoderException("Cannot construct 0 tables");
            }
            tables = new ushort[ntables * TABLE_SIZE];
            Common.memset<ushort>(tables, 0, sizeof(ushort) * ntables * TABLE_SIZE);
        }


        public void setTable(int ntable, ushort[] table, int nfilled)
        {
            if (ntable > ntables)
            {
                throw new RawDecoderException("Table lookup with number greater than number of tables.");
            }
            if (!dither)
            {
                for (int i = 0; i < 65536; i++)
                {
                    tables[i + (ntable * TABLE_SIZE)] = (i < nfilled) ? table[i] : table[nfilled - 1];
                }
                return;
            }
            for (int i = 0; i < nfilled; i++)
            {
                int center = table[i];
                int lower = i > 0 ? table[i - 1] : center;
                int upper = i < (nfilled - 1) ? table[i + 1] : center;
                int delta = upper - lower;
                tables[(i * 2) + (ntable * TABLE_SIZE)] = (ushort)(center - ((upper - lower + 2) / 4));
                tables[(i * 2) + 1 + (ntable * TABLE_SIZE)] = (ushort)delta;
            }

            for (int i = nfilled; i < 65536; i++)
            {
                tables[(i * 2) + (ntable * TABLE_SIZE)] = table[nfilled - 1];
                tables[(i * 2) + 1 + (ntable * TABLE_SIZE)] = 0;
            }
            tables[0] = tables[1];
            tables[TABLE_SIZE - 1] = tables[TABLE_SIZE - 2];
        }

        public ushort[] getTable(int n)
        {
            if (n > ntables)
            {
                throw new RawDecoderException("Table lookup with number greater than number of tables.");
            }
            return tables.Skip(n * TABLE_SIZE).ToArray();
        }
    };

    public enum RawImageType { TYPE_USHORT16, TYPE_FLOAT32 };

    public class RawImage
    {
        public byte[] thumbnail;
        public ushort[] previewData, rawData;
        public string fileName { get; set; }
        public Dictionary<ushort, Tag> exif;
        public ushort colorDepth;
        public iPoint2D dim, mOffset, previewDim, uncropped_dim;
        public ColorFilterArray cfa = new ColorFilterArray();
        public double[] camMul, black, curve;
        public int rotation = 0, blackLevel, saturation, dark;
        public List<BlackArea> blackAreas;
        public bool mDitherScale;           // Should upscaling be done with dither to mimize banding?
        public ImageMetaData metadata = new ImageMetaData();
        public uint pitch, cpp, bpp, whitePoint;
        public int[] blackLevelSeparate = new int[4];
        public List<String> errors;
        internal bool isCFA;
        public TableLookUp table;
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
            rawData = new ushort[dim.x * dim.y];
            if (rawData == null)
                throw new RawDecoderException("RawImageData::createData: Memory Allocation failed.");
            uncropped_dim = dim;
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
                var a = (row * dim.x) + col;
                if (row < 0 || row >= dim.y || col < 0 || col >= dim.x)
                {
                    return 0;
                }
                else return rawData[a];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                rawData[(row * dim.x) + col] = value;
            }
        }

        public void setTable(ushort[] table, int nfilled, bool dither)
        {
            TableLookUp t = new TableLookUp(1, dither);
            t.setTable(0, table, nfilled);
            this.table = (t);
        }

        public void subFrame(iRectangle2D crop)
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
        internal ushort[] getImageAsByteArray()
        {
            ushort[] tempByteArray = new ushort[dim.x * dim.y];
            for (int i = 0; i < tempByteArray.Length; i++)
            {
                //get the pixel
                ushort temp = rawData[(i * colorDepth)];
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
        public void setWithLookUp(UInt16 value, ref ushort[] dst, uint offset, ref UInt32 random)
        {
            if (table == null)
            {
                dst[offset] = value;
                return;
            }
            if (table.dither)
            {/*
                UInt32 lookup = (uint)(table.tables[value * 2] | table.tables[value * 2 + 1] << 16);
                UInt32 basevalue = lookup & 0xffff;
                UInt32 delta = lookup >> 16;*/

                UInt32 basevalue = (uint)table.tables[value * 2];
                UInt32 delta = (uint)table.tables[value * 2 + 1];
                UInt32 r = random;

                uint pix = basevalue + ((delta * (r & 2047) + 1024) >> 12);
                random = 15700 * (r & 65535) + (r >> 16);
                dst[offset] = (ushort)pix;
                return;
            }
            dst[offset] = table.tables[value];
        }
    }
}
