using System;

namespace RawParser.Model.Parser
{
    internal class LinearisationTable
    {
        public byte version0;
        public byte version1;
        public short[][] vpreds;
        public short curveSize;
        public short[] curve;
        public short splitValue;
        public int max;
        public int min;
        public ushort[] hpred;
        private object[] rawdata;
        //huffman tree for the different compression type
        public byte[][] nikonTree =
            {
                    new byte[]{ 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy */
                      5,4,3,6,2,7,1,0,8,9,11,10,12 },
                    new byte[]{ 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,	/* 12-bit lossy after split */
                      0x39,0x5a,0x38,0x27,0x16,5,4,3,2,1,0,11,12,12 },
                    new byte[] { 0,1,4,2,3,1,2,0,0,0,0,0,0,0,0,0,  /* 12-bit lossless */
                      5,4,6,3,7,2,8,1,9,0,10,11,12 },
                    new byte[]{ 0,1,4,3,1,1,1,1,1,2,0,0,0,0,0,0,	/* 14-bit lossy */
                      5,6,4,7,8,3,9,2,1,0,10,11,12,13,14 },
                    new byte[]{ 0,1,5,1,1,1,1,1,1,1,2,0,0,0,0,0,	/* 14-bit lossy after split */
                      8,0x5c,0x4b,0x3a,0x29,7,6,5,4,3,2,1,0,13,14 },
                    new byte [] { 0,1,4,2,2,3,1,2,0,0,0,0,0,0,0,0,	/* 14-bit lossless */
                      7,6,8,5,9,4,10,3,11,12,2,0,1,13,14 }
            };

        /*
         * Source from DCRaw
         * 
         */
        public LinearisationTable(object[] table, ushort compressionType, int colordepth)
        {
            rawdata = table;
            //get the version
            version0 = (byte)table[0];
            version1 = (byte)table[1];

            //get the 4 vpreds

            vpreds = new short[2][];
            vpreds[0] = new short[2];
            vpreds[1] = new short[2];

            //(when ver0 == 0x49 || ver1 == 0x58, fseek (ifp, 2110, SEEK_CUR) before)
            if (version0 == 0x49 || version1 == 0x58)
            {
                //fseek(ifp, 2110, SEEK_CUR) before));

            }
            vpreds[0][0] = BitConverter.ToInt16(new byte[2] { (byte)table[2], (byte)table[3] }, 0);
            vpreds[0][1] = BitConverter.ToInt16(new byte[2] { (byte)table[4], (byte)table[5] }, 0);
            vpreds[1][0] = BitConverter.ToInt16(new byte[2] { (byte)table[6], (byte)table[7] }, 0);
            vpreds[1][1] = BitConverter.ToInt16(new byte[2] { (byte)table[8], (byte)table[9] }, 0);

            //get the curvesize
            curveSize = Convert.ToInt16(table[10]);

            int step = 0;
            max = 1 << colordepth & 0x7fff;
            step = max / (curveSize - 1);

            max = curveSize;
            if (curveSize == 257 && compressionType == 4)
            {
                curveSize = (short)(1 + curveSize * 2);
            }
            curve = new short[curveSize];
            //if certain version
            if (version0 == 0x44 && version1 == 0x20 && step > 0)
            {
                for (int i = 0; i < curveSize * 2; i += 2)
                    curve[i / 2 * step] = BitConverter.ToInt16(new byte[2] { (byte)table[12 + i], (byte)table[13 + i] }, 0);
                for (int i = 0; i < max; i++)
                    curve[i] = (short)((curve[i - i % step] * (step - i % step) +
                         curve[i - i % step + step] * (i % step)) / step);

            }
            //else if otherversion
            else if (version0 != 0x46 && curveSize <= 0x4001)
            {
                for (int i = 0; i < curveSize * 2; i += 2)
                {
                    curve[i] = BitConverter.ToInt16(new byte[2] { (byte)table[12 + i], (byte)table[13 + i] }, 0);
                }
            }

            if (compressionType == 4)
            {
                splitValue = BitConverter.ToInt16(new byte[2] { (byte)table[562], (byte)table[563] }, 0);
            }

        }

        public ushort[] makeDecoder(int index)
        {
            byte[] source = nikonTree[index];
            ushort max, len, h, i, j;
            ushort[] huff;

            for (max = 16; max > 0; max--) ;
            huff = new ushort[1 + (1 << max)];

            huff[0] = max;
            for (h = len = 1; len <= max; len++)
            {
                for (i = 0; i < source[len]; i++)
                {
                    for (j = 0; j < 1 << (max - len); j++)
                    {
                        if (h <= 1 << max)
                        {
                            huff[h++] = (ushort)(len << 8 | source[i]);
                        }
                    }
                }
            }
            return huff;
        }

        internal uint gethuff(ushort[] huff)
        {
            ushort[] temp = new ushort[huff.Length- 1];
            huff.CopyTo(temp, 1);
            return getbithuff(huff[0],temp);
        }

        public uint getbithuff(int nbits, ushort[] huff)
        {
            uint bitbuf = 0;
            int vbits = 0, reset = 0;
            uint c = 0;
            int i = 0;

            if (nbits > 25) return 0;
            if (nbits < 0)
                return 0;
            if (nbits == 0 || vbits < 0) return 0;

            while (reset != 0 && vbits < nbits && i < rawdata.Length)
            {
                i++;
                c = (byte)rawdata[i++ + 10];//todo check
                bitbuf = (bitbuf << 8) + (byte)c;
                vbits += 8;
            }
            c = bitbuf << (32 - vbits) >> (32 - nbits);
            if (huff != null)
            {
                vbits -= huff[c] >> 8;
                c = (byte)huff[c];
            }
            else
                vbits -= nbits;
            if (vbits < 0) throw new Exception("Error");
            return c;
        }
    }
}