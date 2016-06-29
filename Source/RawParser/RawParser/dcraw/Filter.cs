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

namespace dcraw
{
    public abstract class Filter
    {
        protected readonly DcRawState state;

        protected Filter(DcRawState state)
        {
            this.state = state;
        }

        public abstract void Process();

        protected static int FC(uint filters, int row, int col)
        {
            return (int)(filters >> ((((row) << 1 & 14) + ((col) & 1)) << 1) & 3);
        }

        protected class RawImage
        {
            public ushort[] image;
            public int height;
            public int width;
            public int topMargin;
            public int leftMargin;
            public int colours;

            public RawImage(DcRawState state)
            {
                image = state.image;
                width = state.width;
                height = state.height;
                colours = state.colors;
                topMargin = state.top_margin;
                leftMargin = state.left_margin;
            }
        }
    }
}
