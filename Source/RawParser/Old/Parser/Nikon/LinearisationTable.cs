using RawParser.Base;
using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace RawParser.Parser.Nikon
{
    internal class LinearisationTable : IDisposable
    {

        private int colordepth;
        private BinaryReader reader;
        private ushort linearCurveSize = UInt16.MaxValue;
        byte version0;
        byte version1;
        int max;
        short curveSize;
        UShortArrayWithIndexAsDefaultValue curve;
        short splitValue;
        int vbits = 0, reset = 0;
        short[][] vpreds;
        ushort[] hpred = new ushort[2];
        uint bitbuf = 0;
        int rawdataLength;
        uint c = 0;

        //huffman tree for the different compression type
        public byte[][] nikonTree =
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

        /*
         * Source from DCRaw
         * 
         */
        public LinearisationTable(ushort compressionType, int colordepth, uint Rawoffset, uint Linetableoffset, BinaryReader r)
        {
            //rawdataLength = table.Length;
            this.colordepth = colordepth;

            //move the reader to the corresponding offset
            r.BaseStream.Position = Linetableoffset;

            //get the version
            version0 = r.ReadByte();
            version1 = r.ReadByte();

            //get the 4 vpreds

            vpreds = new short[2][];
            vpreds[0] = new short[2];
            vpreds[1] = new short[2];

            //(when ver0 == 0x49 || ver1 == 0x58, fseek (ifp, 2110, SEEK_CUR) before)

            if (version0 == 0x49 || version1 == 0x58)
            {
                //TODO
                r.BaseStream.Position += 2110;

            }
            vpreds[0][0] = r.ReadInt16();
            vpreds[0][1] = r.ReadInt16();
            vpreds[1][0] = r.ReadInt16();
            vpreds[1][1] = r.ReadInt16();

            //get the curvesize
            curveSize = r.ReadInt16();

            int step = 0;
            max = 1 << colordepth & 0x7fff;
            step = max / (curveSize - 1);
            /*
                        if (curveSize == 257 && compressionType == 4)
                        {
                            curveSize = (short)(1 + curveSize * 2);
                        }
                        */

            curve = new UShortArrayWithIndexAsDefaultValue(linearCurveSize);
            /*
            for (ushort i = 0; i < linearCurveSize; i++)
            {
                curve[i] = i;
            }
            */
            //if certain version
            if (version0 == 0x44 && version1 == 0x20 && step > 0)
            {
                for (int i = 0; i < curveSize; i++)
                {
                    curve[i * step] = r.ReadUInt16();
                }
                for (int i = 0; i < max; i++)
                {
                    curve[i] = (ushort)(
                        (
                            curve[i - i % step] * (step - i % step) +
                            curve[i - i % step + step] * (i % step)
                        ) / step);
                }
                r.BaseStream.Position = Linetableoffset + 562;
                splitValue = r.ReadInt16();

            }

            //else if otherversion
            else if (version0 != 0x46 && curveSize <= 0x4001)
            {
                for (int i = 0; i < curveSize; i++)
                {
                    curve[i] = r.ReadUInt16();
                }
            }

            //optimize as memory stream
            //change to filestreamwith bigbuffer
            r.BaseStream.Position = Rawoffset;
            byte[] imageBuffer = new byte[r.BaseStream.Length - r.BaseStream.Position];
            r.BaseStream.Read(imageBuffer, 0, imageBuffer.Length);

            rawdataLength = imageBuffer.Length;
            r.Dispose();
            reader = new BinaryReader(new MemoryStream(imageBuffer));
            reader.BaseStream.Position = 0;


        }

        private short lim(short x, short a, short b)
        {
            var t = ((x) < (b) ? (x) : (b));
            return ((a) > (t) ? (a) : (t));
        }

        /*
          * First for 14bit lossless
          * TODO for other raw
          * ver0 = 70 (0x48)
          * ver1 = 48 (0x30)
          * 
          * From DCraw
          * 
          * 
          */
        public ushort[] uncompressed(uint height, uint width, byte[] cfa)
        {
            ushort[] uncompressedData = new ushort[(int)(height * width *3 )]; //add pixel*
            ushort[] huff;

            int tree = 0, row, col, len, shl, diff;
            int min = 0;
            if (version0 == 0x46) tree = 2;
            if (colordepth == 14) tree += 3;

            int maxcounter = curveSize;
            while (maxcounter - 2 >= 0 && curve[maxcounter - 2] == curve[maxcounter - 1]) max--;
            huff = makeDecoder(tree);
            ushort[] huffMinus1 = huff.Skip(1).ToArray();
            getbithuff(-1, null);

            int i = 0;
            for (min = row = 0; row < height; row++)
            {
                if (splitValue > 1 && row == splitValue)
                {
                    huff = makeDecoder(tree + 1);
                    max += ((min = 16) << 1);
                }
                for (col = 0; col < width; col++)
                {
                    //if (row >= 3083) Debug.WriteLine("Col: " + col + " of: " + width);
                    i = (int)getbithuff(huff[0], huffMinus1);
                    len = (i & 15);
                    shl = i >> 4;

                    diff = (short)((getbithuff(len - shl, null) << 1) + 1) << shl >> 1;
                    if ((diff & (1 << (len - 1))) == 0)
                        diff -= (1 << len) - ((shl != 0) ? 1 : 1);
                    if (col < 2)
                    {
                        vpreds[row & 1][col] += (short)diff;
                        hpred[col] = (ushort)(vpreds[row & 1][col]);
                    }
                    else
                    {
                        hpred[col & 1] += (ushort)diff;
                    }
                    if ((ushort)(hpred[col & 1] + min) >= max) throw new Exception("Error during deflate");

                    //TODO change variable names
                    //ushort xy = curve[lim((short)hpred[col & 1], 0, 0x3fff)];
                    //Check wich color is the pixel 
                    //int cfaoffset =cfa[((row % 2) * 2) + col % 2] * colordepth;
                    uncompressedData[(((int)(row * width) + col) * 3) + cfa[((row % 2) * 2) + col % 2]] = curve[lim((short)hpred[col & 1], 0, 0x3fff)];
                }
            }
            /*
            //Create a object for debug
            ushort[] tempsoieo = new ushort[width * height * 3];
            for(int by = 0; by < tempsoieo.Length; by++)
            {
                ushort trtr = 0;
                for(int tr = 0; tr < colordepth; tr++)
                {
                    if (uncompressedData[(by*colordepth) +tr])
                    {
                        trtr |= (ushort)(1 << k);
                    }
                }
                tempsoieo[by] = BitConverter.ToUInt16(BitConverter.GetBytes(trtr).Reverse().ToArray(), 0);
            }
              
             //*/
            return uncompressedData;
        }

        public BitArray uncompressed2(uint height, uint width, byte[] cfa)
        {
            BitArray uncompressedData = new BitArray((int)(height * width * colordepth)); //add pixel*
            ushort[] huff;

            int tree = 0, row, col, len, shl, diff;
            int min = 0;
            if (version0 == 0x46) tree = 2;
            if (colordepth == 14) tree += 3;

            int maxcounter = curveSize;
            while (maxcounter - 2 >= 0 && curve[maxcounter - 2] == curve[maxcounter - 1]) max--;
            huff = makeDecoder(tree);
            ushort[] huffMinus1 = huff.Skip(1).ToArray();
            getbithuff(-1, null);

            int i = 0;
            int k = 0;
            for (min = row = 0; row < height; row++)
            {
                if (splitValue > 1 && row == splitValue)
                {
                    huff = makeDecoder(tree + 1);
                    max += ((min = 16) << 1);
                }
                for (col = 0; col < width; col++)
                {
                    i = (int)getbithuff(huff[0], huffMinus1);
                    len = (i & 15);
                    shl = i >> 4;

                    diff = (short)((getbithuff(len - shl, null) << 1) + 1) << shl >> 1;
                    if ((diff & (1 << (len - 1))) == 0)
                        diff -= (1 << len) - ((shl != 0) ? 1 : 1);
                    if (col < 2)
                    {
                        vpreds[row & 1][col] += (short)diff;
                        hpred[col] = (ushort)(vpreds[row & 1][col]);
                    }
                    else
                    {
                        hpred[col & 1] += (ushort)diff;
                    }
                    if ((ushort)(hpred[col & 1] + min) >= max) throw new Exception("Error during deflate");

                    //TODO change variable names
                    ushort xy = curve[lim((short)hpred[col & 1], 0, 0x3fff)];
                    //Check wich color is the pixel
                    for (k = 0; k < colordepth; k++)
                    {
                        uncompressedData[(((int)(row * width) + col) *  colordepth) +  k] = (((xy >> k) & 1) == 1);
                    }
                }
            }
            return uncompressedData;
        }

        /*
           Construct a decode tree according the specification in *source.
           The first 16 bytes specify how many codes should be 1-bit, 2-bit
           3-bit, etc.  Bytes after that are the leaf values.

           For example, if the source is

            { 0,1,4,2,3,1,2,0,0,0,0,0,0,0,0,0,
              0x04,0x03,0x05,0x06,0x02,0x07,0x01,0x08,0x09,0x00,0x0a,0x0b,0xff  },

           then the code is

	        00		0x04
	        010		0x03
	        011		0x05
	        100		0x06
	        101		0x02
	        1100		0x07
	        1101		0x01
	        11100		0x08
	        11101		0x09
	        11110		0x00
	        111110		0x0a
	        1111110		0x0b
	        1111111		0xff
         */
        public ushort[] makeDecoder(int index)
        {
            //generate the tree
            byte[] count = new byte[nikonTree[index].Length + 2];
            byte[] source = nikonTree[index].Skip(16).ToArray();
            count[0] = 0;
            nikonTree[index].CopyTo(count, 1);

            int len, maxt = 16;

            for (; maxt >= 0 && count[maxt] == 0; maxt--) ;
            ushort[] huff = new ushort[1 + (1 << maxt)];

            huff[0] = (ushort)maxt;
            int xy = 0;
            for (int h = len = 1; len <= maxt; len++)
            {
                for (int i = 0; i < count[len]; i++, ++xy)
                {
                    for (int j = 0; j < 1 << (maxt - len); j++)
                    {
                        if (h <= (1 << maxt))
                        {
                            huff[h++] = (ushort)((len << 8) | source[xy]);
                        }
                    }
                }
            }
            return huff;
        }

        /*
         * read a byte from the raw data
         * 
         */
        public uint getbithuff(int nbits, ushort[] huff)
        {
            if (nbits > 25) { return 0; }
            if (nbits < 0)
            {
                vbits = 0;
                reset = 0;
                bitbuf = 0;
                return 0;
            }
            if (nbits == 0 || vbits < 0) { return 0; }

            //!reset && vbits < nbits && (c = fgetc(ifp)) != EOF && !(reset = zero_after_ff && c == 0xff && fgetc(ifp))
            while (reset == 0 && vbits < nbits && reader.BaseStream.Position < reader.BaseStream.Length)// && !(c == 0xff && reader.ReadByte()== 0))
            {
                c = reader.ReadByte();
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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    reader.Dispose();
                }
                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LinearisationTable() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}