// dcraw.net - camera raw file decoder
// Copyright (C) 1997-2008  Dave Coffin, dcoffin a cybercom o net
// Copyright (C) 2008-2009  Sam Webster, Dave Brown
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.IO;

namespace dcraw.Loaders
{
    internal class NikonCompressedLoader : RawLoader
    {
        private readonly static byte[][] nikon_tree = new [] {
            new byte[] { 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,5,4,3,6,2,7,1,0,8,9,11,10,12 },                  /* 12-bit lossy */
		    new byte[] { 0,1,5,1,1,1,1,1,1,2,0,0,0,0,0,0,0x39,0x5a,0x38,0x27,0x16,5,4,3,2,1,0,11,12,12 }, /* 12-bit lossy after split */
            new byte[] { 0,1,4,2,3,1,2,0,0,0,0,0,0,0,0,0,5,4,6,3,7,2,8,1,9,0,10,11,12 },                  /* 12-bit lossless */
		    new byte[] { 0,1,4,3,1,1,1,1,1,2,0,0,0,0,0,0,5,6,4,7,8,3,9,2,1,0,10,11,12,13,14 },            /* 14-bit lossy */
		    new byte[] { 0,1,5,1,1,1,1,1,1,1,2,0,0,0,0,0,8,0x5c,0x4b,0x3a,0x29,7,6,5,4,3,2,1,0,13,14 },   /* 14-bit lossy after split */
		    new byte[] { 0,1,4,2,2,3,1,2,0,0,0,0,0,0,0,0,7,6,8,5,9,4,10,3,11,12,2,0,1,13,14 } };          /* 14-bit lossless */

        public NikonCompressedLoader(DcRawState state) : base(state) {}

        public override void LoadRaw()
        {
            ushort[,] vpred = new ushort[2,2];
            ushort[] hpred = new ushort[2];
            ushort csize;
            int step = 0;
            int huff = 0;
            int split = 0;
            int row;

            RawStream ifp = state.ifp;

            ifp.Seek(state.meta_offset, SeekOrigin.Begin);
		    ushort ver0 = (ushort)ifp.ReadByte();
            ushort ver1 = (ushort)ifp.ReadByte();

            if (ver0 == 0x49 || ver1 == 0x58)
            {
                ifp.Seek(2110, SeekOrigin.Current);
            }

            if (ver0 == 0x46) huff = 2;
		    if (state.tiff_bps == 14)
		    {
		        huff += 3;
		    }

            vpred[0, 0] = ifp.get2();
            vpred[0, 1] = ifp.get2();
            vpred[1, 0] = ifp.get2();
            vpred[1, 1] = ifp.get2();

		    int max = 1 << state.tiff_bps & 0x7fff;
		    if ((csize = ifp.get2()) > 1)
			    step = max / (csize-1);
		    if (ver0 == 0x44 && ver1 == 0x20 && step > 0) {
		        int i;
		        for (i=0; i < csize; i++)
		        {
		            state.curve[i*step] = ifp.get2();
		        }

			    for (i=0; i < max; i++)
                {
                    state.curve[i] = (ushort)(( state.curve[i-i%step]*(step-i%step) + state.curve[i-i%step+step]*(i%step) ) / step);
                }

			    ifp.Seek(state.meta_offset+562, SeekOrigin.Begin);
			    split = ifp.get2();
		    } else if (ver0 != 0x46 && csize <= 0x4001)
		    {
		        max = csize;
		        ifp.ReadShorts(state.curve, max);
		    }

            int tempIdx = 0;
            HuffmanTree htree = new HuffmanTree(nikon_tree[huff], ref tempIdx);

		    ifp.Seek(state.data_offset, SeekOrigin.Begin);
            ifp.ResetBits();

		    for (row=0; row < state.height; row++) {
			    if (split != 0 && row == split) {
                    tempIdx = 0;
                    htree = new HuffmanTree(nikon_tree[huff], ref tempIdx);
			    }

		        for (int col=0; col < state.raw_width; col++) {
			        int leaf = htree.ReadNextSymbolLength(ifp);
                    int len = leaf & 15;
                    int shl = leaf >> 4;
                    int diff = (((int)ifp.GetBits(len - shl) << 1) + 1) << shl >> 1;
				    if ((diff & (1 << (len-1))) == 0)
				    {
				        diff -= (1 << len) - (shl == 0 ? 1 : 0);
				    }

				    if (col < 2)
				    {
                        vpred[row & 1, col] = (ushort)(vpred[row & 1,col] + diff);
				        hpred[col] = vpred[row & 1, col];
				    }
				    else
				    {
				        hpred[col & 1] = (ushort)(hpred[col & 1] + diff);
				    }

				    if (hpred[col & 1] >= max)
				    {
				        throw new Exception("derror()");
				    }

				    if ((uint)(col-state.left_margin) < state.width)
				    {
				        state.BAYER_set(row, col-state.left_margin, state.curve[hpred[col & 1] & 0x3fff]);
				    }
			    }
		    }
        }
    }
}
