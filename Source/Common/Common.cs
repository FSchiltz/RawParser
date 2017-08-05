using System;

namespace PhotoNet.Common
{
    public static class Common
    {
        static public string Trim(string val)
        {
            //ifd string have somtimes trailing zero so remove them
            return val.Remove(val.IndexOf((char)0));

        }

        static public int Clampbits(int x, int n)
        {
            int _y_temp = x >> n;
            if ((_y_temp != 0))
                x = ~_y_temp >> (32 - n);
            return x;
        }        

        public static void Memcopy<T>(T[] dest, T[] src, uint count)
        {
            Memcopy<T>(dest, src, count, 0, 0);
        }

        public static void Memcopy<T>(T[] dest, T[] src, uint count, int destOffset, int srcOffset)
        {
            for (int i = 0; i < count; i++) dest[i + destOffset] = src[i + srcOffset];
        }

        public static void ConvertArray(object[] v, out byte[] dest)
        {
            dest = new byte[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                dest[i] = (byte)v[i];
            }
        }

        public static void ByteToChar(byte[] v, out char[] dest, int count)
        {
            dest = new char[count];
            for (int i = 0; i < count; i++)
            {
                dest[i] = (char)v[i];
            }
        }

        public static Endianness GetHostEndianness()
        {
            return (BitConverter.IsLittleEndian) ? Endianness.Little : Endianness.Big;
        }

        public static bool Strncmp(byte[] data, string v1, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if ((char)data[i] != v1[i]) return false;
            }
            return true;
        }

        public static bool Memcmp(char[] a, byte[] b)
        {
            return Memcmp(a, b, a.Length);
        }

        public static bool Memcmp(char[] a, byte[] b, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (a[i] != (char)b[i]) return false;
            }
            return true;
        }

        public static bool IsPowerOfTwo(uint x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
    }
}
