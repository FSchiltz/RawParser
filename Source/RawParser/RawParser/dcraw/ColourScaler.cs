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

namespace dcraw
{
    public class ColourScaler : Filter
    {
        public ColourScaler(DcRawState state) : base(state)
        {
        }

        private static void wavelet_denoise()
        {
            throw new NotImplementedException();
        }

        public override void Process()
        {
            Process(new RawImage(state));
        }

        private void Process(RawImage ri)
        {
            int row;
            int col;
            int i;
            int c;
            int[] sum = new int[8];
            int val;
            double[] dsum = new double[8];
            double dmin;
            double dmax;

            float[] scale_mul = new float[4];

            // State inputs
            ushort[] image = ri.image;
            int colors = ri.colours;

            // TODO: sort out iwidth/iheight and width/height
            int iheight = state.iheight;
            int iwidth = state.iwidth;

            uint maximum = state.maximum;
            uint black = state.black;
            bool use_auto_wb = state.use_auto_wb;
            bool use_camera_wb = state.use_camera_wb;
            float[] cam_mul = state.cam_mul;
            float[] user_mul = state.user_mul;
            uint[] greybox = state.greybox;
            int height = state.height;
            int width = state.width;
            uint filters = state.filters;
            float[] pre_mul = state.pre_mul;
            float threshold = state.threshold;
            int highlight = state.highlight;
            ushort[,] white = state.white;
            string filename = state.inFilename;
            double[] aber = state.aber;
            bool verbose = state.verbose;

            if (user_mul[0] != 0.0f)
            {
                for (c = 0; c < 4; c++)
                {
                    pre_mul[c] = user_mul[c];
                }
            }

            if (use_auto_wb || (use_camera_wb && cam_mul[0] == -1))
            {
                Array.Clear(dsum, 0, dsum.Length);
                //memset (dsum, 0, sizeof dsum);
                uint bottom = (uint)Math.Min(greybox[1] + greybox[3], height);
                uint right = (uint)Math.Min(greybox[0] + greybox[2], width);
                for (row = (int)greybox[1]; row < bottom; row += 8)
                {
                    for (col = (int)greybox[0]; col < right; col += 8)
                    {
                        //memset(sum, 0, sizeof sum);
                        Array.Clear(sum, 0, sum.Length);
                        for (int y = row; y < row + 8 && y < bottom; y++)
                        {
                            for (int x = col; x < col + 8 && x < right; x++)
                            {
                                for (c = 0; c < 4; c++)
                                {
                                    if (filters != 0)
                                    {
                                        c = FC(filters, y, x);
                                        val = state.BAYER_get(y, x);
                                    }
                                    else
                                    {
                                        val = image[(y * width + x) * 4 + c];
                                    }
                                    if (val > maximum - 25) goto skip_block;
                                    if ((val -= (int)black) < 0) val = 0;
                                    sum[c] += val;
                                    sum[c + 4]++;
                                    if (filters != 0) break;
                                }
                            }
                        }

                        for (c = 0; c < 8; c++) dsum[c] += sum[c];
                    skip_block:
                        ;
                    }
                }

                for (c = 0; c < 4; c++)
                {
                    if (dsum[c] != 0)
                    {
                        pre_mul[c] = (float)(dsum[c + 4] / dsum[c]);
                    }
                }
            }

            if (use_camera_wb && cam_mul[0] != -1)
            {
                //memset (sum, 0, sizeof sum);
                Array.Clear(sum, 0, sum.Length);
                for (row = 0; row < 8; row++)
                {
                    for (col = 0; col < 8; col++)
                    {
                        c = FC(filters, row, col);
                        if ((val = (int)(white[row, col] - black)) > 0)
                        {
                            sum[c] += val;
                        }
                        sum[c + 4]++;
                    }
                }

                if (sum[0] != 0 && sum[1] != 0 && sum[2] != 0 && sum[3] != 0)
                {
                    for (c = 0; c < 4; c++) pre_mul[c] = (float)sum[c + 4] / sum[c];
                }
                else if (cam_mul[0] != 0 && cam_mul[2] != 0)
                {
                    Array.Copy(cam_mul, pre_mul, cam_mul.Length);
                }
                else
                {
                    throw new Exception();
                }
            }

            if (pre_mul[3] == 0)
            {
                pre_mul[3] = colors < 4 ? pre_mul[1] : 1;
            }

            uint dark = black;
            uint sat = maximum;

            if (threshold != 0)
            {
                wavelet_denoise();
            }

            maximum -= black;
            for (dmin = double.MaxValue, dmax = c = 0; c < 4; c++)
            {
                if (dmin > pre_mul[c])
                    dmin = pre_mul[c];
                if (dmax < pre_mul[c])
                    dmax = pre_mul[c];
            }

            if (highlight != 0)
            {
                dmax = dmin;
            }

            for (c = 0; c < 4; c++)
            {
                pre_mul[c] /= (float)dmax;
                scale_mul[c] = (float)(pre_mul[c] * 65535.0 / maximum);
            }

            int size = iheight * iwidth;
            for (i = 0; i < size * 4; i++)
            {
                val = image[i];
                if (val == 0) continue;
                val -= (int)black;
                val = (int)(val * scale_mul[i & 3]);
                image[i] = Utils.Clip16(val);
            }


            if ((aber[0] != 1 || aber[2] != 1) && colors == 3)
            {
                for (c = 0; c < 4; c += 2)
                {
                    if (aber[c] == 1) continue;
                    ushort[] img = new ushort[size];

                    for (i = 0; i < size; i++)
                    {
                        img[i] = image[i * 4 + c];
                    }

                    for (row = 0; row < iheight; row++)
                    {
                        float fr = (float)((row - iheight * 0.5) * aber[c] + iheight * 0.5);
                        int ur = (int)fr;
                        if (ur > iheight - 2) continue;
                        fr -= ur;
                        for (col = 0; col < iwidth; col++)
                        {
                            float fc = (float)((col - iwidth * 0.5) * aber[c] + iwidth * 0.5);
                            int uc = (int)fc;

                            if (uc > iwidth - 2) continue;
                            fc -= uc;
                            int ipix = ur * iwidth + uc;

                            image[(row * iwidth + col) * 4 + c] =
                                (ushort)((img[ipix + 0] * (1 - fc) + img[ipix + 1] * fc) * (1 - fr) +
                                (img[ipix + iwidth] * (1 - fc) + img[ipix + iwidth + 1] * fc) * fr);
                        }
                    }
                }
            }

            // State outputs
            state.maximum = maximum;
        }

    }
}
