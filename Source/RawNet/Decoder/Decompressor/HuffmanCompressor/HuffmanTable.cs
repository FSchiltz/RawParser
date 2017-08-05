using System;
using System.Linq;
using RawNet.Decoder.Decompressor;
using PhotoNet.Common;

namespace RawNet.Decoder.HuffmanCompressor
{

    /*
    * One of the following structures is created for each huffman coding table.
    * We use the same structure for encoding and decoding, so there may be some extra fields for encoding that aren't used in the decoding and vice-versa.
    */
    internal class HuffmanTable
    {
        /*
        * These two fields directly represent the contents of a JPEG DHT marker
        */
        public uint[] bits = new uint[17];
        public uint[] huffval = new uint[256];
        public BitPump bitPump;
        public bool UseBigTable;
        public bool DNGCompatible;
        public bool Initialized { get; set; } = false;

        /*
        * The remaining fields are computed from the above to allow more efficient coding and decoding.  
        * These fields should be considered private to the Huffman compression & decompression modules.
        */
        protected ushort[] minCode = new ushort[17];
        protected int[] maxcode = new int[18];
        protected short[] valptr = new short[17];
        protected uint[] numbits = new uint[256];
        protected int[] bigTable;
        public int precision;
        protected static uint[] bitMask = {  0xffffffff, 0x7fffffff,
                           0x3fffffff, 0x1fffffff,
                           0x0fffffff, 0x07ffffff,
                           0x03ffffff, 0x01ffffff,
                           0x00ffffff, 0x007fffff,
                           0x003fffff, 0x001fffff,
                           0x000fffff, 0x0007ffff,
                           0x0003ffff, 0x0001ffff,
                           0x0000ffff, 0x00007fff,
                           0x00003fff, 0x00001fff,
                           0x00000fff, 0x000007ff,
                           0x000003ff, 0x000001ff,
                           0x000000ff, 0x0000007f,
                           0x0000003f, 0x0000001f,
                           0x0000000f, 0x00000007,
                           0x00000003, 0x00000001
                        };

        public HuffmanTable(bool DNGCompatible, bool UseBigTable)
        {
            this.UseBigTable = UseBigTable;
            this.DNGCompatible = DNGCompatible;

            //init to maxvalue
            bits = Enumerable.Repeat(UInt32.MaxValue, 17).ToArray();
            huffval = Enumerable.Repeat(UInt32.MaxValue, 256).ToArray();
            minCode = Enumerable.Repeat(UInt16.MaxValue, 17).ToArray();
            maxcode = Enumerable.Repeat(Int32.MinValue, 18).ToArray();
            valptr = Enumerable.Repeat(Int16.MaxValue, 17).ToArray();
            //numbits = Enumerable.Repeat<uint>(uint.MaxValue, 256).ToArray();
        }

        public virtual void Create(int precision)
        {
            int p, i, l, lastp, si;
            byte[] huffsize = new byte[257];
            UInt16[] huffcode = new ushort[257];
            //for (int k = 0; k < huffcode.Length; k++) { huffcode[k] = 52428; }
            UInt16 code;
            int size;
            int value, ll, ul;
            this.precision = precision;
            //Figure C.1: make table of Huffman code length for each symbol. Note that this is in code-length order.            
            p = 0;
            for (l = 1; l <= 16; l++)
            {
                for (i = 1; i <= (int)bits[l]; i++)
                {
                    huffsize[p++] = (byte)l;
                    if (p > 256)
                        throw new RawDecoderException("Code length too long. Corrupt data.");
                }
            }
            huffsize[p] = 0;
            lastp = p;


            /*
            * Figure C.2: generate the codes themselves. Note that this is in code-length order.
            */
            code = 0;
            si = huffsize[0];
            p = 0;
            while (huffsize[p] != 0)
            {
                while (huffsize[p] == si)
                {
                    huffcode[p++] = code;
                    code++;
                }
                code <<= 1;
                si++;
                if (p > 256)
                    throw new RawDecoderException("Code length too long. Corrupt data.");
            }


            /*
            * Figure F.15: generate decoding tables
            */
            minCode[0] = 0;
            maxcode[0] = 0;
            p = 0;
            for (l = 1; l <= 16; l++)
            {
                if (bits[l] != 0)
                {
                    valptr[l] = (short)p;
                    minCode[l] = huffcode[p];
                    p += (int)bits[l];
                    maxcode[l] = huffcode[p - 1];
                }
                else
                {
                    valptr[l] = 0xff;   // This check must be present to avoid crash on junk
                    maxcode[l] = -1;
                }
                if (p > 256)
                    throw new RawDecoderException("createHuffmanTable: Code length too long. Corrupt data.");
            }

            // put in this value to ensure HuffDecode terminates.           
            maxcode[17] = (int)0xFFFFFL;

            /*
            * Build the numbits, value lookup tables.
            * These table allow us to gather 8 bits from the bits stream, and immediately lookup the size and value of the huffman codes.
            * If size is zero, it means that more than 8 bits are in the huffman code (this happens about 3-4% of the time).
            */
            for (p = 0; p < lastp; p++)
            {
                size = huffsize[p];
                if (size <= 8)
                {
                    value = (int)huffval[p];
                    code = huffcode[p];
                    ll = code << (8 - size);
                    if (size < 8)
                    {
                        ul = (int)((uint)ll | bitMask[24 + size]);
                    }
                    else
                    {
                        ul = ll;
                    }
                    if (ul > 256 || ll > ul)
                        throw new RawDecoderException("Code length too long. Corrupt data.");
                    for (i = ll; i <= ul; i++)
                    {
                        numbits[i] = (uint)(size | (value << 4));
                    }
                }
            }
            if (UseBigTable)
                CreateBigTable();
            Initialized = true;
        }

        /************************************
         * Bitable creation         
         * This is expanding the concept of fast lookups
         * A complete table for 14 arbitrary bits will be created that enables fast lookup of number of bits used, and final delta result.
         * Hit rate is about 90-99% for typical LJPEGS, usually about 98%
         ************************************/
        public void CreateBigTable()
        {
            uint bits = 14;      // HuffDecode functions must be changed, if this is modified.
            uint size = (uint)(1 << (int)(bits));
            int rv = 0;
            int temp;
            uint l;

            if (bigTable == null)
                bigTable = new int[size];
            if (bigTable == null)
                throw new RawDecoderException("Out of memory, failed to allocate " + size * sizeof(int) + " bytes");
            for (uint i = 0; i < size; i++)
            {
                UInt16 input = (ushort)((int)i << 2); // Calculate input value
                int code = input >> 8;   // Get 8 bits
                uint val = numbits[code];
                l = val & 15;
                if (l != 0)
                {
                    rv = (int)val >> 4;
                }
                else
                {
                    l = 8;
                    while (code > maxcode[l])
                    {
                        temp = input >> (int)(15 - l) & 1;
                        code = (code << 1) | temp;
                        l++;
                    }

                    //With garbage input we may reach the sentinel value l = 17.                    
                    if (l > precision || valptr[l] == 0xff)
                    {
                        bigTable[i] = 0xff;
                        continue;
                    }
                    else
                    {
                        rv = (int)huffval[valptr[l] + (code - minCode[l])];
                    }
                }

                if (rv == 16)
                {
                    if (DNGCompatible)
                        bigTable[i] = (-(32768 << 8)) | (16 + (int)l);
                    else
                        bigTable[i] = (-(32768 << 8)) | (int)l;
                    continue;
                }

                if (rv + l > bits)
                {
                    bigTable[i] = 0xff;
                    continue;
                }

                if (rv != 0)
                {
                    int x = input >> (int)(16 - l - rv) & ((1 << rv) - 1);
                    if ((x & (1 << (rv - 1))) == 0)
                        x -= (1 << rv) - 1;
                    bigTable[i] = (x << 8) | ((int)l + rv);
                }
                else
                {
                    bigTable[i] = (int)l;
                }
            }
        }

        /*
        *--------------------------------------------------------------        
        * HuffDecode --        
        * Taken from Figure F.16: extract next coded symbol from input stream. This should becode a macro.
        *
        * Results:
        * Next coded symbol
        *
        * Side effects:
        * Bitstream is parsed.        
        *--------------------------------------------------------------
        */
        public virtual int Decode()
        {
            int rv;
            int temp;
            int code, val;
            int l;
            //First attempt to do complete decode, by using the first 14 bits        
            bitPump.Fill();
            code = (int)bitPump.PeekBits(14);
            if (bigTable != null)
            {
                val = bigTable[code];
                if ((val & 0xff) != 0xff)
                {
                    bitPump.SkipBits(val & 0xff);
                    return val >> 8;
                }
            }
            /*
            * If the huffman code is less than 8 bits, we can use the fast table lookup to get its value.
            * It's more than 8 bits about 3-4% of the time.
            */
            rv = 0;
            code = code >> 6;
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

                //With garbage input we may reach the sentinel value l = 17.             
                if (l > precision || valptr[l] == 0xff)
                {
                    throw new RawDecoderException("Corrupt JPEG data: bad Huffman code:" + l);
                }
                else
                {
                    rv = (int)huffval[valptr[l] + (code - minCode[l])];
                }
            }

            if (rv == 16)
            {
                if (DNGCompatible)
                    bitPump.SkipBits(16);
                return -32768;
            }

            // Ensure we have enough bits
            if ((rv + l) > 24)
            {
                if (rv > 16) // There is no values above 16 bits.
                    throw new RawDecoderException("Corrupt JPEG data: Too many bits requested.");
                else
                    bitPump.Fill();
            }

            //Section F.2.2.1: decode the difference and  extend sign bit         
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

