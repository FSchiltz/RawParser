using RawNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawEditor.Effect
{
    static class FujiDemos
    {
        static RawImage img;
        static ushort fcol(int row, int col) { return (ushort)img.colorFilter.cfa[((row + 6) % 6) * 6 + (col + 6) % 6]; }
        static unsafe public void Demosaic(RawImage image)
        {
            img = image;
            /* Taken from https://gist.github.com/TJC/09920b086911ad3a24a4 and  https://www.cybercom.net/~dcoffin/dcraw/dcraw.c */
            /* Which itself attributes this algorithm to "Frank Markesteijn" */
            int passes = 0;
            int TS = 512;   // Tile Size 
            int d, f, g, h, i, v, ng, row, col, top, left, mrow, mcol;
            int val, ndir, pass;
            int[] hm = new int[8], avg = new int[4];
            int[,] color = new int[3, 8];
            short[] orth = { 1, 0, 0, 1, -1, 0, 0, -1, 1, 0, 0, 1 };
            short[][] patt = { new short[] { 0, 1, 0, -1, 2, 0, -1, 0, 1, 1, 1, -1, 0, 0, 0, 0 }, new short[] { 0, 1, 0, -2, 1, 0, -2, 0, 1, 1, -2, -2, 1, -1, -1, 1 } };
            int[] dir = { 1, TS, TS + 1, TS - 1 };
            long[,,][] allhex = new long[3, 3, 2][];
            long[] hex;
            ushort min, max;
            int sgrow = 0, sgcol = 0;
            uint height = image.raw.dim.height;
            uint width = image.raw.dim.width;
            long pix;
            /*ushort(*rgb)[TS][TS][3], (*rix)[3], (*pix)[4];
            short(*lab)[TS][3], (*lix)[3];
            float(*drv)[TS][TS];*/
            float[] diff = new float[6];
            float tr;
            //byte(*homo)[TS][TS];
            //cielab(0, 0);
            ndir = 4 << ((passes > 1) ? 1 : 0);
            byte[] buffer = new byte[TS * TS * (ndir * 11 + 6)];
            //merror(buffer, "xtrans_interpolate()");
            /* rgb = (ushort(*)[TS][TS][3]) buffer;
             lab = (short(*)[TS][3])(buffer + TS * TS * (ndir * 6));
             drv = (float(*)[TS][TS])(buffer + TS * TS * (ndir * 6 + 6));
             homo = (char(*)[TS][TS])(buffer + TS * TS * (ndir * 10 + 6));
             */

            /* Map a green hexagon around each non-green pixel and vice versa:	*/
            for (row = 0; row < 3; row++)
            {
                for (col = 0; col < 3; col++)
                {
                    for (ng = d = 0; d < 10; d += 2)
                    {
                        g = (fcol(row, col) == 1) ? 1 : 0;
                        if (fcol(row + orth[d], col + orth[d + 2]) == 1)
                        {
                            ng = 0;
                        }
                        else
                        {
                            ng++;
                        }
                        if (ng == 4)
                        {
                            sgrow = row;
                            sgcol = col;
                        }
                        if (ng == g + 1)
                        {
                            for (int c = 0; c < 8; c++)
                            {
                                v = orth[d] * patt[g][c * 2] + orth[d + 1] * patt[g][c * 2 + 1];
                                h = orth[d + 2] * patt[g][c * 2] + orth[d + 3] * patt[g][c * 2 + 1];
                                allhex[row, col, 0][c ^ (g * 2 & d)] = h + v * width;
                                allhex[row, col, 1][c ^ (g * 2 & d)] = h + v * TS;
                            }
                        }
                    }
                }
            }
            /* Set green1 and green3 to the minimum and maximum allowed values:	
            for (row = 2; row < height - 2; row++)
            {
                for (min = (ushort)~(max = 0), col = 2; col < width - 2; col++)
                {
                    if ((ushort)((fcol(row, col) == 1) ? 1 : 0) && (min = (ushort)~(max = 0))) continue;

                    pix = row * width + col;
                    hex = allhex[row % 3, col % 3, 0];
                    if (max == 0)
                    {
                        for (int c = 0; c < 6; c++)
                        {
                            val = pix[hex[c]][1];
                            if (min > val) min = (ushort)val;
                            if (max < val) max = (uhsort)val;
                        }
                    }
                    pix[0][1] = min;
                    pix[0][3] = max;
                    switch ((row - sgrow) % 3)
                    {
                        case 1:
                            if (row < height - 3) { row++; col--; }
                            break;
                        case 2:
                            if (((min = (ushort)~(max = 0))) != 0 && (col += 2) < width - 3 && row > 2) row--;
                            break;
                    }
                }
            }*/

            for (top = 3; top < height - 19; top += TS - 16)
            {
                for (left = 3; left < width - 19; left += TS - 16)
                {
                    mrow = (int)Math.Min(top + TS, height - 3);
                    mcol = (int)Math.Min(left + TS, width - 3);
                    for (row = top; row < mrow; row++)
                    {
                        for (col = left; col < mcol; col++)
                        {
                            memcpy(rgb[0][row - top][col - left], image[row * width + col], 6);
                        }
                    }
                    for (int c = 0; c < 3; c++)
                    {
                        memcpy(rgb[c + 1], rgb[0], sizeof *rgb);
                    }

                    /* Interpolate green horizontally, vertically, and along both diagonals: */
                    for (row = top; row < mrow; row++)
                    {
                        for (col = left; col < mcol; col++)
                        {
                            if ((f = fcol(row, col)) == 1) continue;
                            pix = image + row * width + col;
                            hex = allhex[row % 3, col % 3, 0];
                            color[1, 0] = 174 * (pix[hex[1]][1] + pix[hex[0]][1]) -
                                   46 * (pix[2 * hex[1]][1] + pix[2 * hex[0]][1]);
                            color[1, 1] = 223 * pix[hex[3]][1] + pix[hex[2]][1] * 33 +
                                   92 * (pix[0][f] - pix[-hex[2]][f]);

                            for (int c = 0; c < 2; c++)
                            {
                                color[1, 2 + c] = 164 * pix[hex[4 + c]][1] + 92 * pix[-2 * hex[4 + c]][1] + 33 * (2 * pix[0][f] - pix[3 * hex[4 + c]][f] - pix[-3 * hex[4 + c]][f]);
                            }
                            for (int c = 0; c < 4; c++)
                            {
                                rgb[c ^ !((row - sgrow) % 3)][row - top][col - left][1] = LIM(color[1][c] >> 8, pix[0][1], pix[0][3]);
                            }
                        }
                    }



                    /* Interpolate red and blue values for solitary green pixels:	*/
                    for (row = (top - sgrow + 4) / 3 * 3 + sgrow; row < mrow - 2; row += 3)
                        for (col = (left - sgcol + 4) / 3 * 3 + sgcol; col < mcol - 2; col += 3)
                        {
                            rix = &rgb[0][row - top][col - left];
                            h = fcol(row, col + 1);

                            for (i = 1, d = 0; d < 6; d++, i ^= TS ^ 1, h ^= 2)
                            {
                                for (int c = 0; c < 2; c++, h ^= 2)
                                {
                                    g = 2 * rix[0][1] - rix[i << c][1] - rix[-i << c][1];
                                    color[h][d] = g + rix[i << c][h] + rix[-i << c][h];
                                    if (d > 1)
                                        diff[d] += SQR(rix[i << c][1] - rix[-i << c][1]
                                              - rix[i << c][h] + rix[-i << c][h]) + SQR(g);
                                }
                                if (d > 1 && (d & 1) != 0)
                                {
                                    if (diff[d - 1] < diff[d])
                                    {

                                        for (int c = 0; c < 2; c++)
                                        {
                                            color[c * 2, d] = color[c * 2, d - 1];
                                        }
                                    }
                                }
                                if (d < 2 || (d & 1) != 0)
                                {
                                    for (int c = 0; c < 2; c++)
                                    {
                                        rix[0][c * 2] = CLIP(color[c * 2, d] / 2);
                                    }
                                    rix += TS * TS;
                                }
                            }
                        }

                    /* Interpolate red for blue pixels and vice versa:		*/
                    for (row = top + 3; row < mrow - 3; row++)
                    {
                        for (col = left + 3; col < mcol - 3; col++)
                        {
                            if ((f = 2 - fcol(row, col)) == 1) continue;
                            rix = &rgb[0][row - top][col - left];
                            int c = ((row - sgrow) % 3) != 0 ? TS : 1;
                            h = 3 * (c ^ TS ^ 1);
                            for (d = 0; d < 4; d++, rix += TS * TS)
                            {
                                i = d > 1 || ((d ^ c) & 1) != 0 || ((Math.Abs(rix[0][1] - rix[c][1]) + Math.Abs(rix[0][1] - rix[-c][1])) <
                              2 * (Math.Abs(rix[0][1] - rix[h][1]) + Math.Abs(rix[0][1] - rix[-h][1]))) ? c : h;
                                rix[0][f] = CLIP((rix[i][f] + rix[-i][f] +
                                2 * rix[0][1] - rix[i][1] - rix[-i][1]) / 2);
                            }
                        }
                    }

                    /* Fill in red and blue for 2x2 blocks of green:		*/
                    for (row = top + 2; row < mrow - 2; row++)
                    {
                        if (((row - sgrow) % 3) != 0)
                        {
                            for (col = left + 2; col < mcol - 2; col++)
                            {
                                if (((col - sgcol) % 3) != 0)
                                {
                                    rix = &rgb[0][row - top][col - left];
                                    hex = allhex[row % 3, kcol % 3, 1];
                                    for (d = 0; d < ndir; d += 2, rix += TS * TS)
                                    {
                                        if ((hex[d] + hex[d + 1]) != 0)
                                        {
                                            g = 3 * rix[0][1] - 2 * rix[hex[d]][1] - rix[hex[d + 1]][1];
                                            for (int c = 0; c < 4; c += 2)
                                            {
                                                rix[0][c] = CLIP((g + 2 * rix[hex[d]][c] + rix[hex[d + 1]][c]) / 3);
                                            }
                                        }
                                        else
                                        {
                                            g = 2 * rix[0][1] - rix[hex[d]][1] - rix[hex[d + 1]][1];
                                            for (int c = 0; c < 4; c += 2)
                                            {
                                                rix[0][c] = CLIP((g + rix[hex[d]][c] + rix[hex[d + 1]][c]) / 2);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    rgb = (ushort(*)[TS][TS][3]) buffer;
                    mrow -= top;
                    mcol -= left;

                }
            }
        }
    }
}
