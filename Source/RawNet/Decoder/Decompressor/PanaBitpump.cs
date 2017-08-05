using PhotoNet.Common;
using System;

namespace RawNet.Decoder.Decompressor
{
    class PanaBitpump
    {
        static int BufSize = 0x4000;

        ImageBinaryReader input;

        byte[] buf = new byte[0x4001];
        int vbits = 0;
        int load_flags;

        internal PanaBitpump(ImageBinaryReader _input, uint load)
        {
            var temp = _input.ReadBytes((int)_input.RemainingSize);
            Array.Resize(ref temp, temp.Length + 32);
            input = new ImageBinaryReader(temp);
            vbits = 0;
            load_flags = (int)load;
        }

        public void SkipBytes(int bytes)
        {
            int blocks = (bytes / BufSize) * BufSize;
            input.ReadBytes(blocks);
            for (int i = blocks; i < bytes; i++)
                GetBits(8);
        }

        public int GetBits(int nbits)
        {

            if (vbits == 0)
            {
                /* On truncated files this routine will just return just for the truncated
                * part of the file. Since there is no chance of affecting output buffer
                * size we allow the decoder to decode this
                */
                int size = (int)Math.Min(input.RemainingSize, BufSize - load_flags);
                Common.Memcopy(buf, input.ReadBytes(size), (uint)size, load_flags, 0);

                size = (int)Math.Min(input.RemainingSize, load_flags);
                if (size != 0)
                    Common.Memcopy(buf, input.ReadBytes(size), (uint)size);
            }
            vbits = (vbits - nbits) & 0x1ffff;
            int b = vbits >> 3 ^ 0x3ff0;
            return (buf[b] | buf[b + 1] << 8) >> (vbits & 7) & ~(-(1 << nbits));

        }
    };
}
