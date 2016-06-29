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

namespace dcraw.Loaders
{
    public abstract class LJpegBase : RawLoader
    {
        protected LJpegBase(DcRawState state) : base(state) 
        {
        }

        private int ljpeg_diff(HuffmanTree huffmanTree)
        {
            int len = huffmanTree.ReadNextSymbolLength(state.InStream);
            if (len == 16 && (state.dng_version == 0 || state.dng_version >= 0x1010000))
                return -32768;

            int diff = (int)state.InStream.GetBits(len);

            if ((diff & (1 << (len - 1))) == 0)
                diff -= (1 << len) - 1;
            return diff;
        }

        protected int ljpeg_row(int jrow, JHead jh)
        {
            int col;
            int c;
            ushort mark = 0;
            int[] irow = new int[3];

            if (jrow * jh.wide % jh.restart == 0)
            {
                for (c = 0; c < 4; c++)
                {
                    jh.vpred[c] = 1 << (jh.bits - 1);
                }

                if (jrow != 0)
                {
                    do
                    {
                        c = state.ifp.ReadByte();
                        mark = (ushort)((mark << 8) + c);
                    } while (c != -1 && mark >> 4 != 0xffd);
                }

                state.InStream.ResetBits();
            }

            for (c = 0; c < 3; c++)
            {
                irow[c] = jh.wide * jh.clrs * ((jrow + c) & 1);
            }

            for (col = 0; col < jh.wide; col++)
            {
                for (c = 0; c < jh.clrs; c++)
                {
                    int diff = ljpeg_diff(jh.huff[c]);
                    int pred;

                    if ((jh.sraw && c < 2 && ((col | c) != 0)))
                    {
                        pred = jh.row[irow[0] + (c << 1) - 3];
                    }
                    else if (col != 0)
                    {
                        pred = jh.row[irow[0] - jh.clrs];
                    }
                    else
                    {
                        pred = (jh.vpred[c] += diff) - diff;
                    }

                    if (jrow != 0 && col != 0)
                    {
                        switch (jh.psv)
                        {
                            case 1:
                                break;
                            case 2:
                                pred = jh.row[irow[1]];
                                break;
                            case 3:
                                pred = jh.row[irow[1] - jh.clrs];
                                break;
                            case 4:
                                pred = pred + jh.row[irow[1]] - jh.row[irow[1] - jh.clrs];
                                break;
                            case 5:
                                pred = pred + ((jh.row[irow[1]] - jh.row[irow[1] - jh.clrs]) >> 1);
                                break;
                            case 6:
                                pred = jh.row[irow[1]] + ((pred - jh.row[irow[1] - jh.clrs]) >> 1);
                                break;
                            case 7:
                                pred = (pred + jh.row[irow[1]]) >> 1;
                                break;
                            default:
                                pred = 0;
                                break;
                        }
                    }

                    jh.row[irow[0]] = (ushort)(pred + diff);
                    if (jh.row[irow[0]] >> jh.bits != 0)
                    {
                        throw new Exception("ljpeg_row: ((jh.row[irow[0]] = (ushort)(pred + diff)) >> jh.bits) != 0");
                    }

                    irow[0]++;
                    irow[1]++;
                }
            }

            return irow[2];
        }
    }
}
