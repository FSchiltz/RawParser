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

using System.IO;

namespace dcraw.Loaders
{
    public sealed class AdobeDngLoader : LJpegBase
    {
        public AdobeDngLoader(DcRawState state) : base(state) { }

        public override void LoadRaw()
        {
            int trow = 0;
            int tcol = 0;

            JHead jh;

            while (trow < state.raw_height)
            {
                int save = (int)state.ifp.Position;
                if (state.tile_length < int.MaxValue)
                {
                    state.ifp.Seek(state.ifp.get4(), SeekOrigin.Begin);
                }

                jh = new JHead(state, state.ifp, false, state.dng_version);

                int jwide = jh.wide;
                if (state.filters != 0)
                {
                    jwide *= jh.clrs;
                }

                jwide /= state.is_raw;

                int jrow = 0;
                int row = 0;
                int col = 0;
                for (; jrow < jh.high; jrow++)
                {
                    int rpi = ljpeg_row(jrow, jh);
                    int jcol;
                    for (jcol = 0; jcol < jwide; jcol++)
                    {
                        adobe_copy_pixel(jh.row, trow + row, tcol + col, ref rpi);
                        if (++col >= state.tile_width || col >= state.raw_width)
                            row += 1 + (col = 0);
                    }
                }

                state.ifp.Seek(save + 4, SeekOrigin.Begin);

                if ((tcol += (int)state.tile_width) >= state.raw_width)
                {
                    tcol = 0;
                    trow += (int)state.tile_length + tcol;
                }

                //state.Free(jh.row);
            }
        }

        private void adobe_copy_pixel(ushort[] rows, int row, int col, ref int rpi)
        {
            row -= state.top_margin;
            col -= state.left_margin;
            uint r = (uint)row;
            uint c = (uint)col;

            if (state.is_raw == 2 && state.shot_select != 0) rpi++;

            if (state.filters != 0)
            {
                if (state.fuji_width != 0)
                {
                    r = (uint)(row + state.fuji_width - 1 - (col >> 1));
                    c = (uint)(row + ((col + 1) >> 1));
                }
                if (r < state.height && c < state.width)
                {
                    state.BAYER_set((int)r, (int)c, rows[rpi] < 0x1000 ? state.curve[rows[rpi]] : rows[rpi]);
                }

                rpi += state.is_raw;
            }
            else
            {
                if (r < state.height && c < state.width)
                    for (c = 0; c < state.tiff_samples; c++)
                    {
                        state.image[(row*state.width + col) * 4 +c] = rows[rpi + c] < 0x1000 ? state.curve[rows[rpi + c]] : rows[rpi + c];
                    }
                rpi += state.tiff_samples;
            }

            if (state.is_raw == 2 && state.shot_select != 0) rpi--;
        }
    }
}
