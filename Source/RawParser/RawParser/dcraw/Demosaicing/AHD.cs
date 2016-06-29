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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace dcraw.Demosaicing
{
    /// <summary>
    /// Adaptive Homogeneity-Directed interpolation is based on the work of Keigo Hirakawa, Thomas Parks, and Paul Lee.
    /// </summary>
    public class AHD : Demosaic
    {
        private const int TILE_POW = 8;
        private const int TILE_SIZE = 1 << TILE_POW;
        private static readonly int[] dir = new[] { -1, 1, -TILE_SIZE, TILE_SIZE };
        private const int BORDER = 5;

        public AHD(DcRawState state)
            : base(state)
        {
        }

        private static readonly float[] cbrt;

        static AHD()
        {
            // This calculation is responsible for the differences in MCPP dcraw and C# dcraw output files
            // The following lines compile with different constants in C# and MCPP which result in a different value when cast to an int:
            // Console.WriteLine("{0}f", 32000.0f * (
            //                                          (float)Math.Pow((double)(2241 / 65535.0f), (double)(1 / 3.0f)) -
            //                                          (float)Math.Pow((double)(1745 / 65535.0f), (double)(1 / 3.0f))
            //                                      ));

            cbrt = new float[0x10000];
            for (int i = 0; i < 0x10000; i++)
            {
                float r = i / 65535.0f;
                cbrt[i] = r > 0.008856 ? (float)Math.Pow(r, 1 / 3.0f) : 7.787f * r + 16 / 116.0f;
            }
        }

        public override void Process()
        {
            PreInterpolate();
            
            // Gather values from state
            RawImage img = new RawImage(state);
            double[,] xyz_rgb = state.xyz_rgb;
            float[,] rgb_cam = state.rgb_cam;
            float[] d65_white = state.d65_white;
            uint filters = state.filters;
            //-------------------------

            border_interpolate(img, filters, BORDER);

            float[,] xyz_cam = new float[3, 4];
            for (int i = 0; i < 3; i++)
            {
                
                for (int j = 0; j < img.colours; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        xyz_cam[i, j] += (float)xyz_rgb[i, k] * rgb_cam[k, j] / d65_white[i];
                    }
                }
            }

            Dictionary<Task, TileData> threadTileData = new Dictionary<Task, TileData>();

            using (TaskMultiplexer multiplexer = new TaskMultiplexer())
            {
                for (int top = 2; top < img.height - BORDER; top += TILE_SIZE - BORDER - 1)
                {
                    

                    for (int left = 2; left < img.width - BORDER; left += TILE_SIZE - BORDER - 1)
                    {
                        int topCopy = top;
                        int leftCopy = left;
                        multiplexer.QueueWorkItem(new Task(() =>
                                                      {
                                                          TileData td;

                                                          lock (threadTileData)
                                                          {
                                                              threadTileData.TryGetValue(TaskScheduler., out td);
                                                              if (td == null)
                                                              {
                                                                  td = new TileData();
                                                                  threadTileData[Task.CurrentId] = td;
                                                              }
                                                          }

                                                          ProcessTile(img, leftCopy, topCopy, filters, xyz_cam, td);
                                                      }));
                    }
                }
            }
        }

        private class TileData
        {
            public readonly ushort[] r0 = new ushort[TILE_SIZE * TILE_SIZE];
            public readonly ushort[] g0 = new ushort[TILE_SIZE * TILE_SIZE];
            public readonly ushort[] b0 = new ushort[TILE_SIZE * TILE_SIZE];
            public readonly ushort[] r1 = new ushort[TILE_SIZE * TILE_SIZE];
            public readonly ushort[] g1 = new ushort[TILE_SIZE * TILE_SIZE];
            public readonly ushort[] b1 = new ushort[TILE_SIZE * TILE_SIZE];

            public readonly byte[][] homo = new[] { new byte[TILE_SIZE * TILE_SIZE], new byte[TILE_SIZE * TILE_SIZE] };
            public readonly ushort[][][] rgb;
            public readonly short[][][] lab = new[] { new[]
                                      {
                                          new short[TILE_SIZE * TILE_SIZE],
                                          new short[TILE_SIZE * TILE_SIZE],
                                          new short[TILE_SIZE * TILE_SIZE]
                                      }, 
                                      new[]
                                      {
                                          new short[TILE_SIZE * TILE_SIZE],
                                          new short[TILE_SIZE * TILE_SIZE],
                                          new short[TILE_SIZE * TILE_SIZE]
                                      } };

            public TileData()
            {
                rgb = new[] { new[] { r0, g0, b0 }, new[] { r1, g1, b1 }};
            }
        }

        private static void ProcessTile(RawImage img, int left, int top, uint filters, float[,] xyz_cam, TileData td)
        {
            ushort[] r0 = td.r0;
            ushort[] g0 = td.g0;
            ushort[] b0 = td.b0;
            ushort[] r1 = td.r1;
            ushort[] g1 = td.g1;
            ushort[] b1 = td.b1;
            ushort[][][] rgb = td.rgb;
            short[][][] lab = td.lab;
            byte[][] homo = td.homo;

            // Interpolate green horizontally and vertically
            int imgHeight = img.height;
            int imgWidth = img.width;
            int colors = img.colours;

            using (Profiler.BlockProfile("AHD Green"))
            {
                Green(img, left, top, filters, g0, g1);
            }

            using (Profiler.BlockProfile("AHD Red Blue"))
            {
                RedBlue(img, left, top, colors, filters, lab, rgb, xyz_cam);
            }

            // Build homogeneity maps from the CIELab images
            using (Profiler.BlockProfile("AHD Homogeneity map"))
            {
                Homogeneity(img, top, left, lab, homo);
            }

            // Combine the most homogenous pixels for the final result

            ushort[] imgArray = img.image;

            using (Profiler.BlockProfile("AHD image production"))
            {
                int colLimit = Math.Min(left + TILE_SIZE - 3, imgWidth - BORDER);
                int rowLimit = Math.Min(top + TILE_SIZE - 3, imgHeight - BORDER);
                byte[] homo0 = homo[0];
                byte[] homo1 = homo[1];
                int tileidxLimit = Math.Min(Math.Min(Math.Min(Math.Min(Math.Min(r0.Length, r1.Length), g0.Length), g1.Length), b0.Length), b1.Length);
                for (int row = top + 3; row < rowLimit; row++)
                {
                    int tiley = row - top;
                    int tileySpan = tiley * TILE_SIZE;
                    int iLimit = (tiley + 1) * TILE_SIZE;
                    int col = left + 3;
                    int tilex = col - left;
                    int tileidx = tilex + tileySpan;
                    for (; col < colLimit && tileidx < tileidxLimit; col++, tileidx++)
                    {
                        int hm0 = 0;
                        int hm1 = 0;
                        for (int i = (tiley - 1) * TILE_SIZE; i <= iLimit; i += TILE_SIZE)
                        {
                            int idx = i + tilex - 1;
                            // Weird stuff here - this line speeds up image production by ~100ms but slows down AHD Red Blue by lots more!
                            if (idx > homo0.Length + 2 || idx > homo1.Length + 2) { break;}

                            hm0 += homo0[idx];
                            hm1 += homo1[idx++];
                            hm0 += homo0[idx];
                            hm1 += homo1[idx++];
                            hm0 += homo0[idx];
                            hm1 += homo1[idx];
                        }

                        int index_x4 = (row * imgWidth + col) * 4;

                        if (hm0 != hm1)
                        {
                            ushort[][] rgbx = rgb[hm1 > hm0 ? 1 : 0];
                            for (int c = 0; c < 3 && c < rgbx.Length; c++)
                            {
                                imgArray[index_x4 + c] = rgbx[c][tileidx];
                            }
                        }
                        else
                        {
                            imgArray[index_x4++] = (ushort)((r0[tileidx] + r1[tileidx]) >> 1);
                            imgArray[index_x4++] = (ushort)((g0[tileidx] + g1[tileidx]) >> 1);
                            imgArray[index_x4] = (ushort)((b0[tileidx] + b1[tileidx]) >> 1);
                        }
                    }
                }
            }
        }

        private static void Homogeneity(RawImage img, int top, int left, short[][][] lab, byte[][] homo)
        {
            int imgWidth = img.width;
            int imgHeight = img.height;
            int colLimit = Math.Min(left + TILE_SIZE - 2, imgWidth - 4);
            int rowLimit = Math.Min(top + TILE_SIZE - 2, imgHeight - 4);
            int dir0 = dir[0];
            int dir1 = dir[1];
            int dir2 = dir[2];
            int dir3 = dir[3];

            for (int row = top + 2; row < rowLimit; row++)
            {
                int tiley = row - top;
                for (int col = left + 2; col < colLimit; col++)
                {
                    // Silly amounts of unrolling save approx 800ms
                    int tileidx = (col - left) + tiley*TILE_SIZE;

                    short[][] labx = lab[0];
                    short[] labx0 = labx[0];
                    short[] labx1 = labx[1];
                    short[] labx2 = labx[2];
                    int labx0val = labx0[tileidx];
                    int labx1val = labx1[tileidx];
                    int labx2val = labx2[tileidx];

                    int tileidxDir = tileidx + dir0;
                    int ldiff_0_0 = Math.Abs(labx0val - labx0[tileidxDir]);
                    int a = labx1val - labx1[tileidxDir];
                    int b = labx2val - labx2[tileidxDir];
                    int abdiff_0_0 = a*a + b*b;

                    tileidxDir = tileidx + dir1;
                    int ldiff_0_1 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_0_1 = a*a + b*b;

                    tileidxDir = tileidx + dir2;
                    int ldiff_0_2 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_0_2 = a*a + b*b;

                    tileidxDir = tileidx + dir3;
                    int ldiff_0_3 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_0_3 = a*a + b*b;

                    labx = lab[1];
                    labx0 = labx[0];
                    labx1 = labx[1];
                    labx2 = labx[2];
                    labx0val = labx0[tileidx];
                    labx1val = labx1[tileidx];
                    labx2val = labx2[tileidx];

                    tileidxDir = tileidx + dir0;
                    int ldiff_1_0 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_1_0 = a*a + b*b;

                    tileidxDir = tileidx + dir1;
                    int ldiff_1_1 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_1_1 = a*a + b*b;

                    tileidxDir = tileidx + dir2;
                    int ldiff_1_2 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_1_2 = a*a + b*b;

                    tileidxDir = tileidx + dir3;
                    int ldiff_1_3 = Math.Abs(labx0val - labx0[tileidxDir]);
                    a = labx1val - labx1[tileidxDir];
                    b = labx2val - labx2[tileidxDir];
                    int abdiff_1_3 = a*a + b*b;

                    int leps = Math.Min(
                        Math.Max(ldiff_0_0, ldiff_0_1),
                        Math.Max(ldiff_1_2, ldiff_1_3));
                    int abeps = Math.Min(
                        Math.Max(abdiff_0_0, abdiff_0_1),
                        Math.Max(abdiff_1_2, abdiff_1_3));

                    byte val = 0;
                    if (ldiff_0_0 <= leps && abdiff_0_0 <= abeps) val++;
                    if (ldiff_0_1 <= leps && abdiff_0_1 <= abeps) val++;
                    if (ldiff_0_2 <= leps && abdiff_0_2 <= abeps) val++;
                    if (ldiff_0_3 <= leps && abdiff_0_3 <= abeps) val++;
                    homo[0][tileidx] = val;

                    val = 0;
                    if (ldiff_1_0 <= leps && abdiff_1_0 <= abeps) val++;
                    if (ldiff_1_1 <= leps && abdiff_1_1 <= abeps) val++;
                    if (ldiff_1_2 <= leps && abdiff_1_2 <= abeps) val++;
                    if (ldiff_1_3 <= leps && abdiff_1_3 <= abeps) val++;
                    homo[1][tileidx] = val;
                }
            }
        }

        private static void RedBlue(RawImage imxg, int left, int top, int colors, uint filters, short[][][] lab, ushort[][][] rgb, float[,] xyz_cam)
        {
            ushort[] img = imxg.image;
            int imgWidth = imxg.width;
            int imgHeight = imxg.height;

            int imgWidth_x4 = imgWidth * 4;
            float[] localCbrt = cbrt;
            // Interpolate red and blue, and convert to CIELab
            for (int d = 0; d < 2; d++)
            {
                ushort[][] rgbx = rgb[d];
                ushort[] gx = rgbx[1];
                short[][] labx = lab[d];
                short[] labx0 = labx[0];
                short[] labx1 = labx[1];
                short[] labx2 = labx[2];

                for (int row = top + 1; row < top + TILE_SIZE - 1 && row < imgHeight - 3; row++)
                {
                    int tiley = row - top;
                    int tiley_x_TILE_SIZE = tiley * TILE_SIZE;
                    int col = left + 1;
                    int ipix_x4 = (row * imgWidth + col) * 4;
                    int tileidx = (col - left) + tiley_x_TILE_SIZE;

                    int ipix_x4_limit = img.Length + imgWidth_x4;

                    for (int noPix = Math.Min(left + TILE_SIZE - 1, imgWidth - 3) - col;
                         noPix > 0 && ipix_x4 < ipix_x4_limit;
                         col++, ipix_x4 += 4, noPix--, tileidx++)
                    {
                        int val;

                        int fc_rowcol = FC(filters, row, col);

                        int c = 2 - fc_rowcol;
                        if (c == 1)
                        {
                            c = FC(filters, row + 1, col);
                            int ipix_left = ipix_x4 - 4;
                            int ipix_right = ipix_x4 + 4;

                            val = img[ipix_x4 + 1] +
                                  ((img[ipix_left + 2 - c] +
                                    img[ipix_right + 2 - c]
                                    - gx[tileidx - 1]
                                    - gx[tileidx + 1]) >> 1);

                            rgbx[2 - c][tileidx] = (ushort)Math.Max(Math.Min(val, 65535), 0);
                            int ipix_up = ipix_x4 - imgWidth_x4;
                            int ipix_down = ipix_x4 + imgWidth_x4;
                            val = img[ipix_x4 + 1] +
                                  ((img[ipix_up + c] +
                                    img[ipix_down + c]
                                    - gx[tileidx - TILE_SIZE]
                                    - gx[tileidx + TILE_SIZE]) >> 1);
                        }
                        else
                        {
                            int ipix_upleft = ipix_x4 - imgWidth_x4 - 4;
                            int ipix_upright = ipix_upleft + 8;
                            int ipix_downleft = ipix_x4 + imgWidth_x4 - 4;
                            int ipix_downright = ipix_downleft + 8;

                            val = gx[tileidx] + ((img[ipix_upleft + c] + img[ipix_upright + c] + img[ipix_downleft + c] + img[ipix_downright + c]
                                                  - gx[tileidx - TILE_SIZE - 1] - gx[tileidx - TILE_SIZE + 1]
                                                  - gx[tileidx + TILE_SIZE - 1] - gx[tileidx + TILE_SIZE + 1] + 1) >>
                                                 2);
                        }

                        rgbx[c][tileidx] = (ushort)Math.Max(Math.Min(val, 65535), 0);

                        c = fc_rowcol;
                        rgbx[c][tileidx] = img[ipix_x4 + c];
                        float xyz0 = 0.5f, xyz1 = 0.5f, xyz2 = 0.5f;
                        // This seems to eliminate bounds checking from the inner loop
                        int xyz_cam_length1 = xyz_cam.GetLength(1);
                        for (int cl = 0; cl < colors && cl < xyz_cam_length1; cl++)
                        {
                            int mul = rgbx[cl][tileidx];
                            xyz0 += xyz_cam[0, cl] * mul;
                            xyz1 += xyz_cam[1, cl] * mul;
                            xyz2 += xyz_cam[2, cl] * mul;
                        }

                        xyz0 = localCbrt[Utils.Clip16((int)xyz0)];
                        xyz1 = localCbrt[Utils.Clip16((int)xyz1)];
                        xyz2 = localCbrt[Utils.Clip16((int)xyz2)];
                        labx0[tileidx] = (short)(64 * (116 * xyz1 - 16));
                        labx1[tileidx] = (short)(64 * 500 * (xyz0 - xyz1));
                        labx2[tileidx] = (short)(64 * 200 * (xyz1 - xyz2));
                    }
                }
            }
        }

        private static void Green(RawImage imxg, int left, int top, uint filters, ushort[] g0, ushort[] g1)
        {
            ushort[] img = imxg.image;
            int imgWidth = imxg.width;
            int imgHeight = imxg.height;

            int imgWidth_x4 = imgWidth * 4;

            for (int row = top; row < top + TILE_SIZE && row < imgHeight - 2; row++)
            {
                int col = left + (FC(filters, row, left) & 1);
                int c = FC(filters, row, col);
                for (; col < left + TILE_SIZE && col < imgWidth - 2; col += 2)
                {
                    int ipix = row * imgWidth + col;

                    int ipix_x4 = ipix * 4;
                    int ipix_up = ipix_x4 - imgWidth_x4;
                    int ipix_up2 = ipix_up - imgWidth_x4;
                    int ipix_down = ipix_x4 + imgWidth_x4;
                    int ipix_down2 = ipix_down + imgWidth_x4;
                    int ipix_left = ipix_x4 - 4;
                    int ipix_left2 = ipix_left - 4;
                    int ipix_right = ipix_x4 + 4;
                    int ipix_right2 = ipix_right + 4;

                    int val0 = ((img[ipix_left + 1] + img[ipix_x4 + c] + img[ipix_right + 1]) * 2 -
                                img[ipix_left2 + c] - img[ipix_right2 + c]) >> 2;
                    int tiley = row - top;
                    int tilex = col - left;
                    int tileidx = tilex + tiley * TILE_SIZE;
                    g0[tileidx] = (ushort)Utils.ULim(val0, img[ipix_left + 1], img[ipix_right + 1]);

                    int val1 = ((img[ipix_up + 1] + img[ipix_x4 + c] + img[ipix_down + 1]) * 2 - img[ipix_up2 + c] -
                                img[ipix_down2 + c]) >> 2;
                    g1[tileidx] = (ushort)Utils.ULim(val1, img[ipix_up + 1], img[ipix_down + 1]);
                }
            }
        }
    }
}

