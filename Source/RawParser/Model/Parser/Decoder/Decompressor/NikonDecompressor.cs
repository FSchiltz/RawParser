using RawNet.Decoder.HuffmanCompressor;
using RawNet.Jpeg;
using System;

namespace RawNet.Decoder.Decompressor
{
    internal class NikonDecompressor : JPEGDecompressor
    {
        private UInt16[] curve = new UInt16[65536];

        public NikonDecompressor(TiffBinaryReader file, RawImage<ushort> img) : base(file, img, false, false)
        {
            huff[0] = new NikonHuffman();
            for (int i = 0; i < 0x8000; i++)
            {
                curve[i] = (ushort)i;
            }
        }

        public void DecompressNikon(TiffBinaryReader metadata, uint offset, uint size)
        {
            metadata.Position = 0;
            byte v0 = metadata.ReadByte();
            byte v1 = metadata.ReadByte();
            int huffSelect = 0;
            uint split = 0;
            int[] pUp1 = new int[2];
            int[] pUp2 = new int[2];
            huff[0].UseBigTable = true;

            if (v0 == 73 || v1 == 88)
                metadata.ReadBytes(2110);

            if (v0 == 70) huffSelect = 2;
            if (raw.raw.ColorDepth == 14) huffSelect += 3;

            pUp1[0] = metadata.ReadInt16();
            pUp1[1] = metadata.ReadInt16();
            pUp2[0] = metadata.ReadInt16();
            pUp2[1] = metadata.ReadInt16();

            int max = 1 << raw.raw.ColorDepth & 0x7fff;
            int step = 0, csize = metadata.ReadUInt16();
            if (csize > 1)
                step = max / (csize - 1);
            if (v0 == 68 && v1 == 32 && step > 0)
            {
                for (int i = 0; i < csize; i++)
                    curve[i * step] = metadata.ReadUInt16();
                for (int i = 0; i < max; i++)
                    curve[i] = (ushort)((curve[i - i % step] * (step - i % step) + curve[i - i % step + step] * (i % step)) / step);
                metadata.Position = (562);
                split = metadata.ReadUInt16();
            }
            else if (v0 != 70 && csize <= 0x4001)
            {
                for (int i = 0; i < csize; i++)
                {
                    curve[i] = metadata.ReadUInt16();
                }
                max = csize;
            }
            huff[0].Create(huffSelect);

            raw.whitePoint = curve[max - 1];
            raw.black = curve[0];
            raw.table = new TableLookUp(curve, max, true);

            huff[0].bitPump = new BitPumpMSB(input, offset, size);
            int pLeft1 = 0, pLeft2 = 0;
            uint random = huff[0].bitPump.PeekBits(24);
            for (int y = 0; y < raw.raw.dim.Height; y++)
            {
                if (split != 0 && (y == split))
                {
                    huff[0].Create(huffSelect + 1);
                }
                pUp1[y & 1] += huff[0].Decode();
                pUp2[y & 1] += huff[0].Decode();
                pLeft1 = pUp1[y & 1];
                pLeft2 = pUp2[y & 1];
                long dest = y * raw.raw.dim.Width;
                raw.SetWithLookUp((ushort)Common.Clampbits(pLeft1, 15), raw.raw.rawView, dest++, ref random);
                raw.SetWithLookUp((ushort)Common.Clampbits(pLeft2, 15), raw.raw.rawView, dest++, ref random);
                for (int x = 1; x < raw.raw.dim.Width / 2; x++)
                {
                    huff[0].bitPump.CheckPos();
                    pLeft1 += huff[0].Decode();
                    pLeft2 += huff[0].Decode();
                    raw.SetWithLookUp((ushort)Common.Clampbits(pLeft1, 15), raw.raw.rawView, dest++, ref random);
                    raw.SetWithLookUp((ushort)Common.Clampbits(pLeft2, 15), raw.raw.rawView, dest++, ref random);
                }
            }
            raw.table = null;
        }
    }
}
