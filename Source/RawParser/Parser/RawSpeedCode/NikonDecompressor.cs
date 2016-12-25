using System;

namespace RawParser
{

    class NikonDecompressor : LJpegDecompressor
    {
        bool uncorrectedRawValues;
        private UInt16[] curve = new UInt16[65536];
        private static byte[][] nikon_tree = {
  { 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy */
  5,4,3,6,2,7,1,0,8,9,11,10,12 },
  { 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy after split */
  0x39,0x5a,0x38,0x27,0x16,5,4,3,2,1,0,11,12,12 },
  { 0,1,4,2,3,1,2,0,0,0,0,0,0,0,0,0,  /* 12-bit lossless */
  5,4,6,3,7,2,8,1,9,0,10,11,12 },
  { 0,1,4,3,1,1,1,1,1,2,0,0,0,0,0,0,	/* 14-bit lossy */
  5,6,4,7,8,3,9,2,1,0,10,11,12,13,14 },
  { 0,1,5,1,1,1,1,1,1,1,2,0,0,0,0,0,	/* 14-bit lossy after split */
  8,0x5c,0x4b,0x3a,0x29,7,6,5,4,3,2,1,0,13,14 },
  { 0,1,4,2,2,3,1,2,0,0,0,0,0,0,0,0,	/* 14-bit lossless */
  7,6,8,5,9,4,10,3,11,12,2,0,1,13,14 }
        };

        NikonDecompressor(TiffBinary file, RawImage img) : base(file, img)
        {
            for (UInt32 i = 0; i < 0x8000; i++)
            {
                curve[i] = i;
            }
        }

        void initTable(UInt32 huffSelect)
        {
            HuffmanTable* dctbl1 = &huff[0];
            UInt32 acc = 0;
            for (UInt32 i = 0; i < 16; i++)
            {
                dctbl1.bits[i + 1] = nikon_tree[huffSelect][i];
                acc += dctbl1.bits[i + 1];
            }
            dctbl1.bits[0] = 0;

            for (UInt32 i = 0; i < acc; i++)
            {
                dctbl1.huffval[i] = nikon_tree[huffSelect][i + 16];
            }
            createHuffmanTable(dctbl1);
        }

        void DecompressNikon(ByteStream* metadata, UInt32 w, UInt32 h, UInt32 bitsPS, UInt32 offset, UInt32 size)
        {
            UInt32 v0 = metadata.getByte();
            UInt32 v1 = metadata.getByte();
            UInt32 huffSelect = 0;
            UInt32 split = 0;
            int[] pUp1 = new int[2];
            int[] pUp2 = new int[2];
            mUseBigtable = true;

            _RPT2(0, "Nef version v0:%u, v1:%u\n", v0, v1);

            if (v0 == 73 || v1 == 88)
                metadata.skipBytes(2110);

            if (v0 == 70) huffSelect = 2;
            if (bitsPS == 14) huffSelect += 3;

            pUp1[0] = metadata.getShort();
            pUp1[1] = metadata.getShort();
            pUp2[0] = metadata.getShort();
            pUp2[1] = metadata.getShort();

            int _max = 1 << bitsPS & 0x7fff;
            UInt32 step = 0;
            UInt32 csize = metadata.getShort();
            if (csize > 1)
                step = _max / (csize - 1);
            if (v0 == 68 && v1 == 32 && step > 0)
            {
                for (UInt32 i = 0; i < csize; i++)
                    curve[i * step] = metadata.getShort();
                for (int i = 0; i < _max; i++)
                    curve[i] = (curve[i - i % step] * (step - i % step) +
                                curve[i - i % step + step] * (i % step)) / step;
                metadata.setAbsoluteOffset(562);
                split = metadata.getShort();
            }
            else if (v0 != 70 && csize <= 0x4001)
            {
                for (UInt32 i = 0; i < csize; i++)
                {
                    curve[i] = metadata.getShort();
                }
                _max = csize;
            }
            initTable(huffSelect);

            mRaw.whitePoint = curve[_max - 1];
            mRaw.blackLevel = curve[0];
            if (!uncorrectedRawValues)
            {
                mRaw.setTable(curve, _max, true);
            }

            UInt32 x, y;
            BitPumpMSB bits = new BitPumpMSB(mFile, offset, size);
            byte* draw = mRaw.getData();
            UInt16* dest;
            UInt32 pitch = mRaw.pitch;

            int pLeft1 = 0;
            int pLeft2 = 0;
            UInt32 cw = w / 2;
            UInt32 random = bits.peekBits(24);
            //allow gcc to devirtualize the calls below
            RawImageDataU16[] rawdata = mRaw.get();
            for (y = 0; y < h; y++)
            {
                if (split && y == split)
                {
                    initTable(huffSelect + 1);
                }
                dest = (UInt16*)&draw[y * pitch];  // Adjust destination
                pUp1[y & 1] += HuffDecodeNikon(bits);
                pUp2[y & 1] += HuffDecodeNikon(bits);
                pLeft1 = pUp1[y & 1];
                pLeft2 = pUp2[y & 1];
                rawdata.setWithLookUp(clampbits(pLeft1, 15), (byte8*)dest++, &random);
                rawdata.setWithLookUp(clampbits(pLeft2, 15), (byte8*)dest++, &random);
                for (x = 1; x < cw; x++)
                {
                    bits.checkPos();
                    pLeft1 += HuffDecodeNikon(bits);
                    pLeft2 += HuffDecodeNikon(bits);
                    rawdata.setWithLookUp(clampbits(pLeft1, 15), (byte8*)dest++, &random);
                    rawdata.setWithLookUp(clampbits(pLeft2, 15), (byte8*)dest++, &random);
                }
            }

            if (uncorrectedRawValues)
            {
                mRaw.setTable(curve, _max, false);
            }
            else
            {
                mRaw.setTable(null);
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
        int HuffDecodeNikon(BitPumpMSB bits)
        {
            int rv;
            int l, temp;
            int code, val;

            HuffmanTable* dctbl1 = &huff[0];

            bits.fill();
            code = bits.peekBitsNoFill(14);
            val = dctbl1.bigTable[code];
            if ((val & 0xff) != 0xff)
            {
                bits.skipBitsNoFill(val & 0xff);
                return val >> 8;
            }

            rv = 0;
            code = bits.peekByteNoFill();
            val = dctbl1.numbits[code];
            l = val & 15;
            if (l)
            {
                bits.skipBitsNoFill(l);
                rv = val >> 4;
            }
            else
            {
                bits.skipBits(8);
                l = 8;
                while (code > dctbl1.maxcode[l])
                {
                    temp = bits.getBitNoFill();
                    code = (code << 1) | temp;
                    l++;
                }

                if (l > 16)
                {
                    throw new Exception("Corrupt JPEG data: bad Huffman code:%u\n", l);
                }
                else
                {
                    rv = dctbl1.huffval[dctbl1.valptr[l] + ((int)(code - dctbl1.((code[l]))];
                }
            }

            if (rv == 16)
                return -32768;

            /*
            * Section F.2.2.1: decode the difference and
            * Figure F.12: extend sign bit
            */
            UInt32 len = rv & 15;
            UInt32 shl = rv >> 4;
            int diff = ((bits.getBits(len - shl) << 1) + 1) << shl >> 1;
            if ((diff & (1 << (len - 1))) == 0)
                diff -= (1 << len) - !shl;
            return diff;
        }
    }
}
