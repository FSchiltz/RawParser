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

namespace dcraw
{
    public sealed class DcRawState
    {
        public RawStream InStream
        {
            get { return ifp; }
        }

        public RawStream ifp;

        public string inFilename;

        public ushort[] image;
        public int height;
        public int width;
        public int iheight;
        public int iwidth;
        public int colors;

        // TODO: re-add meta_data if we need foveon support in due course
        //public sbyte* meta_data;

        //public fixed char cdesc [5];
        //public fixed char desc [512];
        //public fixed char make [64];
        //public fixed char model [64];
        //public fixed char model2 [64];
        //public fixed char artist[64];

        public float flash_used;
        public float canon_ev;
        public float iso_speed;
        public float shutter;
        public float aperture;
        public float focal_len;

        public DateTime? timestamp;

        public uint shot_order;
        public uint kodak_cbpp;
        public uint filters;
        public uint exif_cfa;
        public uint unique_id;

        public long strip_offset;
        public long data_offset;
        public long thumb_offset;
        public long meta_offset;
        public long profile_offset;
        public int thumb_length;
        public long meta_length;
        public long profile_length;
        public int thumb_misc;
        public uint[] oprof;
        public uint fuji_layout;
        public uint shot_select;
        public bool multi_out;

        public int tiff_nifds;
        public int tiff_samples;
        public int tiff_bps;
        public int tiff_compress;

        public uint black;
        public uint maximum;
        public bool mix_green;
        public bool raw_color;
        public uint use_gamma;
        public uint zero_is_bad;
        public int is_raw;
        public uint dng_version;
        public bool is_foveon;
        public uint data_error;
        public uint tile_width;
        public uint tile_length;
        public uint[] gpsdata = new uint[32];
        public int load_flags;
        public int raw_height;
        public int raw_width;

        public int top_margin;
        public int left_margin;
        public ushort shrink;
        
        public int fuji_width;
        public int thumb_width;
        public int thumb_height;
        public int flip, tiff_flip;
        public double pixel_aspect;
        public double[] aber = { 1, 1, 1, 1 };
        
        public float bright = 1;
        public float[] user_mul = { 0, 0, 0, 0 };
        public uint[] greybox = { 0, 0, uint.MaxValue, uint.MaxValue };
        public float threshold;
        public bool half_size;
        public bool four_color_rgb;
        public int document_mode;
        public int highlight;
        public bool verbose;
        public bool use_auto_wb;
        public bool use_camera_wb;
        public bool use_camera_matrix = true;
        public int output_color = 1;
        public int output_bps = 8;
        public int output_tiff;
        public int med_passes;
        public int no_auto_bright;
        public ushort[] curve = new ushort[0x4001];
        public ushort[] cr2_slice = new ushort[3];
        public ushort[] sraw_mul = new ushort[4];

        public float[] cam_mul = new float[4];
        public float[] pre_mul = new float[4];

        public int[,] histogram = new int[4,0x2000];

        public ushort[,] white = new ushort[8,8];
        public float[,] cmatrix = new float[3,4];
        public float[,] rgb_cam = new float[3, 4];

        public string cdesc;
        public string desc;//[512];
        public string make;//[64];
        public string model;//[64];
        public string model2;//[64];
        public string artist;//[64];

        public delegate void WriteDelegate(Stream stream);
        public delegate void LoadRawDelegate();

        public WriteDelegate   write_thumb;
        public WriteDelegate   write_fun;
        public RawLoader load_raw;
        public RawLoader thumb_load_raw;

        public readonly double[,] xyz_rgb = new double[3,3] {			/* XYZ from RGB */
	        { 0.412453, 0.357580, 0.180423 },
	        { 0.212671, 0.715160, 0.072169 },
	        { 0.019334, 0.119193, 0.950227 }
        };

        public readonly float[] d65_white = new float[3] { 0.950456f, 1, 1.088754f };

        public int FC(int row, int col)
        {
            return (int)(filters >> ((((row) << 1 & 14) + ((col) & 1)) << 1) & 3);
        }

        public void BAYER_set(int row, int col, ushort val)
        {
            image[(((row) >> shrink) * iwidth + ((col) >> shrink)) * 4 + FC(row, col)] = val;
        }

        public ushort BAYER_get(int row, int col)
        {
            return image[(((row) >> shrink) * iwidth + ((col) >> shrink)) * 4 + FC(row, col)];
        }

        public void BAYER_inc(int row, int col, int val)
        {
            int index = (((row) >> shrink) * iwidth + ((col) >> shrink)) * 4 + FC(row, col);
            image[index] = (ushort)(image[index] + val);
        }

    }

}

