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
using System.Text;
using System.IO;
using dcraw.Loaders;

namespace dcraw
{
    public partial class Identifier
    {
        private readonly DcRawState state;

        private class CameraData
        {
            public readonly int fsize;
            public readonly string make;
            public readonly string model;
            public readonly bool withjpeg;

            public CameraData(int fsize, string make, string model, bool withjpeg)
            {
                this.fsize = fsize;
                this.make = make;
                this.model = model;
                this.withjpeg = withjpeg;
            }
        }

        private static readonly Dictionary<int, IList<CameraData>> fileSizeToData = new Dictionary<int, IList<CameraData>>();

        private class AdobeCoeff
        {
            public readonly string prefix;
            public readonly short black;
            public readonly short maximum;
            public readonly short[] trans;

            public AdobeCoeff(string prefix, int black, int maximum, short[] trans)
            {
                this.prefix = prefix;
                this.black = (short)black;
                this.maximum = (short)maximum;
                this.trans = trans;
            }
        }

        private class Ph1
        {
            public int format;
            public int key_off;
            public int black;
            public int black_off;
            public int split_col;
            public int tag_21a;
            public float tag_210;
        }

        public Identifier(DcRawState state)
        {
            this.state = state;
        }

        /// <summary>
        /// Identify which camera created this file, and set global variables accordingly.
        /// </summary>
        /// <param name="ifp"></param>
        public void identify(RawStream ifp)
        {
            byte[] head = new byte[32];
            int cpi;
            int i;
            //struct jhead jh;
            JHead jh;

            InitState(state);

            ifp.Order = ifp.sget2();
            int hlen = (int)ifp.get4();
            ifp.Seek(0, SeekOrigin.Begin);
            ifp.Read(head, 0, 32);
            int fsize = (int) ifp.Length;

            if (((cpi = Utils.memmem(head, "MMMM")) != -1) ||
                ((cpi = Utils.memmem(head, "IIII")) != -1))
            {
                parse_phase_one(ifp, cpi);
                if (cpi != 0)
                {
                    parse_tiff(ifp, 0);
                }
            }
            else if (ifp.Order == 0x4949 || ifp.Order == 0x4d4d)
            {
                if (!Utils.memcmp(head, 6, "HEAPCCDR"))
                {
                    state.data_offset = hlen;
                    parse_ciff(hlen, fsize - hlen);
                }
                else
                {
                    parse_tiff(ifp, 0);
                }
            }
            else if (!Utils.memcmp(head, "\xff\xd8\xff\xe1") &&
                     !Utils.memcmp(head, 6, "Exif"))
            {
                ifp.Seek(4, SeekOrigin.Begin);
                state.data_offset = 4 + ifp.get2();
                ifp.Seek(state.data_offset, SeekOrigin.Begin);
                if (ifp.ReadByte() != 0xff)
                    parse_tiff(ifp, 12);
                state.thumb_offset = 0;
            }
            else if (!Utils.memcmp(head, 25, "ARECOYK"))
            {
                state.make = "Contax";
                state.model = "N Digital";
                ifp.Seek(33, SeekOrigin.Begin);
                get_timestamp(ifp, true);
                ifp.Seek(60, SeekOrigin.Begin);
                for (int c = 0; c < 4; c++) state.cam_mul[c ^ (c >> 1)] = ifp.get4();
            }
            else if (!Utils.strcmp(head, "PXN"))
            {
                state.make = "Logitech";
                state.model = "Fotoman Pixtura";
            }
            else if (!Utils.strcmp(head, "qktk"))
            {
                state.make = "Apple";
                state.model = "QuickTake 100";
            }
            else if (!Utils.strcmp(head, "qktn"))
            {
                state.make = "Apple";
                state.model = "QuickTake 150";
            }
            else if (!Utils.memcmp(head, "FUJIFILM"))
            {
                ifp.Seek(84, SeekOrigin.Begin);
                state.thumb_offset = ifp.get4();
                state.thumb_length = ifp.sget4();
                ifp.Seek(92, SeekOrigin.Begin);
                parse_fuji(ifp.sget4());
                if (state.thumb_offset > 120)
                {
                    ifp.Seek(120, SeekOrigin.Begin);
                    state.is_raw += ((i = ifp.sget4()) != 0) ? 1 : 0;
                    if (state.is_raw == 2 && state.shot_select != 0)
                    {
                        parse_fuji(i);
                    }
                }
                ifp.Seek(100, SeekOrigin.Begin);
                state.data_offset = ifp.get4();
                parse_tiff(ifp, (int)state.thumb_offset + 12);
            }
            else if (!Utils.memcmp(head, "RIFF"))
            {
                ifp.Seek(0, SeekOrigin.Begin);
                parse_riff();
            }
            else if (!Utils.memcmp(head, "\0\001\0\001\0@"))
            {
                ifp.Seek(6, SeekOrigin.Begin);
                state.make = ifp.ReadString(8);
                state.model = ifp.ReadString(8);
                state.model2 = ifp.ReadString(16);
                state.data_offset = ifp.get2();
                ifp.get2();
                state.raw_width = ifp.get2();
                state.raw_height = ifp.get2();
                state.load_raw = new nokia_load_raw(state);
                state.filters = 0x61616161;
            }
            else if (!Utils.memcmp(head,"NOKIARAW")) 
            {
                state.make = "NOKIA";
                state.model = "X2";
                ifp.Order = 0x4949;
                ifp.Seek(300, SeekOrigin.Begin);
                state.data_offset = ifp.get4();
                uint tmp = ifp.get4();
                state.width = ifp.get2();
                state.height = ifp.get2();
                state.data_offset += tmp - state.width * 5 / 4 * state.height;
                state.load_raw = new nokia_load_raw(state);
                state.filters = 0x61616161;
            }
            else if (!Utils.memcmp(head, "DSC-Image"))
            {
                parse_rollei();
            }
            else if (!Utils.memcmp(head, "PWAD"))
            {
                parse_sinar_ia();
            }
            else if (!Utils.memcmp(head, "\0MRM"))
            {
                parse_minolta(0);
            }
            else if (!Utils.memcmp(head, "FOVb"))
            {
                parse_foveon();
            }
            else if (!Utils.memcmp(head, "CI"))
            {
                parse_cine();
            }
            else
            {
                IList<CameraData> cdlist;
                fileSizeToData.TryGetValue(fsize, out cdlist);
                if (cdlist != null)
                {
                    foreach(CameraData cd in cdlist)
                    {
                        state.make = cd.make;
                        state.model = cd.model;
                        if (cd.withjpeg)
                        {
                            parse_external_jpeg();
                        }
                    }
                }
            }

            if (state.make == null)
            {
                parse_smal(0, fsize);
            }

            if (state.make == null)
            {
                state.is_raw = 0;
                parse_jpeg(state.is_raw);
            }

            for(i = 0; i < corp.Length; i++)
            {
                if (state.make.Contains(corp[i]))
                {
                    state.make = corp[i];
                }
            }
            
//#if false
            if (state.make.Substring(0, 5) == "KODAK")
            {
                state.make = state.make.Substring(0, 16);
                state.model = state.model.Substring(0, 16);
            }

            state.make = state.make.Trim();
            //cp = state.make + strlen(state.make); /* Remove trailing spaces */
            //while (*--cp == ' ') *cp = 0;
            state.model = state.model.Trim();
            //cp = state.model + strlen(state.model);
            //while (*--cp == ' ') *cp = 0;
            if (state.model.StartsWith(state.make, StringComparison.CurrentCultureIgnoreCase))
            {
                state.model = state.model.Substring(state.make.Length).Trim();
            }
            //i = strlen(state.make); /* Remove state.make from model */
            //if (!strncasecmp(state.model, state.make, i) && state.model[i++] == ' ')
            //    memmove(state.model, state.model + i, 64 - i);
            if (state.model.StartsWith("Digital Camera "))
            {
                state.model = state.model.Substring(15);
                //strcpy(state.model, state.model + 15);
            }
                
            //desc[511] = artist[63] = state.make[63] = state.model[63] = state.model2[63] = 0;
            if (state.is_raw == 0) goto notraw;

            if (state.maximum == 0) state.maximum = (uint)(1 << (int)state.tiff_bps) - 1;
            if (state.height == 0) state.height = state.raw_height;
            if (state.width == 0) state.width = state.raw_width;

            if (state.fuji_width != 0)
            {
                state.width         = (ushort)state.height + state.fuji_width;
                state.height        = (ushort)state.width - 1;
                state.pixel_aspect  = 1;
            }

            if (state.height == 2624 && state.width == 3936) /* Pentax K10D and Samsung GX10 */
            {
                state.height        = 2616;
                state.width         = 3896;
            }

            if (state.height == 3136 && state.width == 4864) /* Pentax K20D and Samsung GX20 */
            {
                state.filters       = 0x16161616;
                state.height        = 3124;
                state.width         = 4688;
            }

            if (state.model == "K-r" || state.model == "K-x")
            {
                state.filters       = 0x16161616;
                state.width         = 4309;
            }
            if (state.model == "K-5")
            {
                state.filters       = 0x16161616;
                state.width         = 4950;
                state.left_margin   = 10;
            }
            if (state.model == "K-7")
            {
                state.filters       = 0x16161616;
                state.height        = 3122;
                state.width         = 4684;
                state.top_margin    = 2;
            }
            if (state.model == "645D")
            {
                state.filters       = 0x16161616;
                state.height        = 5502;
                state.width         = 7328;
                state.top_margin    = 29;
                state.left_margin   = 48;
            }
            if (state.height == 3014 && state.width == 4096) /* Ricoh GX200 */
            {
                state.width = 4014;
            }

            if (state.dng_version != 0)
            {
                if (state.filters == uint.MaxValue) state.filters = 0;
                if (state.filters != 0) state.is_raw = state.tiff_samples;
                else state.colors = (int)state.tiff_samples;

                if (state.tiff_compress == 1)
                    state.load_raw = new adobe_dng_load_raw_nc(state);
                if (state.tiff_compress == 7)
                    state.load_raw = new AdobeDngLoader(state);
                goto dng_skip;
            }

            bool is_canon = state.make == "Canon";
            if (is_canon)
            {
                state.load_raw = !Utils.memcmp(head, 6, "HEAPCCDR")
                                     ?
                                         (RawLoader)new LosslessJpegLoader(state)
                                     : (RawLoader)new canon_compressed_load_raw(state);
            }

            if (state.make == "NIKON")
            {
                if (state.load_raw == null)
                {
                    state.load_raw = new Packed12Loader(state);
                }
                if (state.model[0] == 'E')
                {
                    state.load_flags |= (state.data_offset == 0 ? 1 : 0 << 2 | 2);
                }
            }

            if (state.make == "CASIO")
            {
                state.load_raw = new Packed12Loader(state);
                state.maximum = 0xf7f;
            }

            /* Set parameters based on camera name (for non-DNG files). */

            if (state.is_foveon)
            {
                if (state.height * 2 < state.width)
                {
                    state.pixel_aspect = 0.5;
                }
                if (state.height > state.width)
                {
                    state.pixel_aspect = 2;
                }
                state.filters       = 0;
                state.load_raw      = new foveon_load_raw(state);
                simple_coeff(0);
            }
            else if (is_canon && state.tiff_bps == 15)
            {
                switch (state.width)
                {
                    case 3344:
                        state.width -= 72;      // dcraw.c: -=66 then fall through
                        break;
                    case 3872:
                        state.width -= 6;
                        break;
                }
                state.filters       = 0;
                state.load_raw      = new canon_sraw_load_raw(state);
            }
            else if (state.model == "PowerShot 600")
            {
                state.height        = 613;
                state.width         = 854;
                state.raw_width     = 896;
                state.pixel_aspect  = 607/628.0;
                state.colors        = 4;
                state.filters       = 0xe1e4e1e4;
                state.load_raw      = new canon_600_load_raw(state);
            }
            else if (state.model == "PowerShot A5" ||
                     state.model == "PowerShot A5 Zoom")
            {
                state.height        = 773;
                state.width         = 960;
                state.raw_width     = 992;
                state.pixel_aspect  = 256/235.0;
                state.colors        = 4;
                state.filters       = 0x1e4e1e4e;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A50")
            {
                state.height        = 968;
                state.width         = 1290;
                state.raw_width     = 1320;
                state.colors        = 4;
                state.filters       = 0x1b4e4b1e;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot Pro70")
            {
                state.height        = 1024;
                state.width         = 1552;
                state.colors        = 4;
                state.filters       = 0x1e4b4e1b;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot SD300")
            {
                state.height        = 1752;
                state.width         = 2344;
                state.raw_height    = 1766;
                state.raw_width     = 2400;
                state.top_margin    = 12;
                state.left_margin   = 12;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A460")
            {
                state.height        = 1960;
                state.width         = 2616;
                state.raw_height    = 1968;
                state.raw_width     = 2664;
                state.top_margin    = 4;
                state.left_margin   = 4;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A530")
            {
                state.height        = 1984;
                state.width         = 2620;
                state.raw_height    = 1992;
                state.raw_width     = 2672;
                state.top_margin    = 6;
                state.left_margin   = 10;
                state.load_raw      = new canon_a5_load_raw(state);
                state.raw_color     = false;
            }
            else if (state.model == "PowerShot A610")
            {
                if (canon_s2is())
                {
                    state.model     = "PowerShot S2 IS";
                }
                state.height        = 1960;
                state.width         = 2616;
                state.raw_height    = 1968;
                state.raw_width     = 2672;
                state.top_margin    = 8;
                state.left_margin   = 12;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A620")
            {
                state.height        = 2328;
                state.width         = 3112;
                state.raw_height    = 2340;
                state.raw_width     = 3152;
                state.top_margin    = 12;
                state.left_margin   = 36;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A470")
            {
                state.height        = 2328;
                state.width         = 3096;
                state.raw_height    = 2346;
                state.raw_width     = 3152;
                state.top_margin    = 6;
                state.left_margin   = 12;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A720 IS")
            {
                state.height        = 2472;
                state.width         = 3298;
                state.raw_height    = 2480;
                state.raw_width     = 3336;
                state.top_margin    = 5;
                state.left_margin   = 6;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A630")
            {
                state.height        = 2472;
                state.width         = 3288;
                state.raw_height    = 2484;
                state.raw_width     = 3344;
                state.top_margin    = 6;
                state.left_margin   = 12;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A640")
            {
                state.height        = 2760;
                state.width         = 3672;
                state.raw_height    = 2772;
                state.raw_width     = 3736;
                state.top_margin    = 6;
                state.left_margin   = 12;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot A650")
            {
                state.height        = 3024;
                state.width         = 4032;
                state.raw_height    = 3048;
                state.raw_width     = 4104;
                state.top_margin    = 12;
                state.left_margin   = 48;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot S3 IS")
            {
                state.height        = 2128;
                state.width         = 2840;
                state.raw_height    = 2136;
                state.raw_width     = 2888;
                state.top_margin    = 8;
                state.left_margin   = 44;
                state.load_raw  = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot SX110 IS")
            {
                state.height        = 2760;
                state.width         = 3684;
                state.raw_height    = 2772;
                state.raw_width     = 3720;
                state.top_margin    = 12;
                state.left_margin   = 6;
                //todostate.load_flags = 40;  
                state.zero_is_bad   = 1;
                state.load_raw  = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot SX120 IS")
            {
                state.height        = 2742;
                state.width         = 3664;
                state.raw_height    = 2778;
                state.raw_width     = 3728;
                state.top_margin    = 18;
                state.left_margin   = 16;
                //todostate.load_flags = 40;  
                state.zero_is_bad   = 1;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot SX20 IS")
            {
                state.height        = 3024;
                state.width         = 4032;
                state.raw_height    = 3048;
                state.raw_width     = 4080;
                state.top_margin    = 12;
                state.left_margin   = 24;
                //todostate.load_flags = 40;  
                state.zero_is_bad   = 1;
                state.load_raw      = new canon_a5_load_raw(state);
            }
            else if (state.model == "PowerShot Pro90 IS")
            {
                state.width         = 1896;
                state.colors        = 4;
                state.filters       = 0xb4b4b4b4;
            }
            else if (is_canon && state.raw_width == 2144)
            {
                state.height        = 1550;
                state.width         = 2088;
                state.top_margin    = 8;
                state.left_margin   = 4;
                if (state.model == "PowerShot G1")
                {
                    state.colors    = 4;
                    state.filters   = 0xb4b4b4b4;
                }
            }
            else if (is_canon && state.raw_width == 2224)
            {
                state.height        = 1448;
                state.width         = 2176;
                state.top_margin    = 6;
                state.left_margin   = 48;
            }
            else if (is_canon && state.raw_width == 2376)
            {
                state.height        = 1720;
                state.width         = 2312;
                state.top_margin    = 6;
                state.left_margin   = 12;
            }
            else if (is_canon && state.raw_width == 2672)
            {
                state.height        = 1960;
                state.width         = 2616;
                state.top_margin    = 6;
                state.left_margin   = 12;
            }
            else if (is_canon && state.raw_width == 3152)
            {
                state.height        = 2056;
                state.width         = 3088;
                state.top_margin    = 12;
                state.left_margin   = 64;
                if (state.unique_id == 0x80000170)
                    adobe_coeff("Canon", "EOS 300D");
            }
            else if (is_canon && state.raw_width == 3160)
            {
                state.height        = 2328;
                state.width         = 3112;
                state.top_margin    = 12;
                state.left_margin   = 44;
            }
            else if (is_canon && state.raw_width == 3344)
            {
                state.height        = 2472;
                state.width         = 3288;
                state.top_margin    = 6;
                state.left_margin   = 4;
            }
            else if (state.model == "EOS D2000C")
            {
                state.filters       = 0x61616161;
                state.black         = state.curve[200];
            }
            else if (is_canon && state.raw_width == 3516)
            {
                state.top_margin    = 14;
                state.left_margin   = 42;
                if (state.unique_id == 0x80000189)
                    adobe_coeff("Canon", "EOS 350D");
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 3596)
            {
                state.top_margin    = 12;
                state.left_margin   = 74;
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 3744)
            {
                state.height        = 2760;
                state.width         = 3684;
                state.top_margin    = 16;
                state.left_margin   = 8;
                if (state.unique_id > 0x2720000)
                {
                    state.top_margin = 12;
                    state.left_margin = 52;
                }
            }
            else if (is_canon && state.raw_width == 3944)
            {
                state.height        = 2602;
                state.width         = 3908;
                state.top_margin    = 18;
                state.left_margin   = 30;
            }
            else if (is_canon && state.raw_width == 3948)
            {
                state.top_margin    = 18;
                state.left_margin   = 42;
                state.height        -= 2;
                if (state.unique_id == 0x80000236)
                    adobe_coeff("Canon", "EOS 400D");
                if (state.unique_id == 0x80000254)
                    adobe_coeff("Canon", "EOS 1000D");
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 3984)
            {
                state.top_margin    = 20;
                state.left_margin   = 76;
                state.height        -= 2;
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 4104)
            {
                state.height        = 3024;
                state.width         = 4032;
                state.top_margin    = 12;
                state.left_margin   = 48;
            }
            else if (is_canon && state.raw_width == 4152)
            {
                state.top_margin    = 12;
                state.left_margin   = 192;
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 4312)
            {
                state.top_margin    = 18;
                state.left_margin   = 22;
                state.height        -= 2;
                if (state.unique_id == 0x80000176)
                    adobe_coeff("Canon", "EOS 450D");
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 4476)
            {
                state.top_margin    = 34;
                state.left_margin   = 90;
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 4480) 
            {
                state.height        = 3326;
                state.width         = 4432;
                state.top_margin    = 10;
                state.left_margin   = 12;
                state.filters       = 0x49494949;
            }
            else if (is_canon && state.raw_width == 4832) 
            {
                state.top_margin    = state.unique_id == 0x80000261 ? 51 : 26;
                state.left_margin   = 62;
                if (state.unique_id == 0x80000252)
                    adobe_coeff ("Canon","EOS 500D");
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 5120) 
            {
                state.height        -= state.top_margin = 45;
                state.left_margin   = 142;
                state.width         = 4916;
            }
            else if (is_canon && state.raw_width == 5344) 
            {
                state.top_margin    = 51;
                state.left_margin   = 142;
                if (state.unique_id == 0x80000270)
                    adobe_coeff ("Canon","EOS 550D");
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 5360) 
            {
                state.top_margin    = 51;
                state.left_margin   = 158;
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 5792) 
            {
                state.top_margin    = 51;
                state.left_margin   = 158;
                canon_cr2();
            } 
            else if (is_canon && state.raw_width == 5108)
            {
                state.top_margin    = 13;
                state.left_margin   = 98;
                canon_cr2();
            }
            else if (is_canon && state.raw_width == 5712)
            {
                state.height        = 3752;
                state.width         = 5640;
                state.top_margin    = 20;
                state.left_margin   = 62;
            }
            else if (state.model == "D1")
            {
                state.cam_mul[0] *= 256 / 527.0f;
                state.cam_mul[2] *= 256 / 317.0f;
            }
            else if (state.model == "D1X")
            {
                state.width -= 4;
                state.pixel_aspect = 0.5;
            }
            else if (state.model == "D40X" ||
                     state.model == "D60" ||
                     state.model == "D80" ||
                     state.model == "D3000")
            {
                state.height -= 3;
                state.width -= 4;
            }
            else if (state.model == "D3" ||
                     state.model == "D3S" ||
                     state.model == "D700")
            {
                state.width -= 4;
                state.left_margin = 2;
            }
            else if (state.model == "D5000")
            {
                state.width -= 42;
            }
            else if (state.model == "D7000")
            {
                state.width -= 44;
            }
            else if (state.model == "D3100")
            {
                state.width -= 28;
                state.left_margin = 6;
            }
            else if (state.model.Substring(0, 3) == "D40" ||
                     state.model.Substring(0, 3) == "D50" ||
                     state.model.Substring(0, 3) == "D70")
            {
                state.width--;
            }
            else if (state.model == "D90")
            {
                state.width -= 42;
            }
            else if (state.model == "D100")
            {
                if (state.tiff_compress == 34713 && !nikon_is_compressed())
                {
                    state.load_raw = new Packed12Loader(state);
                    state.load_flags |= 8;
                    state.raw_width = (ushort) ((state.width += 3) + 3);
                }
            }
            else if (state.model == "D200")
            {
                state.left_margin = 1;
                state.width -= 4;
                state.filters = 0x94949494;
            }
            else if (state.model.Substring(0, 3) == "D2H")
            {
                state.left_margin = 6;
                state.width -= 14;
            }
            else if (state.model.Substring(0, 3) == "D2X")
            {
                if (state.width == 3264)
                    state.width -= 32;
                else
                    state.width -= 8;
            }
            else if (state.model == "D300")
            {
                state.width -= 32;
            }
            else if (state.model == "COOLPIX P")
            {
                state.load_flags = 1;       //todo?  v9.05 says --> 24
                state.filters = 0x94949494;
            }
            else if (fsize == 1581060)
            {
                state.height = 963;
                state.width = 1287;
                state.raw_width = 1632;
                state.load_raw = new nikon_e900_load_raw(state);
                state.maximum = 0x3f4;
                state.colors = 4;
                state.filters = 0x1e1e1e1e;
                simple_coeff(3);
                state.pre_mul[0] = 1.2085f;
                state.pre_mul[1] = 1.0943f;
                state.pre_mul[3] = 1.1103f;
            }
            else if (fsize == 2465792)
            {
                state.height = 1203;
                state.width = 1616;
                state.raw_width = 2048;
                state.load_raw = new nikon_e900_load_raw(state);
                state.colors = 4;
                state.filters = 0x4b4b4b4b;
                adobe_coeff("NIKON", "E950");
            }
            else if (fsize == 4771840)
            {
                state.height = 1540;
                state.width = 2064;
                state.colors = 4;
                state.filters = 0xe1e1e1e1;
                state.load_raw = new Packed12Loader(state);
                state.load_flags = 6;
                if (state.timestamp == null && nikon_e995())
                {
                    state.model = "E995";
                }

                if (state.model == "E995")
                {
                    state.filters = 0xb4b4b4b4;
                    simple_coeff(3);
                    state.pre_mul[0] = 1.196f;
                    state.pre_mul[1] = 1.246f;
                    state.pre_mul[2] = 1.018f;
                }
            }
            else if (state.model == "E2100")
            {
                if (state.timestamp == null && !nikon_e2100())
                {
                    state.model = "E2500";
                    state.height = 1204;
                    state.width = 1616;
                    state.colors = 4;
                    state.filters = 0x4b4b4b4b;
                }
                else
                {
                    state.height = 1206;
                    state.width = 1616;
                    state.load_flags = 7;
                }
            }
            else if (state.model == "E2500")
            {
                //cp_e2500:
                state.model = "E2500";
                state.height = 1204;
                state.width = 1616;
                state.colors = 4;
                state.filters = 0x4b4b4b4b;
            }
            else if (fsize == 4775936)
            {
                state.height = 1542;
                state.width = 2064;
                state.load_raw = new Packed12Loader(state);
                state.load_flags = 7;
                state.pre_mul[0] = 1.818f;
                state.pre_mul[2] = 1.618f;
                if (state.timestamp == null)
                {
                    nikon_3700();
                }

                if (state.model[0] == 'E' && int.Parse(state.model.Substring(1)) < 3700)
                {
                    state.filters = 0x49494949;
                }

                if (state.model == "Optio 33WR")
                {
                    state.flip = 1;
                    state.filters = 0x16161616;
                    state.pre_mul[0] = 1.331f;
                    state.pre_mul[2] = 1.820f;
                }
            }
            else if (fsize == 5869568)
            {
                state.height = 1710;
                state.width = 2288;
                state.filters = 0x16161616;
                if (state.timestamp == null && minolta_z2())
                {
                    state.make = "Minolta";
                    state.model = "DiMAGE Z2";
                }
                state.load_raw = new Packed12Loader(state);
                state.load_flags = 6 + (state.make[0] == 'M' ? 1 : 0);
            }
            else if (state.model == "E4500")
            {
                state.height = 1708;
                state.width = 2288;
                state.colors = 4;
                state.filters = 0xb4b4b4b4;
            }
            else if (fsize == 7438336)
            {
                state.height = 1924;
                state.width = 2576;
                state.colors = 4;
                state.filters = 0xb4b4b4b4;
            }
            else if (fsize == 8998912)
            {
                state.height = 2118;
                state.width = 2832;
                state.maximum = 0xf83;
                state.load_raw = new Packed12Loader(state);
                state.load_flags = 7;
            }
            else if (state.model == "FinePix S5100" ||
                     state.model == "FinePix S5500")
            {
                state.load_raw = new unpacked_load_raw(state);
            }
            else if (state.make == "FUJIFILM")
            {
                if (state.model.Substring(7) == "S2Pro")
                {
                    state.model = state.model.Substring(0, 7) + " S2Pro";
                    state.height = 2144;
                    state.width = 2880;
                    state.flip = 6;
                }
                else
                    state.maximum = 0x3e00;
                if (state.is_raw == 2 && state.shot_select != 0)
                    state.maximum = 0x2f00;
                state.top_margin = (ushort) ((state.raw_height - state.height) / 2);
                state.left_margin = (ushort) ((state.raw_width - state.width) / 2);
                if (state.is_raw == 2)
                {
                    state.data_offset += (state.shot_select > 0 ? 1 : 0) * (state.fuji_layout != 0
                                                                      ?
                                                                          (state.raw_width *= 2)
                                                                      : state.raw_height * state.raw_width * 2);
                }
                state.fuji_width = (ushort) (state.width >> (state.fuji_layout == 0 ? 0 : 1));
                state.width = (state.height >> (int) state.fuji_layout) + state.fuji_width;
                state.raw_height = state.height;
                state.height = (ushort) state.width - 1;
                state.load_raw = new fuji_load_raw(state);
                if ((state.fuji_width & 1) == 0)
                    state.filters = 0x49494949;
            }
            else if (state.model == "RD175")
            {
                state.height = 986;
                state.width = 1534;
                state.data_offset = 513;
                state.filters = 0x61616161;
                state.load_raw = new minolta_rd175_load_raw(state);
            }
            else if (state.model == "KD-400Z")
            {
                state.height = 1712;
                state.width = 2312;
                state.raw_width = 2336;
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0x3df;
                ifp.Order = 0x4d4d;
            }
            else if (state.model == "KD-510Z")
            {
                //goto konica_510z;
                state.height = 1956;
                state.width = 2607;
                state.raw_width = 2624;
                state.data_offset += 14;
                state.filters = 0x61616161;
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0x3df;
                ifp.Order = 0x4d4d;
            }
            else if (state.make.Equals("MINOLTA", StringComparison.CurrentCultureIgnoreCase))
            {
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0xfff;
                if (state.model.Substring(0, 8) == "DiMAGE A")
                {
                    if (state.model == "DiMAGE A200")
                        state.filters = 0x49494949;
                    state.load_raw = new Packed12Loader(state);
                }
                else if (state.model.Substring(0, 5) == "ALPHA" ||
                         state.model.Substring(0, 5) == "DYNAX" ||
                         state.model.Substring(0, 6) == "MAXXUM")
                {
                    throw new NotImplementedException();
                    //sprintf(state.model + 20, "DYNAX %-10s", state.model + 6 + (state.model[0] == 'M'));
                    //adobe_coeff(state.make, state.model + 20);
                    //state.load_raw = new Packed12Loader(state);
                }
                else if (state.model.Substring(0, 8) == "DiMAGE G")
                {
                    if (state.model[8] == '4')
                    {
                        state.height = 1716;
                        state.width = 2304;
                    }
                    else if (state.model[8] == '5')
                    {
                        //konica_510z:
                        state.height = 1956;
                        state.width = 2607;
                        state.raw_width = 2624;
                    }
                    else if (state.model[8] == '6')
                    {
                        state.height = 2136;
                        state.width = 2848;
                    }
                    state.data_offset += 14;
                    state.filters = 0x61616161;
                    state.load_raw = new unpacked_load_raw(state);
                    state.maximum = 0x3df;
                    ifp.Order = 0x4d4d;
                }
            }
            else if (state.model == "*ist D")
            {
                //todo: state.data_error = -1;
            }
            else if (state.model == "*ist DS")
            {
                state.height -= 2;
            }
            else if (state.model == "K20D")
            {
                state.filters = 0x16161616;
            }
            else if (state.model == "Optio S")
            {
                if (fsize == 3178560)
                {
                    state.height = 1540;
                    state.width = 2064;
                    state.load_raw = new eight_bit_load_raw(state);
                    state.cam_mul[0] *= 4;
                    state.cam_mul[2] *= 4;
                    state.pre_mul[0] = 1.391f;
                    state.pre_mul[2] = 1.188f;
                }
                else
                {
                    state.height = 1544;
                    state.width = 2068;
                    state.raw_width = 3136;
                    state.load_raw = new Packed12Loader(state);
                    state.maximum = 0xf7c;
                    state.pre_mul[0] = 1.137f;
                    state.pre_mul[2] = 1.453f;
                }
            }
            else if (fsize == 6114240)
            {
                state.height = 1737;
                state.width = 2324;
                state.raw_width = 3520;
                state.load_raw = new Packed12Loader(state);
                state.maximum = 0xf7a;
                state.pre_mul[0] = 1.980f;
                state.pre_mul[2] = 1.570f;
            }
            else if (state.model == "Optio 750Z")
            {
                state.height = 2302;
                state.width = 3072;
                state.load_raw = new Packed12Loader(state);
                state.load_flags = 7;
            }
            else if (state.model == "DC-833m")
            {
                state.height = 2448;
                state.width  = 3264;
                ifp.Order = 0x4949;
                state.filters = 0x61616161;
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0xfc00;
            }
            else if (state.model == "S85") 
            {
                state.height = 2448;
                state.width  = 3264;
                state.raw_width = fsize/state.height/2;
                ifp.Order = 0x4d4d;
                state.load_raw = new unpacked_load_raw(state);
            }
            else if (state.model == "NX10") 
            {
                state.height -= state.top_margin = 4;
                state.width -= 2 * (state.left_margin = 8);
            }
            else if (state.model == "EX1")
            {
                ifp.Order = 0x4949;
                state.height = 2760;
                state.top_margin = 2;
                if ((state.width -= 6) > 3682) 
                {
                    state.height = 2750;
                    state.width  = 3668;
                    state.top_margin = 8;
                }
            }
            else if (state.model == "WB2000")
            {
                ifp.Order = 0x4949;
                state.height -= 3;
                state.width -= 10;
                state.top_margin = 2;
            }
            else if (fsize == 20487168) 
            {
                state.height = 2808;
                state.width  = 3648;
                //goto wb550;
                state.model = "WB550";
                ifp.Order = 0x4d4d;
                state.load_raw = new unpacked_load_raw(state);
                state.load_flags = 6;   //todo check
                state.maximum = 0x3df;
            }
            else if (fsize == 24000000) 
            {
                state.height = 3000;
                state.width  = 4000;
                //wb550:
                state.model = "WB550";
                ifp.Order = 0x4d4d;
                state.load_raw = new unpacked_load_raw(state);
                state.load_flags = 6;   //todo check
                state.maximum = 0x3df;
            }
            else if (state.model == "STV680 VGA")
            {
                state.height = 484;
                state.width = 644;
                state.load_raw = new eight_bit_load_raw(state);
                state.flip = 2;
                state.filters = 0x16161616;
                state.black = 16;
                state.pre_mul[0] = 1.097f;
                state.pre_mul[2] = 1.128f;
            }
            else if (state.model == "KAI-0340")
            {
                state.height = 477;
                state.width = 640;
                ifp.Order = 0x4949;
                state.data_offset = 3840;
                state.load_raw = new unpacked_load_raw(state);
                state.pre_mul[0] = 1.561f;
                state.pre_mul[2] = 2.454f;
            }
            else if (state.model == "N95")
            {
                state.top_margin = 2;
                state.height = (ushort)(state.raw_height - state.top_margin);
            }
            else if (state.model == "531C")
            {
                state.height = 1200;
                state.width = 1600;
                state.load_raw = new unpacked_load_raw(state);
                state.filters = 0x49494949;
                state.pre_mul[1] = 1.218f;
            }
            else if (state.model == "F-080C")
            {
                state.height = 768;
                state.width = 1024;
                state.load_raw = new eight_bit_load_raw(state);
            }
            else if (state.model == "F-145C")
            {
                state.height = 1040;
                state.width = 1392;
                state.load_raw = new eight_bit_load_raw(state);
            }
            else if (state.model == "F-201C")
            {
                state.height = 1200;
                state.width = 1600;
                state.load_raw = new eight_bit_load_raw(state);
            }
            else if (state.model == "F-510C")
            {
                state.height = 1958;
                state.width = 2588;
                state.load_raw = fsize < 7500000
                               ?
                                   (RawLoader) new eight_bit_load_raw(state)
                               : (RawLoader) new unpacked_load_raw(state);
                state.maximum = 0xfff0;
            }
            else if (state.model == "F-810C")
            {
                state.height = 2469;
                state.width = 3272;
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0xfff0;
            }
            else if (state.model == "XCD-SX910CR")
            {
                state.height = 1024;
                state.width = 1375;
                state.raw_width = 1376;
                state.filters = 0x49494949;
                state.maximum = 0x3ff;
                state.load_raw = fsize < 2000000
                               ?
                                   (RawLoader) new eight_bit_load_raw(state)
                               : (RawLoader) new unpacked_load_raw(state);
            }
            else if (state.model == "2010")
            {
                state.height = 1207;
                state.width = 1608;
                ifp.Order = 0x4949;
                state.filters = 0x16161616;
                state.data_offset = 3212;
                state.maximum = 0x3ff;
                state.load_raw = new unpacked_load_raw(state);
            }
            else if (state.model == "A782")
            {
                state.height = 3000;
                state.width = 2208;
                state.filters = 0x61616161;
                state.load_raw = fsize < 10000000
                               ?
                                   (RawLoader) new eight_bit_load_raw(state)
                               : (RawLoader) new unpacked_load_raw(state);
                state.maximum = 0xffc0;
            }
            else if (state.model == "3320AF")
            {
                state.height = 1536;
                state.raw_width = state.width = 2048;
                state.filters = 0x61616161;
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0x3ff;
                state.pre_mul[0] = 1.717f;
                state.pre_mul[2] = 1.138f;
                ifp.Seek(0x300000, SeekOrigin.Begin);
                if ((ifp.Order = guess_byte_order(0x10000)) == 0x4d4d)
                {
                    state.height -= (state.top_margin = 16);
                    state.width -= (state.left_margin = 28);
                    state.maximum = 0xf5c0;
                    state.make = "ISG";
                    state.model = null;
                }
            }
            else if (state.make == "Hasselblad")
            {
                if (state.load_raw is LosslessJpegLoader)
                {
                    state.load_raw = new hasselblad_load_raw(state);
                }

                if (state.raw_width == 7262)
                {
                    state.height = 5444;
                    state.width = 7248;
                    state.top_margin = 4;
                    state.left_margin = 7;
                    state.filters = 0x61616161;
                }
                else if (state.raw_width == 7410) 
                {
                    state.height = 5502;
                    state.width  = 7328;
                    state.top_margin  = 4;
                    state.left_margin = 41;
                    state.filters = 0x61616161;
                }
                else if (state.raw_width == 4090) 
                {
                    state.model = "V96C";
                    state.height -= (state.top_margin = 6);
                    state.width -= (state.left_margin = 3) + 7;
                    state.filters = 0x61616161;
                }
            }
            else if (state.make == "Sinar")
            {
                if (!Utils.memcmp(head, "8BPS"))
                {
                    ifp.Seek(14, SeekOrigin.Begin);
                    state.height = (ushort) ifp.get4();
                    state.width = (ushort) ifp.get4();
                    state.filters = 0x61616161;
                    state.data_offset = 68;
                }
                if (state.load_raw == null)
                {
                    state.load_raw = new unpacked_load_raw(state);
                }
                state.maximum = 0x3fff;
            }
            else if (state.make == "Leaf")
            {
                state.maximum = 0x3fff;
                if (state.tiff_samples > 1) state.filters = 0;
                if (state.tiff_samples > 1 || state.tile_length < state.raw_height)
                {
                    state.load_raw = new leaf_hdr_load_raw(state);
                }
                if ((state.width | state.height) == 2048)
                {
                    if (state.tiff_samples == 1)
                    {
                        state.filters = 1;
                        state.cdesc = "RBTG";
                        state.model = "CatchLight";
                        state.top_margin = 8;
                        state.left_margin = 18;
                        state.height = 2032;
                        state.width = 2016;
                    }
                    else
                    {
                        state.model = "DCB2";
                        state.top_margin = 10;
                        state.left_margin = 16;
                        state.height = 2028;
                        state.width = 2022;
                    }
                }
                else if (state.width + state.height == 3144 + 2060)
                {
                    if (state.model == null)
                    {
                        state.model = "Cantare";
                    }
                    if (state.width > state.height)
                    {
                        state.top_margin = 6;
                        state.left_margin = 32;
                        state.height = 2048;
                        state.width = 3072;
                        state.filters = 0x61616161;
                    }
                    else
                    {
                        state.left_margin = 6;
                        state.top_margin = 32;
                        state.width = 2048;
                        state.height = 3072;
                        state.filters = 0x16161616;
                    }
                    if (state.cam_mul[0] == 0 || state.model[0] == 'V')
                        state.filters = 0;
                    else
                    {
                        state.is_raw = state.tiff_samples;
                    }
                }
                else if (state.width == 2116)
                {
                    state.model = "Valeo 6";
                    state.top_margin = 30;
                    state.left_margin = 55;
                    state.height -= (ushort) (2 * state.top_margin);
                    state.width -= (ushort) (2 * state.left_margin);
                    state.filters = 0x49494949;
                }
                else if (state.width == 3171)
                {
                    state.model = "Valeo 6";
                    state.top_margin = 24;
                    state.left_margin = 24;
                    state.height -= (ushort) (2 * state.top_margin);
                    state.width -= (ushort) (2 * state.left_margin);
                    state.filters = 0x16161616;
                }
            }
            else if (state.make == "LEICA" || state.make == "Panasonic")
            {
                state.maximum = 0xfff0;
                if ((fsize - state.data_offset) / (state.width * 8 / 7) == state.height)
                    state.load_raw = new panasonic_load_raw(state);
                if (state.load_raw == null)
                    state.load_raw = new unpacked_load_raw(state);
                switch (state.width)
                {
                    case 2568:
                        adobe_coeff("Panasonic", "DMC-LC1");
                        break;
                    case 3130:
                        state.left_margin = -14;
                        state.left_margin += 18;
                        state.width = 3096;
                        if (state.height > 2326)
                        {
                            state.height = 2326;
                            state.top_margin = 13;
                            state.filters = 0x49494949;
                        }
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-FZ8");
                        break;
                    case 3170:
                        state.left_margin += 18;
                        state.width = 3096;
                        if (state.height > 2326)
                        {
                            state.height = 2326;
                            state.top_margin = 13;
                            state.filters = 0x49494949;
                        }
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-FZ8");
                        break;
                    case 3213:
                        state.width -= 27;
                        state.width -= 10;
                        state.filters = 0x49494949;
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-L1");
                        break;
                    case 3177:
                        state.width -= 10;
                        state.filters = 0x49494949;
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-L1");
                        break;
                    case 3304:
                        state.width -= 16;
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-FZ30");
                        break;
                    case 3330:
                        state.width = 3291;
                        state.left_margin = 9;
                        state.maximum = 0xf7f0;
                        //goto fz18;
                        if (state.height > 2480)
                        {
                            state.height = (ushort) (2480 - (state.top_margin = 10));
                        }
                        state.filters = 0x49494949;
                        state.zero_is_bad = 1;
                        break;
                    case 3370:
                        state.width = 3288;
                        state.left_margin = 15;
                        //fz18:
                        if (state.height > 2480)
                        {
                            state.height = (ushort) (2480 - (state.top_margin = 10));
                        }
                        state.filters = 0x49494949;
                        state.zero_is_bad = 1;
                        break;
                    case 3690:
                        state.height += 36;
                        state.left_margin = -14;
                        state.filters = 0x49494949;
                        state.maximum = 0xf7f0;
                        state.width = 3672;
                        if ((state.height -= 39) == 2760)
                            state.top_margin = 15;
                        state.left_margin += 17;
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-FZ50");
                        break;
                    case 3770:
                        state.width = 3672;
                        if ((state.height -= 39) == 2760)
                            state.top_margin = 15;
                        state.left_margin += 17;
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-FZ50");
                        break;
                    case 3710:
                        state.width = 3682;
                        state.filters = 0x49494949;
                        break;
                    case 3724:
                        state.width -= 14;
                        state.width += 36;
                        state.width -= 78;
                        state.filters = 0x16161616;
                        state.maximum = 0xfff;
                        break;
                    case 3836:
                        state.width += 36;
                        state.width -= 78;
                        state.filters = 0x16161616;
                        state.maximum = 0xfff;
                        break;
                    case 4060:
                        state.width -= 78;
                        state.filters = 0x16161616;
                        state.maximum = 0xfff;
                        break;
                    case 3880:
                        state.width -= 22;
                        state.left_margin = 6;
                        state.zero_is_bad = 1;
                        adobe_coeff("Panasonic", "DMC-LX1");
                        break;
                    case 4290:
                        state.height += 38;
                        state.left_margin = -14;
                        state.filters = 0x49494949;
                        state.width = 4248;
                        if ((state.height -= 39) == 2400)
                            state.top_margin = 15;
                        state.left_margin += 17;
                        adobe_coeff("Panasonic", "DMC-LX2");
                        break;
                    case 4330:
                        state.width = 4248;
                        if ((state.height -= 39) == 2400)
                            state.top_margin = 15;
                        state.left_margin += 17;
                        adobe_coeff("Panasonic", "DMC-LX2");
                        break;
                }
            }
            else if (state.model == "C770UZ")
            {
                state.height = 1718;
                state.width = 2304;
                state.filters = 0x16161616;
                state.load_raw = new Packed12Loader(state);
                state.load_flags = 7;
            }
            else if (state.make == "OLYMPUS")
            {
                state.height += (ushort) (state.height & 1);
                state.filters = state.exif_cfa;
                if (state.load_raw is olympus_e410_load_raw)
                {
                    state.black >>= 4;
                }
                else if (state.model == "E-10" ||
                         state.model.Substring(0, 4) == "E-20")
                {
                    state.black <<= 2;
                }
                else if (state.model == "E-300" ||
                         state.model == "E-500")
                {
                    state.width -= 20;
                    if (state.load_raw is unpacked_load_raw)
                    {
                        state.maximum = 0xfc30;
                        state.black = 0;
                    }
                }
                else if (state.model == "E-330")
                {
                    state.width -= 30;
                    if (state.load_raw is unpacked_load_raw)
                        state.maximum = 0xf790;
                }
                else if (state.model == "SP550UZ")
                {
                    state.thumb_offset = 0xa39800;
                    state.thumb_length = (int) (fsize - state.thumb_offset);
                    state.thumb_height = 480;
                    state.thumb_width = 640;
                }
            }
            else if (state.model == "N Digital")
            {
                state.height = 2047;
                state.width = 3072;
                state.filters = 0x61616161;
                state.data_offset = 0x1a00;
                state.load_raw = new Packed12Loader(state);
            }
            else if (state.model == "DSC-F828")
            {
                state.width = 3288;
                state.left_margin = 5;
                state.data_offset = 862144;
                state.load_raw = new sony_load_raw(state);
                state.filters = 0x9c9c9c9c;
                state.colors = 4;
                state.cdesc = "RGBE";
            }
            else if (state.model == "DSC-V3")
            {
                state.width = 3109;
                state.left_margin = 59;
                state.data_offset = 787392;
                state.load_raw = new sony_load_raw(state);
            }
            else if (state.make == "SONY" && state.raw_width == 3984)
            {
                adobe_coeff("SONY", "DSC-R1");
                state.width = 3925;
                ifp.Order = 0x4d4d;
            }
            else if (state.model == "DSLR-A100")
            {
                if (state.width == 3880)
                {
                    state.height--;
                    state.width = ++state.raw_width;
                }
                else
                {
                    ifp.Order = 0x4d4d;
                    state.load_flags = 2;   //todo check it
                }
                state.filters = 0x61616161;
            }
            else if (state.model == "DSLR-A350")
            {
                state.height -= 4;
            }
            else if (state.model == "PIXL")
            {
                state.height -= state.top_margin = 4;
                state.width -= state.left_margin = 32;
                //todo  gamma_curve(0, 7, 1, 255);
            }
            else if (state.model == "C603v")
            {
                state.height = 480;
                state.width = 640;
                //goto c603v;
                state.filters = 0;
                state.load_raw = new kodak_yrgb_load_raw(state);
            }
            else if (state.model == "C603y")
            {
                state.height = 2134;
                state.width = 2848;
                //c603v:
                state.filters = 0;
                state.load_raw = new kodak_yrgb_load_raw(state);
            }
            else if (state.model == "C603")
            {
                state.raw_height = state.height = 2152;
                state.raw_width = state.width = 2864;
                c603(ifp, fsize);
            }
            else if (state.model == "C330")
            {
                state.height = 1744;
                state.width = 2336;
                state.raw_height = 1779;
                state.raw_width = 2338;
                state.top_margin = 33;
                state.left_margin = 1;
                c603(ifp, fsize);
            }
            else if (state.make.Equals("KODAK", StringComparison.CurrentCultureIgnoreCase))
            {
                if (state.filters == uint.MaxValue)
				{
                    state.filters = 0x61616161;
				}

                if (state.model.Substring(0, 6) == "NC2000")
                {
                    state.width -= 4;
                    state.left_margin = 2;
                }
                else if (state.model == "EOSDCS3B")
                {
                    state.width -= 4;
                    state.left_margin = 2;
                }
                else if (state.model == "EOSDCS1")
                {
                    state.width -= 4;
                    state.left_margin = 2;
                }
                else if (state.model == "DCS420")
                {
                    state.width -= 4;
                    state.left_margin = 2;
                }
                else if (state.model == "DCS460")
                {
                    state.width -= 4;
                    state.left_margin = 2;
                }
                else if (state.model == "DCS460A")
                {
                    state.width -= 4;
                    state.left_margin = 2;
                    state.colors = 1;
                    state.filters = 0;
                }
                else if (state.model == "DCS660M")
                {
                    state.black = 214;
                    state.colors = 1;
                    state.filters = 0;
                }
                else if (state.model == "DCS760M")
                {
                    state.colors = 1;
                    state.filters = 0;
                }
                if (state.model.Contains("DC25"))
                {
                    state.model = "DC25";
                    state.data_offset = 15424;
                }
                if (state.model.Substring(0, 3) == "DC2")
                {
                    state.height = 242;
                    if (fsize < 100000)
                    {
                        state.raw_width = 256;
                        state.width = 249;
                        state.pixel_aspect = (4.0 * state.height) / (3.0 * state.width);
                    }
                    else
                    {
                        state.raw_width = 512;
                        state.width = 501;
                        state.pixel_aspect = (493.0 * state.height) / (373.0 * state.width);
                    }
                    state.data_offset += state.raw_width + 1;
                    state.colors = 4;
                    state.filters = 0x8d8d8d8d;
                    simple_coeff(1);
                    state.pre_mul[1] = 1.179f;
                    state.pre_mul[2] = 1.209f;
                    state.pre_mul[3] = 1.036f;
                    state.load_raw = new eight_bit_load_raw(state);
                }
                else if (state.model == "40")
                {
                    state.model = "DC40";
                    state.height = 512;
                    state.width = 768;
                    state.data_offset = 1152;
                    state.load_raw = new kodak_radc_load_raw(state);
                }
                else if (state.model.Contains("DC50"))
                {
                    state.model = "DC50";
                    state.height = 512;
                    state.width = 768;
                    state.data_offset = 19712;
                    state.load_raw = new kodak_radc_load_raw(state);
                }
                else if (state.model.Contains("DC120"))
                {
                    state.model = "DC120";
                    state.height = 976;
                    state.width = 848;
                    state.pixel_aspect = state.height / 0.75 / state.width;
                    state.load_raw = state.tiff_compress == 7
                                   ?
                                       (RawLoader)new kodak_jpeg_load_raw(state)
                                   : (RawLoader)new kodak_dc120_load_raw(state);
                }
                else if (state.model == "DCS200")
                {
                    state.thumb_height = 128;
                    state.thumb_width = 192;
                    state.thumb_offset = 6144;
                    state.thumb_misc = 360;
                    state.write_thumb = layer_thumb;
                    state.height = 1024;
                    state.width = 1536;
                    state.data_offset = 79872;
                    state.load_raw = new eight_bit_load_raw(state);
                    state.black = 17;
                }
            }
            else if (state.model == "Fotoman Pixtura")
            {
                state.height = 512;
                state.width = 768;
                state.data_offset = 3632;
                state.load_raw = new kodak_radc_load_raw(state);
                state.filters = 0x61616161;
                simple_coeff(2);
            }
            else if (state.model == "QuickTake 100")
            {
                state.data_offset = 736;
                state.load_raw = new quicktake_100_load_raw(state);
                state.height = 480;
                state.width = 640;
                state.filters = 0x61616161;
            }
            else if (state.model == "QuickTake 150")
            {
                state.data_offset = 738 - head[5];
                if (head[5] != 0)
                {
                    state.model = "QuickTake 200";
                }
                state.load_raw = new kodak_radc_load_raw(state);
                state.height = 480;
                state.width = 640;
                state.filters = 0x61616161;
            }
            else if (state.make == "Rollei" && state.load_raw == null)
            {
                switch (state.raw_width)
                {
                    case 1316:
                        state.height = 1030;
                        state.width = 1300;
                        state.top_margin = 1;
                        state.left_margin = 6;
                        break;
                    case 2568:
                        state.height = 1960;
                        state.width = 2560;
                        state.top_margin = 2;
                        state.left_margin = 8;
                        break;
                }
                state.filters = 0x16161616;
                state.load_raw = new rollei_load_raw(state);
                state.pre_mul[0] = 1.8f;
                state.pre_mul[2] = 1.3f;
            }
            else if (state.model == "PC-CAM 600")
            {
                state.height = 768;
                state.data_offset = state.width = 1024;
                state.filters = 0x49494949;
                state.load_raw = new eight_bit_load_raw(state);
                state.pre_mul[0] = 1.14f;
                state.pre_mul[2] = 2.73f;
            }
            else if (state.model == "QV-2000UX")
            {
                state.height = 1208;
                state.width = 1632;
                state.data_offset = state.width * 2;
                state.load_raw = new eight_bit_load_raw(state);
            }
            else if (fsize == 3217760)
            {
                state.height = 1546;
                state.width = 2070;
                state.raw_width = 2080;
                state.load_raw = new eight_bit_load_raw(state);
            }
            else if (state.model == "QV-4000")
            {
                state.height = 1700;
                state.width = 2260;
                state.load_raw = new unpacked_load_raw(state);
                state.maximum = 0xffff;
            }
            else if (state.model == "QV-5700")
            {
                state.height = 1924;
                state.width = 2576;
                state.load_raw = new casio_qv5700_load_raw(state);
            }
            else if (state.model == "QV-R41")
            {
                state.height = 1720;
                state.width = 2312;
                state.raw_width = 3520;
                state.left_margin = 2;
            }
            else if (state.model == "QV-R51")
            {
                state.height = 1926;
                state.width = 2580;
                state.raw_width = 3904;
                state.pre_mul[0] = 1.340f;
                state.pre_mul[2] = 1.672f;
            }
            else if (state.model == "EX-S20")
            {
                state.height = 1208;
                state.width = 1620;
                state.raw_width = 2432;
                state.flip = 3;
            }
            else if (state.model == "EX-S100")
            {
                state.height = 1544;
                state.width = 2058;
                state.raw_width = 3136;
                state.pre_mul[0] = 1.631f;
                state.pre_mul[2] = 1.106f;
            }
            else if (state.model == "EX-Z50")
            {
                state.height = 1931;
                state.width = 2570;
                state.raw_width = 3904;
                state.pre_mul[0] = 2.529f;
                state.pre_mul[2] = 1.185f;
            }
            else if (state.model == "EX-Z55")
            {
                state.height = 1960;
                state.width = 2570;
                state.raw_width = 3904;
                state.pre_mul[0] = 1.520f;
                state.pre_mul[2] = 1.316f;
            }
            else if (state.model == "EX-Z60")
            {
                state.height = 2145;
                state.width = 2833;
                state.raw_width = 3584;
                state.filters = 0x16161616;
                state.tiff_bps = 10;
            }
            else if (state.model == "EX-Z75")
            {
                state.height = 2321;
                state.width = 3089;
                state.raw_width = 4672;
                state.maximum = 0xfff;
            }
            else if (state.model == "EX-Z750")
            {
                state.height = 2319;
                state.width = 3087;
                state.raw_width = 4672;
                state.maximum = 0xfff;
            }
            else if (state.model == "EX-Z850")
            {
                state.height = 2468;
                state.width = 3279;
                state.raw_width = 4928;
                state.maximum = 0xfff;
            }
            else if (state.model == "EX-Z1050")
            {
                state.height = 2752;
                state.width = 3672;
                state.raw_width = 4928;
                state.maximum = 0xffc;
            }
            else if (state.model == "EX-P505")
            {
                state.height = 1928;
                state.width = 2568;
                state.raw_width = 3852;
                state.maximum = 0xfff;
                state.pre_mul[0] = 2.07f;
                state.pre_mul[2] = 1.88f;
            }
            else if (fsize == 9313536)      /* EX-P600 or QV-R61 */
            {
                state.height = 2142;
                state.width = 2844;
                state.raw_width = 4288;
                state.pre_mul[0] = 1.797f;
                state.pre_mul[2] = 1.219f;
            }
            else if (state.model == "EX-P700")
            {
                state.height = 2318;
                state.width = 3082;
                state.raw_width = 4672;
                state.pre_mul[0] = 1.758f;
                state.pre_mul[2] = 1.504f;
            }
            if (state.model == null)
            {
                state.model = string.Format("{0}x{1}", state.width, state.height);
            }

            if (state.filters == uint.MaxValue) state.filters = 0x94949494;
            if (state.raw_color) adobe_coeff(state.make, state.model);
            if (state.thumb_offset != 0 && state.thumb_height == 0)
            {
                ifp.Seek(state.thumb_offset, SeekOrigin.Begin);
                jh = new JHead(state, ifp, true, state.dng_version);
                state.thumb_width = (ushort)jh.wide;
                state.thumb_height = (ushort)jh.high;
            }
            dng_skip:
            if (state.load_raw == null || state.height < 22) state.is_raw = 0;
#if NO_JPEG
            if (state.load_raw == new kodak_jpeg_load_raw)(state) 
            {
                System::Console::WriteLine("{0}: You must link dcraw with libjpeg!!\n", state.inFilename);
		        state.is_raw = 0;
	        }
#endif
            if (state.cdesc == null)
            {
                state.cdesc = state.colors == 3 ? "RGB" : "GMCY";
            }
            if (state.raw_height == 0) state.raw_height = state.height;
            if (state.raw_width == 0) state.raw_width = state.width;
            if (state.filters != 0 && state.colors == 3)
                for (i = 0; i < 32; i += 4)
                {
                    if ((state.filters >> i & 15) == 9)
                    {
                        state.filters |= (uint)2 << i;
                    }
                    if ((state.filters >> i & 15) == 6)
                    {
                        state.filters |= (uint)8 << i;
                    }
                }
            notraw:
            if (state.flip == -1) state.flip = state.tiff_flip;
            if (state.flip == -1) state.flip = 0;
        }

        private void c603(RawStream ifp, int fsize)
        {
            ifp.Order = 0x4949;
            state.data_offset = fsize - state.raw_height*state.raw_width;
            if (state.data_offset != 0)
            {
                ifp.Seek(168, SeekOrigin.Begin);
                ifp.ReadShorts(state.curve, 256);
            }
            else state.use_gamma = 0;
            state.load_raw = new eight_bit_load_raw(state);
        }

        private void canon_cr2()
        {
            state.height -= state.top_margin;
            state.width -= state.left_margin;
        }

        /*
            Since the TIFF DateTime string has no timezone information,
            assume that the camera's clock was set to Universal Time.
        */
        private void get_timestamp(RawStream ifp, bool reversed)
        {
            //struct tm t;
            string str;

            if (reversed)
            {
                char[] chars = new char[19];
                for (int i = 19; i-- != 0;)
                {
                    chars[i] = (char)ifp.ReadByte();
                }
                str = new string(chars);
            }
            else
            {
                str = ifp.ReadString(19);
                //fread(str, 19, 1, state.ifp);
            }

            //memset(&t, 0, sizeof t);
            /*
            if (sscanf(str, "%d:%d:%d %d:%d:%d", &t.tm_year, &t.tm_mon,
                       &t.tm_mday, &t.tm_hour, &t.tm_min, &t.tm_sec) != 6)
                return;
            t.tm_year -= 1900;
            t.tm_mon -= 1;
             */
            string[] bits = str.Split(new[] {' ', ':'});

            int year = int.Parse(bits[0]);
            int month = int.Parse(bits[1]);
            int day = int.Parse(bits[2]);
            int hour = int.Parse(bits[3]);
            int minute = int.Parse(bits[4]);
            int second = int.Parse(bits[5]);

            state.timestamp = new DateTime(year, month, day, hour, minute, second);
        }

        private void adobe_coeff(string make, string model)
        {
            double[,] cam_xyz = new double[4,3];
            string name = make + " " + model;

            foreach (AdobeCoeff ac in coeffTable)
            {
                if (name.StartsWith(ac.prefix))
                {
                    if (ac.black != 0) state.black = (ushort) ac.black;
                    if (ac.maximum != 0) state.maximum = (ushort)ac.maximum;
                    for (int j = 0; j < ac.trans.Length; j++)
                    {
                        cam_xyz[j / 3, j % 3] = ac.trans[j]/10000.0;
                    }

                    cam_xyz_coeff(cam_xyz);
                    break;
                }
            }
        }

        private static void InitState(DcRawState state)
        {
            state.tiff_flip = -1;
            state.flip = -1;
            state.filters = 0xffffffff;//-1;	/* 0 is valid, so -1 is unknown */
            state.raw_height = 0;
            state.raw_width = 0;
            state.fuji_width = 0;
            state.fuji_layout = 0;
            state.cr2_slice[0] = 0;
            state.maximum = 0;
            state.height = 0;
            state.width = 0;
            state.top_margin = 0;
            state.left_margin = 0;

            state.cdesc = null;
            state.desc = null;
            state.artist = null;
            state.make = null;
            state.model = null;
            state.model2 = null;
            state.iso_speed = 0;
            state.shutter = 0;
            state.aperture = 0;
            state.focal_len = 0;
            state.unique_id = 0;
            //memset (white, 0, sizeof white);
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    state.white[i, j] = 0;
                }
            }

            state.thumb_offset = 0;
            state.thumb_length = 0;
            state.thumb_width = 0;
            state.thumb_height = 0;
            state.load_raw = null;
            state.thumb_load_raw = null;
            state.write_thumb = jpeg_thumb;
            state.data_offset = 0;
            state.meta_length = 0;
            state.tiff_bps = 0;
            state.tiff_compress = 0;
            state.kodak_cbpp = 0;
            state.dng_version = 0;
            state.load_flags = 0;
            state.timestamp = null;
            state.shot_order = 0;
            state.tiff_samples = 0;
            state.black = 0;
            state.is_foveon = false;
            state.mix_green = false;
            state.profile_length = 0;
            state.data_error = 0;
            state.zero_is_bad = 0;
            state.pixel_aspect = 1;
            state.is_raw = 1;
            state.raw_color = true;
            state.use_gamma = 1;
            state.tile_width = int.MaxValue;
            state.tile_length = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                state.cam_mul[i] = i == 1 ? 1 : 0;
                state.pre_mul[i] = i < 3 ? 1 : 0;
                for (int c = 0; c < 3; c++)
                {
                    state.cmatrix[c, i] = 0;
                }

                for (int c = 0; c < 3; c++)
                {
                    state.rgb_cam[c, i] = c == i ? 1 : 0;
                }
            }
            state.colors = 3;
            state.tiff_bps = 12;
            for (int i = 0; i < 0x4000; i++)
            {
                state.curve[i] = (ushort)i;
            }
        }
        
        

        private void parse_phase_one(RawStream ifp, int foffset)
        {
            float[,] romm_cam = new float[3,3];
            Ph1 ph1 = new Ph1();

            ifp.Seek(foffset, SeekOrigin.Begin);
            ifp.Order = (short)(ifp.get4() & 0xffff);
            if (ifp.get4() >> 8 != 0x526177) return; /* "Raw" */
            ifp.Seek(foffset + ifp.get4(), SeekOrigin.Begin);
            uint entries = ifp.get4();
            ifp.get4();
            while (entries-- != 0)
            {
                uint tag = ifp.get4();
                /*uint type = */ifp.get4();
                uint len = ifp.get4();
                int data = ifp.sget4();
                uint save = (uint)ifp.Position;
                ifp.Seek(foffset + data, SeekOrigin.Begin);
                //fseek(state.ifp, foffset + data, SEEK_SET);
                switch (tag)
                {
                    case 0x100:
                        state.flip = "0653"[data & 3] - '0';
                        break;
                    case 0x106:
                        for (int i = 0; i < 9; i++)
                            romm_cam[0,i] = (float)ifp.getreal(11);
                        romm_coeff(romm_cam);
                        break;
                    case 0x107:
                        for (int c = 0; c < 3; c++)
                        {
                            state.cam_mul[c] = (float)ifp.getreal(11);
                        }
                        break;
                    case 0x108:
                        state.raw_width = data;
                        break;
                    case 0x109:
                        state.raw_height = data;
                        break;
                    case 0x10a:
                        state.left_margin = data;
                        break;
                    case 0x10b:
                        state.top_margin = data;
                        break;
                    case 0x10c:
                        state.width = data;
                        break;
                    case 0x10d:
                        state.height = data;
                        break;
                    case 0x10e:
                        ph1.format = data;
                        break;
                    case 0x10f:
                        state.data_offset = data + foffset;
                        break;
                    case 0x110:
                        state.meta_offset = data + foffset;
                        state.meta_length = len;
                        break;
                    case 0x112:
                        ph1.key_off = (int)save - 4;
                        break;
                    case 0x210:
                        ph1.tag_210 = Utils.IntToFloat(data);
                        break;
                    case 0x21a:
                        ph1.tag_21a = data;
                        break;
                    case 0x21c:
                        state.strip_offset = data + foffset;
                        break;
                    case 0x21d:
                        ph1.black = data;
                        break;
                    case 0x222:
                        ph1.split_col = data - state.left_margin;
                        break;
                    case 0x223:
                        ph1.black_off = data + foffset;
                        break;
                    case 0x301:
                        state.model = ifp.ReadString(63);
                        //fread(model, 1, 63, state.ifp);
                        //if ((cp = strstr(model, " camera"))) *cp = 0;
                        int cameraIndex = state.model.IndexOf(" camera");
                        if (cameraIndex != -1)
                        {
                            state.model = state.model.Substring(0, cameraIndex);
                        }
                        break;
                }
                ifp.Seek(save, SeekOrigin.Begin);
            }
            state.load_raw = ph1.format < 3 ? (RawLoader)new phase_one_load_raw(state) :
                                              (RawLoader)new phase_one_load_raw_c(state);
            state.maximum = 0xffff;
            state.make = "Phase One";
            if (state.model != null) return;
            switch (state.raw_height)
            {
                case 2060:
                    state.model = "LightPhase";
                    break;
                case 2682:
                    state.model = "H 10";
                    break;
                case 4128:
                    state.model = "H 20";
                    break;
                case 5488:
                    state.model = "H 25";
                    break;
            }
        }

        private class TiffIfd {
            public int width, height, bps, comp, phint, offset, flip, samples, bytes;
        }

        private readonly TiffIfd[] tiff_ifd = CreateTiffIfds(10);

        private static TiffIfd[] CreateTiffIfds(int total)
        {
            TiffIfd[] ifds = new TiffIfd[total];

            for(int i = 0; i < total; i++)
            {
                ifds[i] = new TiffIfd();
            }

            return ifds;
        }

        private void parse_tiff(RawStream ifp, int foffset)
        {
            int doff, max_samp = 0, raw = -1, thm = -1, i;
            JHead jh;

            ifp.Seek(foffset, SeekOrigin.Begin);
            ifp.Order = ifp.sget2();
            if (ifp.Order != 0x4949 && ifp.Order != 0x4d4d) return;
            ifp.get2();
            //memset(tiff_ifd, 0, sizeof tiff_ifd);

            state.tiff_nifds = 0;
            while ((doff = ifp.sget4()) != 0)
            {
                ifp.Seek(doff + foffset, SeekOrigin.Begin);
                if (parse_tiff_ifd(ifp, foffset)) break;
            }
            state.thumb_misc = 16;
            if (state.thumb_offset != 0)
            {
                ifp.Seek(state.thumb_offset, SeekOrigin.Begin);
                jh = new JHead(state, ifp, true, state.dng_version);
                state.thumb_misc = jh.bits;
                state.thumb_width = jh.wide;
                state.thumb_height = jh.high;
            }

            for (i = 0; i < state.tiff_nifds; i++)
            {
                if (max_samp < tiff_ifd[i].samples)
                    max_samp = tiff_ifd[i].samples;
                if (max_samp > 3) max_samp = 3;
                if ((tiff_ifd[i].comp != 6 || tiff_ifd[i].samples != 3) &&
                    tiff_ifd[i].width*tiff_ifd[i].height > state.raw_width*state.raw_height)
                {
                    state.raw_width = tiff_ifd[i].width;
                    state.raw_height = tiff_ifd[i].height;
                    state.tiff_bps = tiff_ifd[i].bps;
                    state.tiff_compress = tiff_ifd[i].comp;
                    state.data_offset = tiff_ifd[i].offset;
                    state.tiff_flip = tiff_ifd[i].flip;
                    state.tiff_samples = tiff_ifd[i].samples;
                    raw = i;
                }
            }
            state.fuji_width *= (state.raw_width + 1)/2;
            if (tiff_ifd[0].flip != 0) state.tiff_flip = tiff_ifd[0].flip;
            if (raw >= 0 && state.load_raw == null)
                switch (state.tiff_compress)
                {
                    case 0:
                    case 1:
                        switch (state.tiff_bps)
                        {
                            case 8:
                                state.load_raw = new eight_bit_load_raw(state);
                                break;
                            case 12:
                                state.load_raw = new Packed12Loader(state);

                                if (tiff_ifd[raw].phint == 2)
                                {
                                    state.load_flags = 6;
                                }
                                if (!state.make.StartsWith("PENTAX")) break;
                                state.load_raw = new unpacked_load_raw(state);
                                break;
                            case 14:
                            case 16:
                                state.load_raw = new unpacked_load_raw(state);
                                break;
                        }
                        if (tiff_ifd[raw].bytes*5 == state.raw_width*state.raw_height*8)
                        {
                            state.load_raw = new olympus_e300_load_raw(state);
                        }
                        break;
                    case 6:
                    case 7:
                    case 99:
                        state.load_raw = new LosslessJpegLoader(state);
                        break;
                    case 262:
                        state.load_raw = new kodak_262_load_raw(state);
                        break;
                    case 32767:
                        state.load_raw = new sony_arw2_load_raw(state);
                        if (tiff_ifd[raw].bytes*8 == state.raw_width*state.raw_height*state.tiff_bps)
                            break;
                        state.raw_height += 8;
                        state.load_raw = new sony_arw_load_raw(state);
                        break;
                    case 32769:
                        state.load_flags = 8;
                        state.load_raw = new Packed12Loader(state);
                        break;
                    case 32773:
                        state.load_raw = new Packed12Loader(state);
                        break;
                    case 34713:
                        state.load_raw = new NikonCompressedLoader(state);
                        break;
                    case 65535:
                        state.load_raw = new pentax_k10_load_raw(state);
                        break;
                    case 65000:
                        switch (tiff_ifd[raw].phint)
                        {
                            case 2:
                                state.load_raw = new kodak_rgb_load_raw(state);
                                state.filters = 0;
                                break;
                            case 6:
                                state.load_raw = new kodak_ycbcr_load_raw(state);
                                state.filters = 0;
                                break;
                            case 32803:
                                state.load_raw = new kodak_65000_load_raw(state);
                                break;
                        }
                        break;
                    case 32867:
                        break;
                    default:
                        state.is_raw = 0;
                        break;
                }

            if (state.dng_version == 0 && state.tiff_samples == 3)
                if (tiff_ifd[raw].bytes != 0 && state.tiff_bps != 14 && state.tiff_bps != 2048)
                    state.is_raw = 0;
            if (state.dng_version == 0 && state.tiff_bps == 8 && state.tiff_compress == 1 &&
                tiff_ifd[raw].phint == 1) state.is_raw = 0;
            if (state.tiff_bps == 8 && state.tiff_samples == 4) state.is_raw = 0;
            for (i = 0; i < state.tiff_nifds; i++)
                
                if (i != raw && tiff_ifd[i].samples == max_samp &&
                    tiff_ifd[i].width*tiff_ifd[i].height/((tiff_ifd[i].bps + 1) * (tiff_ifd[i].bps + 1)) >
                    state.thumb_width*state.thumb_height/((state.thumb_misc + 1) * (state.thumb_misc + 1)))
                {
                    state.thumb_width = tiff_ifd[i].width;
                    state.thumb_height = tiff_ifd[i].height;
                    state.thumb_offset = tiff_ifd[i].offset;
                    state.thumb_length = tiff_ifd[i].bytes;
                    state.thumb_misc = tiff_ifd[i].bps;
                    thm = i;
                }
            if (thm >= 0)
            {
                state.thumb_misc |= tiff_ifd[thm].samples << 5;
                switch (tiff_ifd[thm].comp)
                {
                    case 0:
                        state.write_thumb = layer_thumb;
                        break;
                    case 1:
                        if (tiff_ifd[thm].bps > 8)
                        {
                            state.thumb_load_raw = new kodak_thumb_load_raw(state);
                        }
                        else
                        {
                            state.write_thumb = ppm_thumb;
                        }
                        break;
                    case 65000:
                        state.thumb_load_raw = tiff_ifd[thm].phint == 6
                                                   ? (RawLoader) new kodak_ycbcr_load_raw(state)
                                                   : (RawLoader) new kodak_rgb_load_raw(state);
                        break;
                }
            }
        }

        private bool parse_tiff_ifd(RawStream ifp, int foffset)
        {
            int use_cm = 0;
            int i, j, c, ima_len = 0;
            string software;
            int plen = 16;
            byte[] cfa_pat = new byte[16];
            byte[] cfa_pc = new byte[] { 0, 1, 2, 3 };
            byte[] tab = new byte[256];
            //byte[] cfa_pat = new byte[16];
            //byte[] cfa_pc = {0, 1, 2, 3};
            //byte[] tab = new byte[256];

            double[,] cc = new double[4,4];
            double[,] cm = new double[4,3];
            double[,] cam_xyz = new double[4,3];
            double[] ab = {1, 1, 1, 1};
            double[] asn = {0, 0, 0, 0};
            double[] xyz = {1,1,1};
            int[] sony_curve = {0,0,0,0,0,4095};
            int sony_offset = 0;
            int sony_length = 0;
            int sony_key = 0;
            JHead jh;

            if (state.tiff_nifds >= tiff_ifd.Length) return true;

            int ifd = state.tiff_nifds++;
            for (j = 0; j < 4; j++)
            {
                for (i = 0; i < 4; i++)
                {
                    cc[j, i] = (i == j) ? 1 : 0;
                }
            }

            int entries = ifp.get2();
            if (entries > 512)
            {
                return true;
            }

            while (entries-- > 0)
            {
                int len;
                int tag;
                int type;
                int save;
                tiff_get(ifp, foffset, out tag, out type, out len, out save);

                switch (tag)
                {
                    case 17:
                    case 18:
                        if (type == 3 && len == 1)
                            state.cam_mul[(tag - 17)*2] = ifp.get2()/256.0f;
                        break;
                    case 23:
                        if (type == 3) state.iso_speed = ifp.get2();
                        break;
                    case 36:
                    case 37:
                    case 38:
                        state.cam_mul[tag - 0x24] = ifp.get2();
                        break;
                    case 39:
                        if (len < 50 || state.cam_mul[0] != 0) break;
                        ifp.Seek(12, SeekOrigin.Current);
                        //fseek(state.ifp, 12, SEEK_CUR);
                        for (c = 0; c < 3; c++) state.cam_mul[c] = ifp.get2();
                        break;
                    case 46:
                        if (type != 7 || ifp.ReadByte() != 0xff || ifp.ReadByte() != 0xd8) break;
                        state.thumb_offset = ifp.Position - 2;
                        state.thumb_length = (int)len;
                        break;
                    case 2:
                    case 256: /* ImageWidth */
                        tiff_ifd[ifd].width = (int)ifp.getint((int)type);
                        break;
                    case 3:
                    case 257: /* ImageHeight */
                        tiff_ifd[ifd].height = (int)ifp.getint((int)type);
                        break;
                    case 258: /* BitsPerSample */
                        tiff_ifd[ifd].samples = (int)(len & 7);
                        tiff_ifd[ifd].bps = ifp.get2();
                        break;
                    case 259: /* Compression */
                        tiff_ifd[ifd].comp = ifp.get2();
                        break;
                    case 262: /* PhotometricInterpretation */
                        tiff_ifd[ifd].phint = ifp.get2();
                        break;
                    case 270: /* ImageDescription */
                        state.desc = ifp.ReadString(512);
                        break;
                    case 271: /* Make */
                        state.make = ifp.fgets(64);
                        break;
                    case 272: /* Model */
                        state.model = ifp.fgets(64);
                        break;
                    case 280: /* Panasonic RW2 offset */
                    case 273: /* StripOffset */
                    case 513:
                        if (tag == 280)
                        {
                            if ((~tiff_ifd[ifd].offset) != 0) break;
                            state.load_raw = new panasonic_load_raw(state);
                            state.load_flags = 0x2008;
                        }

                        tiff_ifd[ifd].offset = (int)(ifp.get4() + foffset);
                        if (tiff_ifd[ifd].bps == 0)
                        {
                            ifp.Seek(tiff_ifd[ifd].offset, SeekOrigin.Begin);
                            jh = new JHead(state, ifp, true, state.dng_version);
                            tiff_ifd[ifd].comp = 6;
                            tiff_ifd[ifd].width = jh.wide << (jh.clrs == 2 ? 1 : 0);
                            tiff_ifd[ifd].height = jh.high;
                            tiff_ifd[ifd].bps = jh.bits;
                            tiff_ifd[ifd].samples = jh.clrs;
                        }
                        break;
                    case 274: /* Orientation */
                        tiff_ifd[ifd].flip = "50132467"[ifp.get2() & 7] - '0';
                        break;
                    case 277: /* SamplesPerPixel */
                        tiff_ifd[ifd].samples = (int)ifp.getint(type) & 7;
                        break;
                    case 279: /* StripByteCounts */
                    case 514:
                        tiff_ifd[ifd].bytes = ifp.sget4();
                        break;
                    case 305: /* Software */
                        software = ifp.fgets(64);
                        if (software.StartsWith("Adobe") ||
                            software.StartsWith("dcraw") ||
                            software.StartsWith("Bibble") ||
                            software.StartsWith("Nikon Scan") ||
                            software == "Digital Photo Professional")
                            state.is_raw = 0;
                        break;
                    case 306: /* DateTime */
                        get_timestamp(ifp, false);
                        break;
                    case 315: /* Artist */
                        state.artist = ifp.ReadString(64);
                        break;
                    case 322: /* TileWidth */
                        state.tile_width = ifp.getint((int)type);
                        break;
                    case 323: /* TileLength */
                        state.tile_length = ifp.getint((int)type);
                        break;
                    case 324: /* TileOffsets */
                        tiff_ifd[ifd].offset = (int)(len > 1 ? ifp.Position : ifp.get4());
                        if (len == 4)
                        {
                            state.load_raw = new sinar_4shot_load_raw(state);
                            state.is_raw = 5;
                        }
                        break;
                    case 330: /* SubIFDs */
                        if (state.model == "DSLR-A100" && tiff_ifd[ifd].width == 3872)
                        {
                            state.load_raw = new sony_arw_load_raw(state);
                            state.data_offset = ifp.get4() + foffset;
                            ifd++;
                            break;
                        }
                        while (len-- != 0)
                        {
                            i = (int)ifp.Position;
                            ifp.Seek(ifp.get4() + foffset, SeekOrigin.Begin);
                            if (parse_tiff_ifd(ifp, foffset)) break;
                            ifp.Seek(i + 4, SeekOrigin.Begin);
                        }
                        break;
                    case 400:
                        state.make = "Sarnoff";
                        state.maximum = 0xfff;
                        break;
                    case 28688:
                        for (c = 0; c < 4; c++)
                        {
                            sony_curve[c + 1] = ifp.get2() >> 2 & 0xfff;
                        }
                        for (i = 0; i < 5; i++)
                        {
                            for (j = sony_curve[i] + 1; j <= sony_curve[i + 1]; j++)
                            {
                                state.curve[j] = (ushort)(state.curve[j - 1] + (1 << i));
                            }
                        }
                        break;
                    case 29184:
                        sony_offset = ifp.sget4();
                        break;
                    case 29185:
                        sony_length = ifp.sget4();
                        break;
                    case 29217:
                        sony_key = ifp.sget4();
                        break;
                    case 29264:
                        parse_minolta((int)ifp.Position);
                        state.raw_width = 0;
                        break;
                    case 29443:
                        for (c = 0; c < 4; c++)
                        {
                            state.cam_mul[c ^ (c < 2 ? 1 : 0)] = ifp.get2();
                        }
                        break;
                    case 33405: /* Model2 */
                        state.model2 = ifp.fgets(64);
                        break;
                    case 33422: /* CFAPattern */
                    case 64777: /* Kodak P-series */
                        if ((plen = len) > 16) plen = 16;
                        ifp.Read(cfa_pat, 0, plen);
                        //fread(cfa_pat, 1, plen, state.ifp);
                        int cfa;
                        for (state.colors = cfa = i = 0; i < plen; i++)
                        {
                            state.colors += (cfa & (1 << cfa_pat[i])) == 0 ? 1 : 0;
                            cfa |= 1 << cfa_pat[i];
                        }
                        if (cfa == 070) {
                            cfa_pc[0] = 3;
                            cfa_pc[1] = 4;
                            cfa_pc[2] = 5;
                            //memcpy(cfa_pc, "\003\004\005", 3); // CMY
                        }
                        if (cfa == 072)
                        {
                            cfa_pc[0] = 5;
                            cfa_pc[1] = 3;
                            cfa_pc[2] = 4;
                            cfa_pc[3] = 1;
                            //memcpy(cfa_pc, "\005\003\004\001", 4); // GMCY}
                        }

                        //goto guess_cfa_pc;
                        for (c = 0; c < state.colors; c++)
                        {
                            tab[cfa_pc[c]] = (byte)c;
                        }

                        // TODO: truncate cdesc?
                        //cdesc[c] = 0;

                        for (i = 16; i-- != 0; )
                        {
                            state.filters = state.filters << 2 | tab[cfa_pat[i % plen]];
                        }
                        break;

                    case 33424:
                        ifp.Seek(ifp.get4() + foffset, SeekOrigin.Begin);
                        parse_kodak_ifd(foffset);
                        break;
                    case 33434: /* ExposureTime */
                        state.shutter = (float)ifp.getreal(type);
                        break;
                    case 33437: /* FNumber */
                        state.aperture = (float)ifp.getreal(type);
                        break;
                    case 34306: /* Leaf white balance */
                        for (c = 0; c < 4; c++)
                        {
                            state.cam_mul[c ^ 1] = 4096.0f / ifp.get2();
                        }
                        break;
                    case 34307: /* Leaf CatchLight color matrix */
                        software = ifp.ReadString(7);

                        if (software.StartsWith("MATRIX")) break;

                        state.colors = 4;
                        state.raw_color = false;
                        throw new NotImplementedException();
                    /*
                    for (i = 0; i < 3; i++)
                    {
                        for (c = 0; c < 4; c++)
                        {
                            float tempFloat;
                            fscanf_f(state.ifp, &tempFloat);
                            state.rgb_cam[i, c ^ 1] = tempFloat;
                        }
                        if (!state.use_camera_wb) continue;
                        double num = 0;
                        for (c = 0; c < 4; c++)
                        {
                            num += state.rgb_cam[i, c];
                        }
                        for (c = 0; c < 4; c++)
                        {
                            state.rgb_cam[i, c] /= (float)num;
                        }
                    }
                    break;
                     */
                    case 34310: /* Leaf metadata */
                        parse_mos(ifp.Position);
                        state.make = "Leaf";
                        break;
                    case 34303:
                        state.make = "Leaf";
                        break;
                    case 34665: /* EXIF tag */
                        ifp.Seek(ifp.get4() + foffset, SeekOrigin.Begin);
                        parse_exif(ifp, foffset);
                        break;
                    case 34853: /* GPSInfo tag */
                        ifp.Seek(ifp.get4() + foffset, SeekOrigin.Begin);
                        parse_gps(foffset);
                        break;
                    case 34675: /* InterColorProfile */
                    case 50831: /* AsShotICCProfile */
                        state.profile_offset = ifp.Position;
                        state.profile_length = len;
                        break;
                    case 37122: /* CompressedBitsPerPixel */
                        state.kodak_cbpp = ifp.get4();
                        break;
                    case 37386: /* FocalLength */
                        state.focal_len = (float)ifp.getreal(type);
                        break;
                    case 37393: /* ImageNumber */
                        state.shot_order = ifp.getint(type);
                        break;
                    case 37400: /* old Kodak KDC tag */
                        state.raw_color = false;
                        for (i = 0; i < 3; i++)
                        {
                            ifp.getreal(type);
                            for (c = 0; c < 3; c++)
                            {
                                state.rgb_cam[i, c] = (float)ifp.getreal(type);
                            }
                        }
                        break;
                    case 46275: /* Imacon tags */
                        state.make = "Imacon";
                        state.data_offset = ifp.Position;
                        ima_len = len;
                        break;
                    case 46279:
                        ifp.Seek(78, SeekOrigin.Current);
                        state.raw_width = ifp.sget4();
                        state.raw_height = ifp.sget4();
                        state.left_margin = ifp.sget4() & 7;
                        state.width = state.raw_width - state.left_margin - (ifp.sget4() & 7);
                        state.top_margin = ifp.sget4() & 7;
                        state.height = state.raw_height - state.top_margin - (ifp.sget4() & 7);
                        if (state.raw_width == 7262)
                        {
                            state.height = 5444;
                            state.width = 7244;
                            state.left_margin = 7;
                        }
                        ifp.Seek(52, SeekOrigin.Current);
                        for (c = 0; c < 3; c++)
                        {
                            state.cam_mul[c] = (float) ifp.getreal(11);
                        }
                        ifp.Seek(114, SeekOrigin.Current);
                        state.flip = (ifp.get2() >> 7)*90;
                        if (state.width*state.height*6 == ima_len)
                        {
                            if (state.flip%180 == 90)
                            {
                                int temp = state.width;
                                state.width = state.height;
                                state.height = temp;
                                //SWAP(state.width, state.height);
                            }
                            state.filters = 0;
                            state.flip = 0;
                        }
                        state.model = string.Format("Ixpress {0}-Mp", state.height*state.width/1000000);
                        //sprintf(model, "Ixpress %d-Mp", state.height*state.width/1000000);
                        state.load_raw = new imacon_full_load_raw(state);
                        if (state.filters != 0)
                        {
                            if ((state.left_margin & 1) != 0) state.filters = 0x61616161;
                            state.load_raw = new unpacked_load_raw(state);
                        }
                        state.maximum = 0xffff;
                        break;
                    case 50454: /* Sinar tag */
                    case 50455:
                        throw new NotImplementedException();
                        /*
                        if (!(cbuf = (char*) state.Alloc(len))) break;
                        fread(cbuf, 1, len, state.ifp);
                        for (cp = cbuf - 1; cp && cp < cbuf + len; cp = strchr(cp, '\n'))
                        {
                            if (!strncmp(++cp, "Neutral ", 8))
                            {
                                float cam_mul0, cam_mul1, cam_mul2;
                                sscanf(cp + 8, "%f %f %f", &cam_mul0, &cam_mul1, &cam_mul2);
                                state.cam_mul[0] = cam_mul0;
                                state.cam_mul[1] = cam_mul1;
                                state.cam_mul[2] = cam_mul2;
                            }
                        }
                        state.Free(cbuf);
                        break;
                         */
                    case 50459: /* Hasselblad tag */
                        i = ifp.Order;
                        j = (int)ifp.Position;
                        c = state.tiff_nifds;
                        ifp.Order = ifp.sget2();
                        // ----
                        ifp.get2();
                        ifp.Seek(ifp.get4(), SeekOrigin.Begin);
                        //fseek(state.ifp, j + (state.ifp.get2(), state.ifp.get4()),SEEK_SET);
                        // ----
                        parse_tiff_ifd(ifp, j);
                        state.maximum = 0xffff;
                        state.tiff_nifds = c;
                        ifp.Order = (short)i;
                        break;
                    case 50706: /* DNGVersion */
                        for (c = 0; c < 4; c++)
                        {
                            state.dng_version = (uint)((state.dng_version << 8) + ifp.ReadByte());
                        }
                        break;
                    case 50710: /* CFAPlaneColor */
                        if (len > 4) len = 4;
                        state.colors = len;
                        ifp.Read(cfa_pc, 0, state.colors);
                        //fread(cfa_pc, 1, state.colors, state.ifp);
                        guess_cfa_pc:
                        for (c = 0; c < state.colors; c++)
                        {
                            tab[cfa_pc[c]] = (byte)c;
                        }

                        // TODO: truncate cdesc?
                        //cdesc[c] = 0;

                        for (i = 16; i-- != 0; )
                        {
                            state.filters = state.filters << 2 | tab[cfa_pat[i % plen]];
                        }
                        break;
                    case 50711: /* CFALayout */
                        if (ifp.get2() == 2)
                        {
                            state.fuji_width = 1;
                            state.filters = 0x49494949;
                        }
                        break;
                    case 291:
                    case 50712: /* LinearizationTable */
                        linear_table(len);
                        break;
                    case 50714: /* BlackLevel */
                    case 50715: /* BlackLevelDeltaH */
                    case 50716: /* BlackLevelDeltaV */
                        double dblack;
                        for (dblack = i = 0; i < len; i++)
                        {
                            dblack += ifp.getreal(type);
                        }
                        state.black += (uint)(dblack/len + 0.5);
                        break;
                    case 50717: /* WhiteLevel */
                        state.maximum = ifp.getint(type);
                        break;
                    case 50718: /* DefaultScale */
                        state.pixel_aspect = ifp.getreal(type);
                        state.pixel_aspect /= ifp.getreal(type);
                        break;
                    case 50721: /* ColorMatrix1 */
                    case 50722: /* ColorMatrix2 */
                        for (c = 0; c < state.colors; c++)
                            for (j = 0; j < 3; j++)
                                cm[c,j] = ifp.getreal(type);
                        use_cm = 1;
                        break;
                    case 50723: /* CameraCalibration1 */
                    case 50724: /* CameraCalibration2 */
                        for (i = 0; i < state.colors; i++)
                        {
                            for (c = 0; c < state.colors; c++)
                            {
                                cc[i, c] = ifp.getreal(type);
                            }
                        }
                        for (c = 0; c < state.colors; c++)
                        {
                            ab[c] = ifp.getreal(type);
                        }
                        break;
                    case 50727: /* AnalogBalance */
                        for (c = 0; c < state.colors; c++)
                        {
                            ab[c] = ifp.getreal(type);
                        }
                        break;
                    case 50728: /* AsShotNeutral */
                        for (c = 0; c < state.colors; c++)
                        {
                            asn[c] = ifp.getreal(type);
                        }
                        break;
                    case 50729: /* AsShotWhiteXY */
                        xyz[0] = ifp.getreal((int)type);
                        xyz[1] = ifp.getreal((int)type);
                        xyz[2] = 1 - xyz[0] - xyz[1];
                        for (c = 0; c < 3; c++) xyz[c] /= state.d65_white[c];
                        break;
                    case 50740: /* DNGPrivateData */
                        if (state.dng_version != 0) break;
                        j = ifp.sget4() + foffset;
                        parse_minolta(j);
                        ifp.Seek(j, SeekOrigin.Begin);
                        parse_tiff_ifd(ifp, foffset);
                        break;
                    case 50752:
                        ifp.ReadShorts(state.cr2_slice, 3);
                        break;
                    case 50829: /* ActiveArea */
                        state.top_margin = (int)ifp.getint((int)type);
                        state.left_margin = (int)ifp.getint((int)type);
                        state.height = (int)ifp.getint((int)type) - state.top_margin;
                        state.width = (int)ifp.getint((int)type) - state.left_margin;
                        break;
                    case 64772: /* Kodak P-series */
                        ifp.Seek(16, SeekOrigin.Current);
                        state.data_offset = ifp.get4();
                        ifp.Seek(28, SeekOrigin.Current);
                        state.data_offset += ifp.get4();
                        state.load_raw = new Packed12Loader(state);
                        break;
                }
                ifp.Seek(save, SeekOrigin.Begin);
            }

            if (sony_length != 0)
            {
                byte[] buf = new byte[sony_length];
                ifp.Seek(sony_offset, SeekOrigin.Begin);

                ifp.Read(buf, 0, sony_length);
                //fread(buf, sony_length, 1, state.ifp);
                sony_decrypt(buf, sony_length / 4, 1, sony_key);

                RawStream newfp = new RawStream(buf, 0, buf.Length);
                parse_tiff_ifd(newfp, (int)-sony_offset);
                newfp.Dispose();
            }
            for (i = 0; i < state.colors; i++)
            {
                for (c = 0; c < state.colors; c++)
                {
                    cc[i, c] *= ab[i];
                }
            }

            if (use_cm != 0)
            {
                for (c = 0; c < state.colors; c++)
                {
                    for (i = 0; i < 3; i++)
                    {
                        for (cam_xyz[c, i] = j = 0; j < state.colors; j++)
                        {
                            cam_xyz[c, i] += cc[c, j]*cm[j, i]*xyz[i];
                        }
                    }
                }

                cam_xyz_coeff(cam_xyz);
            }
            
            if (asn[0] != 0)
            {
                state.cam_mul[3] = 0;
                for (c = 0; c < state.colors; c++)
                {
                    state.cam_mul[c] = (float)(1f/asn[c]);
                }
            }

            if (use_cm == 0)
            {
                for (c = 0; c < state.colors; c++)
                {
                    state.pre_mul[c] /= (float)cc[c, c];
                }
            }

            return false;
        }

        private static void tiff_get(RawStream ifp, int foffset, out int tag, out int type, out int len, out int save)
        {
            tag  = ifp.get2();
	        type = ifp.get2();
	        len  = ifp.sget4();
	        save = (int)(ifp.Position + 4);

	        if (len * ("11124811248488"[type < 14 ? type : 0] - '0') > 4)
	        {
	            ifp.Seek(ifp.get4() + foffset, SeekOrigin.Begin);
	            //fseek (state.ifp, state.ifp.get4()+base, SEEK_SET);
	        }
        }

        private void parse_exif (RawStream ifp, int foffset)
        {
            bool kodak = state.make.StartsWith("EASTMAN");
            int entries = ifp.get2();
            while (entries-- != 0)
            {
                int tag;
                int len;
                int type;
                int save;
                tiff_get(ifp, foffset, out tag, out type, out len, out save);
                switch (tag)
                {
                    case 33434:
                        state.shutter = (float)ifp.getreal(type);
                        break;
                    case 33437:
                        state.aperture = (float)ifp.getreal(type);
                        break;
                    case 34855:
                        state.iso_speed = ifp.get2();
                        break;
                    case 36867:
                    case 36868:
                        get_timestamp(ifp, false);
                        break;
                    case 37377:
                        double expo;
                        if ((expo = -ifp.getreal(type)) < 128)
                            state.shutter = (float)Math.Pow(2, expo);
                        break;
                    case 37378:
                        state.aperture = (float)Math.Pow(2, ifp.getreal(type) / 2);
                        break;
                    case 37386:
                        state.focal_len = (float)ifp.getreal(type);
                        break;
                    case 37500:
                        parse_makernote(ifp, foffset, 0);
                        break;
                    case 40962:
                        if (kodak)
                        {
                            state.raw_width = ifp.sget4();
                        }
                        break;
                    case 40963:
                        if (kodak)
                        {
                            state.raw_height = ifp.sget4();
                        }
                        break;
                    case 41730:
                        if (ifp.get4() == 0x20002)
                        {
                            state.exif_cfa = 0;
                            for (int c = 0; c < 8; c += 2)
                            {
                                state.exif_cfa |= (uint)(ifp.ReadByte()*0x01010101 << c);
                            }
                        }
                        break;
                }

                ifp.Seek(save, SeekOrigin.Begin);
            }
        }

        private void parse_makernote(RawStream ifp, int foffset, int uptag)
        {
            const int BUF97LEN = 324;
            int offset;
            int c;
            int ver97 = 0;
            int serial = 0;
            int i;
            int wbi = 0;
            int[] wb = {0, 0, 0, 0};
            byte[] buf97 = new byte[BUF97LEN];

            short sorder = ifp.Order;
            /*
		        The MakerNote might have its own TIFF header (possibly with
		        its own byte-order!), or it might just be a table.
		    */
            string buf = ifp.ReadString(10);
            if (buf.StartsWith("KDK") || /* these aren't TIFF tables */
                buf.StartsWith("VER") ||
                buf.StartsWith("IIII") ||
                buf.StartsWith("MMMM")) return;
            if (buf.StartsWith("KC") || /* Konica KD-400Z, KD-510Z */
                buf.StartsWith("MLY"))  /* Minolta DiMAGE G series */
            {
                ifp.Order = 0x4d4d;
                while ((i = (int)ifp.Position) < state.data_offset && i < 16384)
                {
                    wb[0] = wb[2];
                    wb[2] = wb[1];
                    wb[1] = wb[3];
                    wb[3] = ifp.get2();
                    if (wb[1] == 256 && wb[3] == 256 &&
                        wb[0] > 256 && wb[0] < 640 && wb[2] > 256 && wb[2] < 640)
                    {
                        for (c = 0; c < 4; c++)
                        {
                            state.cam_mul[c] = wb[c];
                        }
                    }
                }
                ifp.Order = sorder;
                return;
            }

            if (buf == "Nikon")
            {
                foffset = (int)ifp.Position;
                ifp.Order = ifp.sget2();
                if (ifp.get2() != 42)
                {
                    ifp.Order = sorder;
                    return;
                }
                offset = ifp.sget4();
                ifp.Seek(offset - 8, SeekOrigin.Current);
            }
            else if (buf == "OLYMPUS")
            {
                foffset = (int)ifp.Position - 10;
                ifp.Seek(-2, SeekOrigin.Current);
                ifp.Order = ifp.sget2();
                ifp.get2();
            }
            else if (buf.StartsWith("FUJIFILM") ||
                     buf.StartsWith("SONY") ||
                     buf == "Panasonic")
            {
                ifp.Order = 0x4949;
                ifp.Seek(2, SeekOrigin.Current);
            }
            else if (buf == "OLYMP" ||
                     buf == "LEICA" ||
                     buf == "Ricoh" ||
                     buf == "EPSON")
            {
                ifp.Seek(-2, SeekOrigin.Current);
            }
            else if (buf == "AOC" ||
                     buf == "QVC")
            {
                ifp.Seek(-4, SeekOrigin.Current);
            }
            else
            {
                ifp.Seek(-10, SeekOrigin.Current);
            }

            int entries = ifp.get2();
            if (entries > 1000)         //todo: why? what does it mean?
            {
                return;
            }

            while (entries-- != 0)
            {
                bool get2_rggb_B = false;
                bool get2_256_B = false;
                int type;
                int tag;
                int len;
                int save;

                tiff_get(ifp, foffset, out tag, out type, out len, out save);
                tag |= uptag << 16;

                if (tag == 2 && state.make.Contains("NIKON"))
                {
                    ifp.get2();
                    state.iso_speed = ifp.get2();
                }

                if (tag == 4 && len > 26 && len < 35)
                {
                    ifp.get4();
                    if ((i = ifp.get2()) != 0x7fff && state.iso_speed == 0.0f)
                    {
                        state.iso_speed = (float)(50 * Math.Pow(2, i / 32.0 - 4));
                    }

                    ifp.get2();
                    i = ifp.get2();
                    if (i != 0x7fff && state.aperture == 0.0f)
                    {
                        state.aperture = (float)Math.Pow(2, i/64.0);
                    }

                    if ((i = ifp.get2()) != 0xffff && state.shutter == 0.0f)
                    {
                        state.shutter = (float)Math.Pow(2, (short) i/-32.0);
                    }

                    ifp.get2();
                    wbi = ifp.get2();

                    ifp.get2();
                    state.shot_order = ifp.get2();
                }

                if ((tag == 4 || tag == 0x114) && !state.make.StartsWith("KONICA"))
                {
                    ifp.Seek(tag == 4 ? 140 : 160, SeekOrigin.Current);
                    switch (ifp.get2())
                    {
                        case 72:
                            state.flip = 0;
                            break;
                        case 76:
                            state.flip = 6;
                            break;
                        case 82:
                            state.flip = 5;
                            break;
                    }
                }

                if (tag == 7 && type == 2 && len > 20)
                {
                    state.model2 = ifp.ReadString(64);
                }

                if (tag == 8 && type == 4)
                {
                    state.shot_order = ifp.get4();
                }

                if (tag == 9 && state.make == "Canon")
                {
                    state.artist = ifp.ReadString(64);
                }

                if (tag == 0xc && len == 4)
                {
                    state.cam_mul[0] = (float)ifp.getreal(type);
                    state.cam_mul[2] = (float)ifp.getreal(type);
                }

                if (tag == 0xd && type == 7 && ifp.get2() == 0xaaaa)
                {
                    ifp.Read(buf97, 0, BUF97LEN);
                    //i = (uchar *) memmem (buf97, sizeof buf97,"\xbb\xbb",2) - buf97 + 10;
                    //todo check this!
                    i = Utils.memmem(buf97, "\xbb\xbb") - BUF97LEN + 10;

                    if (i < 70 && buf97[i] < 3)
                    {
                        state.flip = "065"[buf97[i]]-'0';
                    }
                }

                if (tag == 0x10 && type == 4)
                {
                    state.unique_id = ifp.get4();
                }

                if (tag == 0x11 && state.is_raw != 0 && state.make.StartsWith("NIKON"))
                {
                    ifp.Seek(ifp.get4() + foffset, SeekOrigin.Begin);
                    parse_tiff_ifd(ifp, foffset);
                }
                if (tag == 0x14 && len == 2560 && type == 7)
                {
                    ifp.Seek(1248, SeekOrigin.Current);
                    get2_256_B = true;
                    goto get2_256;
                }
                if (tag == 0x15 && type == 2 && state.is_raw != 0)
                {
                    state.model = ifp.ReadString(64);
                }
                if (state.make.Contains("PENTAX"))
                {
                    if (tag == 0x1b) tag = 0x1018;
                    if (tag == 0x1c) tag = 0x1017;
                }
                if (tag == 0x1d)
                {
                    while ((c = ifp.ReadByte()) != 0 && c != -1)
                    {
                        serial = serial*10 + ((c >= '0' && c <= '9') ? c - '0' : c % 10);
                    }
                }

                if (tag == 0x81 && type == 4)
                {
                    state.data_offset = ifp.get4();
                    ifp.Seek(state.data_offset + 41, SeekOrigin.Begin);
                    state.raw_height = ifp.get2() * 2;
                    state.raw_width = ifp.get2();
                    state.filters = 0x61616161;
                }

                if (tag == 0x29 && type == 1)
                {
                    c = wbi < 18 ? "012347800000005896"[wbi] - '0' : 0;
                    ifp.Seek(8 + c*32, SeekOrigin.Current);
                    for (c = 0; c < 4; c++)
                    {
                        state.cam_mul[c ^ (c >> 1) ^ 1] = ifp.get4();
                    }
                }

                if ((tag == 0x81 && type == 7) ||
                    (tag == 0x100 && type == 7) ||
                    (tag == 0x280 && type == 1))
                {
                    state.thumb_offset = ifp.Position;
                    state.thumb_length = len;
                }

                if (tag == 0x88 && type == 4 && (state.thumb_offset = ifp.get4()) != 0)
                {
                    state.thumb_offset += foffset;
                }

                if (tag == 0x89 && type == 4)
                {
                    state.thumb_length = ifp.sget4();
                }

                if (tag == 0x8c || tag == 0x96)
                {
                    state.meta_offset = ifp.Position;
                }

                if (tag == 0x97)
                {
                    for (i = 0; i < 4; i++)
                    {
                        ver97 = ver97 * 10 + ifp.ReadByte() - '0';
                    }

                    switch (ver97)
                    {
                        case 100:
                            ifp.Seek(68, SeekOrigin.Current);
                            for (c = 0; c < 4; c++)
                            {
                                state.cam_mul[(c >> 1) | ((c & 1) << 1)] = ifp.get2();
                            }
                            break;
                        case 102:
                            ifp.Seek(6, SeekOrigin.Current);
                            get2_rggb_B = true;
                            goto get2_rggb;
                        case 103:
                            ifp.Seek(16, SeekOrigin.Current);
                            for (c = 0; c < 4; c++)
                            {
                                state.cam_mul[c] = ifp.get2();
                            }
                            break;
                    }

                    if (ver97 >= 200)
                    {
                        if (ver97 != 205)
                        {
                            ifp.Seek(280, SeekOrigin.Current);
                        }
                        ifp.Read(buf97, 0, BUF97LEN);
                    }
                }

                if (tag == 0xa1 && type == 7)
                {
                    type = ifp.Order;
                    ifp.Order = 0x4949;
                    ifp.Seek(140, SeekOrigin.Current);
                    for (c = 0; c < 3; c++)
                    {
                        state.cam_mul[c] = ifp.get4();
                    }
                    ifp.Order = (short)type;
                }

                if (tag == 0xa4 && type == 3)
                {
                    ifp.Seek(wbi*48, SeekOrigin.Current);
                    for (c = 0; c < 3; c++)
                    {
                        state.cam_mul[c] = ifp.get2();
                    }
                }

                if (tag == 0xa7 && (uint) (ver97 - 200) < 12 && state.cam_mul[0] == 0)
                {
                    byte ci = xlat[0,serial & 0xff];
                    byte cj = xlat[1,ifp.ReadByte() ^ ifp.ReadByte() ^ ifp.ReadByte() ^ ifp.ReadByte()];
                    byte ck = 0x60;
                    for (i = 0; i < BUF97LEN; i++)
                    {
                        cj += (byte)(ci*ck++);
                        buf97[i] ^= cj;
                    }

                    i = "66666>666;6A"[ver97 - 200] - '0';
                    for (c = 0; c < 4; c++)
                    {
                        state.cam_mul[c ^ (c >> 1) ^ (i & 1)] = Utils.sget2(ifp.Order, buf97, (i & -2) + c*2);
                    }
                }

                if (tag == 0x200 && len == 3)
                {
                    ifp.get4();
                    state.shot_order = ifp.get4();
                }

                if (tag == 0x200 && len == 4)
                {
                    //for (c = 0; c < 4; c++)
                    //{
                    //    cblack[c ^ c >> 1] = get2();
                    //}
                    state.black = (uint)(ifp.get2() + ifp.get2() + ifp.get2() + ifp.get2())/4;
                }

                if (tag == 0x201 && len == 4)
                {
                    get2_rggb_B = true;
                    goto get2_rggb;
                }

                if (tag == 0x220 && type == 7)
                {
                    state.meta_offset = ifp.Position;
                }

                if (tag == 0x401 && len == 4)
                {
                    //for (c=0; c < 4; c++)
                    //    cblack[c ^ c >> 1] = get4();
                    state.black = (ifp.get4() + ifp.get4() + ifp.get4() + ifp.get4())/4;
                }

                if (tag == 0xe01)           /* Nikon Capture note */
                {
                    type = ifp.Order;
                    ifp.Order = 0x4949;
                    ifp.Seek(22, SeekOrigin.Current);
                    for (offset = 22; offset + 22 < len; offset += 22 + i)
                    {
                        tag = ifp.sget4();
                        ifp.Seek(14, SeekOrigin.Current);
                        i = ifp.sget4() - 4;
                        if (tag == 0x76a43207)
                        {
                            state.flip = ifp.get2();
                        }
                        else
                        {
                            ifp.Seek(i, SeekOrigin.Current);
                        }
                    }
                    ifp.Order = (short)type;
                }

                if (tag == 0xe80 && len == 256 && type == 7)
                {
                    ifp.Seek(48, SeekOrigin.Current);
                    state.cam_mul[0] = ifp.get2()*508*1.078f/0x10000;
                    state.cam_mul[2] = ifp.get2()*382*1.173f/0x10000;
                }

                if (tag == 0xf00 && type == 7)
                {
                    if (len == 614)
                        ifp.Seek(176, SeekOrigin.Current);
                    else if (len == 734 || len == 1502)
                        ifp.Seek(148, SeekOrigin.Current);
                    else goto next;

                    get2_256_B = true;
                    goto get2_256;
                }

                if ((tag == 0x1011 && len == 9) || tag == 0x20400200)
                {
                    for (i = 0; i < 3; i++)
                    {
                        for (c = 0; c < 3; c++)
                        {
                            state.cmatrix[i, c] = ((short) ifp.get2()) / 256.0f;
                        }
                    }
                }

                if ((tag == 0x1012 || tag == 0x20400600) && len == 4)
                {
                    //for (c=0; c < 4; c++)
                    //    cblack[c ^ c >> 1] = get2();
                    state.black = 0;
                    for (i = 0; i < 4; i++)
                    {
                        state.black += (uint)(ifp.get2() << 2);
                    }
                }
                if (tag == 0x1017 || tag == 0x20400100)
                {
                    state.cam_mul[0] = ifp.get2() / 256.0f;
                }

                if (tag == 0x1018 || tag == 0x20400100)
                {
                    state.cam_mul[2] = ifp.get2() / 256.0f;
                }

                if (tag == 0x2011 && len == 2)
                {
                    get2_256_B = true;
                }

            get2_256:
                if (get2_256_B)
                {
                    ifp.Order = 0x4d4d;
                    state.cam_mul[0] = ifp.get2() / 256.0f;
                    state.cam_mul[2] = ifp.get2() / 256.0f;
                }

                if ((tag | 0x70) == 0x2070 && type == 4)
                {
                    ifp.Seek(ifp.get4() + foffset, SeekOrigin.Current);
                }

                if (tag == 0x2010 && type != 7)
                {
                    state.load_raw = new olympus_e410_load_raw(state);
                }

                if (tag == 0x2020)
                {
                    parse_thumb_note(foffset, 257, 258);
                }

                if (tag == 0x2040)
                {
                    parse_makernote(ifp, foffset, 0x2040);
                }

                if (tag == 0xb028)
                {
                    ifp.Seek(ifp.get4()+foffset, SeekOrigin.Begin);
                    parse_thumb_note(foffset, 136, 137);
                }

                if (tag == 0x4001 && len > 500)
                {
                    i = len == 582 ? 50 : len == 653 ? 68 : len == 5120 ? 142 : 126;    // haha straight from dcraw.c
                    ifp.Seek(i, SeekOrigin.Current);
                    
                    get2_rggb_B = true;
                }

            get2_rggb:
                if (get2_rggb_B)
                {
                    for (c = 0; c < 4; c++)
                    {
                        state.cam_mul[c ^ (c >> 1)] = ifp.get2();
                    }

                    ifp.Seek(22, SeekOrigin.Current);
                    for (c = 0; c < 4; c++)
                    {
                        state.sraw_mul[c ^ (c >> 1)] = ifp.get2();
                    }
                }
            next:
                ifp.Seek(save, SeekOrigin.Begin);
            }
            ifp.Order = sorder;
        }

        private void cam_xyz_coeff(double[,] cam_xyz)
        {
            double[,] cam_rgb = new double[4,3];
            double[,] inverse = new double[4,3];
            int i, j;

            for (i = 0; i < state.colors; i++) /* Multiply out XYZ colorspace */
            {
                for (j = 0; j < 3; j++)
                {
                    int k;
                    cam_rgb[i, j] = 0;
                    for (k = 0; k < 3; k++)
                    {
                        cam_rgb[i,j] += cam_xyz[i,k] * state.xyz_rgb[k, j];
                    }
                }
            }

            for (i = 0; i < state.colors; i++)
            {
                /* Normalize cam_rgb so that */
                double num;
                for (num = j = 0; j < 3; j++) /* cam_rgb * (1,1,1) is (1,1,1,1) */
                    num += cam_rgb[i,j];
                for (j = 0; j < 3; j++)
                    cam_rgb[i,j] /= num;
                state.pre_mul[i] = (float)(1/num);
            }
            pseudoinverse(cam_rgb, inverse, state.colors);
            state.raw_color = false;
            for (i = 0; i < 3; i++)
            {
                for (j = 0; j < state.colors; j++)
                {
                    state.rgb_cam[i, j] = (float)inverse[j, i];
                }
            }
        }

        private static void pseudoinverse(double[,] inVal, double[,] outVal, int size)
        {
            double[,] work = new double[3,6];
            int i;

            for (i = 0; i < 3; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    work[i, j] = (j == i + 3) ? 1 : 0;
                }

                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < size; k++)
                    {
                        work[i, j] += inVal[k, i]*inVal[k, j];
                    }
                }
            }

            for (i = 0; i < 3; i++)
            {
                double num = work[i, i];
                for (int j = 0; j < 6; j++)
                {
                    work[i, j] /= num;
                }

                for (int k = 0; k < 3; k++)
                {
                    if (k == i) continue;
                    num = work[k, i];
                    for (int j = 0; j < 6; j++)
                    {
                        work[k, j] -= work[i, j]*num;
                    }
                }
            }

            for (i = 0; i < size; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    outVal[i, j] = 0;
                    for (int k = 0; k < 3; k++)
                        outVal[i, j] += work[j, k + 3]*inVal[i, k];
                }
            }
        }

        #region Unimplemented stuff
        private void parse_thumb_note(int foffset, int i, int i1)
        {
            throw new NotImplementedException();
        }

        private void linear_table(int len)
        {
            throw new NotImplementedException();
        }

        private void parse_gps(int foffset)
        {
            uint entries = state.ifp.get2();

            while (entries-- != 0)
            {
                int tag;
                int type;
                int len;
                int save;

                tiff_get(state.ifp, foffset, out tag, out type, out len, out save);

                switch (tag)
                {
                    case 1:
                    case 3:
                    case 5:
                        state.gpsdata[29 + tag / 2] = (uint)state.ifp.ReadByte();
                        break;
                    case 2:
                    case 4:
                    case 7:
                        for (int c = 0; c < 6; c++)
                        {
                            state.gpsdata[tag/3*6 + c] = state.ifp.get4();
                        }
                        break;
                    case 6:
                        for (int c = 0; c < 2; c++)
                        {
                            state.gpsdata[18 + c] = state.ifp.get4();
                        } 
                        break;

                    case 18:
                    case 29:
                        state.ifp.ReadString(Math.Min(len, 12));
                        // TODO: do something nicer than use a bit INT array thing
                        
                        //fgetsdotnet((char*)(STATE->gpsdata[14 + tag / 3]), MIN(len, 12), STATE->ifp);
                        break;
                }

                state.ifp.Seek(save, SeekOrigin.Begin);
            }
        }

        private short guess_byte_order(int words)
        {
            throw new NotImplementedException();
            //byte[][] test = new byte[4][];
            //test[0] = new byte[2];
            //test[1] = new byte[2];
            //test[2] = new byte[2];
            //test[3] = new byte[2];
            //int t = 2;
            //int msb;
            //double diff;
            //double[] sum = new double[2] {0,0};

            //state.ifp.Read(test[0], 0, 2 * 2);

            //for (words-=2; words--; ) 
            //{
            //    state.ifp.Read(test[t], 0, 2);
            //    for (msb=0; msb < 2; msb++) 
            //    {
            //        diff = (test[t^2][msb] << 8 | test[t^2][!msb]) - (test[t  ][msb] << 8 | test[t  ][!msb]);
            //        sum[msb] += diff*diff;
            //    }
            //    t = (t+1) & 3;
            //}
            //return sum[0] < sum[1] ? 0x4d4d : 0x4949;
        }

        private bool minolta_z2()
        {
            throw new NotImplementedException();
        }

        private void nikon_3700()
        {
            throw new NotImplementedException();
        }

        private bool nikon_e2100()
        {
            throw new NotImplementedException();
        }

        private bool nikon_e995()
        {
            throw new NotImplementedException();
        }

        private bool canon_s2is()
        {
            throw new NotImplementedException();
        }

        private void simple_coeff(int p)
        {
            throw new NotImplementedException();
        }

        private bool nikon_is_compressed()
        {
            throw new NotImplementedException();
        }

        private void parse_mos(long position)
        {
            throw new NotImplementedException();
        }

        private void sony_decrypt(byte[] buf, int u, int i, int sony_key)
        {
            throw new NotImplementedException();
        }

        private void parse_kodak_ifd(int foffset)
        {
            throw new NotImplementedException();
        }

        private void romm_coeff(float[,] romm_cam)
        {
            throw new NotImplementedException();
        }

        private static void jpeg_thumb(Stream target)
        {
            throw new NotImplementedException();
        }

        private void layer_thumb(Stream target)
        {
            throw new NotImplementedException();
        }

        private void ppm_thumb(Stream target)
        {
            throw new NotImplementedException();
        }

        private void parse_jpeg(int p)
        {
            throw new NotImplementedException();
        }

        private void parse_smal(int p, int fsize)
        {
            throw new NotImplementedException();
        }

        private void parse_external_jpeg()
        {
            throw new NotImplementedException();
        }

        private void parse_fuji(int i)
        {
            throw new NotImplementedException();
        }

        private void parse_riff()
        {
            throw new NotImplementedException();
        }

        private void parse_cine()
        {
            throw new NotImplementedException();
        }

        private void parse_foveon()
        {
            throw new NotImplementedException();
        }

        private void parse_minolta(int p)
        {
            throw new NotImplementedException();
        }

        private void parse_sinar_ia()
        {
            throw new NotImplementedException();
        }

        private void parse_rollei()
        {
            throw new NotImplementedException();
        }

        private void parse_ciff(int hlen, int u)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
