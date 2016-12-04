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

//#define DEBUG_HUFFMANTREE


namespace dcraw
{
    public sealed class HuffmanTree
    {
        private int treeDepth;
        private readonly byte[] symbolLength;
        private readonly byte[] symbolValue;

        public HuffmanTree(byte[] source, ref int sourceIndex)
        {
            int leaf = 0;
            Decode root = makeWorker(source, ref sourceIndex, 0, ref leaf, 0);
            int tableSize = 1 << treeDepth;
            symbolLength = new byte[tableSize];
            symbolValue = new byte[tableSize];

            for (int i = 0; i < tableSize; i++)
            {
                byte bitsUsed = 0;
                Decode node = root;
                while (node.branch[0] != null)
                {
                    bitsUsed++;
                    uint bits = (uint)(i >> (treeDepth - bitsUsed)) & 1;
                    node = node.branch[bits];
                }

                symbolLength[i] = bitsUsed;
                symbolValue[i] = node.leaf;
            }
        }

        public int ReadNextSymbolLength(RawStream input)
        {
            uint bits = input.PeekBits(treeDepth);
            input.GetBits(symbolLength[bits]);
            return symbolValue[bits];
        }
        
        private Decode makeWorker(byte[] source, ref int sourceIndex, int level, ref int leaf, int currentDepth)
        {
            if (currentDepth > treeDepth)
            {
                treeDepth = currentDepth;
            }

            Decode cur = new Decode();
            int i, next;

            for (i = next = 0; i <= leaf && next < 16; )
            {
                i += source[sourceIndex + next++];
            }

            if (i > leaf)
            {
                if (level < next)
                {
                    int tempIndex = sourceIndex;
                    cur.branch[0] = makeWorker(source, ref tempIndex, level + 1, ref leaf, currentDepth + 1);
                    tempIndex = sourceIndex;
                    cur.branch[1] = makeWorker(source, ref tempIndex, level + 1, ref leaf, currentDepth + 1);
                }
                else
                {
                    cur.leaf = source[sourceIndex + 16 + leaf];
                    leaf++;
                }
            }

            sourceIndex = sourceIndex + 16 + leaf;
            return cur;
        }

        private sealed class Decode
        {
            public readonly Decode[] branch = new Decode[2];
            public byte leaf;
        }
    }
}