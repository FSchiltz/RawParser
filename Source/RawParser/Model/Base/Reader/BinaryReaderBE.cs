using System;
using System.IO;
using System.Text;

namespace RawParser.Reader
{
    class TIFFBinaryReader : BinaryReader
    {
        public TIFFBinaryReader(Stream s) : base(s) { }
        public TIFFBinaryReader(Stream s, Encoding e) : base(s, e) { }

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
    }

    class TIFFBinaryReaderRE : TIFFBinaryReader
    {
        public TIFFBinaryReaderRE(Stream s) : base(s) { }
        public TIFFBinaryReaderRE(Stream s, Encoding e) : base(s, e) { }

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
