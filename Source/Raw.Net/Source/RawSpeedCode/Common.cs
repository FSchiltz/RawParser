using System;
using System.Collections;

namespace RawSpeed
{
    public enum Endianness
    {
        big, little, unknown
    };

    public static class Common
    {
        static uint rawspeed_get_number_of_processor_cores() { return 2; }
        static int DEBUG_PRIO_ERROR = 0x10;
        static int DEBUG_PRIO_WARNING = 0x100;
        static int DEBUG_PRIO_INFO = 0x1000;
        static int DEBUG_PRIO_EXTRA = 0x10000;

        public static int get2BE(int data, int pos)
        {
            return ((((UInt16)(data)[pos]) << 8) | ((UInt16)(data)[pos + 1]));
        }

        static public int get2LE(int data, int pos) { return ((((UInt16)(data)[pos + 1]) << 8) | ((UInt16)(data)[pos])); }

        public static int get4BE(int data, int pos)
        {
            return ((((UInt32)(data)[pos + 0]) << 24) |
(((UInt32)(data)[pos + 1]) << 16) |
(((UInt32)(data)[pos + 2]) << 8) |
((UInt32)(data)[pos + 3]));
        }

        public static int get4LE(int data, int pos)
        {
            return ((((UInt32)(data)[pos + 3]) << 24) |
  (((UInt32)(data)[pos + 2]) << 16) |
  (((UInt32)(data)[pos + 1]) << 8) |
   ((UInt32)(data)[pos]));
        }

        public static int get8LE(int data, int pos)
        {
            return ((((UInt64)(data)[pos + 7]) << 56) |
(((UInt64)(data)[pos + 6]) << 48) |
(((UInt64)(data)[pos + 5]) << 40) |
(((UInt64)(data)[pos + 4]) << 32) |
(((UInt64)(data)[pos + 3]) << 24) |
(((UInt64)(data)[pos + 2]) << 16) |
(((UInt64)(data)[pos + 1]) << 8) |
((UInt64)(data)[pos]));
        }

        public static int get8BE(data, pos)
        {
            return ((((UInt64)(data)[pos + 0]) << 56) | (((UInt64)(data)[pos + 1]) << 48) | (((UInt64)(data)[pos + 2]) << 40) | (((UInt64)(data)[pos + 3]) << 32) |
(((UInt64)(data)[pos + 4]) << 24) | (((UInt64)(data)[pos + 5]) << 16) | (((UInt64)(data)[pos + 6]) << 8) | ((UInt64)(data)[pos + 7]));
        }

        static void BitBlt(byte[] dstp, int dst_pitch, byte[] srcp, int src_pitch, int row_size, int height)
        {
            if (height == 1 || (dst_pitch == src_pitch && src_pitch == row_size))
            {
                srcp.CopyTo(dstp, 0);
                return;
            }
            for (int y = height; y > 0; --y)
            {
                srcp.CopyTo(dstp, 0);
                dstp += dst_pitch;
                srcp += src_pitch;
            }
        }
        static bool isPowerOfTwo(int val)
        {
            return (val & (~val + 1)) == val;
        }

        static int min(int p0, int p1)
        {
            return p1 + ((p0 - p1) & ((p0 - p1) >> 31));
        }

        static int max(int p0, int p1)
        {
            return p0 - ((p0 - p1) & ((p0 - p1) >> 31));
        }

        static public UInt32 getThreadCount()
        {
            return rawspeed_get_number_of_processor_cores();
        }

        static public Endianness getHostEndianness()
        {
            UInt16 testvar = 0xfeff;
            UInt32 firstbyte = (uint)testvar >> 8;
            if (firstbyte == 0xff)
                return Endianness.little;
            else if (firstbyte == 0xfe)
                return Endianness.big;
            else
                return Endianness.unknown;
        }

        public static UInt32 clampbits(int x, UInt32 n)
        {
            UInt32 _y_temp = (uint)(x >> (int)n);
            if (_y_temp != 0)
                x = ~(int)_y_temp >> (int)(32 - n);
            return (uint)x;
        }

        /* This is faster - at least when compiled on visual studio 32 bits */
        public static int other_abs(int x) { int mask = x >> 31; return (x + mask) ^ mask; }
    }

    public enum BitOrder
    {
        BitOrder_Plain,  /* Memory order */
        BitOrder_Jpeg,   /* Input is added to stack byte by byte, and output is lifted from top */
        BitOrder_Jpeg16, /* Same as above, but 16 bits at the time */
        BitOrder_Jpeg32, /* Same as above, but 32 bits at the time */
    };
}

