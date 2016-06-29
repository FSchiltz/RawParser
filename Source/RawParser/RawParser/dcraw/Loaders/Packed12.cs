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
    internal class Packed12Loader : RawLoader
    {
        public Packed12Loader(DcRawState state) : base(state) {}

        public override void LoadRaw()
        {
            int vbits = 0, rbits = 0, irow;
            ulong bitbuf = 0;

            if (state.raw_width * 2 >= state.width * 3)
            {
                /* If state.raw_width is in bytes, */
                rbits = state.raw_width * 8;
                state.raw_width = state.raw_width * 2 / 3; /* convert it to pixels and  */
                rbits -= state.raw_width * 12; /* save the remainder.       */
            }

            state.ifp.Order = (state.load_flags & 1) != 0 ? (short)0x4949 : (short)0x4d4d;

            for (irow = 0; irow < state.height; irow++)
            {
                int row = irow;
                if ((state.load_flags & 2) != 0 && (row = irow * 2 % state.height + irow / (state.height / 2)) == 1 && (state.load_flags & 4) != 0)
                {
                    vbits = 0;
                    if (state.tiff_compress != 0)
                    {
                        state.ifp.Seek(state.data_offset - (-state.width * state.height * 3 / 4 & -2048), SeekOrigin.Begin);
                    }
                    else
                    {
                        state.ifp.Seek(0, SeekOrigin.End);
                        state.ifp.Seek(state.ifp.Position / 2, SeekOrigin.Begin);
                    }
                }
                for (int col = 0; col < state.raw_width; col++)
                {
                    if ((vbits -= 12) < 0)
                    {
                        bitbuf = bitbuf << 32 | state.ifp.get4();
                        vbits += 32;
                    }
                    if ((uint)(col - state.left_margin) < state.width)
                    {
                        state.BAYER_set(row, col - state.left_margin, (ushort)(bitbuf << (52 - vbits) >> 52));
                    }
                    if ((state.load_flags & 8) != 0 && (col % 10) == 9)
                    {
                        vbits = 0;
                        if ((bitbuf & 255) != 0)
                        {
                            throw new Exception("derror()");
                        }
                    }
                }
                vbits -= rbits;
            }

            if (state.make == "OLYMPUS")
            {
                state.black >>= 4;
            }
        }
    }
}
