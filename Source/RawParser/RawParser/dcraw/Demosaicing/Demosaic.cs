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

namespace dcraw.Demosaicing
{
    public abstract class Demosaic : Filter
    {
        protected Demosaic(DcRawState state) : base(state)
        {
        }

        private static readonly byte[,] filter = new byte[16, 16]
                                                     { { 2,1,1,3,2,3,2,0,3,2,3,0,1,2,1,0 },
                                                       { 0,3,0,2,0,1,3,1,0,1,1,2,0,3,3,2 },
                                                       { 2,3,3,2,3,1,1,3,3,1,2,1,2,0,0,3 },
                                                       { 0,1,0,1,0,2,0,2,2,0,3,0,1,3,2,1 },
                                                       { 3,1,1,2,0,1,0,2,1,3,1,3,0,1,3,0 },
                                                       { 2,0,0,3,3,2,3,1,2,0,2,0,3,2,2,1 },
                                                       { 2,3,3,1,2,1,2,1,2,1,1,2,3,0,0,1 },
                                                       { 1,0,0,2,3,0,0,3,0,3,0,3,2,1,2,3 },
                                                       { 2,3,3,1,1,2,1,0,3,2,3,0,2,3,1,3 },
                                                       { 1,0,2,0,3,0,3,2,0,1,1,2,0,1,0,2 },
                                                       { 0,1,1,3,3,2,2,1,1,3,3,0,2,1,3,2 },
                                                       { 2,3,2,0,0,1,3,0,2,0,1,2,3,0,1,0 },
                                                       { 1,3,1,2,3,2,3,2,0,2,0,1,1,0,3,0 },
                                                       { 0,2,0,3,1,0,0,1,1,3,3,2,3,2,2,1 },
                                                       { 2,1,3,2,3,1,2,1,0,3,0,2,0,2,0,2 },
                                                       { 0,3,1,0,0,2,0,3,2,1,3,1,1,3,1,3 } };

        protected static void border_interpolate(RawImage img, uint filters, int border)
        {
            int height = img.height;
            int width = img.width;
            ushort[] image = img.image;
            int colors = img.colours;
            int left_margin = img.leftMargin;
            int top_margin = img.topMargin;

            for (int row = 0; row < height; row++)
            {
                
                for (int col = 0; col < width; col++)
                {
                    if (col == border && row >= border && row < height - border)
                    {
                        col = width - border;
                    }
                    int[] sum = new int[8];
                    for (uint y = (uint)row - 1; y != row + 2; y++)
                    {
                        for (uint x = (uint)col - 1; x != col + 2; x++)
                        {
                            int f;
                            // Using uint for x and y means we don't need to check < 0
                            
                            if (y < height && x < width)
                            {
                                f = fc(filters, (int)y, (int)x, left_margin, top_margin);
                                sum[f] += image[(y * width + x) * 4 + f];
                                sum[f + 4]++;
                            }
                            f = fc(filters, row, col, left_margin, top_margin);
                            
                            for (int c = 0; c < colors; c++)
                            {
                                if (c != f && sum[c + 4] != 0)
                                {
                                    image[(row * width + col) * 4 + c] = (ushort)(sum[c] / sum[c + 4]);
                                }
                            }
                        }
                    }
                }
            }
        }

        protected static int fc(uint filters, int row, int col, int top_margin, int left_margin)
        {
            return filters != 1 ? FC(filters, row, col) : filter[(row + top_margin) & 15, (col + left_margin) & 15];
        }

        protected void PreInterpolate()
        {
            PreInterpolate(state);
        }

        public static void PreInterpolate(DcRawState state)
        {
            if (state.shrink != 0)
            {
                if (state.half_size)
                {
                    state.height = state.iheight;
                    state.width = state.iwidth;
                }
                else
                {
                    Shrink(state);
                }
            }

            if (state.filters != 0 && state.colors == 3)
            {
                state.mix_green = state.four_color_rgb;
                if (state.mix_green)
                {
                    state.colors++;
                }
                else
                {
                    for (int row = state.FC(1, 0) >> 1; row < state.height; row += 2)
                    {
                        for (int col = state.FC(row, 1) & 1; col < state.width; col += 2)
                        {
                            state.image[(row*state.width + col) * 4 + 1] = state.image[(row*state.width + col) * 4 + 3];
                        }
                    }

                    state.filters &= ~((state.filters & 0x55555555) << 1);
                }
            }

            if (state.half_size)
            {
                state.filters = 0;
            }
        }

        private static int fc(DcRawState state, int row, int col)
        {
            return state.filters != 1 ? state.FC(row, col) : filter[(row + state.top_margin) & 15, (col + state.left_margin) & 15];
        }

        private static void Shrink(DcRawState state)
        {
            ushort[] img = new ushort[state.height * state.width * 4];
            for (int row = 0; row < state.height; row++)
            {
                for (int col = 0; col < state.width; col++)
                {
                    int c = fc(state, row, col);
                    img[(row*state.width + col) * 4 + c] = state.image[((row >> 1)*state.iwidth + (col >> 1)) * 4 + c];
                }
            }

            state.image = img;
            state.shrink = 0;
        }
    }

    public class BasicDemosiac : Demosaic
    {
        public BasicDemosiac(DcRawState state) : base(state) { }

        public override void Process()
        {
            PreInterpolate();
        }
    }
}
