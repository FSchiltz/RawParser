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
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                DisplayUsage();
                return;
            }

            bool timestamp_only = false;
            bool thumbnail_only = false;
            bool identify_only = false;
            int user_qual = -1;
            int user_black = -1;
            int user_sat = -1;
            int user_flip = -1;
            bool use_fuji_rotate = true;
            bool write_to_stdout = false;
            string bpfile;
            string dark_frame;
            string cam_profile;
            string out_profile;
            //const char *write_ext;
            //	struct utimbuf ut;
            //	FILE *ofp;

            DcRawState state = new DcRawState();

            int i = 0;
            for (; args[i][0] == '-' || args[i][0] == '/' || args[i][0] == '+'; i++)
            {
                char opm = args[i][0];
                char opt = args[i][1];

                /*
                if ((cp = strchr(sp = "nbrkStqmHAC", opt)))
                {
                    for (i = 0; i < "11411111142"[cp - sp] - '0'; i++)
                    {
                        if (!isdigit(argv[arg + i][0]))
                        {
                            fprintf(stderr, _("Non-numeric argument to \"-%c\"\n"), opt);
                            return 1;
                        }
                    }
                }
                 */

                switch (opt)
                {
                    case 'n':
                        state.threshold = float.Parse(args[i++]);
                        break;
                    case 'b':
                        state.bright = float.Parse(args[i++]);
                        break;
                    case 'r':
                        for (int c = 0; c < 4; c++)
                        {
                            state.user_mul[c] = float.Parse(args[i++]);
                        }
                        break;
                    case 'C':
                        state.aber[0] = 1 / float.Parse(args[i++]);
                        state.aber[2] = 1 / float.Parse(args[i++]);
                        break;
                    case 'k':
                        user_black = int.Parse(args[i++]);
                        break;
                    case 'S':
                        user_sat = int.Parse(args[i++]);
                        break;
                    case 't':
                        user_flip = int.Parse(args[i++]);
                        break;
                    case 'q':
                        user_qual = int.Parse(args[i++]);
                        break;
                    case 'm':
                        state.med_passes = int.Parse(args[i++]);
                        break;
                    case 'H':
                        state.highlight = int.Parse(args[i++]);
                        break;
                    case 's':
                        state.shot_select = (uint)Math.Abs(int.Parse(args[i++]));
                        state.multi_out = string.Compare(args[i++], "all") == 0;
                        break;
                    case 'o':
                        if (char.IsDigit(args[i][0]) && args[i].Length == 1)
                        {
                            state.output_color = int.Parse(args[i++]);
                        }
                        else
                        {
                            out_profile = args[i++];
                        }
                        break;
                    case 'p':
                        cam_profile = args[i++];
                        break;
                    case 'P':
                        bpfile = args[i++];
                        break;
                    case 'K':
                        dark_frame = args[i++];
                        break;
                    case 'z':
                        timestamp_only = true;
                        break;
                    case 'e':
                        thumbnail_only = true;
                        break;
                    case 'i':
                        identify_only = true;
                        break;
                    case 'c':
                        write_to_stdout = true;
                        break;
                    case 'v':
                        state.verbose = true;
                        break;
                    case 'h':
                        state.half_size = true; /* "-h" implies "-f" */
                        state.four_color_rgb = true;
                        break;
                    case 'f':
                        state.four_color_rgb = true;
                        break;
                    case 'A':
                        for (int c = 0; c < 4; c++)
                        {
                            state.greybox[c] = uint.Parse(args[i++]);
                        }
                        state.use_auto_wb = true;
                        break;

                    case 'a':
                        state.use_auto_wb = true;
                        break;
                    case 'w':
                        state.use_camera_wb = true;
                        break;
                    case 'M':
                        state.use_camera_matrix = (opm == '+');
                        break;
                    case 'D':
                        state.document_mode = 0;
                        use_fuji_rotate = false;
                        break;
                    case 'd':
                        state.document_mode = 1;
                        use_fuji_rotate = false;
                        break;
                    case 'j':
                        use_fuji_rotate = false;
                        break;
                    case 'W':
                        state.no_auto_bright = 1;
                        break;
                    case 'T':
                        state.output_tiff = 1;
                        break;
                    case '4':
                        state.output_bps = 16;
                        break;
                    default:
                        Console.WriteLine("Unknown option {0}.\n", opt);
                        return;// 1;
                }
            }

            if (state.use_camera_matrix)
            {
                state.use_camera_matrix = state.use_camera_wb;
            }

            if (i >= args.Length)
            {
                Console.WriteLine("No files to process.");
                return;// 1;
            }

            Settings settings = new Settings(user_flip, timestamp_only, identify_only, thumbnail_only, use_fuji_rotate, user_qual,
                         user_black, user_sat);
            for(; i < args.Length; i++)
            {
                MainStuff.DoStuff(args[i], state, settings);
            }

            Console.WriteLine("Done");
        }

        static void DisplayUsage() {
	        Console.WriteLine("Raw photo decoder \"dcraw\"");
	        Console.WriteLine("original C implementation by Dave Coffin, dcoffin a cybercom o net");
	        Console.WriteLine("Usage:  dcraw [OPTION]... [FILE]...");
            Console.WriteLine();
	        Console.WriteLine("-v        Print verbose messages");
	        Console.WriteLine("-c        Write image data to standard output");
	        Console.WriteLine("-e        Extract embedded thumbnail image");
	        Console.WriteLine("-i        Identify files without decoding them");
	        Console.WriteLine("-i -v     Identify files and show metadata");
	        Console.WriteLine("-z        Change file dates to camera timestamp");
	        Console.WriteLine("-w        Use camera white balance, if possible");
	        Console.WriteLine("-a        Average the whole image for white balance");
	        Console.WriteLine("-A <x y w h> Average a grey box for white balance");
	        Console.WriteLine("-r <r g b g> Set custom white balance");
	        Console.WriteLine("+M/-M     Use/don't use an embedded color matrix");
	        Console.WriteLine("-C <r b>  Correct chromatic aberration");
	        Console.WriteLine("-P <file> Fix the dead pixels listed in this file");
	        Console.WriteLine("-K <file> Subtract dark frame (16-bit raw PGM)");
	        Console.WriteLine("-k <num>  Set the darkness level");
	        Console.WriteLine("-S <num>  Set the saturation level");
	        Console.WriteLine("-n <num>  Set threshold for wavelet denoising");
	        Console.WriteLine("-H [0-9]  Highlight mode (0=clip, 1=unclip, 2=blend, 3+=rebuild)");
	        Console.WriteLine("-t [0-7]  Flip image (0=none, 3=180, 5=90CCW, 6=90CW)");
	        Console.WriteLine("-o [0-5]  Output colorspace (raw,sRGB,Adobe,Wide,ProPhoto,XYZ)");
        /* TODO #ifndef NO_LCMS
	        Console.WriteLine("-o <file> Apply output ICC profile from file");
	        Console.WriteLine("-p <file> Apply camera ICC profile from file or \"embed\"");
        #endif*/
	        Console.WriteLine("-d        Document mode (no color, no interpolation)");
	        Console.WriteLine("-D        Document mode without scaling (totally raw)");
	        Console.WriteLine("-j        Don't stretch or rotate raw pixels");
	        Console.WriteLine("-W        Don't automatically brighten the image");
	        Console.WriteLine("-b <num>  Adjust brightness (default = 1.0)");
	        Console.WriteLine("-q [0-3]  Set the interpolation quality");
	        Console.WriteLine("-h        Half-size color image (twice as fast as \"-q 0\")");
	        Console.WriteLine("-f        Interpolate RGGB as four colors");
	        Console.WriteLine("-m <num>  Apply a 3x3 median filter to R-G and B-G");
	        Console.WriteLine("-s [0..N-1] Select one raw image or \"all\" from each file");
	        Console.WriteLine("-4        Write 16-bit linear instead of 8-bit with gamma");
	        Console.WriteLine("-T        Write TIFF instead of PPM");
        }
    }
}
