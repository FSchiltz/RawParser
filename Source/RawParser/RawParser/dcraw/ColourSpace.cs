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
    public class ColourSpace : Filter
    {
        public ColourSpace(DcRawState state) : base(state)
        {
        }

        private interface ColourSpaceDefinition
        {
            double[,] Data { get; }
            string Name { get; }
        }

        private class StandardRGB : ColourSpaceDefinition
        {
            private static readonly double[,] rgb_rgb = new double[3,3]
	            { { 1,0,0 }, { 0,1,0 }, { 0,0,1 } };

            public double[,] Data
            {
                get
                {
                    return rgb_rgb;
                }
            }
            
            public string Name
            {
                get
                {
                    return "sRGB";
                }
            }
        }

        private class AdobeRGB : ColourSpaceDefinition
        {
            private static readonly double[,] adobe_rgb = new double[3,3]
	            { { 0.715146, 0.284856, 0.000000 },
	            { 0.000000, 1.000000, 0.000000 },
	            { 0.000000, 0.041166, 0.958839 } };

            public double[,] Data
            {
                get
                {
                    return adobe_rgb;
                }
            }
            
            public string Name
            {
                get
                {
                    return "Adobe RGB (1998)";
                }
            }
        }

        private class WideGamut : ColourSpaceDefinition
        {
            private static readonly double[,] wide_rgb = new double[3,3]
	            { { 0.593087, 0.404710, 0.002206 },
	            { 0.095413, 0.843149, 0.061439 },
	            { 0.011621, 0.069091, 0.919288 } };

            public double[,] Data
            {
                get
                {
                    return wide_rgb;
                }
            }
            
            public string Name
            {
                get
                {
                    return "WideGamut D65";
                }
            }
        }

        private class ProPhoto : ColourSpaceDefinition
        {
            private static readonly double[,] prophoto_rgb = new double[3,3]
	            { { 0.529317, 0.330092, 0.140588 },
	            { 0.098368, 0.873465, 0.028169 },
	            { 0.016879, 0.117663, 0.865457 } };

            public double[,] Data
            {
                get
                {
                    return prophoto_rgb;
                }
            }
            
            public string Name
            {
                get
                {
                    return "ProPhoto D65";
                }
            }
        }

        private class Xyz : ColourSpaceDefinition
        {
            private static readonly double[,] xyz_rgb = new double[3,3] {			// XYZ from RGB
	            { 0.412453, 0.357580, 0.180423 },
	            { 0.212671, 0.715160, 0.072169 },
	            { 0.019334, 0.119193, 0.950227 }
	            };

            public double[,] Data
            {
                get
                {
                    return xyz_rgb;
                }
            }
            
            public string Name
            {
                get
                {
                    return "XYZ";
                }
            }
        }

        private static readonly double[,] xyzd50_srgb = new double[3,3] 
	        { { 0.436083, 0.385083, 0.143055 },
	        { 0.222507, 0.716888, 0.060608 },
	        { 0.013930, 0.097097, 0.714022 } };
	    
        private static readonly ColourSpaceDefinition[] spaces = new ColourSpaceDefinition[] {
	        new StandardRGB(),
            new AdobeRGB(),
            new WideGamut(),
            new ProPhoto(),
            new Xyz()
        };
        private static readonly uint[] phead = new uint[]
	        { 1024, 0, 0x2100000, 0x6d6e7472, 0x52474220, 0x58595a20, 0, 0, 0,
	        0x61637370, 0, 0, 0x6e6f6e65, 0, 0, 0, 0, 0xf6d6, 0x10000, 0xd32d };
        private static readonly uint[] pbody = new uint[]
	        { 10, 0x63707274, 0, 36,	// cprt
	        0x64657363, 0, 40,	// desc
	        0x77747074, 0, 20,	// wtpt
	        0x626b7074, 0, 20,	// bkpt
	        0x72545243, 0, 14,	// rTRC
	        0x67545243, 0, 14,	// gTRC
	        0x62545243, 0, 14,	// bTRC
	        0x7258595a, 0, 20,	// rXYZ
	        0x6758595a, 0, 20,	// gXYZ
	        0x6258595a, 0, 20 };	// bXYZ
        private static readonly uint[] pwhite = new uint[] { 0xf351, 0x10000, 0x116cc };
        private static readonly uint[] pcurve = new uint[] { 0x63757276, 0, 1, 0x1000000 };
         
        public override void Process()
        {
            // State inputs
            int document_mode = state.document_mode;
            int colors = state.colors;
            int output_color = state.output_color;
            bool raw_color = state.raw_color;

	        float[,] out_cam = new float[3, 4];
            float[,] rgb_cam = state.rgb_cam;

            for(int i = 0; i < 3; i++) {
                for(int j = 0; j < 4; j++) {
                    out_cam[i,j] = rgb_cam[i,j];
                }
            }

            state.raw_color |= colors == 1 || document_mode != 0 ||
		        output_color < 1 || output_color > 5;

            if (!raw_color)
            {
		        state.oprof = new uint[phead[0] >> 2];
                for(int i=0; i<phead.Length; i++)
                {
                    state.oprof[i] = phead[i];
                }
		        if (output_color == 5) state.oprof[4] = state.oprof[5];
		        state.oprof[0] = 132 + 12*pbody[0];
		        for (int i=0; i < pbody[0]; i++)
                {
			        state.oprof[state.oprof[0]/4] = (uint)((i != 0) ? (i > 1 ? 0x58595a20 : 0x64657363) : 0x74657874);
			        pbody[i*3+2] = state.oprof[0];
			        state.oprof[0] += (uint)((pbody[i*3+3] + 3) & -4);
		        }
                for(int i=0; i<pbody.Length; i++)
                {
                    state.oprof[i+32] = pbody[i];
                }
		        state.oprof[pbody[5]/4+2] = (uint)spaces[output_color-1].Name.Length + 1;
                for(int i=0; i<pwhite.Length; i++)
                {
                    state.oprof[((pbody[8]+8) >> 2) + i] = pwhite[i];
                }
		        if (state.output_bps == 8)
                {
        //#ifdef SRGB_GAMMA
		//	        pcurve[3] = 0x2330000;
        //#else
			        pcurve[3] = 0x1f00000;
        //#endif
                }
		        for (int i=4; i < 7; i++)
                {
                    for (int j = 0; j < pcurve.Length; j++)
                    {
                        state.oprof[(pbody[i * 3 + 2] >> 2) + j] = pcurve[j];
                    }
                }
                double[,] inverse = PseudoInverse(spaces[output_color-1].Data, 3);
		        for (int i=0; i < 3; i++)
                {
			        for (int j=0; j < 3; j++)
                    {
                        double num = 0;
				        for (int k=0; k < 3; k++)
                        {
					        num += xyzd50_srgb[i, k] * inverse[j, k];
                        }
				        state.oprof[pbody[j*3+23]/4+i+2] = (uint)(num * 0x10000 + 0.5);
			        }
                }
		        for (int i=0; i < phead[0]/4; i++)
                {
			        state.oprof[i] = Utils.htonl(state.oprof[i]);
                }
		        WriteStringToIntArray(state.oprof, pbody[2]+8, "auto-generated by dcraw");
		        WriteStringToIntArray(state.oprof, pbody[5]+12, spaces[output_color-1].Name);
		        for (int i=0; i < 3; i++)
                {
			        for (int j=0; j < colors; j++)
                    {
                        out_cam[i, j] = 0;
				        for (int k=0; k < 3; k++)
                        {
					        out_cam[i, j] += (float)spaces[output_color-1].Data[i, k] * rgb_cam[k,j];
                        }
                    }
                }
	        }

            int[,] histogram = state.histogram;
            histogram.Initialize();
            int imgIndex = 0;
            ushort[] image = state.image;
            int height = state.height;
            int width = state.width;
            uint filters = state.filters;

            if (!raw_color)
            {
                int imgLimit = image.Length - 3;

                for (imgIndex = 0; imgIndex < imgLimit; imgIndex += 4)
                {
                    float output0 = 0, output1 = 0, output2 = 0;

                    for (int c = 0; c < colors; c++)
                    {
                        ushort pixel = image[imgIndex + c];
                        output0 += out_cam[0, c] * pixel;
                        output1 += out_cam[1, c] * pixel;
                        output2 += out_cam[2, c] * pixel;
                    }

                    ushort val = Utils.Clip16((int)output0);
                    image[imgIndex] = val;
                    histogram[0, val >> 3]++;

                    val = Utils.Clip16((int) output1);
                    image[imgIndex + 1] = val;
                    histogram[1, val >> 3]++;

                    val = Utils.Clip16((int) output2);
                    image[imgIndex + 2] = val;
                    histogram[2, val >> 3]++;

                    /*
                    for (int c = 0; c < colors; c++)
                    {
                        ushort pixel = image[imgIndex + c];
                        histogram[c, pixel >> 3]++;
                    }*/
                }
            }
            else
            {
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        if (document_mode != 0)
                        {
                            ushort pixel = image[imgIndex + FC(filters, row, col)];
                            image[imgIndex ] = pixel;
                        }

                        for (int c = 0; c < colors; c++)
                        {
                            ushort pixel = image[imgIndex + c];
                            histogram[c, pixel >> 3]++;
                        }
                        imgIndex+=4;
                    }
                }
            }

            if (colors == 4 && output_color != 0)
            {
                state.colors = 3;
            }

	        if (document_mode != 0 && filters != 0)
	        {
	            state.colors = 1;
	        }
        }

        private static double[,] PseudoInverse(double[,] input, int size)
        {
	        double[,] work = new double[3,6];
	        for (int i=0; i < 3; i++)
            {
		        for (int j=0; j < 6; j++)
                {
			        work[i, j] = j == i+3 ? 1 : 0;
                }
		        for (int j=0; j < 3; j++)
                {
			        for (int k=0; k < size; k++)
                    {
				        work[i, j] += input[k, i] * input[k, j];
                    }
                }
	        }
	        for (int i=0; i < 3; i++)
            {
		        double num = work[i, i];
		        for (int j=0; j < 6; j++)
                {
			        work[i, j] /= num;
                }
		        for (int k=0; k < 3; k++)
                {
			        if (k==i) continue;
			        num = work[k, i];
			        for (int j=0; j < 6; j++)
                    {
				        work[k, j] -= work[i, j] * num;
                    }
		        }
	        }
            double[,] output = new double[size, 3];
	        for (int i=0; i < size; i++)
            {
		        for (int j=0; j < 3; j++)
                {
                    output[i, j] = 0;
			        for (int k=0; k < 3; k++)
                    {
				        output[i, j] += work[j, k+3] * input[i, k];
                    }
                }
            }
            return output;
        }

        private static void WriteStringToIntArray(uint[] destination, uint offset, string s)
        {
            int length = s.Length + (4 - (s.Length % 4));
            byte[] data = new byte[length];
            System.Text.Encoding.ASCII.GetBytes(s, 0, s.Length, data, 0);
            for (int i = 0; i < length; i += 4)
            {
                destination[offset + i >> 2] = (uint)(
                    (data[i + 3] << 24) |
                    (data[i + 2] << 16) |
                    (data[i + 1] << 8)  |
                     data[i + 0]);
            }
        }
         
    }

}
