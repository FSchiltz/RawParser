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

namespace dcraw.Filters
{
    public class Median : Filter
    {
        /* Optimal 9-element median search */
        private static readonly byte[] opt = new byte[] { 1,2, 4,5, 7,8, 0,1, 3,4, 6,7, 1,2, 4,5, 7,8, 
            0,3, 5,8, 4,7, 3,6, 1,4, 2,5, 4,7, 4,2, 6,4, 4,2 };

        public Median(DcRawState state) : base(state)
        {
        }

        public override void Process()
        {
            Process(new RawImage(state));
        }

        private void Process(RawImage ri)
        {
            //ushort (*pix)[4];
            int pass;
            int[] med = new int[9];

            ushort[] image = ri.image;
            int width = ri.width;
            int height = ri.height;

            // State inputs
            int med_passes = state.med_passes;

            int totalPix = width * height;
            int totalPix_minusOneRow = width * (height - 1);

            for (pass = 1; pass <= med_passes; pass++)
            {
                for (int c = 0; c < 3; c += 2)
                {
                    for (int ipix = 0; ipix < totalPix; ipix++)
                    {
                        image[ipix * 4 + 3] = image[ipix * 4 + c];
                        //pix[0][3] = pix[0][c];
                    }

                    for (int ipix = width; ipix < totalPix_minusOneRow; ipix++)
                    {
                        if ((ipix + 1) % width < 2) continue;
                        for (int k = 0, i = -width; i <= width; i += width)
                        {
                            for (int j = i - 1; j <= i + 1; j++)
                            {
                                med[k++] = image[ipix * 4 + 3] - image[ipix * 4 + 1];;
                            }
                        }

                        for (int i = 0; i < opt.Length; i += 2)
                        {
                            if (med[opt[i]] > med[opt[i + 1]])
                            {
                                int aa = med[opt[i]];
                                int bb = med[opt[i + 1]];
                                med[opt[i]] = bb;
                                med[opt[i + 1]] = aa;
                            }
                        }

                        image[ipix * 4 + c] = Utils.Clip16(med[4] + image[ipix * 4 + 1]);
                    }
                }
            }
        }
    }
}
