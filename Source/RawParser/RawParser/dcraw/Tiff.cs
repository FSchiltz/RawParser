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
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace dcraw
{
    internal class Tiff
    {
        private DcRawState state;

        public Tiff(DcRawState state)
        {
            this.state = state;
        }

        public void write_ppm_tiff(Stream ofp)
        {
            write_ppm_tiff_internal(ofp);
        }

        private void write_ppm_tiff_internal(Stream ofp)
        {
	        state.iheight = state.height;
	        state.iwidth  = state.width;
	        if ((state.flip & 4) != 0)
            {
                int temp = state.height;
                state.height = state.width;
                state.width = temp;
            }

	        if (state.output_tiff != 0)
            {
                TiffHeader header = new TiffHeader(state, true);
                byte[] headerData = header.Write();
                ofp.Write(headerData, 0, headerData.Length);

                if (state.oprof != null)
                {
                    uint length = Utils.htonl(state.oprof[0]) >> 2;
                    for(int i=0; i<length; i++)
                    {
                        TiffHeader.WriteInt(ofp, (int)state.oprof[i]);
                    }
                }
	        }
            else if (state.colors > 3)
            {
                string output = string.Format("P7\nWIDTH {0}\nHEIGHT {1}\nDEPTH {2}\nMAXVAL {3}\nTUPLTYPE {4}\nENDHDR\n",
		                state.width, state.height, state.colors, (1 << state.output_bps)-1, state.cdesc);
                byte[] outputBytes = Encoding.ASCII.GetBytes(output);
                ofp.Write(outputBytes, 0, outputBytes.Length);
            }
	        else
            {
		        string output = string.Format("P{0}\n{1} {2}\n{3}\n",
		                state.colors/2+5, state.width, state.height, (1 << state.output_bps)-1);
                byte[] outputBytes = Encoding.ASCII.GetBytes(output);
                ofp.Write(outputBytes, 0, outputBytes.Length);
            }

            byte[] lut = null;
	        if (state.output_bps == 8) lut = gamma_lut(state);

            byte[] ppm = new byte[state.width * (state.colors*state.output_bps/8)];

	        int soff  = flip_index (0, 0);
	        int cstep = flip_index (0, 1) - soff;
	        int rstep = flip_index (1, 0) - flip_index (0, state.width);
	        for (int row=0; row < state.height; row++)
            {                    
		        for (int col=0; col < state.width; col++)
                {
			        if (state.output_bps == 8)
                    {
				        for (int c = 0; c < state.colors; c++)
                        {
                            ppm[col*state.colors+c] = lut[state.image[soff * 4 + c]];
                        }
                    }
			        else
                    {
                        for (int c = 0; c < state.colors; c++)
                        {
                            ushort pixel = state.image[soff * 4 + c];
                            ppm[(col * state.colors + c) * 2] = (byte)(pixel >> 8);
                            ppm[(col * state.colors + c) * 2 + 1] = (byte)(pixel &0xff);
                        }
                    }
                    soff += cstep;
                }
		        if (state.output_bps == 16 && state.output_tiff ==0 && true/*htons(0x55aa) != 0x55aa*/)
                {
                    throw new NotImplementedException();
			        //swab ((char*)ppm2, (char*)ppm2, state.width*state.colors*2);
                }
                ofp.Write(ppm, 0, ppm.Length);
                soff += rstep;
	        }
        }

        private int flip_index(int row, int col)
        {
            if ((state.flip & 4) != 0)
            {
                int temp = row;
                row = col;
                col = temp;
            }
            if ((state.flip & 2) != 0) row = state.iheight - 1 - row;
            if ((state.flip & 1) != 0) col = state.iwidth - 1 - col;
            return row * state.iwidth + col;
        }

        public static byte[] gamma_lut(DcRawState state)
        {
	        int perc = (int)(state.width * state.height * 0.01);		/* 99th percentile white level */
	        if (state.fuji_width != 0) perc /= 2;
	        if (((state.highlight & ~2) != 0) || state.no_auto_bright != 0) perc = -1;
            float white = 0;
	        for (int c = 0; c < state.colors; c++)
            {
                int total = 0;
                int val;
		        for (val=0x2000; --val > 32; )
                {
			        if ((total += state.histogram[c, val]) > perc) break;
                }
		        if (white < val) white = val;
	        }
	        white *= 8 / state.bright;
            byte[] lut = new byte[0x10000];
	        for (int i=0; i < 0x10000; i++)
            {
		        float r = i / white;
		        int val = (int)(256 * ( state.use_gamma == 0 ? r :
        //#ifdef SRGB_GAMMA
		//	        r <= 0.00304 ? r*12.92 : pow(r,2.5/6)*1.055-0.055 );
        //#else
			        r <= 0.018 ? r*4.5 : Math.Pow(r,0.45f)*1.099-0.099 ));
        //#endif
		        if (val > 255) val = 255;
		        lut[i] = (byte)val;
	        }
            return lut;
        }

    }

    internal class TiffTag
    {
        public ushort tag, type;
        public int count;
        //union { char c[4]; short s[2]; int i; } val;
        public int val;

        public TiffTag(ushort tag, ushort type, int count, int val)
        {
            this.tag = tag;
	        this.type = type;
	        this.count = count;
            if (type < 3 && count <= 4)
            {
                // Wrong way round?
                this.val = val;
            }
            else if (type == 3 && count <= 2)
            {
                // Wrong way round?
                this.val = val;
            }
            else
            {
                this.val = val;
            }
        }
    }

    internal class TiffHeader
    {
        ushort order, magic;
        int offset_ntag;
        // ushort pad
        // ntag
        List<TiffTag> tags = new List<TiffTag>();
        int nextifd;
        // ushort pad
        // nexif
        List<TiffTag> exif = new List<TiffTag>();
        // ushort pad
        // ngps
        List<TiffTag> gpst = new List<TiffTag>();
        short[] bps = new short[4];
        int[] rat = new int[10];
        uint[] gps = new uint[26];
        string desc; // 512 bytes
        string make; // 64 bytes 
        string model; // 64 bytes
        string soft; // 32 bytes
        string date; // 20 bytes
        string artist; // 64 bytes;
 
        public TiffHeader(DcRawState state, bool full)
        {
            order = (ushort)(Utils.htonl(0x4d4d4949) >> 16);
            offset_ntag = 2 + 2 + 4 + 2;
            int offset_nextifd = offset_ntag + 2 + 2 + (2 + 2 + 4 + 4) * 23;
            int offset_nexif = offset_nextifd + 4;
            int offset_ngps = offset_nexif + 2 + 2 + (2 + 2 + 4 + 4) * 4;
            int offset_bps = offset_ngps + 2 + 2 + (2 + 2 + 4 + 4) * 10;
            offset_bps -= 2; // HACK - why do we need this?
            int offset_rat = offset_bps + 4*2;
            int offset_gps = offset_rat + 10*4;
            int offset_desc = offset_gps + 26*4;
            int offset_make = offset_desc + 512;
            int offset_model = offset_make + 64;
            int offset_soft = offset_model + 64;
            int offset_date = offset_soft + 32;
            int offset_artist = offset_date + 20;
            int TH_SIZE = offset_artist + 64;

            // Tags
            if (full)
            {
		        tags.Add(new TiffTag(254, 4, 1, 0));
		        tags.Add(new TiffTag(256, 4, 1, state.width));
		        tags.Add(new TiffTag(257, 4, 1, state.height));
		        tags.Add(new TiffTag(258, 3, state.colors, 
                    (state.colors > 2) ? offset_bps : state.output_bps));
		        for (int c = 0; c < 4; c++)
                {
                    bps[c] = (short)state.output_bps;
                }
		        tags.Add(new TiffTag(259, 3, 1, 1));
		        tags.Add(new TiffTag(262, 3, 1, 1 + (state.colors > 1 ? 1 : 0)));
            }

            uint psize = 0;

            
	        tags.Add(new TiffTag(270, 2, 512, offset_desc));
	        tags.Add(new TiffTag(271, 2, 64, offset_make));
	        tags.Add(new TiffTag(272, 2, 64, offset_model));
	        if (full)
            {
		        if (state.oprof != null)
                {
                    psize = Utils.htonl(state.oprof[0]);
                }
		        tags.Add(new TiffTag(273, 4, 1, (int)(TH_SIZE + psize)));
		        tags.Add(new TiffTag(277, 3, 1, state.colors));
		        tags.Add(new TiffTag(278, 4, 1, state.height));
		        tags.Add(new TiffTag(279, 4, 1, state.height*state.width*state.colors*state.output_bps/8));
	        }
            else
            {
		        tags.Add(new TiffTag(274, 3, 1, "12435867"[state.flip]-'0'));
            }
	        tags.Add(new TiffTag(282, 5, 1, offset_rat));
	        tags.Add(new TiffTag(283, 5, 1, offset_rat+(4*2)));
	        tags.Add(new TiffTag(284, 3, 1, 1));
	        tags.Add(new TiffTag(296, 3, 1, 2));
	        tags.Add(new TiffTag(305, 2, 32, offset_soft));
	        tags.Add(new TiffTag(306, 2, 20, offset_date));
	        tags.Add(new TiffTag(315, 2, 64, offset_artist));
	        tags.Add(new TiffTag(34665, 4, 1, offset_nexif));
	        if (psize > 0)
            {
                tags.Add(new TiffTag(34675, 7, (int)psize, (int)TH_SIZE));
            }
               
	        exif.Add(new TiffTag(33434, 5, 1, offset_rat + 4*4));
	        exif.Add(new TiffTag(33437, 5, 1, offset_rat + 6*4));
	        exif.Add(new TiffTag(34855, 3, 1, (int)state.iso_speed));
	        exif.Add(new TiffTag(37386, 5, 1, offset_rat + 8*4));
	        /*if (state.gpsdata[1])
            {
		        tiff_set (&th->ntag, 34853, 4, 1, TOFF(th->ngps));
		        tiff_set (&th->ngps,  0, 1,  4, 0x202);
		        tiff_set (&th->ngps,  1, 2,  2, state.gpsdata[29]);
		        tiff_set (&th->ngps,  2, 5,  3, TOFF(th->gps[0]));
		        tiff_set (&th->ngps,  3, 2,  2, state.gpsdata[30]);
		        tiff_set (&th->ngps,  4, 5,  3, TOFF(th->gps[6]));
		        tiff_set (&th->ngps,  5, 1,  1, state.gpsdata[31]);
		        tiff_set (&th->ngps,  6, 5,  1, TOFF(th->gps[18]));
		        tiff_set (&th->ngps,  7, 5,  3, TOFF(th->gps[12]));
		        tiff_set (&th->ngps, 18, 2, 12, TOFF(th->gps[20]));
		        tiff_set (&th->ngps, 29, 2, 12, TOFF(th->gps[23]));
		        //memcpy (th->gps, gpsdata, sizeof th->gps);
		        for (int i=0; i<26; i++) {
			        state.gpsdata[i] = th->gps[i];
		        }
	        }*/

            rat[0] = rat[2] = 300;
            rat[1] = rat[3] = 1;
            for (int c = 0; c < 6; c++)
            {
                rat[4 + c] = 1000000;
            }
            rat[4] = (int)(rat[4] * state.shutter);
            rat[6] = (int)(rat[6] * state.aperture);
            rat[8] = (int)(rat[8] * state.focal_len);

            desc = state.desc;
            make = state.make;
            model = state.model;

	        DateTime ts = state.timestamp.Value;
            date = string.Format("{0:d2}:{1:d2}:{2:d2} {3:d2}:{4:d2}:{5:d2}",
                ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, ts.Second);
            soft = "dcraw";
        }

        private static void WriteShort(Stream stream, short value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)(value >> 8));
        }

        public static void WriteInt(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 24));
        }

        private static void WriteString(Stream stream, string s, int length)
        {
            byte[] data = new byte[length];
            if (s == null) s = String.Empty;
            Encoding.ASCII.GetBytes(s, 0, Math.Min(s.Length, length), data, 0);
            stream.Write(data, 0, data.Length);
        }

        private static void WriteTag(Stream stream, TiffTag tag)
        {
            WriteShort(stream, (short)tag.tag);
            WriteShort(stream, (short)tag.type);
            WriteInt(stream, tag.count);
            WriteInt(stream, tag.val);
        }

        private static void WriteTags(Stream stream, List<TiffTag> tags, short number)
        {
            WriteShort(stream, 0); // padding
            WriteShort(stream, (short)tags.Count);
            foreach (TiffTag tag in tags)
            {
                WriteTag(stream, tag);
            }
            for(int i=0; i<number-tags.Count; i++)
            {
                WriteTag(stream, new TiffTag(0, 0, 0, 0));
            }
        }

        public byte[] Write()
        {
	        MemoryStream stream = new MemoryStream();

            WriteShort(stream, (short)order);
            WriteShort(stream, 42);
            WriteInt(stream, offset_ntag);
            
            WriteTags(stream, tags, 23);

            WriteInt(stream, nextifd);

            WriteTags(stream, exif, 4);
            WriteTags(stream, gpst, 10);

            foreach (short value in bps)
            {
                WriteShort(stream, value);
            }
            foreach (int value in rat)
            {
                WriteInt(stream, value);
            }
            foreach (uint value in gps)
            {
                WriteInt(stream, (int)value);
            }

            WriteString(stream, desc, 512);
            WriteString(stream, make, 64);
            WriteString(stream, model, 64);
            WriteString(stream, soft, 32);
            WriteString(stream, date, 20);
            WriteString(stream, artist, 64);

            return stream.ToArray();
        }

    }


}
