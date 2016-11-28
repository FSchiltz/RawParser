using System.IO;
using System;

namespace RawNet
{
    static public class Data
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
        TIFF_NOTYPE = 0, /* placeholder */
        TIFF_BYTE = 1, /* 8-bit unsigned integer */
        TIFF_ASCII = 2, /* 8-bit bytes w/ last byte null */
        TIFF_SHORT = 3, /* 16-bit unsigned integer */
        TIFF_LONG = 4, /* 32-bit unsigned integer */
        TIFF_RATIONAL = 5, /* 64-bit unsigned fraction */
        TIFF_SBYTE = 6, /* !8-bit signed integer */
        TIFF_UNDEFINED = 7, /* !8-bit untyped data */
        TIFF_SSHORT = 8, /* !16-bit signed integer */
        TIFF_SLONG = 9, /* !32-bit signed integer */
        TIFF_SRATIONAL = 10, /* !64-bit signed fraction */
        TIFF_FLOAT = 11, /* !32-bit IEEE floating point */
        TIFF_DOUBLE = 12, /* !64-bit IEEE floating point */
        TIFF_OFFSET = 13, /* 32-bit unsigned offset used in ORF at least */
    };

    public class Tag
    {
        public TagType tagId { get; set; }
        public ushort dataType;
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
                        case 1:
                        case 6:
                        case 7:
                            foreach (object t in data)
                            {
                                temp += (byte)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 2:
                            temp = (string)data[0];
                            break;
                        case 3:
                            foreach (object t in data)
                            {
                                temp += (ushort)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 4:
                            foreach (object t in data)
                            {
                                temp += (uint)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 8:
                            foreach (object t in data)
                            {
                                temp += (short)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 9:
                            foreach (object t in data)
                            {
                                temp += (int)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;
                        case 11:
                            foreach (object t in data)
                            {
                                temp += (int)t;
                                temp += " ";
                            }
                            temp += "\0";
                            break;

                        case 5:
                        case 10:
                        case 12:
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
            dataType = 1;
            displayName = "";

        }

        public int getTypeSize(ushort id)
        {
            int size = 0;
            switch (id)
            {
                case 1:
                case 2:
                case 6:
                case 7:
                    size = 1;
                    break;
                case 3:
                case 8:
                    size = 2;
                    break;
                case 4:
                case 9:
                case 11:
                    size = 4;
                    break;
                case 10:
                case 5:
                case 12:
                    size = 8;
                    break;
            }
            return size;
        }

        public int getInt(int v)
        {
            try
            {
                return Convert.ToInt32(data[0]);
            }
            catch (Exception e)
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
            catch (Exception e)
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
            catch (Exception e)
            {
                return v;
            }
        }

        public short getShort(short v)
        {
            try
            {
                return Convert.ToInt16(data[0]);
            }
            catch (Exception e)
            {
                return v;
            }
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
