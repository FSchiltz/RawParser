using System;

namespace RawNet
{
    internal class Common
    {
        static public void memset<T>(T[] array, T value, int count)
        {
            for (int i = 0; i < count && i < array.Length; i++) array[i] = value;
        }

        static public int clampbits(int x, int n)
        {
            int _y_temp = x >> n;
            if ((_y_temp != 0))
                x = ~_y_temp >> (32 - n);
            return x;
        }

        static public int[] convertByteToInt(byte[] array)
        {
            int bytePerInt = sizeof(int);
            int[] temp = new int[array.Length / bytePerInt];
            for (int i = 0; i < temp.Length; ++i)
            {
                temp[i] = BitConverter.ToInt32(array, i * bytePerInt);
            }
            return temp;
        }

        static public void memcopy<T>(T[] dest, T[] src)
        {
            memcopy<T>(dest, src, (uint)dest.Length);
        }

        internal static void memcopy<T>(T[] dest, T[] src, uint count)
        {
            memcopy<T>(dest, src, count, 0, 0);
        }

        internal static void memcopy<T>(T[] dest, T[] src, uint count, int destOffset, int srcOffset)
        {
            for (int i = 0; i < count; i++) dest[i + destOffset] = src[i + srcOffset];
        }

        internal static void ConvertArray(ref byte[] v, out int[] dest)
        {
            dest = new int[v.Length / 4];
            for (int i = 0; i < v.Length / 4; i++)
            {
                dest[i] = v[i] << 24 + v[i + 1] << 16 + v[i + 2] << 8 + v[i + 3];
            }
        }

        internal static void ConvertArray(ref object[] v, out byte[] dest)
        {
            dest = new byte[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                dest[i] = (byte)v[i];
            }
        }

        internal static void ByteToChar(ref byte[] v, out char[] dest)
        {
            dest = new char[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                dest[i] = (char)v[i];
            }
        }

        internal static Endianness getHostEndianness()
        {
            return (BitConverter.IsLittleEndian) ? Endianness.little : Endianness.big;
        }

        internal static bool strncmp(byte[] data, string v1, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if ((char)data[i] != v1[i]) return false;
            }
            return true;
        }

        internal static bool memcmp(ref char[] a, ref byte[] b)
        {
            return memcmp(ref a, ref b, a.Length);
        }

        internal static bool memcmp(ref char[] a, ref byte[] b, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (a[i] != (char)b[i]) return false;
            }
            return true;
        }

        internal static uint[] convertByteToUInt(byte[] array)
        {
            int bytePerInt = sizeof(uint);
            uint[] temp = new uint[array.Length / bytePerInt];
            for (int i = 0; i < temp.Length; ++i)
            {
                temp[i] = BitConverter.ToUInt32(array, i * bytePerInt);
            }
            return temp;
        }

        internal static bool isPowerOfTwo(uint x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }
    }
}
