using System.IO;
using System;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace RawNet.Format.TIFF
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
        public TagType TagId { get; set; }
        public TiffDataType dataType;
        public uint dataCount;
        public uint dataOffset;
        public object[] data;
        internal int parent_offset;

        public string DataAsString
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
                            //remove \0 if any
                            if ((byte)data[dataCount - 1] == 0) data[dataCount - 1] = (byte)' ';
                            temp = Encoding.ASCII.GetString(data.Cast<byte>().ToArray());
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

        public Tag(TagType id, TiffDataType type, uint count)
        {
            TagId = id;
            dataType = type;
            dataCount = count;
            data = new Object[dataCount];
        }

        public Tag(TIFFBinaryReader fileStream, int baseOffset)
        {
            parent_offset = baseOffset;
            TagId = (TagType)fileStream.ReadUInt16();
            dataType = (TiffDataType)fileStream.ReadUInt16();
            if (TagId == TagType.FUJI_RAW_IFD)
            {
                if (dataType == TiffDataType.OFFSET) // FUJI - correct type
                    dataType = TiffDataType.LONG;
            }

            dataCount = fileStream.ReadUInt32();
            dataOffset = 0;
            if (((dataCount * GetTypeSize(dataType) > 4)))
            {
                dataOffset = fileStream.ReadUInt32();
            }

            //Get the tag data
            data = new Object[dataCount];
            long firstPosition = fileStream.Position;

            if (dataOffset < fileStream.BaseStream.Length && TagId != TagType.MAKERNOTE && TagId != TagType.MAKERNOTE_ALT)
            {
                if (dataOffset > 1)
                {
                    fileStream.Position = dataOffset + baseOffset;
                }

                try
                {
                    for (int j = 0; j < dataCount; j++)
                    {
                        switch (dataType)
                        {
                            case TiffDataType.BYTE:
                            case TiffDataType.UNDEFINED:
                            case TiffDataType.ASCII:
                                data[j] = fileStream.ReadByte();
                                break;
                            case TiffDataType.SHORT:
                                data[j] = fileStream.ReadUInt16();
                                break;
                            case TiffDataType.LONG:
                            case TiffDataType.OFFSET:
                                data[j] = fileStream.ReadUInt32();
                                break;
                            case TiffDataType.SBYTE:
                                data[j] = fileStream.ReadSByte();
                                break;
                            case TiffDataType.SSHORT:
                                data[j] = fileStream.ReadInt16();
                                //if ( dataOffset == 0 &&  dataCount == 1) fileStream.ReadInt16();
                                break;
                            case TiffDataType.SLONG:
                                data[j] = fileStream.ReadInt32();
                                break;
                            case TiffDataType.SRATIONAL:
                            case TiffDataType.RATIONAL:
                                //Because the nikonmakernote is broken with the tag 0x19 wich is double but offset of zero.                              
                                if (dataOffset == 0)
                                {
                                    data[j] = .0;
                                }
                                else
                                {
                                    data[j] = fileStream.ReadRational();
                                }
                                break;
                            case TiffDataType.FLOAT:
                                data[j] = fileStream.ReadSingle();
                                break;
                            case TiffDataType.DOUBLE:
                                data[j] = fileStream.ReadDouble();
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error " + e.Message + " while reading IFD tag: " + TagId.ToString());
                }
            }
            if (dataOffset > 1)
            {
                fileStream.BaseStream.Position = firstPosition;
            }
            else if (dataOffset == 0)
            {
                int k = (int)dataCount * GetTypeSize(dataType);
                if (k < 4)
                    fileStream.ReadBytes(4 - k);
            }
        }

        protected static int GetTypeSize(TiffDataType id)
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

        internal void GetShortArray(out ushort[] array, int count)
        {
            array = new ushort[count];
            for (int i = 0; i < count; i++)
                array[i] = Convert.ToUInt16(data[i]);
        }

        internal void GetIntArray(out int[] array, int count)
        {
            array = new int[count];
            for (int i = 0; i < count; i++)
                array[i] = Convert.ToInt32(data[i]);
        }

        internal void GetFloatArray(out float[] array, int count)
        {
            array = new float[count];
            for (int i = 0; i < count; i++)
                array[i] = Convert.ToSingle(data[i]);
        }

        internal byte[] GetByteArray()
        {
            byte[] array = new byte[dataCount];
            for (int i = 0; i < dataCount; i++)
                array[i] = Convert.ToByte(data[i]);
            return array;
        }

        internal short GetShort(int pos) { return Convert.ToInt16(data[pos]); }

        internal ushort GetUShort(int pos) { return Convert.ToUInt16(data[pos]); }

        internal int GetInt(int pos) { return Convert.ToInt32(data[pos]); }

        internal uint GetUInt(int pos) { return Convert.ToUInt32(data[pos]); }

        internal double GetDouble(int pos) { return Convert.ToDouble(data[pos]); }

        internal float GetFloat(int pos) { return Convert.ToSingle(data[pos]); }

        public void WriteToStream(Stream s, long offset)
        {
            throw new NotImplementedException();
        }

        internal uint Get4LE(uint pos)
        {
            return ((((uint)(data)[pos + 3]) << 24) | (((uint)(data)[pos + 2]) << 16) | (((uint)(data)[pos + 1]) << 8) | ((uint)(data)[pos]));
        }

        internal ushort Get2BE(uint pos) { return (ushort)(((ushort)(data)[pos] << 8) | (ushort)(data)[pos + 1]); }

        internal ushort Get2LE(uint pos)
        {
            return (ushort)((((ushort)(data)[pos + 1]) << 8) | ((ushort)(data)[pos]));
        }

        internal uint Get4BE(uint pos)
        {
            return ((((uint)(data)[pos + 0]) << 24) | (((uint)(data)[pos + 1]) << 16) | (((uint)(data)[pos + 2]) << 8) | ((uint)(data)[pos + 3]));
        }

        internal UInt64 Get8LE(uint pos)
        {
            return ((((UInt64)(data)[pos + 7]) << 56) | (((UInt64)(data)[pos + 6]) << 48) | (((UInt64)(data)[pos + 5]) << 40) |
      (((UInt64)(data)[pos + 4]) << 32) |
      (((UInt64)(data)[pos + 3]) << 24) |
      (((UInt64)(data)[pos + 2]) << 16) |
      (((UInt64)(data)[pos + 1]) << 8) |
       ((UInt64)(data)[pos]));
        }

        internal UInt64 Get8BE(uint pos)
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
