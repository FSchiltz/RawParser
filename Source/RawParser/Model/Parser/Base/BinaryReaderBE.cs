﻿using RawNet.Format.TIFF;
using System;
using System.IO;
using System.Text;

namespace RawNet
{
    public class TIFFBinaryReader : BinaryReader
    {
        private uint offset;
        public long Position
        {
            get { return BaseStream.Position; }
            set { BaseStream.Position = value + offset; }
        }

        public TIFFBinaryReader(Stream data) : base(data, Encoding.ASCII) { }
        public TIFFBinaryReader(Stream data, uint offset) : base(data, Encoding.ASCII)
        {
            this.offset = offset;
            data.Position = offset;
        }

        public TIFFBinaryReader(byte[] data, uint offset) : this(StreamFromArray(data), offset) { }
        public TIFFBinaryReader(byte[] data) : this(StreamFromArray(data)) { }

        public TIFFBinaryReader(object[] data, TiffDataType dataType) : this(StreamFromArray(data, dataType)) { }
        public TIFFBinaryReader(object[] data, TiffDataType dataType, uint offset) : this(StreamFromArray(data, dataType), offset) { }

        public void SkipToMarker()
        {
            byte[] buffer = ReadBytes(2);
            BaseStream.Position -= 1;
            while (!(buffer[0] == 0xFF && buffer[1] != 0 && buffer[1] != 0xFF))
            {
                buffer = ReadBytes(2);
                BaseStream.Position -= 1;
                if (this.Position >= this.BaseStream.Length)
                    throw new IOException("No marker found inside rest of buffer");
            }
            BaseStream.Position -= 1;
        }

        public int RemainingSize { get { return (int)(BaseStream.Length - Position); } }

        public virtual double ReadRational()
        {
            /* byte[] part1 = ; (4);
             byte[] part2 = base.ReadBytes(4);
             double d1 = BitConverter.ToInt32(part1, 0);
             double d2 = BitConverter.ToInt32(part2, 0);*/
            return base.ReadInt32() / (double)base.ReadInt32();
        }       

        public bool IsValid(uint offset, uint count)
        {
            return offset + count <= this.BaseStream.Length;
        }

        public bool IsValid(uint offset)
        {
            return offset <= this.BaseStream.Length;
        }

        protected static Stream StreamFromArray(byte[] data)
        {
            Stream stream = new MemoryStream(data)
            {
                Position = 0
            };
            return stream;
        }

        protected static Stream StreamFromArray(object[] array, TiffDataType type)
        {
            byte[] temp;
            //get type of array
            switch (type)
            {
                case TiffDataType.BYTE: /* 8-bit unsigned integer */
                case TiffDataType.ASCII: /* 8-bit bytes w/ last byte null */
                case TiffDataType.UNDEFINED: /* !8-bit untyped data */
                case TiffDataType.SBYTE: /* !8-bit signed integer */
                    temp = new byte[array.Length];
                    for (int i = 0; i < array.Length; i++) temp[i] = (byte)array[i];
                    break;
                case TiffDataType.SHORT: /* 16-bit unsigned integer */
                case TiffDataType.SSHORT: /* !16-bit signed integer */
                    temp = new byte[array.Length * 2];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = (byte)((int)array[i] >> 8);
                        temp[i + 1] = (byte)((int)array[i]);
                    }
                    break;
                case TiffDataType.LONG: /* 32-bit unsigned integer */
                case TiffDataType.OFFSET: /* 32-bit unsigned offset used in ORF at least */
                case TiffDataType.FLOAT: /* !32-bit IEEE floating point */
                case TiffDataType.SLONG: /* !32-bit signed integer */
                    temp = new byte[array.Length * 4];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = (byte)((int)array[i] >> 24);
                        temp[i + 1] = (byte)((int)array[i] >> 16);
                        temp[i + 2] = (byte)((int)array[i] >> 8);
                        temp[i + 3] = (byte)((int)array[i]);
                    }
                    break;
                case TiffDataType.SRATIONAL:/* !64-bit signed fraction */
                case TiffDataType.DOUBLE: /* !64-bit IEEE floating point */
                case TiffDataType.RATIONAL: /* 64-bit unsigned fraction */
                    temp = new byte[array.Length * 8];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = (byte)((int)array[i] >> 56);
                        temp[i + 1] = (byte)((int)array[i] >> 48);
                        temp[i + 2] = (byte)((int)array[i] >> 40);
                        temp[i + 3] = (byte)((int)array[i] >> 32);
                        temp[i + 4] = (byte)((int)array[i] >> 24);
                        temp[i + 5] = (byte)((int)array[i] >> 16);
                        temp[i + 6] = (byte)((int)array[i] >> 8);
                        temp[i + 7] = (byte)((int)array[i]);
                    }
                    break;
                default:
                    throw new IOException();
            }

            Stream stream = new MemoryStream(temp)
            {
                Position = 0
            };
            return stream;
        }
    }

    public class TIFFBinaryReaderRE : TIFFBinaryReader
    {
        public TIFFBinaryReaderRE(Stream data) : base(data) { }
        public TIFFBinaryReaderRE(Stream data, uint offset) : base(data, offset) { }
        public TIFFBinaryReaderRE(byte[] data, uint offset) : base(StreamFromArray(data), offset) { }
        public TIFFBinaryReaderRE(byte[] data) : base(StreamFromArray(data)) { }
        public TIFFBinaryReaderRE(object[] data, TiffDataType dataType) : base(StreamFromArray(data, dataType)) { }

        public override ushort ReadUInt16()
        {
            byte[] temp = new byte[2];
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToUInt16(temp, 0);
            //return (ushort)(ReadByte() | ReadByte() << 8);
        }

        public override uint ReadUInt32()
        {
            byte[] temp = new byte[4];
            temp[3] = base.ReadByte();
            temp[2] = base.ReadByte();
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToUInt32(temp, 0);

            // return (uint)(ReadByte() | ReadByte() << 8 | ReadByte() << 16 | ReadByte() << 24);
        }

        public override short ReadInt16()
        {
            byte[] temp = new byte[2];
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToInt16(temp, 0);
            //return (short)(ReadByte() | ReadByte() << 8);
        }

        public override int ReadInt32()
        {
            byte[] temp = new byte[4];
            temp[3] = base.ReadByte();
            temp[2] = base.ReadByte();
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToInt32(temp, 0);

            // return ReadByte() | ReadByte() << 8 | ReadByte() << 16 | ReadByte() << 24;
        }

        public override double ReadRational()
        {
            return ReadInt32() / (double)ReadInt32();
        }

        public override float ReadSingle()
        {
            byte[] temp = new byte[4];
            temp[3] = base.ReadByte();
            temp[2] = base.ReadByte();
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToSingle(temp, 0);
        }

        public override double ReadDouble()
        {
            byte[] temp = new byte[8];
            for (int i = 7; i >= 0; i--)
            {
                temp[i] = base.ReadByte();
            }
            return BitConverter.ToDouble(temp, 0);
        }
    }
}
