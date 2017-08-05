using PhotoNet.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RawNet
{
    internal class TableLookUp
    {
        static int TABLE_SIZE = 65536 * 2;
        public int ntables;
        public ushort[] tables;
        public bool Dither { get; set; }

        public TableLookUp(ushort[] table, int nfilled, bool dither) : this(1, dither)
        {
            SetTable(0, table, nfilled);
        }

        // Creates n numer of tables.
        public TableLookUp(int _ntables, bool _dither)
        {
            ntables = _ntables;
            Dither = _dither;
            tables = null;
            if (ntables < 1)
            {
                throw new RawDecoderException("Cannot construct 0 tables");
            }
            tables = new ushort[ntables * TABLE_SIZE];
        }

        public void SetTable(int ntable, ushort[] table, int nfilled)
        {
            if (ntable > ntables)
            {
                throw new RawDecoderException("Table lookup with number greater than number of tables.");
            }
            if (!Dither)
            {
                for (int i = 0; i < 65536; i++)
                {
                    tables[i + (ntable * TABLE_SIZE)] = (i < nfilled) ? table[i] : table[nfilled - 1];
                }
                return;
            }
            for (int i = 0; i < nfilled; i++)
            {
                int center = table[i];
                int lower = i > 0 ? table[i - 1] : center;
                int upper = i < (nfilled - 1) ? table[i + 1] : center;
                int delta = upper - lower;
                tables[(i * 2) + (ntable * TABLE_SIZE)] = (ushort)(center - ((upper - lower + 2) / 4));
                tables[(i * 2) + 1 + (ntable * TABLE_SIZE)] = (ushort)delta;
            }

            for (int i = nfilled; i < 65536; i++)
            {
                tables[(i * 2) + (ntable * TABLE_SIZE)] = table[nfilled - 1];
                tables[(i * 2) + 1 + (ntable * TABLE_SIZE)] = 0;
            }
            tables[0] = tables[1];
            tables[TABLE_SIZE - 1] = tables[TABLE_SIZE - 2];
        }

        protected ushort[] GetTable(int n)
        {
            if (n > ntables)
            {
                throw new RawDecoderException("Table lookup with number greater than number of tables.");
            }
            return tables.Skip(n * TABLE_SIZE).ToArray();
        }
    };
}

