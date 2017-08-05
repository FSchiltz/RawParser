using System.IO;
using System;
using System.Text;
using System.Linq;
using PhotoNet.Common;

namespace RawNet.Format.Tiff
{
    static internal class Data
    {
        public static uint[] datasizes = { 0, 1, 1, 2, 4, 8, 1, 1, 2, 4, 8, 4, 8, 4 };
        // 0-1-2-3-4-5-6-7-8-9-10-11-12-13
        public static uint[] datashifts = { 0, 0, 0, 1, 2, 3, 0, 0, 1, 2, 3, 2, 3, 2 };
        // 0-1-2-3-4-5-6-7-8-9-10-11-12-13
    }

    internal class Tag
    {
        public TagType TagId { get; set; }
        public TiffDataType dataType;
        public uint dataCount;
        public uint dataOffset;
        public object[] data;
        internal int parent_offset;

        public Tag(TagType id, TiffDataType type, uint count)
        {
            TagId = id;
            dataType = type;
            dataCount = count;
            data = new Object[dataCount];
        }

        public Tag(ImageBinaryReader fileStream, int baseOffset)
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
            if (((dataCount * dataType.GetTypeSize() > 4)))
            {
                dataOffset = fileStream.ReadUInt32();
            }
            else
            {
                GetData(fileStream);
                int k = (int)dataCount * dataType.GetTypeSize();
                if (k < 4)
                    fileStream.ReadBytes(4 - k);
            }
            /*            if (dataOffset < fileStream.BaseStream.Length && dataOffset > 1)
            {
                long firstPosition = fileStream.Position;
                fileStream.Position = dataOffset + parent_offset;
                GetData(fileStream);
                fileStream.BaseStream.Position = firstPosition;
            ¨*/
        }

        public void ReadData(ImageBinaryReader fileStream)
        {
            if (data == null && dataOffset + parent_offset < fileStream.BaseStream.Length && dataOffset > 1)
            {
                long firstPosition = fileStream.Position;
                fileStream.Position = dataOffset + parent_offset;
                GetData(fileStream);
                fileStream.BaseStream.Position = firstPosition;
            }
        }

        private void GetData(ImageBinaryReader fileStream)
        {
            data = new Object[dataCount];
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
                                temp += Convert.ToChar(t);
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

        internal ushort[] GetUShortArray()
        {
            var array = new ushort[data.Length];
            for (int i = 0; i < data.Length; i++)
                array[i] = Convert.ToUInt16(data[i]);
            return array;
        }

        internal int[] GetIntArray()
        {
            var array = new int[data.Length];
            for (int i = 0; i < data.Length; i++)
                array[i] = Convert.ToInt32(data[i]);
            return array;
        }

        internal uint[] GetUIntArray()
        {
            var array = new uint[data.Length];
            for (int i = 0; i < data.Length; i++)
                array[i] = Convert.ToUInt32(data[i]);
            return array;
        }

        internal float[] GetFloatArray()
        {
            var array = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
                array[i] = Convert.ToSingle(data[i]);
            return array;
        }

        internal byte[] GetByteArray()
        {
            byte[] array = new byte[dataCount];
            for (int i = 0; i < dataCount; i++)
                array[i] = Convert.ToByte(data[i]);
            return array;
        }

        internal double[] GetAsDoubleArray()
        {
            var array = new double[data.Length];
            for (int i = 0; i < data.Length; i++)
                array[i] = Convert.ToDouble(data[i]);
            return array;
        }

        internal byte GetByte(int pos) { return Convert.ToByte(data[pos]); }

        internal sbyte GetSByte(int pos) { return Convert.ToSByte(data[pos]); }

        internal short GetShort(int pos) { return Convert.ToInt16(data[pos]); }

        internal ushort GetUShort(int pos) { return Convert.ToUInt16(data[pos]); }

        internal int GetInt(int pos) { return Convert.ToInt32(data[pos]); }

        internal uint GetUInt(int pos) { return Convert.ToUInt32(data[pos]); }

        internal long GetLong(int pos) { return Convert.ToInt64(data[pos]); }

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

        internal ulong Get8LE(uint pos)
        {
            return ((((ulong)(data)[pos + 7]) << 56) | (((ulong)(data)[pos + 6]) << 48) | (((ulong)(data)[pos + 5]) << 40) | (((ulong)(data)[pos + 4]) << 32) |
                 (((ulong)(data)[pos + 3]) << 24) | (((ulong)(data)[pos + 2]) << 16) | (((ulong)(data)[pos + 1]) << 8) | ((ulong)(data)[pos]));
        }

        internal ulong Get8BE(uint pos)
        {
            return ((((ulong)(data)[pos + 0]) << 56) | (((ulong)(data)[pos + 1]) << 48) | (((ulong)(data)[pos + 2]) << 40) | (((ulong)(data)[pos + 3]) << 32) |
                (((ulong)(data)[pos + 4]) << 24) | (((ulong)(data)[pos + 5]) << 16) | (((ulong)(data)[pos + 6]) << 8) | ((ulong)(data)[pos + 7]));
        }
    }
}
