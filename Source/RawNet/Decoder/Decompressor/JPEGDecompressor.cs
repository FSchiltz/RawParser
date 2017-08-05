using PhotoNet.Common;
using RawNet.Decoder.HuffmanCompressor;
using RawNet.Jpeg;
using System.Collections.Generic;
using System.Diagnostics;

namespace RawNet.Decoder.Decompressor
{
    internal abstract class JPEGDecompressor
    {
        public ImageBinaryReader input;
        public BitPump bits;
        public RawImage raw;
        public SOFInfo frame = new SOFInfo();
        public List<uint> slicesW = new List<uint>(1);
        public int predictor;
        public int Pt;
        public uint offX = 0, offY = 0;  // Offset into image where decoding should start
        public uint skipX = 0, skipY = 0;   // Tile is larger than output, skip these border pixels
        public HuffmanTable[] huff;
        public bool UseBigTable { get; set; }     // Use only for large images
        public bool DNGCompatible { get; set; }  // DNG v1.0.x compatibility
        public CosineTable dct = new CosineTable();

        public virtual void DecodeScan() { throw new RawDecoderException("No scan decoder found"); }

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
        public JPEGDecompressor(ImageBinaryReader file, RawImage img, bool DNGCompatible, bool UseBigTable)
        {
            raw = img;
            input = file;
            this.DNGCompatible = DNGCompatible;
            this.UseBigTable = UseBigTable;
            huff = new HuffmanTable[4];
        }

        public SOFInfo GetSOF(uint offset, uint size)
        {
            // JPEG is big endian
            if (Common.GetHostEndianness() == Endianness.Big)
                input = new ImageBinaryReader(input.BaseStream, offset);
            else
                input = new ImageBinaryReaderBigEndian(input.BaseStream, offset);

            if (GetNextMarker(false) != JpegMarker.SOI)
                throw new RawDecoderException("Image did not start with SOI. Probably not an LJPEG");

            while (true)
            {
                JpegMarker m = GetNextMarker(true);
                if (m == JpegMarker.Sof3)
                {
                    SOFInfo sof = new SOFInfo();
                    ParseSOF(sof);
                    return sof;
                }
                if (m == JpegMarker.EOI)
                {
                    throw new RawDecoderException("Could not locate Start of Frame.");
                }
            }
        }

        public void StartDecoder(uint offset, uint size)
        {
            if (!input.IsValid(offset, size))
                throw new RawDecoderException("Start offset plus size is longer than file. Truncated file.");
            if ((int)offX >= raw.fullSize.dim.width)
                throw new RawDecoderException("X offset outside of image");
            if ((int)offY >= raw.fullSize.dim.height)
                throw new RawDecoderException("Y offset outside of image");

            // JPEG is big endian
            if (Common.GetHostEndianness() == Endianness.Big)
                input = new ImageBinaryReader(input.BaseStream, offset);
            else
                input = new ImageBinaryReaderBigEndian(input.BaseStream, offset);

            if (GetNextMarker(false) != JpegMarker.SOI)
                throw new RawDecoderException("Image did not start with SOI. Probably not an LJPEG");

            bool moreImage = true;
            while (moreImage)
            {
                JpegMarker m = GetNextMarker(true);
                switch (m)
                {
                    case JpegMarker.DQT:
                        throw new RawDecoderException("Not a valid RAW file.");
                    case JpegMarker.DHT:
                        //          _RPT0(0,"Found DHT marker\n");
                        ParseDHT();
                        break;
                    case JpegMarker.SOS:
                        //          _RPT0(0,"Found SOS marker\n");
                        ParseSOS();
                        break;
                    case JpegMarker.Sof3:
                        //          _RPT0(0,"Found SOF 3 marker:\n");
                        ParseSOF(frame);
                        break;
                    case JpegMarker.EOI:
                        //          _RPT0(0,"Found EOI marker\n");
                        moreImage = false;
                        break;
                    case JpegMarker.DRI:                        //          _RPT0(0,"Found DRI marker\n");                        
                    case JpegMarker.App0:                        //          _RPT0(0,"Found APP0 marker\n");                        
                    default: // _RPT1(0, "Found marker:0x%x. Skipping\n", m);
                             // Just let it skip to next marker
                        break;
                }
            }
        }

        public void ParseSOF(SOFInfo sof)
        {
            uint headerLength = input.ReadUInt16();
            sof.precision = input.ReadByte();
            sof.height = input.ReadUInt16();
            sof.width = input.ReadUInt16();
            sof.numComponents = input.ReadByte();

            if (sof.precision > 16)
                throw new RawDecoderException("More than 16 bits per channel is not supported.");

            if (sof.numComponents > 4 || sof.numComponents < 1)
                throw new RawDecoderException("Only from 1 to 4 components are supported.");

            if (headerLength != 8 + sof.numComponents * 3)
                throw new RawDecoderException("Header size mismatch.");

            for (int i = 0; i < sof.numComponents; i++)
            {
                sof.ComponentInfo[i].componentId = input.ReadByte();
                uint subs = input.ReadByte();
                frame.ComponentInfo[i].superV = subs & 0xf;
                frame.ComponentInfo[i].superH = subs >> 4;
                uint Tq = input.ReadByte();
                if (Tq != 0)
                    throw new RawDecoderException("Quantized components not supported.");
            }
            sof.Initialized = true;
        }

        public void ParseSOS()
        {
            if (!frame.Initialized)
                throw new RawDecoderException("Frame not yet initialized (SOF Marker not parsed)");

            input.ReadInt16();
            uint soscps = input.ReadByte();
            if (frame.numComponents != soscps)
                throw new RawDecoderException("Component number mismatch.");

            for (int i = 0; i < frame.numComponents; i++)
            {
                uint cs = input.ReadByte();
                uint count = 0;  // Find the correct component
                while (frame.ComponentInfo[count].componentId != cs)
                {
                    if (count >= frame.numComponents)
                        throw new RawDecoderException("Invalid Component Selector");
                    count++;
                }

                uint b1 = input.ReadByte();
                uint td = b1 >> 4;
                if (td > 3)
                    throw new RawDecoderException("Invalid Huffman table selection");
                if (!huff[td].Initialized)
                    throw new RawDecoderException("Invalid Huffman table selection, not defined.");

                if (count > 3)
                    throw new RawDecoderException("Component count out of range");

                frame.ComponentInfo[count].dcTblNo = td;
            }

            predictor = input.ReadByte();
            if (predictor > 7)
                throw new RawDecoderException("Invalid predictor mode.");

            input.ReadBytes(1); // Se + Ah Not used in LJPEG
            int b = input.ReadByte();
            Pt = b & 0xf;        // Point Transform            

            bits = new BitPumpJPEG(input);
            for (int i = 0; i < huff.Length; i++)
            {
                huff[i].bitPump = bits;
                huff[i].precision = frame.precision;
            }
            DecodeScan();
            input.Position = bits.Offset;
        }

        public void ParseDHT()
        {
            uint headerLength = (uint)input.ReadInt16() - 2; // Subtract myself
            while (headerLength != 0)
            {
                uint b = input.ReadByte();
                uint Tc = (b >> 4);
                if (Tc != 0)
                    throw new RawDecoderException("Unsupported Table class.");

                uint Th = b & 0xf;
                if (Th > 3)
                    throw new RawDecoderException("Invalid huffman table destination id.");

                uint acc = 0;
                HuffmanTable table = huff[Th];

                if (table.Initialized)
                    throw new RawDecoderException("Duplicate table definition");

                for (int i = 0; i < 16; i++)
                {
                    table.bits[i + 1] = input.ReadByte();
                    acc += table.bits[i + 1];
                }
                table.bits[0] = 0;
                if (acc > 256)
                    throw new RawDecoderException("Invalid DHT table.");

                if (headerLength < 1 + 16 + acc)
                    throw new RawDecoderException("Invalid DHT table length.");

                for (int i = 0; i < acc; i++)
                {
                    table.huffval[i] = input.ReadByte();
                }
                table.Create(frame.precision);
                headerLength -= 1 + 16 + acc;
            }
        }

        public JpegMarker GetNextMarker(bool allowskip)
        {
            if (!allowskip)
            {
                byte idL = input.ReadByte();
                if (idL != 0xff)
                    throw new RawDecoderException("Expected marker not found. Probably corrupt file.");

                JpegMarker markL = (JpegMarker)input.ReadByte();

                if (JpegMarker.Fill == markL || JpegMarker.Stuff == markL)
                    throw new RawDecoderException("Expected marker, but found stuffed 00 or ff.");

                return markL;
            }
            input.SkipToMarker();
            var id = input.ReadByte();

            Debug.Assert(0xff == id);
            JpegMarker mark = (JpegMarker)input.ReadByte();
            return mark;
        }
    }
}

