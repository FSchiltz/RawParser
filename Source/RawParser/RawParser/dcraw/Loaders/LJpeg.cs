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
    public sealed class JHead
    {
        public int bits;
        public int high;
        public int wide;
        public int clrs;
        public bool sraw;
        public int psv;
        public int restart;
        public int[] vpred = new int[4];
        public HuffmanTree[] huff = new HuffmanTree[4];
        public ushort[] row;

        public JHead(DcRawState state, RawStream s, bool info_only, uint dng_version)
        {
            byte[] data = new byte[0x10000];

            restart = int.MaxValue;

            s.Read(data, 0, 2);

            if (data[1] != 0xd8)
            {
                // Error state (I think)
                throw new Exception("unexpected value in jpeg header");
                //return 0;
            }

            int tag;
            int len;
            do
            {
                s.Read(data, 0, 2 * 2);
                tag = data[0] << 8 | data[1];
                len = (data[2] << 8 | data[3]) - 2;
                if (tag <= 0xff00)
                {
                    // Non error
                    return;
                }

                s.Read(data, 0, len);
                switch (tag)
                {
                    case 0xffc0:    // SOF0 - Start of Frame 0 - Baseline DCT
                    case 0xffc3:    // SOF3 - Start of Frame 3 - Lossless (sequential)
                        if (tag == 0xffc3)
                        {
                            sraw = ((((data[7] >> 4) * (data[7] & 15) - 1) & 3) == 0) ? false : true;
                        }
                        bits = data[0];
                        high = data[1] << 8 | data[2];
                        wide = data[3] << 8 | data[4];
                        clrs = data[5] - (sraw ? 1 : 0);
                        if (len == 9 && dng_version == 0)
                        {
                            s.ReadByte();
                        }
                        break;

                    case 0xffc4:    // DHT - Define Huffman Table
                        if (info_only) break;

                        for (int dpi = 0; dpi < len && data[dpi] < 4; )
                        {
                            int idx = data[dpi];
                            dpi++;
                            huff[idx] = new HuffmanTree(data, ref dpi);
                        }
                        break;

                    case 0xffda:    // SOS - Start of Scan
                        psv = data[1 + data[0] * 2];
                        bits -= data[3 + data[0] * 2] & 15;
                        break;

                    case 0xffdd:    // DRI - Define Restart Interval
                        restart = data[0] << 8 | data[1];
                        break;

                    // <-- end of dcraw.c ported code (for this switch statement) -->

                    // thumbnail image
                    // for more unhandled tags, see: http://www.impulseadventure.com/photo/jpeg-decoder.html
                    case 0xffd8:    // SOI - Start of Image
                    case 0xffd9:    // EOI - End of Image
                    case 0xffdb:    // DQT - Define Quantization Table
                        break;

                    default:
                       
                        break;
                }
            } while (tag != 0xffda);

            if (info_only)
            {
                // No error
                //return 1;
                return;
            }

            if (sraw)
            {
                huff[3] = huff[2] = huff[1];
                huff[1] = huff[0];
            }

            row = new ushort[wide * clrs * 2];

            /*
            for (int iii = 0; iii < huff.Length; iii++)
            {
                Console.WriteLine("huff[{0}]", iii);
                Decode.DumpTable(huff[iii], 0);
            }
             */
            
            //row = (ushort*) calloc(wide * clrs, 4);
            //merror(jh->row, "ljpeg_start()");
            // TODO: why do we need error handling here?
            s.ZeroAfterFF = true;
        }
    }

    public sealed class LosslessJpegLoader : LJpegBase
    {
        public LosslessJpegLoader(DcRawState state)
            : base(state)
        {
        }

        private void canon_black(double[] dark)
        {
            if (state.raw_width < state.width + 4) return;

            for (int c = 0; c < 2; c++)
            {
                dark[c] /= (state.raw_width - state.width - 2) * state.height >> 1;
            }

            int diff = (int)(dark[0] - dark[1]);

            if (diff != 0)
            {
                for (int row = 0; row < state.height; row++)
                {
                    for (int col = 1; col < state.width; col += 2)
                    {
                        state.BAYER_inc(row, col, diff);
                    }
                }
            }
            dark[1] += diff;
            state.black = (uint)((dark[0] + dark[1] + 1) / 2);
        }

        public override void LoadRaw()
        {
            //lossless_jpeg_load_raw()
            double[] dark = { 0.0, 0.0 };

            //struct jhead jh;
            int min = int.MaxValue;
            int row = 0;
            int col = 0;

            JHead jh = new JHead(state, state.InStream, false, state.dng_version);
            int jwide = jh.wide * jh.clrs;

            for (int jrow = 0; jrow < jh.high; jrow++)
            {
                int rpi = ljpeg_row(jrow, jh);
                ushort[] rp = jh.row;

                for (int jcol = 0; jcol < jwide; jcol++)
                {
                    int val = rp[rpi++];
                    if (jh.bits <= 12)
                    {
                        val = state.curve[val & 0xfff];
                    }

                    if (state.cr2_slice[0] != 0)
                    {
                        int jidx = jrow * jwide + jcol;
                        int i = jidx / (state.cr2_slice[1] * jh.high);
                        bool j = i >= state.cr2_slice[0];
                        if (j)
                        {
                            i = state.cr2_slice[0];
                        }

                        jidx -= i * (state.cr2_slice[1] * jh.high);
                        row = jidx / state.cr2_slice[1 + (j ? 1 : 0)];
                        col = jidx % state.cr2_slice[1 + (j ? 1 : 0)] + i * state.cr2_slice[1];
                    }

                    if (state.raw_width == 3984 && (col -= 2) < 0)
                    {
                        col += row--;
                    }

                    if ((uint)(row - state.top_margin) < state.height)
                    {
                        if ((uint)(col - state.left_margin) < state.width)
                        {
                            state.BAYER_set(row - state.top_margin, col - state.left_margin, (ushort)val);

                            if (min > val) min = val;
                        }
                        else if (col > 1)
                            dark[(col - state.left_margin) & 1] += val;
                    }
                    if (++col >= state.raw_width)
                    {
                        col = row++;
                    }
                }
            }

            canon_black(dark);
            //if (!strcasecmp(make, "KODAK"))
            //    STATE->black = min;
        }
    }
}