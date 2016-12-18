using System;

namespace RawSpeed
{
    class TiffEntryBE : TiffEntry
    {
        TiffEntryBE(ref FileMap f, UInt32 offset, UInt32 up_offset)
        {
            parent_offset = up_offset;
            own_data = null;
            empty_data = 0;
            file = f;
            type = TiffDataType.TIFF_UNDEFINED;  // We set type to undefined to avoid debug assertion errors.

            byte[] temp_data = f.getData(offset, 8);
            tag = (TiffTag)get2BE(temp_data, 0);
            type = (TiffDataType)get2BE(temp_data, 2);
            count = get4BE(temp_data, 4);

            if ((int)type > 13)
                TiffParserException.ThrowTPE("Error reading TIFF structure. Unknown Type " + type + " encountered.");

            bytesize = (UInt64)count << (int)Data.datashifts[(int)type];
            if (bytesize > UInt32.MaxValue)
                TiffParserException.ThrowTPE("TIFF entry is supposedly " + bytesize + " bytes");

            if (bytesize == 0) // Better return empty than null-dereference later
                data = empty_data;
            else if (bytesize <= 4)
                data = f.getDataWrt(offset + 8, bytesize);
            else
            { // offset
                data_offset = get4BE(f.getData(offset + 8, 4), 0);
                data = f.getDataWrt(data_offset, bytesize);
            }
        }

        TiffEntryBE(TiffTag tag, TiffDataType type, UInt32 count, byte[] data) : base(tag, type, count, data)
        {
        }

        UInt16 getShort(UInt32 num)
        {
            if (type == TiffDataType.TIFF_BYTE) return getByte(num);
            if (type != TiffDataType.TIFF_SHORT && type != TiffDataType.TIFF_UNDEFINED)
                ThrowTPE("TIFF, getShort: Wrong type %u encountered. Expected Short or Undefined on 0x%x", type, tag);

            if (num * 2 + 1 >= bytesize)
                ThrowTPE("TIFF, getShort: Trying to read out of bounds");

            return get2BE(data, num * 2);
        }

        Int16 getSShort(UInt32 num)
        {
            if (type != TiffDataType.TIFF_SSHORT && type != TiffDataType.TIFF_UNDEFINED)
                ThrowTPE("TIFF, getSShort: Wrong type %u encountered. Expected Short or Undefined on 0x%x", type, tag);

            if (num * 2 + 1 >= bytesize)
                ThrowTPE("TIFF, getSShort: Trying to read out of bounds");

            return (Int16)get2LE(data, num * 2);
        }

        UInt32 getInt(UInt32 num)
        {
            if (type == TiffDataType.TIFF_SHORT) return getShort(num);
            if (!(type == TiffDataType.TIFF_LONG || type == TiffDataType.TIFF_OFFSET || type == TiffDataType.TIFF_BYTE
                || type == TiffDataType.TIFF_UNDEFINED || type == TiffDataType.TIFF_RATIONAL || type == TiffDataType.TIFF_SRATIONAL))
                ThrowTPE("TIFF, getInt: Wrong type %u encountered. Expected Long, Offset or Undefined on 0x%x", type, tag);

            if (num * 4 + 3 >= bytesize)
                ThrowTPE("TIFF, getInt: Trying to read out of bounds");

            return get4BE(data, num * 4);
        }

        Int32 getSInt(UInt32 num)
        {
            if (type == TiffDataType.TIFF_SSHORT) return getSShort(num);
            if (!(type == TiffDataType.TIFF_SLONG || type == TiffDataType.TIFF_UNDEFINED))
                ThrowTPE("TIFF, getSInt: Wrong type %u encountered. Expected SLong or Undefined on 0x%x", type, tag);

            if (num * 4 + 3 >= bytesize)
                ThrowTPE("TIFF, getSInt: Trying to read out of bounds");

            return get4BE(data, num * 4);
        }

        float getFloat(UInt32 num)
        {
            if (!isFloat())
                ThrowTPE("TIFF, getFloat: Wrong type 0x%x encountered. Expected Float or something convertible on 0x%x", type, tag);

            if (type == TIFF_DOUBLE)
            {
                if (num * 8 + 7 >= bytesize)
                    ThrowTPE("TIFF, getFloat: Trying to read out of bounds");
                return (float)get8BE(data, num * 8);
            }
            else if (type == TIFF_FLOAT)
            {
                if (num * 4 + 3 >= bytesize)
                    ThrowTPE("TIFF, getFloat: Trying to read out of bounds");
                return (float)get4BE(data, num * 4);
            }
            else if (type == TIFF_LONG || type == TIFF_SHORT)
            {
                return (float)getInt(num);
            }
            else if (type == TIFF_SLONG || type == TIFF_SSHORT)
            {
                return (float)getSInt(num);
            }
            else if (type == TIFF_RATIONAL)
            {
                UInt32 a = getInt(num * 2);
                UInt32 b = getInt(num * 2 + 1);
                if (b != 0)
                    return (float)a / b;
            }
            else if (type == TIFF_SRATIONAL)
            {
                int a = (int)getInt(num * 2);
                int b = (int)getInt(num * 2 + 1);
                if (b != 0)
                    return (float)a / b;
            }
            return 0.0f;
        }

        void setData(void* in_data, UInt32 byte_count)
        {
            if ((int)Data.datashifts[(int)type] != 0)
                ThrowTPE("TIFF, Unable to set data on byteswapped platforms (unsupported)");
            base.setData(in_data, byte_count);
        }
    }
}
