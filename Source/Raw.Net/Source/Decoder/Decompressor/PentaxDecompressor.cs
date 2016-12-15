using System;

namespace RawNet
{

    class PentaxDecompressor : LJpegDecompressor
    {
        BitPumpMSB pentaxBits;
        public PentaxDecompressor(TIFFBinaryReader file, RawImage img) : base(file, img) { }

        public void DecodePentax(IFD root, UInt32 offset, UInt32 size)
        {
            // Prepare huffmann table              0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 = 16 entries
            byte[] pentax_tree =  { 0, 2, 3, 1, 1, 1, 1, 1, 1, 2, 0, 0, 0, 0, 0, 0,
                                         3, 4, 2, 5, 1, 6, 0, 7, 8, 9, 10, 11, 12
                                       };
            //                                     0 1 2 3 4 5 6 7 8 9  0  1  2 = 13 entries
            HuffmanTable dctbl1 = huff[0];

            /* Attempt to read huffman table, if found in makernote */

            Tag t = root.GetEntryRecursive((TagType)0x220);
            if (t != null)
            {
                if (t.dataType == TiffDataType.UNDEFINED)
                {
                    TIFFBinaryReader stream;
                    if (root.endian == Common.GetHostEndianness())
                        stream = new TIFFBinaryReader(t.GetByteArray());
                    else
                        stream = new TIFFBinaryReaderRE(t.GetByteArray());

                    UInt32 depth = (uint)(stream.ReadUInt16() + 12) & 0xf;

                    stream.ReadBytes(12);
                    UInt32[] v0 = new UInt32[16];
                    UInt32[] v1 = new UInt32[16];
                    UInt32[] v2 = new UInt32[16];
                    for (UInt32 i = 0; i < depth; i++)
                        v0[i] = stream.ReadUInt16();

                    for (UInt32 i = 0; i < depth; i++)
                        v1[i] = stream.ReadByte();

                    /* Reset bits */
                    for (UInt32 i = 0; i < 17; i++)
                        dctbl1.bits[i] = 0;

                    /* Calculate codes and store bitcounts */
                    for (UInt32 c = 0; c < depth; c++)
                    {
                        v2[c] = v0[c] >> (int)(12 - v1[c]);
                        dctbl1.bits[v1[c]]++;
                    }
                    /* Find smallest */
                    for (UInt32 i = 0; i < depth; i++)
                    {
                        UInt32 sm_val = 0xfffffff;
                        UInt32 sm_num = 0xff;
                        for (UInt32 j = 0; j < depth; j++)
                        {
                            if (v2[j] <= sm_val)
                            {
                                sm_num = j;
                                sm_val = v2[j];
                            }
                        }
                        dctbl1.huffval[i] = sm_num;
                        v2[sm_num] = 0xffffffff;
                    }
                    stream.Dispose();
                }
                else
                {
                    throw new RawDecoderException("PentaxDecompressor: Unknown Huffman table type.");
                }
            }
            else
            {
                /* Initialize with legacy data */
                UInt32 acc = 0;
                for (UInt32 i = 0; i < 16; i++)
                {
                    dctbl1.bits[i + 1] = pentax_tree[i];
                    acc += dctbl1.bits[i + 1];
                }
                dctbl1.bits[0] = 0;
                for (UInt32 i = 0; i < acc; i++)
                {
                    dctbl1.huffval[i] = pentax_tree[i + 16];
                }
            }
            UseBigtable = true;
            CreateHuffmanTable(dctbl1);

            input.BaseStream.Position = 0;
            pentaxBits = new BitPumpMSB(ref input, offset, size);
            unsafe
            {
                fixed (ushort* tt = mRaw.rawData)
                {
                    byte* draw = (byte*)tt;
                    UInt16* dest;
                    Int32 w = mRaw.dim.width;
                    Int32 h = mRaw.dim.height;
                    int[] pUp1 = { 0, 0 };
                    int[] pUp2 = { 0, 0 };
                    int pLeft1 = 0;
                    int pLeft2 = 0;

                    for (UInt32 y = 0; y < h; y++)
                    {
                        pentaxBits.checkPos();
                        dest = (UInt16*)&draw[y * mRaw.pitch];  // Adjust destination
                        pUp1[y & 1] += HuffDecodePentax();
                        pUp2[y & 1] += HuffDecodePentax();
                        dest[0] = (ushort)(pLeft1 = pUp1[y & 1]);
                        dest[1] = (ushort)(pLeft2 = pUp2[y & 1]);
                        for (UInt32 x = 2; x < w; x += 2)
                        {
                            pLeft1 += HuffDecodePentax();
                            pLeft2 += HuffDecodePentax();
                            dest[x] = (ushort)pLeft1;
                            dest[x + 1] = (ushort)pLeft2;
                            //Debug.Assert(pLeft1 >= 0 && pLeft1 <= (65536));
                            //Debug.Assert(pLeft2 >= 0 && pLeft2 <= (65536));
                        }
                    }
                }
            }
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
        int HuffDecodePentax()
        {
            int rv;
            uint l, code, temp;
            int val;

            HuffmanTable dctbl1 = huff[0];
            /*
            * If the huffman code is less than 8 bits, we can use the fast
            * table lookup to get its value.  It's more than 8 bits about
            * 3-4% of the time.
            */
            pentaxBits.fill();
            code = pentaxBits.peekBitsNoFill(14);
            val = dctbl1.bigTable[code];
            if ((val & 0xff) != 0xff)
            {
                pentaxBits.skipBitsNoFill((uint)(val & 0xff));
                return val >> 8;
            }

            rv = 0;
            code = pentaxBits.peekByteNoFill();
            val = (int)dctbl1.numbits[code];
            l = (uint)val & 15;
            if (l != 0)
            {
                pentaxBits.skipBitsNoFill(l);
                rv = val >> 4;
            }
            else
            {
                pentaxBits.skipBits(8);
                l = 8;
                while (code > dctbl1.maxcode[l])
                {
                    temp = pentaxBits.getBitNoFill();
                    code = (code << 1) | temp;
                    l++;
                }

                /*
                * With garbage input we may reach the sentinel value l = 17.
                */

                if (l > 16)
                {
                    throw new RawDecoderException("Corrupt JPEG data: bad Huffman code:" + l);
                }
                else
                {
                    rv = (int)dctbl1.huffval[dctbl1.valptr[l] + ((int)(code - dctbl1.minCode[l]))];
                }
            }

            if (rv == 16)
                return -32768;

            /*
            * Section F.2.2.1: decode the difference and
            * Figure F.12: extend sign bit
            */

            if (rv != 0)
            {
                int x = (int)pentaxBits.getBits((uint)rv);
                if ((x & (1 << (rv - 1))) == 0)
                    x -= (1 << rv) - 1;
                return x;
            }
            return 0;
        }

    }
}
