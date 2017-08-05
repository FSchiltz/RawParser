using PhotoNet.Common;
using System;

namespace RawNet.Decoder.HuffmanCompressor
{
    class NikonHuffman : HuffmanTable
    {
        private static byte[][] nikon_tree =
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

        public NikonHuffman() : base(false, false) { }

        public override void Create(int huffSelect)
        {
            uint acc = 0;
            for (int i = 0; i < 16; i++)
            {
                bits[i + 1] = nikon_tree[huffSelect][i];
                acc += bits[i + 1];
            }
            bits[0] = 0;

            for (int i = 0; i < acc; i++)
            {
                huffval[i] = nikon_tree[huffSelect][i + 16];
            }
            base.Create(0);
        }

        public override int Decode()
        {
            int rv;
            int l, temp;
            int code, val;

            bitPump.Fill();
            code = (int)bitPump.PeekBits(14);
            val = bigTable[code];
            if ((val & 0xff) != 0xff)
            {
                bitPump.SkipBits(val & 0xff);
                return val >> 8;
            }

            rv = 0;
            code = (int)bitPump.PeekBits(8);
            val = (int)numbits[code];
            l = val & 15;
            if (l != 0)
            {
                bitPump.SkipBits(l);
                rv = val >> 4;
            }
            else
            {
                bitPump.SkipBits(8);
                l = 8;
                while (code > maxcode[l])
                {
                    temp = bitPump.GetBit();
                    code = (code << 1) | temp;
                    l++;
                }

                if (l > 16)
                    throw new RawDecoderException("Corrupt JPEG data: bad Huffman code:" + l);
                rv = (int)huffval[valptr[l] + (code - minCode[l])];
            }

            if (rv == 16)
                return -32768;

            /*
            * Section F.2.2.1: decode the difference and
            * Figure F.12: extend sign bit
            */
            Int32 len = rv & 15;
            Int32 shl = rv >> 4;
            int diff = (int)((bitPump.GetBits(len - shl) << 1) + 1) << shl >> 1;
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
