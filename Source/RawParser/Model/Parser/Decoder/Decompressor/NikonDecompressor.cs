using System;

namespace RawNet.Decoder.Decompressor
{
    internal class NikonDecompressor : LJpegDecompressor
    {
        private UInt16[] curve = new UInt16[65536];
        protected byte[][] nikon_tree =
            {
                    new byte[32]{ 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy */
                      5,4,3,6,2,7,1,0,8,9,11,10,12,0,0,0 },
                    new byte[32]{ 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy after split */
                      0x39,0x5a,0x38,0x27,0x16,5,4,3,2,1,0,11,12,12,0,0 },
                    new byte[32] { 0,1,4,2,3,1,2,0,0,0,0,0,0,0,0,0,  /* 12-bit lossless */
                      5,4,6,3,7,2,8,1,9,0,10,11,12,0,0,0 },
                    new byte[32]{ 0,1,4,3,1,1,1,1,1,2,0,0,0,0,0,0,	/* 14-bit lossy */
                      5,6,4,7,8,3,9,2,1,0,10,11,12,13,14,0 },
                    new byte[32]{ 0,1,5,1,1,1,1,1,1,1,2,0,0,0,0,0,	/* 14-bit lossy after split */
                      8,0x5c,0x4b,0x3a,0x29,7,6,5,4,3,2,1,0,13,14,0 },
                    new byte [32] { 0,1,4,2,2,3,1,2,0,0,0,0,0,0,0,0,	/* 14-bit lossless */
                      7,6,8,5,9,4,10,3,11,12,2,0,1,13,14,0 }
            };

        public NikonDecompressor(TIFFBinaryReader file, RawImage img) : base(file, img)
        {
            for (int i = 0; i < 0x8000; i++)
            {
                curve[i] = (ushort)i;
            }
        }

        public void InitTable(uint huffSelect)
        {
            HuffmanTable dctbl1 = huff[0];
            uint acc = 0;
            for (int i = 0; i < 16; i++)
            {
                dctbl1.bits[i + 1] = nikon_tree[huffSelect][i];
                acc += dctbl1.bits[i + 1];
            }
            dctbl1.bits[0] = 0;

            for (int i = 0; i < acc; i++)
            {
                dctbl1.huffval[i] = nikon_tree[huffSelect][i + 16];
            }
            CreateHuffmanTable(dctbl1);
        }

        public void DecompressNikon(TIFFBinaryReader metadata, uint offset, uint size)
        {
            metadata.Position = 0;
            byte v0 = metadata.ReadByte();
            byte v1 = metadata.ReadByte();
            uint huffSelect = 0;
            uint split = 0;
            int[] pUp1 = new int[2];
            int[] pUp2 = new int[2];
            UseBigtable = true;

            //_RPT2(0, "Nef version v0:%u, v1:%u\n", v0, v1);

            if (v0 == 73 || v1 == 88)
                metadata.ReadBytes(2110);

            if (v0 == 70) huffSelect = 2;
            if (raw.ColorDepth == 14) huffSelect += 3;

            pUp1[0] = metadata.ReadInt16();
            pUp1[1] = metadata.ReadInt16();
            pUp2[0] = metadata.ReadInt16();
            pUp2[1] = metadata.ReadInt16();

            int max = 1 << raw.ColorDepth & 0x7fff;
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
            InitTable(huffSelect);

            raw.whitePoint = curve[max - 1];
            raw.BlackLevel = curve[0];
            raw.SetTable(curve, max, true);

            BitPumpMSB bits = new BitPumpMSB(input, offset, size);
            int pLeft1 = 0, pLeft2 = 0;
            uint random = bits.PeekBits(24);
            for (int y = 0; y < raw.raw.dim.height; y++)
            {
                if (split != 0 && (y == split))
                {
                    InitTable(huffSelect + 1);
                }
                pUp1[y & 1] += HuffDecodeNikon(bits);
                pUp2[y & 1] += HuffDecodeNikon(bits);
                pLeft1 = pUp1[y & 1];
                pLeft2 = pUp2[y & 1];
                long dest = y * raw.raw.dim.width;
                raw.SetWithLookUp((ushort)Common.Clampbits(pLeft1, 15), raw.raw.data, dest++, ref random);
                raw.SetWithLookUp((ushort)Common.Clampbits(pLeft2, 15), raw.raw.data, dest++, ref random);
                for (int x = 1; x < raw.raw.dim.width / 2; x++)
                {
                    bits.CheckPos();
                    pLeft1 += HuffDecodeNikon(bits);
                    pLeft2 += HuffDecodeNikon(bits);
                    raw.SetWithLookUp((ushort)Common.Clampbits(pLeft1, 15), raw.raw.data, dest++, ref random);
                    raw.SetWithLookUp((ushort)Common.Clampbits(pLeft2, 15), raw.raw.data, dest++, ref random);
                }
            }
            raw.SetTable(curve, max, false);
        }

        /*
        *--------------------------------------------------------------
        *
        * HuffDecode --
        *
        * Taken from Figure F.16: extract next coded symbol from
        * input stream.  This should becode a macro.
        *
        * Results:
        * Next coded symbol
        *
        * Side effects:
        * Bitstream is parsed.
        *
        *--------------------------------------------------------------
        */
        protected int HuffDecodeNikon(BitPumpMSB bits)
        {
            int rv;
            int l, temp;
            int code, val;

            HuffmanTable dctbl1 = huff[0];

            bits.FillCheck();
            code = (int)bits.PeekBitsNoFill(14);
            val = dctbl1.bigTable[code];
            if ((val & 0xff) != 0xff)
            {
                bits.SkipBitsNoFill((uint)val & 0xff);
                return val >> 8;
            }

            rv = 0;
            code = (int)bits.PeekByteNoFill();
            val = (int)dctbl1.numbits[code];
            l = val & 15;
            if (l != 0)
            {
                bits.SkipBitsNoFill((uint)l);
                rv = val >> 4;
            }
            else
            {
                bits.SkipBits(8);
                l = 8;
                while (code > dctbl1.maxcode[l])
                {
                    temp = (int)bits.GetBitNoFill();
                    code = (code << 1) | temp;
                    l++;
                }

                if (l > 16)
                    throw new RawDecoderException("Corrupt JPEG data: bad Huffman code:" + l);
                rv = (int)dctbl1.huffval[dctbl1.valptr[l] + (code - dctbl1.minCode[l])];
            }

            if (rv == 16)
                return -32768;

            /*
            * Section F.2.2.1: decode the difference and
            * Figure F.12: extend sign bit
            */
            Int32 len = rv & 15;
            Int32 shl = rv >> 4;
            int diff = (int)((bits.GetBits((uint)(len - shl)) << 1) + 1) << shl >> 1;
            if ((diff & (1 << (len - 1))) == 0)
            {
                //TODO optimise
                if (shl == 0) shl = 1;
                else shl = 0;
                diff -= (1 << len) - shl;
            }
            return diff;
        }
    }
}
