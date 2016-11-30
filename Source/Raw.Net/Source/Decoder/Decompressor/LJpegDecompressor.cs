using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RawNet
{
    public enum JpegMarker
    {       /* JPEG marker codes			*/
        M_STUFF = 0x00,
        M_SOF0 = 0xc0,  /* baseline DCT				*/
        M_SOF1 = 0xc1,  /* extended sequential DCT		*/
        M_SOF2 = 0xc2,  /* progressive DCT			*/
        M_SOF3 = 0xc3,  /* lossless (sequential)		*/

        M_SOF5 = 0xc5,  /* differential sequential DCT		*/
        M_SOF6 = 0xc6,  /* differential progressive DCT		*/
        M_SOF7 = 0xc7,  /* differential lossless		*/

        M_JPG = 0xc8,   /* JPEG extensions			*/
        M_SOF9 = 0xc9,  /* extended sequential DCT		*/
        M_SOF10 = 0xca, /* progressive DCT			*/
        M_SOF11 = 0xcb, /* lossless (sequential)		*/

        M_SOF13 = 0xcd, /* differential sequential DCT		*/
        M_SOF14 = 0xce, /* differential progressive DCT		*/
        M_SOF15 = 0xcf, /* differential lossless		*/

        M_DHT = 0xc4,   /* define Huffman tables		*/

        M_DAC = 0xcc,   /* define arithmetic conditioning table	*/

        M_RST0 = 0xd0,  /* restart				*/
        M_RST1 = 0xd1,  /* restart				*/
        M_RST2 = 0xd2,  /* restart				*/
        M_RST3 = 0xd3,  /* restart				*/
        M_RST4 = 0xd4,  /* restart				*/
        M_RST5 = 0xd5,  /* restart				*/
        M_RST6 = 0xd6,  /* restart				*/
        M_RST7 = 0xd7,  /* restart				*/

        M_SOI = 0xd8,   /* start of image			*/
        M_EOI = 0xd9,   /* end of image				*/
        M_SOS = 0xda,   /* start of scan			*/
        M_DQT = 0xdb,   /* define quantization tables		*/
        M_DNL = 0xdc,   /* define number of lines		*/
        M_DRI = 0xdd,   /* define restart interval		*/
        M_DHP = 0xde,   /* define hierarchical progression	*/
        M_EXP = 0xdf,   /* expand reference image(s)		*/

        M_APP0 = 0xe0,  /* application marker, used for JFIF	*/
        M_APP1 = 0xe1,  /* application marker			*/
        M_APP2 = 0xe2,  /* application marker			*/
        M_APP3 = 0xe3,  /* application marker			*/
        M_APP4 = 0xe4,  /* application marker			*/
        M_APP5 = 0xe5,  /* application marker			*/
        M_APP6 = 0xe6,  /* application marker			*/
        M_APP7 = 0xe7,  /* application marker			*/
        M_APP8 = 0xe8,  /* application marker			*/
        M_APP9 = 0xe9,  /* application marker			*/
        M_APP10 = 0xea, /* application marker			*/
        M_APP11 = 0xeb, /* application marker			*/
        M_APP12 = 0xec, /* application marker			*/
        M_APP13 = 0xed, /* application marker			*/
        M_APP14 = 0xee, /* application marker, used by Adobe	*/
        M_APP15 = 0xef, /* application marker			*/

        M_JPG0 = 0xf0,  /* reserved for JPEG extensions		*/
        M_JPG13 = 0xfd, /* reserved for JPEG extensions		*/
        M_COM = 0xfe,   /* comment				*/

        M_TEM = 0x01,   /* temporary use			*/
        M_FILL = 0xFF
    };


    /*
    * The following structure stores basic information about one component.
    */
    public struct JpegComponentInfo
    {
        /*
        * These values are fixed over the whole image.
        * They are read from the SOF marker.
        */
        public UInt32 componentId;     /* identifier for this component (0..255) */
        public UInt32 componentIndex;  /* its index in SOF or cPtr.compInfo[]   */

        /*
        * Huffman table selector (0..3). The value may vary
        * between scans. It is read from the SOS marker.
        */
        public UInt32 dcTblNo;
        public UInt32 superH; // Horizontal Supersampling
        public UInt32 superV; // Vertical Supersampling
    };

    /*
    * One of the following structures is created for each huffman coding
    * table.  We use the same structure for encoding and decoding, so there
    * may be some extra fields for encoding that aren't used in the decoding
    * and vice-versa.
    */
    public class HuffmanTable
    {
        /*
        * These two fields directly represent the contents of a JPEG DHT
        * marker
        */
        public UInt32[] bits = new uint[17];
        public UInt32[] huffval = new uint[256];

        /*
        * The remaining fields are computed from the above to allow more
        * efficient coding and decoding.  These fields should be considered
        * private to the Huffman compression & decompression modules.
        */

        public UInt16[] minCode = new UInt16[17];
        public int[] maxcode = new int[18];
        public short[] valptr = new short[17];
        public UInt32[] numbits = new UInt32[256];
        public int[] bigTable;
        public bool initialized;

        public HuffmanTable()
        {
            //init to maxvalue
            bits = Enumerable.Repeat<uint>(UInt32.MaxValue, 17).ToArray();
            huffval = Enumerable.Repeat<uint>(UInt32.MaxValue, 256).ToArray();
            minCode = Enumerable.Repeat<UInt16>(UInt16.MaxValue, 17).ToArray();
            maxcode = Enumerable.Repeat<int>(Int32.MinValue, 18).ToArray();
            valptr = Enumerable.Repeat<short>(Int16.MaxValue, 17).ToArray();
            //numbits = Enumerable.Repeat<uint>(UInt32.MaxValue, 256).ToArray();
        }
    };

    public class SOFInfo
    {
        public UInt32 w;    // Width
        public UInt32 h;    // Height
        public UInt32 cps;  // Components
        public UInt32 prec; // Precision
        public JpegComponentInfo[] compInfo = new JpegComponentInfo[4];
        public bool initialized;
    };

    public class LJpegDecompressor
    {
        public bool mDNGCompatible;  // DNG v1.0.x compatibility
        public bool mUseBigtable;    // Use only for large images
        public bool mCanonFlipDim;   // Fix Canon 6D mRaw where width/height is flipped
        public bool mCanonDoubleHeight; // Fix Canon double height on 4 components (EOS 5DS R)
        public void addSlices(List<int> slices) { slicesW = slices; }  // CR2 slices.

        public virtual void decodeScan() { throw new Exception("LJpegDecompressor: No Scan decoder found"); }

        public TIFFBinaryReader input;
        public BitPumpJPEG bits;
        public RawImage mRaw;

        public SOFInfo frame = new SOFInfo();
        public List<int> slicesW = new List<int>(1);
        public UInt32 pred;
        public UInt32 Pt;
        public UInt32 offX, offY;  // Offset into image where decoding should start
        public UInt32 skipX, skipY;   // Tile is larger than output, skip these border pixels
        public HuffmanTable[] huff = new HuffmanTable[4] { new HuffmanTable(), new HuffmanTable(), new HuffmanTable(), new HuffmanTable() };

        /*
        * Huffman table generation:
        * HuffDecode,
        * createHuffmanTable
        * and used data structures are originally grabbed from the IJG software,
        * and adapted by Hubert Figuiere.
        *
        * Copyright (C) 1991, 1992, Thomas G. Lane.
        * Part of the Independent JPEG Group's software.
        * See the file Copyright for more details.
        *
        * Copyright (c) 1993 Brian C. Smith, The Regents of the University
        * of California
        * All rights reserved.
        *
        * Copyright (c) 1994 Kongji Huang and Brian C. Smith.
        * Cornell University
        * All rights reserved.
        *
        * Permission to use, copy, modify, and distribute this software and its
        * documentation for any purpose, without fee, and without written agreement is
        * hereby granted, provided that the above copyright notice and the following
        * two paragraphs appear in all copies of this software.
        *
        * IN NO EVENT SHALL CORNELL UNIVERSITY BE LIABLE TO ANY PARTY FOR
        * DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES ARISING OUT
        * OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN IF CORNELL
        * UNIVERSITY HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
        *
        * CORNELL UNIVERSITY SPECIFICALLY DISCLAIMS ANY WARRANTIES,
        * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
        * AND FITNESS FOR A PARTICULAR PURPOSE.  THE SOFTWARE PROVIDED HEREUNDER IS
        * ON AN "AS IS" BASIS, AND CORNELL UNIVERSITY HAS NO OBLIGATION TO
        * PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
        */
        public UInt32[] bitMask = {  0xffffffff, 0x7fffffff,
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

        public LJpegDecompressor(TIFFBinaryReader file, RawImage img)
        {
            mRaw = (img);
            input = file;
            skipX = skipY = 0;
            for (int i = 0; i < 4; i++)
            {
                huff[i].initialized = false;
                huff[i].bigTable = null;
            }
            mDNGCompatible = false;
            slicesW.Clear();
            mUseBigtable = false;
            mCanonFlipDim = false;
            mCanonDoubleHeight = false;
        }

        public void getSOF(SOFInfo sof, UInt32 offset, UInt32 size)
        {
            if (!input.isValid(offset, size))
                throw new Exception("getSOF: Start offset plus size is longer than file. Truncated file.");
            try
            {
                Endianness host_endian = Common.getHostEndianness();
                // JPEG is big endian
                if (host_endian == Endianness.big)
                    input = new TIFFBinaryReader(input.BaseStream, offset, size);
                else
                    input = new TIFFBinaryReaderRE(input.BaseStream, offset, size);

                if (getNextMarker(false) != JpegMarker.M_SOI)
                    throw new Exception("getSOF: Image did not start with SOI. Probably not an LJPEG");

                while (true)
                {
                    JpegMarker m = getNextMarker(true);
                    if (JpegMarker.M_SOF3 == m)
                    {
                        parseSOF(sof);
                        return;
                    }
                    if (JpegMarker.M_EOI == m)
                    {
                        throw new Exception("LJpegDecompressor: Could not locate Start of Frame.");
                    }
                }
            }
            catch (IOException e)
            {
                throw new Exception("LJpegDecompressor: IO exception, read outside file. Corrupt File.");
            }
        }

        public void startDecoder(UInt32 offset, UInt32 size, UInt32 offsetX, UInt32 offsetY)
        {
            if (!input.isValid(offset, size))
                throw new Exception("startDecoder: Start offset plus size is longer than file. Truncated file.");
            if ((int)offsetX >= mRaw.dim.x)
                throw new Exception("startDecoder: X offset outside of image");
            if ((int)offsetY >= mRaw.dim.y)
                throw new Exception("startDecoder: Y offset outside of image");
            offX = offsetX;
            offY = offsetY;

            try
            {
                Endianness host_endian = Common.getHostEndianness();
                // JPEG is big endian
                if (host_endian == Endianness.big)
                    input = new TIFFBinaryReader(input.BaseStream, offset, size);
                else
                    input = new TIFFBinaryReaderRE(input.BaseStream, offset, size);

                if (getNextMarker(false) != JpegMarker.M_SOI)
                    throw new Exception("startDecoder: Image did not start with SOI. Probably not an LJPEG");
                //    _RPT0(0,"Found SOI marker\n");

                bool moreImage = true;
                while (moreImage)
                {
                    JpegMarker m = getNextMarker(true);

                    switch (m)
                    {
                        case JpegMarker.M_SOS:
                            //          _RPT0(0,"Found SOS marker\n");
                            parseSOS();
                            break;
                        case JpegMarker.M_EOI:
                            //          _RPT0(0,"Found EOI marker\n");
                            moreImage = false;
                            break;

                        case JpegMarker.M_DHT:
                            //          _RPT0(0,"Found DHT marker\n");
                            parseDHT();
                            break;

                        case JpegMarker.M_DQT:
                            throw new Exception("LJpegDecompressor: Not a valid RAW file.");
                            break;

                        case JpegMarker.M_DRI:
                            //          _RPT0(0,"Found DRI marker\n");
                            break;

                        case JpegMarker.M_APP0:
                            //          _RPT0(0,"Found APP0 marker\n");
                            break;

                        case JpegMarker.M_SOF3:
                            //          _RPT0(0,"Found SOF 3 marker:\n");
                            parseSOF(frame);
                            break;

                        default:  // Just let it skip to next marker
                                  // _RPT1(0, "Found marker:0x%x. Skipping\n", m);
                            break;
                    }
                }

            }
            catch (IOException)
            {
                throw;
            }
        }

        public void parseSOF(SOFInfo sof)
        {
            UInt32 headerLength = (uint)input.ReadInt16();
            sof.prec = input.ReadByte();
            sof.h = (uint)input.ReadInt16();
            sof.w = (uint)input.ReadInt16();

            sof.cps = input.ReadByte();

            if (sof.prec > 16)
                throw new Exception("LJpegDecompressor: More than 16 bits per channel is not supported.");

            if (sof.cps > 4 || sof.cps < 1)
                throw new Exception("LJpegDecompressor: Only from 1 to 4 components are supported.");

            if (headerLength != 8 + sof.cps * 3)
                throw new Exception("LJpegDecompressor: Header size mismatch.");

            for (UInt32 i = 0; i < sof.cps; i++)
            {
                sof.compInfo[i].componentId = input.ReadByte();
                UInt32 subs = input.ReadByte();
                frame.compInfo[i].superV = subs & 0xf;
                frame.compInfo[i].superH = subs >> 4;
                UInt32 Tq = input.ReadByte();
                if (Tq != 0)
                    throw new Exception("LJpegDecompressor: Quantized components not supported.");
            }
            sof.initialized = true;
        }

        public void parseSOS()
        {
            if (!frame.initialized)
                throw new Exception("parseSOS: Frame not yet initialized (SOF Marker not parsed)");

            UInt32 headerLength = (uint)input.ReadInt16();
            UInt32 soscps = input.ReadByte();
            if (frame.cps != soscps)
                throw new Exception("parseSOS: Component number mismatch.");

            for (UInt32 i = 0; i < frame.cps; i++)
            {
                UInt32 cs = input.ReadByte();

                UInt32 count = 0;  // Find the correct component
                while (frame.compInfo[count].componentId != cs)
                {
                    if (count >= frame.cps)
                        throw new Exception("parseSOS: Invalid Component Selector");
                    count++;
                }

                UInt32 b1 = input.ReadByte();
                UInt32 td = b1 >> 4;
                if (td > 3)
                    throw new Exception("parseSOS: Invalid Huffman table selection");
                if (!huff[td].initialized)
                    throw new Exception("parseSOS: Invalid Huffman table selection, not defined.");

                if (count > 3)
                    throw new Exception("parseSOS: Component count out of range");

                frame.compInfo[count].dcTblNo = td;
            }

            // Get predictor
            pred = input.ReadByte();
            if (pred > 7)
                throw new Exception("parseSOS: Invalid predictor mode.");

            input.ReadBytes(1);                    // Se + Ah Not used in LJPEG
            UInt32 b = input.ReadByte();
            Pt = b & 0xf;        // Point Transform

            UInt32 cheadersize = 3 + frame.cps * 2 + 3;
            //_ASSERTE(cheadersize == headerLength);

            bits = new BitPumpJPEG(input);
            decodeScan();
            input.ReadBytes((int)bits.getOffset());

        }

        public void parseDHT()
        {
            UInt32 headerLength = (uint)input.ReadInt16() - 2; // Subtract myself

            while (headerLength != 0)
            {
                UInt32 b = input.ReadByte();

                UInt32 Tc = (b >> 4);
                if (Tc != 0)
                    throw new Exception("parseDHT: Unsupported Table class.");

                UInt32 Th = b & 0xf;
                if (Th > 3)
                    throw new Exception("parseDHT: Invalid huffman table destination id.");

                UInt32 acc = 0;
                HuffmanTable t = huff[Th];

                if (t.initialized)
                    throw new Exception("parseDHT: Duplicate table definition");

                for (UInt32 i = 0; i < 16; i++)
                {
                    t.bits[i + 1] = input.ReadByte();
                    acc += t.bits[i + 1];
                }
                t.bits[0] = 0;
                //Common.memset<uint>(t.huffval, 0, sizeof(uint) * t.huffval.Length);
                if (acc > 256)
                    throw new Exception("parseDHT: Invalid DHT table.");

                if (headerLength < 1 + 16 + acc)
                    throw new Exception("parseDHT: Invalid DHT table length.");

                for (UInt32 i = 0; i < acc; i++)
                {
                    t.huffval[i] = input.ReadByte();
                }
                createHuffmanTable(t);
                headerLength -= 1 + 16 + acc;
            }
        }


        public JpegMarker getNextMarker(bool allowskip)
        {
            if (!allowskip)
            {
                byte idL = input.ReadByte();
                if (idL != 0xff)
                    throw new Exception("getNextMarker: (Noskip) Expected marker not found. Propably corrupt file.");

                JpegMarker markL = (JpegMarker)input.ReadByte();

                if (JpegMarker.M_FILL == markL || JpegMarker.M_STUFF == markL)
                    throw new Exception("getNextMarker: (Noskip) Expected marker, but found stuffed 00 or ff.");

                return markL;
            }
            input.skipToMarker();
            byte id = input.ReadByte();
            //TODO change
            //_ASSERTE(0xff == id);
            JpegMarker mark = (JpegMarker)input.ReadByte();
            return mark;
        }

        public void createHuffmanTable(HuffmanTable htbl)
        {
            int p, i, l, lastp, si;
            byte[] huffsize = new byte[257];
            UInt16[] huffcode = new ushort[257];
            UInt16 code;
            int size;
            int value, ll, ul;

            /*
            * Figure C.1: make table of Huffman code length for each symbol
            * Note that this is in code-length order.
            */
            p = 0;
            for (l = 1; l <= 16; l++)
            {
                for (i = 1; i <= (int)htbl.bits[l]; i++)
                {
                    huffsize[p++] = (byte)l;
                    if (p > 256)
                        throw new Exception("createHuffmanTable: Code length too long. Corrupt data.");
                }
            }
            huffsize[p] = 0;
            lastp = p;


            /*
            * Figure C.2: generate the codes themselves
            * Note that this is in code-length order.
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
                    throw new Exception("createHuffmanTable: Code length too long. Corrupt data.");
            }


            /*
            * Figure F.15: generate decoding tables
            */
            htbl.minCode[0] = 0;
            htbl.maxcode[0] = 0;
            p = 0;
            for (l = 1; l <= 16; l++)
            {
                if (htbl.bits[l] != 0)
                {
                    htbl.valptr[l] = (short)p;
                    htbl.minCode[l] = huffcode[p];
                    p += (int)htbl.bits[l];
                    htbl.maxcode[l] = huffcode[p - 1];
                }
                else
                {
                    htbl.valptr[l] = 0xff;   // This check must be present to avoid crash on junk
                    htbl.maxcode[l] = -1;
                }
                if (p > 256)
                    throw new Exception("createHuffmanTable: Code length too long. Corrupt data.");
            }

            /*
            * We put in this value to ensure HuffDecode terminates.
            */
            htbl.maxcode[17] = (int)0xFFFFFL;

            /*
            * Build the numbits, value lookup tables.
            * These table allow us to gather 8 bits from the bits stream,
            * and immediately lookup the size and value of the huffman codes.
            * If size is zero, it means that more than 8 bits are in the huffman
            * code (this happens about 3-4% of the time).
            */
            //Common.memset<uint>(htbl.numbits, 0, sizeof(uint) * htbl.numbits.Length);
            for (p = 0; p < lastp; p++)
            {
                size = huffsize[p];
                if (size <= 8)
                {
                    value = (int)htbl.huffval[p];
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
                        throw new Exception("createHuffmanTable: Code length too long. Corrupt data.");
                    for (i = ll; i <= ul; i++)
                    {
                        htbl.numbits[i] = (uint)(size | (value << 4));
                    }
                }
            }
            if (mUseBigtable)
                createBigTable(ref htbl);
            htbl.initialized = true;
        }

        /************************************
         * Bitable creation
         *
         * This is expanding the concept of fast lookups
         *
         * A complete table for 14 arbitrary bits will be
         * created that enables fast lookup of number of bits used,
         * and final delta result.
         * Hit rate is about 90-99% for typical LJPEGS, usually about 98%
         *
         ************************************/
        public void createBigTable(ref HuffmanTable htbl)
        {
            UInt32 bits = 14;      // HuffDecode functions must be changed, if this is modified.
            UInt32 size = (uint)(1 << (int)(bits));
            int rv = 0;
            int temp;
            UInt32 l;

            if (htbl.bigTable == null)
                htbl.bigTable = new int[size];
            if (htbl.bigTable == null)
                throw new Exception("Out of memory, failed to allocate " + size * sizeof(int) + " bytes");
            for (UInt32 i = 0; i < size; i++)
            {
                UInt16 input = (ushort)((int)i << 2); // Calculate input value
                int code = input >> 8;   // Get 8 bits
                UInt32 val = htbl.numbits[code];
                l = val & 15;
                if (l != 0)
                {
                    rv = (int)val >> 4;
                }
                else
                {
                    l = 8;
                    while (code > htbl.maxcode[l])
                    {
                        temp = input >> (int)(15 - l) & 1;
                        code = (code << 1) | temp;
                        l++;
                    }

                    /*
                    * With garbage input we may reach the sentinel value l = 17.
                    */
                    if (l > frame.prec || htbl.valptr[l] == 0xff)
                    {
                        htbl.bigTable[i] = 0xff;
                        continue;
                    }
                    else
                    {
                        rv = (int)htbl.huffval[htbl.valptr[l] + (code - htbl.minCode[l])];
                    }
                }


                if (rv == 16)
                {
                    if (mDNGCompatible)
                        htbl.bigTable[i] = (-(32768 << 8)) | (16 + (int)l);
                    else
                        htbl.bigTable[i] = (-(32768 << 8)) | (int)l;
                    continue;
                }

                if (rv + l > bits)
                {
                    htbl.bigTable[i] = 0xff;
                    continue;
                }

                if (rv != 0)
                {
                    int x = input >> (int)(16 - l - rv) & ((1 << rv) - 1);
                    if ((x & (1 << (rv - 1))) == 0)
                        x -= (1 << rv) - 1;
                    htbl.bigTable[i] = (x << 8) | ((int)l + rv);
                }
                else
                {
                    htbl.bigTable[i] = (int)l;
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
        public int HuffDecode(ref HuffmanTable htbl)
        {
            int rv;
            int temp;
            int code, val;
            UInt32 l;
            /**
             * First attempt to do complete decode, by using the first 14 bits
             */

            bits.fill();
            code = (int)bits.peekBitsNoFill(14);
            if (htbl.bigTable != null)
            {
                val = htbl.bigTable[code];
                if ((val & 0xff) != 0xff)
                {
                    bits.skipBitsNoFill((uint)val & 0xff);
                    return val >> 8;
                }
            }
            /*
            * If the huffman code is less than 8 bits, we can use the fast
            * table lookup to get its value.  It's more than 8 bits about
            * 3-4% of the time.
            */
            rv = 0;
            code = code >> 6;
            val = (int)htbl.numbits[code];
            l = (uint)val & 15;
            if (l != 0)
            {
                bits.skipBitsNoFill(l);
                rv = val >> 4;
            }
            else
            {
                bits.skipBitsNoFill(8);
                l = 8;
                while (code > htbl.maxcode[l])
                {
                    temp = (int)bits.getBitNoFill();
                    code = (code << 1) | temp;
                    l++;
                }

                /*
                * With garbage input we may reach the sentinel value l = 17.
                */

                if (l > frame.prec || htbl.valptr[l] == 0xff)
                {
                    throw new Exception("Corrupt JPEG data: bad Huffman code:" + l);
                }
                else
                {
                    rv = (int)htbl.huffval[htbl.valptr[l] + (code - htbl.minCode[l])];
                }
            }

            if (rv == 16)
            {
                if (mDNGCompatible)
                    bits.skipBitsNoFill(16);
                return -32768;
            }

            // Ensure we have enough bits
            if ((rv + l) > 24)
            {
                if (rv > 16) // There is no values above 16 bits.
                    throw new Exception("Corrupt JPEG data: Too many bits requested.");
                else
                    bits.fill();
            }

            /*
            * Section F.2.2.1: decode the difference and
            * Figure F.12: extend sign bit
            */
            if (rv != 0)
            {
                int x = (int)bits.getBitsNoFill((uint)rv);
                if ((x & (1 << (rv - 1))) == 0)
                    x -= (1 << rv) - 1;
                return x;
            }
            return 0;
        }
    }
}

