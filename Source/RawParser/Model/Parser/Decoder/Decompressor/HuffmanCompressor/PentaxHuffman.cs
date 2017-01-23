using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawNet.Decoder.HuffmanCompressor
{
    class PentaxHuffman : HuffmanTable
    {
        public PentaxHuffman() : base(false, false) { }

        /*
        * Taken from Figure F.16: extract next coded symbol from input stream.  This should becode a macro.
        *
        * Results:
        * Next coded symbol
        *
        * Side effects:
        * Bitstream is parsed.
        */
        public override int Decode()
        {
            int rv;
            int l;
            uint code, temp;
            int val;
            /*
            * If the huffman code is less than 8 bits, we can use the fast
            * table lookup to get its value.  It's more than 8 bits about
            * 3-4% of the time.
            */
            bitPump.Fill();
            code = bitPump.PeekBitsNoFill(14);
            val = bigTable[code];
            if ((val & 0xff) != 0xff)
            {
                bitPump.SkipBitsNoFill(val & 0xff);
                return val >> 8;
            }

            rv = 0;
            code = bitPump.PeekByteNoFill();
            val = (int)numbits[code];
            l = val & 15;
            if (l != 0)
            {
                bitPump.SkipBitsNoFill(l);
                rv = val >> 4;
            }
            else
            {
                bitPump.SkipBits(8);
                l = 8;
                while (code > maxcode[l])
                {
                    temp = bitPump.GetBitNoFill();
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
                    rv = (int)huffval[valptr[l] + ((int)(code - minCode[l]))];
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
                int x = (int)bitPump.GetBits(rv);
                if ((x & (1 << (rv - 1))) == 0)
                    x -= (1 << rv) - 1;
                return x;
            }
            return 0;
        }
    }
}
