using System.IO;
using System;

namespace RawNet
{
    static internal class Data
    {
        public static uint[] datasizes = { 0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8, 4 };
        // 0-1-2-3-4-5-6-7-8-9-10-11-12-13
        public static uint[] datashifts = { 0, 0, 0, 1, 2, 3, 0, 0, 1, 2, 3, 2, 3, 2 };
        // 0-1-2-3-4-5-6-7-8-9-10-11-12-13
    }
    /*
     * Tag data type information.
     *
     * Note: RATIONALs are the ratio of two 32-bit integer values.
     */
    public enum TiffDataType
    {
        NOTYPE = 0, /* placeholder */
        BYTE = 1, /* 8-bit unsigned integer */
        ASCII = 2, /* 8-bit bytes w/ last byte null */
        SHORT = 3, /* 16-bit unsigned integer */
        LONG = 4, /* 32-bit unsigned integer */
        RATIONAL = 5, /* 64-bit unsigned fraction */
        SBYTE = 6, /* !8-bit signed integer */
        UNDEFINED = 7, /* !8-bit untyped data */
        SSHORT = 8, /* !16-bit signed integer */
        SLONG = 9, /* !32-bit signed integer */
        SRATIONAL = 10, /* !64-bit signed fraction */
        FLOAT = 11, /* !32-bit IEEE floating point */
        DOUBLE = 12, /* !64-bit IEEE floating point */
        OFFSET = 13, /* 32-bit unsigned offset used in ORF at least */
    };

    internal class Tag
    {
        public TagType tagId { get; set; }
        public TiffDataType dataType;
        public uint dataCount;
        public uint dataOffset;
        public object[] data;
        public string displayName { get; set; }
        public string dataAsString
        {
            get
            {
                if (data != null)
                {
                    string temp = "";
                    switch (dataType)
                    {
                        case TiffDataType.BYTE:
                        case TiffDataType.SBYTE:
                        case TiffDataType.UNDEFINED:
                            foreach (object t in data)
                            {
                                temp += (byte)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case TiffDataType.ASCII:
                            temp = (string)data[0];
                            break;
                        case TiffDataType.SHORT:
                            foreach (object t in data)
                            {
                                temp += (ushort)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case TiffDataType.LONG:
                            foreach (object t in data)
                            {
                                temp += (uint)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case TiffDataType.SSHORT:
                            foreach (object t in data)
                            {
                                temp += (short)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case TiffDataType.SLONG:
                            foreach (object t in data)
                            {
                                temp += (int)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case TiffDataType.FLOAT:

                        case TiffDataType.RATIONAL:
                        case TiffDataType.DOUBLE:
                        case TiffDataType.SRATIONAL:
                            foreach (object t in data)
                            {
                                temp += (double)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                    }
                    return temp;
                }
                else return "";
            }
        }

        public int getInt()
        {
            return Convert.ToInt32(data[0]);
        }

        public uint getUInt()
        {
            return Convert.ToUInt32(data[0]);
        }

        public double getDouble()
        {
            return Convert.ToDouble(data[0]);

        }
        public Tag()
        {
            data = new object[1];
            dataCount = 1;
            dataType = TiffDataType.UNDEFINED;
            displayName = "";

        }

        public int getTypeSize(TiffDataType id)
        {
            int size = 0;
            switch (id)
            {
                case TiffDataType.BYTE:
                case TiffDataType.ASCII:
                case TiffDataType.SBYTE:
                case TiffDataType.UNDEFINED:
                case TiffDataType.OFFSET:
                    size = 1;
                    break;
                case TiffDataType.SHORT:
                case TiffDataType.SSHORT:
                    size = 2;
                    break;
                case TiffDataType.LONG:
                case TiffDataType.SLONG:
                case TiffDataType.FLOAT:
                    size = 4;
                    break;
                case TiffDataType.RATIONAL:
                case TiffDataType.DOUBLE:
                case TiffDataType.SRATIONAL:
                    size = 8;
                    break;
            }
            return size;
        }

        public void getShortArray(out ushort[] array, int num)
        {
            array = new ushort[num];
            for (int i = 0; i < num; i++)
                array[i] = Convert.ToUInt16(data[i]);
        }

        public void getIntArray(out int[] array, int num)
        {
            array = new int[num];
            for (int i = 0; i < num; i++)
                array[i] = Convert.ToInt32(data[i]);
        }

        public void getFloatArray(out float[] array, int num)
        {
            array = new float[num];
            for (int i = 0; i < num; i++)
                array[i] = Convert.ToSingle(data[i]);
        }

        public int getInt(int v)
        {
            try
            {
                return Convert.ToInt32(data[0]);
            }
            catch (Exception)
            {
                return v;
            }
        }

        public uint getUInt(uint v)
        {
            try
            {
                return Convert.ToUInt32(data[0]);
            }
            catch (Exception)
            {
                return v;
            }
        }

        public float getFloat(float v)
        {
            try
            {
                return (float)Convert.ToDouble(data[0]);
            }
            catch (Exception)
            {
                return v;
            }
        }


        internal float getFloat()
        {
            return (float)Convert.ToDouble(data[0]);
        }

        public short getShort(short v)
        {
            try
            {
                return Convert.ToInt16(data[0]);
            }
            catch (Exception)
            {
                return v;
            }
        }

        internal ushort getUShort()
        {
            return Convert.ToUInt16(data[0]);
        }

        internal short getShort()
        {
            return Convert.ToInt16(data[0]);
        }


        public void writeToStream(Stream s, ushort name, ulong count, object data, long offset)
        {
            throw new NotImplementedException();
        }

        public uint get4LE(uint pos)
        {
            return ((((uint)(data)[pos + 3]) << 24) | (((uint)(data)[pos + 2]) << 16) | (((uint)(data)[pos + 1]) << 8) | ((uint)(data)[pos]));
        }
        public ushort get2BE(uint pos) { return (ushort)(((ushort)(data)[pos] << 8) | (ushort)(data)[pos + 1]); }

        public ushort get2LE(uint pos)
        {
            return (ushort)((((ushort)(data)[pos + 1]) << 8) | ((ushort)(data)[pos]));
        }

        public uint get4BE(uint pos)
        {
            return ((((uint)(data)[pos + 0]) << 24) | (((uint)(data)[pos + 1]) << 16) | (((uint)(data)[pos + 2]) << 8) | ((uint)(data)[pos + 3]));
        }

        public UInt64 get8LE(uint pos)
        {
            return ((((UInt64)(data)[pos + 7]) << 56) | (((UInt64)(data)[pos + 6]) << 48) |
      (((UInt64)(data)[pos + 5]) << 40) |
      (((UInt64)(data)[pos + 4]) << 32) |
      (((UInt64)(data)[pos + 3]) << 24) |
      (((UInt64)(data)[pos + 2]) << 16) |
      (((UInt64)(data)[pos + 1]) << 8) |
       ((UInt64)(data)[pos]));
        }

        public UInt64 get8BE(uint pos)
        {
            return ((((UInt64)(data)[pos + 0]) << 56) |
                (((UInt64)(data)[pos + 1]) << 48) |
                (((UInt64)(data)[pos + 2]) << 40) |
(((UInt64)(data)[pos + 3]) << 32) |
(((UInt64)(data)[pos + 4]) << 24) |
(((UInt64)(data)[pos + 5]) << 16) |
(((UInt64)(data)[pos + 6]) << 8) |
((UInt64)(data)[pos + 7]));
        }
    }
}
