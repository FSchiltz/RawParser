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

namespace dcraw.Demosaicing
{
    /// <summary>
    /// Bilinear interpolation
    /// </summary>
    public class Bilinear : Demosaic
    {
        public Bilinear(DcRawState state) : base(state)
        {
        }

        public override void Process()
        {
            uint filters = state.filters;
            RawImage img = new RawImage(state);

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            Console.WriteLine("Bilinear interpolation...");
            sw.Start();
            PreInterpolate();
            border_interpolate(img, filters, 1);
            InterpolateInternal(img, filters);
            sw.Stop();
            Console.WriteLine("Took {0}ms to interpolate", sw.ElapsedMilliseconds);
        }

        private static void InterpolateInternal(RawImage img, uint filters)
        {
            int topMargin = img.topMargin;
            int leftMargin = img.leftMargin;
            int width = img.width;
            int height = img.height;
            int colours = img.colours;
            ushort[] image = img.image;

            int[] sum = new int[4];
            int[] code = new int[16 * 16 * 32];

            for (int row=0; row < 16; row++)
            {
		        for (int col=0; col < 16; col++)
                {
                    int ip = CodeOffset(row, col);
                    sum[0] = 0; sum[1] = 0; sum[2] = 0; sum[3] = 0;
			        for (int y=-1; y <= 1; y++)
                    {
				        for (int x=-1; x <= 1; x++)
                        {
					        int shift = (y==0 ? 1 : 0) + (x==0 ? 1 : 0);
					        if (shift == 2) continue;
                            int color = fc(filters, row+y,col+x, topMargin, leftMargin);
					        code[ip++] = (width*y + x)*4 + color;
                            code[ip++] = shift;
                            code[ip++] = color;
					        sum[color] += 1 << shift;
				        }
                    }
			        for (int c = 0; c < colours; c++)
                    {
				        if (c != fc(filters, row,col, topMargin, leftMargin))
                        {
                            code[ip++] = c;
                            code[ip++] = 256 / sum[c];
				        }
                    }
		        }
            }

            for (int row=1; row < height-1; row++)
            {
		        for (int col=1; col < width-1; col++)
                {
                    int pixelIndex = row * width + col;
                    int ip = CodeOffset(row & 15, col & 15);
                    sum[0] = 0; sum[1] = 0; sum[2] = 0; sum[3] = 0;

                    for (int i=0; i<8; i++)
                    {
                        ushort pixel = image[pixelIndex * 4 + code[ip]];
				        sum[code[ip + 2]] += pixel << code[ip + 1];
                        ip += 3;
                    }
			        for (int i=colours; i>1; i--)
                    {
                        ushort pixel = (ushort)(sum[code[ip]] * code[ip + 1] >> 8);
                        image[pixelIndex * 4 + code[ip]] = pixel;
                        ip += 2;
                    }
		        }
            }
        }

        private static int CodeOffset(int row, int col)
        {
            return ((row * 16) + col) * 32;
        }

    }

}