using RawNet.Format.TIFF;
using System;

namespace RawNet.Decoder.Decompressor
{

    class PentaxDecompressor : LJpegDecompressor
    {
        BitPumpMSB pentaxBits;
        public PentaxDecompressor(TIFFBinaryReader file, RawImage img) : base(file, img) { }

        public void DecodePentax(IFD root, uint offset, uint size)
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

                    int depth = (stream.ReadUInt16() + 12) & 0xf;

                    stream.ReadBytes(12);
                    uint[] v0 = new uint[16];
                    uint[] v1 = new uint[16];
                    uint[] v2 = new uint[16];
                    for (int i = 0; i < depth; i++)
                        v0[i] = stream.ReadUInt16();

                    for (int i = 0; i < depth; i++)
                        v1[i] = stream.ReadByte();

                    /* Reset bits */
                    for (int i = 0; i < 17; i++)
                        dctbl1.bits[i] = 0;

                    /* Calculate codes and store bitcounts */
                    for (int c = 0; c < depth; c++)
                    {
                        v2[c] = v0[c] >> (int)(12 - v1[c]);
                        dctbl1.bits[v1[c]]++;
                    }
                    /* Find smallest */
                    for (int i = 0; i < depth; i++)
                    {
                        uint sm_val = 0xfffffff;
                        uint sm_num = 0xff;
                        for (uint j = 0; j < depth; j++)
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
                uint acc = 0;
                for (int i = 0; i < 16; i++)
                {
                    dctbl1.bits[i + 1] = pentax_tree[i];
                    acc += dctbl1.bits[i + 1];
                }
                dctbl1.bits[0] = 0;
                for (int i = 0; i < acc; i++)
                {
                    dctbl1.huffval[i] = pentax_tree[i + 16];
                }
            }
            UseBigtable = true;
            CreateHuffmanTable(dctbl1);

            input.BaseStream.Position = 0;
            pentaxBits = new BitPumpMSB(input, offset, size);
            unsafe
            {
                int[] pUp1 = { 0, 0 };
                int[] pUp2 = { 0, 0 };
                int pLeft1 = 0;
                int pLeft2 = 0;

                for (int y = 0; y < raw.raw.dim.height; y++)
                {
                    pentaxBits.CheckPos();
                    fixed (UInt16* dest = &raw.raw.data[y * raw.raw.dim.width])
                    {
                        // Adjust destination
                        pUp1[y & 1] += HuffDecodePentax();
                        pUp2[y & 1] += HuffDecodePentax();
                        dest[0] = (ushort)(pLeft1 = pUp1[y & 1]);
                        dest[1] = (ushort)(pLeft2 = pUp2[y & 1]);
                        for (int x = 2; x < raw.raw.dim.width; x += 2)
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
            pentaxBits.FillCheck();
            code = pentaxBits.PeekBitsNoFill(14);
            val = dctbl1.bigTable[code];
            if ((val & 0xff) != 0xff)
            {
                pentaxBits.SkipBitsNoFill((uint)(val & 0xff));
                return val >> 8;
            }

            rv = 0;
            code = pentaxBits.PeekByteNoFill();
            val = (int)dctbl1.numbits[code];
            l = (uint)val & 15;
            if (l != 0)
            {
                pentaxBits.SkipBitsNoFill(l);
                rv = val >> 4;
            }
            else
            {
                pentaxBits.SkipBits(8);
                l = 8;
                while (code > dctbl1.maxcode[l])
                {
                    temp = pentaxBits.GetBitNoFill();
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
                int x = (int)pentaxBits.GetBits((uint)rv);
                if ((x & (1 << (rv - 1))) == 0)
                    x -= (1 << rv) - 1;
                return x;
            }
            return 0;
        }

    }
}
