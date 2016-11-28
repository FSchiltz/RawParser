using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawSpeed
{
    static public class Data
    {
        public static UInt32[] datasizes = { 0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8, 4 };
        // 0-1-2-3-4-5-6-7-8-9-10-11-12-13
        public static UInt32[] datashifts = { 0, 0, 0, 1, 2, 3, 0, 0, 1, 2, 3, 2, 3, 2 };
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

    public class TiffEntry
    {
        byte[] getData()
        {
            return data;
        }
        // variables:
        public TiffTag tag;
        public TiffDataType type;
        public UInt32 count;

        void offsetFromParent()
        {
            data_offset += parent_offset;
            parent_offset = 0;
            fetchData();
        }
        public UInt32 parent_offset;
        public UInt64 empty_data;
        public byte[] own_data;
        public byte[] data;
        public UInt32 data_offset;
        public UInt64 bytesize;
        public FileMap file;

        public TiffEntry()
        {
            own_data = null;
            parent_offset = 0;
            file = null;
        }

        public TiffEntry(ref FileMap f, UInt32 offset, UInt32 up_offset)
        {
            parent_offset = up_offset;
            own_data = null;
            empty_data = 0;
            file = f;
            type = TiffDataType.TIFF_UNDEFINED;  // We set type to undefined to avoid debug assertion errors.

            byte[] temp_data = f.getData(offset, 8);
            tag = (TiffTag)Common.get4LEget2LE(temp_data, 0);
            type = (TiffDataType)Common.get4LEget2LE(temp_data, 2);
            count = Common.get4LE(temp_data, 4);

            bytesize = (UInt64)count << (int)Data.datashifts[(int)type];
            if (bytesize > UInt32.MaxValue)
                TiffParserException.ThrowTPE("TIFF entry is supposedly " + bytesize + " bytes");

            if (bytesize == 0) // Better return empty than null-dereference later
                data = (byte8*)&empty_data;
            else if (bytesize <= 4)
                data = fgetDataWrt(offset + 8, bytesize);
            else
            { // offset
                data_offset = get4LE(f.getData(offset + 8, 4), 0);
                fetchData();
            }
        }

        public void fetchData()
        {
            data = file?.getDataWrt(data_offset, bytesize);
        }


        public TiffEntry(TiffTag _tag, TiffDataType _type, UInt32 _count, byte[] _data)
        {
            file = null;
            parent_offset = 0;
            tag = _tag;
            type = _type;
            count = _count;
            data_offset = 0; // Set nonsense value in case someone tries to use it
            bytesize = (ulong)_count << (int)Data.datashifts[(int)_type];
            if (null == _data)
            {
                own_data = new byte[bytesize];
                memset(own_data, 0, bytesize);
                data = own_data;
            }
            else
            {
                data = _data;
                own_data = null;
            }
        }

        bool isInt()
        {
            return (type == TiffDataType.TIFF_LONG || type == TiffDataType.TIFF_SHORT || type == TiffDataType.TIFF_BYTE);
        }

        byte getByte(UInt32 num)
        {
            if (type != TiffDataType.TIFF_BYTE)
                TiffParserException.ThrowTPE("TIFF, getByte: Wrong type "+type+" encountered. Expected Byte on "+ tag);

            if (num >= bytesize)
                TiffParserException.ThrowTPE("TIFF, getByte: Trying to read out of bounds");

            return data[num];
        }

        UInt16 getShort(UInt32 num)
        {
            if (type != TiffDataType.TIFF_SHORT && type != TiffDataType.TIFF_UNDEFINED)
                TiffParserException.ThrowTPE("TIFF, getShort: Wrong type " + type + " encountered. Expected Short or Undefined on " + tag);

            if (num * 2 + 1 >= bytesize)
                TiffParserException.ThrowTPE("TIFF, getShort: Trying to read out of bounds");

            return get2LE(data, num * 2);
        }

        Int16 getSShort(UInt32 num)
        {
            if (type != TiffDataType.TIFF_SSHORT && type != TiffDataType.TIFF_UNDEFINED)
                TiffParserException.ThrowTPE("TIFF, getSShort: Wrong type " + type + " encountered. Expected Short or Undefined on " + tag);

            if (num * 2 + 1 >= bytesize)
                TiffParserException.ThrowTPE("TIFF, getSShort: Trying to read out of bounds");

            return (Int16)get2LE(data, num * 2);
        }

        UInt32 getInt(UInt32 num)
        {
            if (type == TiffDataType.TIFF_SHORT) return getShort(num);
            if (!(type == TiffDataType.TIFF_LONG || type == TiffDataType.TIFF_OFFSET || type == TiffDataType.TIFF_BYTE || type == TiffDataType.TIFF_UNDEFINED
                || type == TiffDataType.TIFF_RATIONAL || type == TiffDataType.TIFF_SRATIONAL))
                TiffParserException.ThrowTPE("TIFF, getInt: Wrong type " + type + " encountered. Expected Long, Offset, Rational or Undefined on " + tag);

            if (num * 4 + 3 >= bytesize)
                TiffParserException.ThrowTPE("TIFF, getInt: Trying to read out of bounds");

            return get4LE(data, num * 4);
        }

        Int32 getSInt(UInt32 num)
        {
            if (type == TiffDataType.TIFF_SSHORT) return getSShort(num);
            if (!(type == TiffDataType.TIFF_SLONG || type == TiffDataType.TIFF_UNDEFINED))
                TiffParserException.ThrowTPE("TIFF, getSInt: Wrong type " + type + " encountered. Expected SLong or Undefined on " + tag);

            if (num * 4 + 3 >= bytesize)
                TiffParserException.ThrowTPE("TIFF, getSInt: Trying to read out of bounds");

            return get4LE(data, num * 4);
        }

        void getShortArray(UInt16[] array, UInt32 num)
        {
            for (UInt32 i = 0; i < num; i++)
                array[i] = getShort(i);
        }

        void getIntArray(UInt32[] array, UInt32 num)
        {
            for (UInt32 i = 0; i < num; i++)
                array[i] = getInt(i);
        }

        void getFloatArray(float[] array, UInt32 num)
        {
            for (UInt32 i = 0; i < num; i++)
                array[i] = getFloat(i);
        }

        bool isFloat()
        {
            return (type == TiffDataType.TIFF_FLOAT || type == TiffDataType.TIFF_DOUBLE || type == TiffDataType.TIFF_RATIONAL ||
                     type == TiffDataType.TIFF_SRATIONAL || type == TiffDataType.TIFF_LONG || type == TiffDataType.TIFF_SLONG ||
                     type == TiffDataType.TIFF_SHORT || type == TiffDataType.TIFF_SSHORT);
        }

        float getFloat(UInt32 num)
        {
            if (!isFloat())
                TiffParserException.ThrowTPE("TIFF, getFloat: Wrong type " + type + " encountered. Expected Float or something convertible on " + tag);

            if (type == TiffDataType.TIFF_DOUBLE)
            {
                if (num * 8 + 7 >= bytesize)
                    TiffParserException.ThrowTPE("TIFF, getFloat: Trying to read out of bounds");
                return (float)get8LE(data, num * 8);
            }
            else if (type == TiffDataType.TIFF_FLOAT)
            {
                if (num * 4 + 3 >= bytesize)
                    TiffParserException.ThrowTPE("TIFF, getFloat: Trying to read out of bounds");
                return (float)get4LE(data, num * 4);
            }
            else if (type == TiffDataType.TIFF_LONG || type == TiffDataType.TIFF_SHORT)
            {
                return (float)getInt(num);
            }
            else if (type == TiffDataType.TIFF_SLONG || type == TiffDataType.TIFF_SSHORT)
            {
                return (float)getSInt(num);
            }
            else if (type == TiffDataType.TIFF_RATIONAL)
            {
                UInt32 a = getInt(num * 2);
                UInt32 b = getInt(num * 2 + 1);
                if (b != 0)
                    return (float)a / b;
            }
            else if (type == TiffDataType.TIFF_SRATIONAL)
            {
                int a = (int)getInt(num * 2);
                int b = (int)getInt(num * 2 + 1);
                if (b != 0)
                    return (float)a / b;
            }
            return 0.0f;
        }

        unsafe string getString()
        {
            if (type != TiffDataType.TIFF_ASCII && type != TiffDataType.TIFF_BYTE)
                TiffParserException.ThrowTPE("TIFF, getString: Wrong type " + type + " encountered. Expected Ascii or Byte");

            if (count == 0)
                return "";

            if (own_data == null)
            {
                own_data = new byte[count];
                memcpy(own_data, data, count);
                own_data[count - 1] = 0;  // Ensure string is not larger than count defines
            }
            return new String((char*)own_data[0]);
        }

        bool isString()
        {
            return (type == TiffDataType.TIFF_ASCII);
        }

        int getElementSize()
        {
            return (int)Data.datasizes[(int)type];
        }

        int getElementShift()
        {
            return (int)Data.datashifts[(int)type];
        }

        unsafe void setData(void* in_data, UInt32 byte_count)
        {
            UInt32 bytesize = count << (int)Data.datashifts[(int)type];
            if (byte_count > bytesize)
                TiffParserException.ThrowTPE("TIFF, data set larger than entry size given");

            if (own_data == null)
            {
                own_data = new byte[bytesize];
                memcpy(own_data, data, bytesize);
            }
            memcpy(own_data, in_data, byte_count);
        }

        byte[] getDataWrt()
        {
            if (own_data == null)
            {
                own_data = new byte[bytesize];
                memcpy(own_data, data, bytesize);
            }
            return own_data;
        }

        string getValueAsString()
        {
            if (type == TiffDataType.TIFF_ASCII)
                return new String((char*)&data);
            char[] temp_string = new char[4096];
            if (count == 1)
            {
                switch (type)
                {
                    case TiffDataType.TIFF_LONG:
                        Debug.Write(temp_string + "Long: %u (0x%x)" + getInt() + getInt());
                        break;
                    case TiffDataType.TIFF_SHORT:
                        Debug.Write(temp_string + "Short: %u (0x%x)" + getInt() + getInt());
                        break;
                    case TiffDataType.TIFF_BYTE:
                        Debug.Write(temp_string + "Byte: %u (0x%x)" + getInt() + getInt());
                        break;
                    case TiffDataType.TIFF_FLOAT:
                        Debug.Write(temp_string + "Float: %f" + getFloat());
                        break;
                    case TiffDataType.TIFF_RATIONAL:
                    case TiffDataType.TIFF_SRATIONAL:
                        Debug.Write(temp_string + "Rational Number: %f" + getFloat());
                        break;
                    default:
                        Debug.Write(temp_string + "Type: %x: " + type);
                        for (UInt32 i = 0; i < Data.datasizes[(int)type]; i++)
                        {
                            Debug.Write(temp_string[temp_string.Length - 1] + data[i]);
                        }
                        break;
                }
            }
            return new String(temp_string);
        }
    }
}
