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

        /*
         * Source from DCRaw
         * 
         */
        public LinearisationTable(object[] table, ushort compressionType)
        {
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

            if (curveSize == 257 && compressionType == 4)
            {
                curveSize = (short)(1 + curveSize * 2);
            }
            curve = new short[curveSize];
            for(int i = 0; i < curveSize; i++)
            {
                curve[i] = (byte)table[i + 12];
            }

            if(compressionType == 4)
            {
                splitValue = BitConverter.ToInt16(new byte[2] { (byte)table[562], (byte)table[563] }, 0); 
            }
            
        }
    }
}