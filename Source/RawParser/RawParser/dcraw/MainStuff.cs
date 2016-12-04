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
using System.IO;

namespace dcraw
{
    public class Settings
    {
        public int user_flip = -1;
        public bool timestamp_only = false;
        public bool identify_only = false;
        public bool thumbnail_only = false;
        public bool use_fuji_rotate = false;
        public int user_qual = -1;
        public int user_black = -1;
        public int user_sat = -1;

        public Settings(int user_flip, bool timestamp_only, bool identify_only, bool thumbnail_only, bool use_fuji_rotate, int user_qual, int user_black, int user_sat)
        {
            this.user_flip = user_flip;
            this.timestamp_only = timestamp_only;
            this.identify_only = identify_only;
            this.thumbnail_only = thumbnail_only;
            this.use_fuji_rotate = use_fuji_rotate;
            this.user_qual = user_qual;
            this.user_black = user_black;
            this.user_sat = user_sat;
        }
    }

    public class MainStuff
    {
        /*
        public static void DoStuff(string filename, DcRawState state, Settings settings)
        {
            try
            {
                DoStuff_internal(filename, state, settings);
            }
            finally
            {
               // Profiler.DumpStats();
            }

            //ShowPicture(state);
        }*/

        public static void DoStuff_internal(Stream stream, DcRawState state, Settings settings)
        {
            state.inFilename = "";

            state.ifp = new RawStream(stream);

            Identifier id = new Identifier(state);
            id.identify(state.ifp);

            // Works this far...

            switch ((state.flip + 3600) % 360)
            {
                case 270:
                    state.flip = 5;
                    break;
                case 180:
                    state.flip = 3;
                    break;
                case 90:
                    state.flip = 6;
                    break;
            }

            // TODO: need writer
            Tiff t = new Tiff(state);
            state.write_fun = t.write_ppm_tiff;

            if (state.load_raw is kodak_ycbcr_load_raw)
            {
                state.height += state.height & 1;
                state.width += state.width & 1;
            }

            
            if (state.is_raw == 0)
            {
                throw new FormatException("File not supported");
            }

            if (state.is_raw == 0) return;

            state.shrink = (state.filters != 0 &&
                            (state.half_size || state.threshold != 0 || state.aber[0] != 1 || state.aber[2] != 1))
                               ? (ushort) 1
                               : (ushort) 0;
            state.iheight = (state.height + state.shrink) >> state.shrink;
            state.iwidth = (state.width + state.shrink) >> state.shrink;
            

            if (state.use_camera_matrix && state.cmatrix[0, 0] > 0.25)
            {
                Array.Copy(state.cmatrix, state.rgb_cam, state.cmatrix.Length);
            }

            //memcpy (rgb_cam, cmatrix, sizeof cmatrix);
            state.raw_color = true;

            state.image = new ushort[state.iheight * state.iwidth * 4];

            //SetImage((ushort (*)[4]) state.Alloc (state.iheight * state.iwidth * sizeof *IMAGE));

            // TODO: implement metadata support if we need foveon support
            /*
			if (state.meta_length != 0) {
			    AllocMetadata(state);
				//state.meta_data = (signed char *) state.Alloc (state.meta_length);
				//merror (state.meta_data, "main()");
			}
             */
			if (state.shot_select >= state.is_raw)
			{
                throw new FormatException("File name incorrect");
            }

            state.ifp.Seek(state.data_offset, SeekOrigin.Begin);

            state.load_raw.LoadRaw();
            
			int quality = 2 + (state.fuji_width == 0 ? 1 : 0);
            /*
        thumbnail:

            string write_ext;
            /*if (write_fun == gcnew WriteDelegate(&CLASS jpeg_thumb)) {
				write_ext = ".jpg";
            } else if (state.output_tiff && write_fun == gcnew WriteDelegate(&CLASS write_ppm_tiff)) {*/
				//write_ext = ".tiff";
            /*} else {
				write_ext = (char*)".pgm\0.ppm\0.ppm\0.pam" + state.colors*5-5;
            }*/
            /*
            string ofname = state.inFilename;
            ofname = Path.ChangeExtension(ofname, write_ext);

            /*if (state.multi_out)
				sprintf (ofname+strlen(ofname), "_%0*d",
				snprintf(0,0,"%d",state.is_raw-1), state.shot_select);
			if (thumbnail_only) ofname += ".thumb";*/
            /*
		    Stream ofp = File.OpenWrite(ofname);
			if (state.verbose) Console.WriteLine("Writing data to {0} ...\n", ofname);
            using (Profiler.BlockProfile("Writer: " + state.write_fun.Method.ReflectedType + "." + state.write_fun.Method.Name))
            {
                state.write_fun(ofp);
            }

            state.ifp.Close();
			ofp.Close();
            */
        }

        /*
        private static IList<Filter> GetFilters(DcRawState state, Settings settings, int quality)
        {
            IList<Filter> filters = new List<Filter>();

            if (!state.is_foveon && state.document_mode < 2) {
                //scale_colors();
                Filter colourScaler = new ColourScaler(state);
                filters.Add(colourScaler);
                //colourScaler.Process();
            }

            // Select demosaicing filter
            Filter demosaic = GetDemosaic(state, quality);
            filters.Add(demosaic);
            //demosaic.Process();

            if (state.mix_green) {
                Filter greenMixer = new GreenMixer(state);
                filters.Add(greenMixer);
                //greenMixer.Process();
            }

            if (!state.is_foveon && state.colors == 3) {
                //median_filter();
                Filter medianFilter = new Median(state);
                filters.Add(medianFilter);
                //medianFilter.Process();
            }

            if (!state.is_foveon && state.highlight == 2) {
                throw new NotImplementedException();
                //blend_highlights();
            }

            if (!state.is_foveon && state.highlight > 2) {
                throw new NotImplementedException();
                //recover_highlights();
            }

            if (settings.use_fuji_rotate && state.fuji_width != 0)
            {
                throw new NotImplementedException();
                //fuji_rotate();
            }

//#ifndef NO_LCMS
            //if (cam_profile) apply_profile (cam_profile, out_profile);
            //#endif

            Filter colourSpace = new ColourSpace(state);
            filters.Add(colourSpace);
            //colourSpace.Process();
            //convert_to_rgb();

            if (settings.use_fuji_rotate && state.pixel_aspect != 1)
            {
                throw new NotImplementedException();
                //stretch();
            }
            return filters;
        }

        private class GreenMixer : Filter
        {
            public GreenMixer(DcRawState state) : base(state) {}

            public override void Process()
            {
                state.colors = 3;
                for (int i = 0; i < state.height * state.width; i++)
                {
                    state.image[i * 4 + 1] = (ushort)(((state.image[i * 4 + 1] + state.image[i * 4 + 3])) >> 1);
                    //IMAGE[i][1] = (IMAGE[i][1] + IMAGE[i][3]) >> 1;
                }
            }
        }

        /*
        private static Filter GetDemosaic(DcRawState state, int quality)
        {
            Filter demosaic;
            if (state.filters != 0 && state.document_mode == 0) {
                if (quality == 0)
                {
                    demosaic = new Bilinear(state);
                }
                else if (quality == 1 || state.colors > 3)
                {
                    Demosaic.PreInterpolate(state);
                    throw new NotImplementedException();
                    //vng_interpolate();
                }
                else if (quality == 2)
                {
                    Demosaic.PreInterpolate(state);
                    throw new NotImplementedException();
                    //ppg_interpolate();
                }
                else
                {
                    demosaic = new AHD(state);
                }
            } else {
                demosaic = new BasicDemosiac(state);
            }

            return demosaic;
        }*/
    }
}
