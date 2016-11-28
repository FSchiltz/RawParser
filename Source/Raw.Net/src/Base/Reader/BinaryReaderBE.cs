using System;
using System.IO;
using System.Text;

namespace RawNet
{
    public enum Endianness
    {
        big, little, unknown
    };

    public class TIFFBinaryReader : BinaryReader
    {
        private uint offset;
        private uint count;

        public TIFFBinaryReader(Stream s) : base(s) { }
        public TIFFBinaryReader(Stream s, Encoding e) : base(s, e) { }
        public TIFFBinaryReader(Stream s, uint offset, uint count) : base(s)
        {
            this.offset = offset;
            s.Position = offset;
            this.count = count;
        }

        public static Stream streamFromArray(object[] array, TiffDataType type)
        {
            byte[] temp;
            //get type of array
            switch (type)
            {
                case TiffDataType.TIFF_BYTE: /* 8-bit unsigned integer */
                case TiffDataType.TIFF_ASCII: /* 8-bit bytes w/ last byte null */
                case TiffDataType.TIFF_UNDEFINED: /* !8-bit untyped data */
                case TiffDataType.TIFF_SBYTE: /* !8-bit signed integer */
                    temp = new byte[array.Length];
                    for (int i = 0; i < array.Length; i++) temp[i] = (byte)array[i];
                    break;
                case TiffDataType.TIFF_SHORT: /* 16-bit unsigned integer */
                case TiffDataType.TIFF_SSHORT: /* !16-bit signed integer */
                    temp = new byte[array.Length * 2];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = (byte)((int)array[i] >> 8);
                        temp[i + 1] = (byte)((int)array[i]);
                    }
                    break;
                case TiffDataType.TIFF_LONG: /* 32-bit unsigned integer */
                case TiffDataType.TIFF_OFFSET: /* 32-bit unsigned offset used in ORF at least */
                case TiffDataType.TIFF_FLOAT: /* !32-bit IEEE floating point */
                case TiffDataType.TIFF_SLONG: /* !32-bit signed integer */
                    temp = new byte[array.Length * 4];
                    for (int i = 0; i < temp.Length; i++)
                    {
                        temp[i] = (byte)((int)array[i] >> 24);
                        temp[i + 1] = (byte)((int)array[i] >> 16);
                        temp[i + 2] = (byte)((int)array[i] >> 8);
                        temp[i + 3] = (byte)((int)array[i]);
                    }
                    break;
                case TiffDataType.TIFF_SRATIONAL:/* !64-bit signed fraction */
                case TiffDataType.TIFF_DOUBLE: /* !64-bit IEEE floating point */
                case TiffDataType.TIFF_RATIONAL: /* 64-bit unsigned fraction */
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
                    throw new Exception();
                    break;
            }

            Stream stream = new MemoryStream(temp);
            stream.Position = 0;
            return stream;
        }

        public long Position
        {
            get { return this.BaseStream.Position; }
            set { this.BaseStream.Position = value + offset; }
        }

        public void skipToMarker()
        {
            while (!(this.ReadByte() == 0xFF && this.PeekChar() != 0 && this.PeekChar() != 0xFF))
            {
                if (this.Position >= this.BaseStream.Length)
                    throw new IOException("No marker found inside rest of buffer");
            }
        }

        public int getRemainSize() { return (int)(this.BaseStream.Length - this.Position); }
        public override double ReadDouble()
        {
            byte[] part1 = base.ReadBytes(4);
            byte[] part2 = base.ReadBytes(4);
            double d1 = BitConverter.ToInt32(part1, 0);
            double d2 = BitConverter.ToInt32(part2, 0);
            return d1 / d2;
        }

        public ushort readUshortFromArray(ref byte[] array, int offset)
        {
            return BitConverter.ToUInt16(new byte[2] { array[offset], array[offset + 1] }, 0);
        }

        public short readshortFromArray(ref byte[] array, int offset)
        {
            return BitConverter.ToInt16(new byte[2] { array[offset], array[offset + 1] }, 0);
        }

        public ushort readUshortFromArrayC(ref object[] array, int offset)
        {
            return BitConverter.ToUInt16(new byte[2] { (byte)array[offset], (byte)array[offset + 1] }, 0);
        }

        public short readshortFromArrayC(ref object[] array, int offset)
        {
            return BitConverter.ToInt16(new byte[2] { (byte)array[offset], (byte)array[offset + 1] }, 0);
        }

        public bool isValid(uint offset, uint count)
        {
            return (offset <= this.offset + count);
        }

        public bool isValid(uint offset)
        {
            return offset < this.BaseStream.Length;
        }
    }

    public class TIFFBinaryReaderRE : TIFFBinaryReader
    {
        public TIFFBinaryReaderRE(Stream s) : base(s) { }
        public TIFFBinaryReaderRE(Stream s, Encoding e) : base(s, e) { }
        public TIFFBinaryReaderRE(Stream s, uint o, uint c) : base(s, o, c) { }

        public override ushort ReadUInt16()
        {
            byte[] temp = new byte[2];
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToUInt16(temp, 0);
        }

        public override uint ReadUInt32()
        {
            byte[] temp = new byte[4];
            temp[3] = base.ReadByte();
            temp[2] = base.ReadByte();
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToUInt32(temp, 0);
        }

        public override short ReadInt16()
        {
            byte[] temp = new byte[2];
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToInt16(temp, 0);
        }

        public override int ReadInt32()
        {
            byte[] temp = new byte[4];
            temp[3] = base.ReadByte();
            temp[2] = base.ReadByte();
            temp[1] = base.ReadByte();
            temp[0] = base.ReadByte();
            return BitConverter.ToInt32(temp, 0);
        }

        public override double ReadDouble()
        {
            byte[] part1 = new byte[4];
            part1[3] = base.ReadByte();
            part1[2] = base.ReadByte();
            part1[1] = base.ReadByte();
            part1[0] = base.ReadByte();

            byte[] part2 = new byte[4];
            part2[3] = base.ReadByte();
            part2[2] = base.ReadByte();
            part2[1] = base.ReadByte();
            part2[0] = base.ReadByte();
            double d1 = BitConverter.ToInt32(part1, 0);
            double d2 = BitConverter.ToInt32(part2, 0);
            return d1 / d2;
        }

        public new ushort readUshortFromArray(ref byte[] array, int offset)
        {
            return BitConverter.ToUInt16(new byte[2] { array[offset + 1], array[offset] }, 0);
        }

        public new short readshortFromArray(ref byte[] array, int offset)
        {
            return BitConverter.ToInt16(new byte[2] { array[offset + 1], array[offset] }, 0);
        }

        public new ushort readUshortFromArrayC(ref object[] array, int offset)
        {
            return BitConverter.ToUInt16(new byte[2] { (byte)array[offset + 1], (byte)array[offset] }, 0);
        }

        public new short readshortFromArrayC(ref object[] array, int offset)
        {
            return BitConverter.ToInt16(new byte[2] { (byte)array[offset + 1], (byte)array[offset] }, 0);
        }
    }
}
