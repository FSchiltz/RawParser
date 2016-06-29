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

namespace dcraw
{
    internal static class Utils
    {
        internal static ushort Clip16(int val)
        {
            return (ushort)Math.Max(Math.Min(val, 65535), 0);
        }

        internal static int Lim(int x, int min, int max)
        {
            return Math.Max(min, Math.Min(x, max));
        }

        internal static int ULim(int x, int y, int z)
        {
            return ((y) < (z) ? Lim(x, y, z) : Lim(x, z, y));
        }

        internal static float IntToFloat(int i)
        {
            throw new NotImplementedException();
            //union { int i; float f; } u;
            //u.i = i;
            //return u.f;
        }

        internal static int memmem(byte[] array, byte[] search)
        {
            for (int i = 0; i < array.Length - search.Length; i++)
            {
                bool found = true;
                for (int j = 0; i < search.Length; j++)
                {
                    if (array[i + j] != search[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                {
                    return i;
                }
            }

            return -1;
        }

        internal static int memcmp(byte[] array, int index, byte[] compare)
        {
            for (int i = 0; i < compare.Length; i++)
            {
                int cmp = array[index + i] - compare[i];
                if (cmp != 0) return cmp;
            }

            return 0;
        }

        // TODO: implement this right
        internal static bool strcmp(byte[] array, string search)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(search);
            if (array.Length != bytes.Length)
            {
                return true;
            }

            return memcmp(array, 0, bytes) == 0;
        }

        internal static bool memcmp(byte[] array, int index, string compare)
        {
            return memcmp(array, index, Encoding.ASCII.GetBytes(compare)) == 0;
        }

        /*
        private static bool memcmp(byte[] array, byte[] compare)
        {
            return memcmp(array, 0, compare) == 0;
        }*/

        internal static int memmem(byte[] array, string search)
        {
            return memmem(array, Encoding.ASCII.GetBytes(search));
        }

        internal static bool memcmp(byte[] array, string search)
        {
            return memcmp(array, 0, Encoding.ASCII.GetBytes(search)) == 0;
        }

        internal static uint htonl(uint input)
        {
            return ((input & 0x000000ff) << 24) |
                   ((input & 0x0000ff00) << 8) |
                   ((input & 0x00ff0000) >> 8) |
                   ((input & 0xff000000) >> 24);
        }

        public static ushort sget2(short order, byte[] s, int offset)
        {
            if (order == 0x4949)		/* "II" means little-endian */
                return (ushort)(s[offset + 0] | s[offset + 1] << 8);

            /* "MM" means big-endian */
            return (ushort)(s[offset + 0] << 8 | s[offset + 1]);
        }
    }
}
