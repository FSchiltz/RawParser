﻿using System;

namespace RawNet
{
    class PanaBitpump
    {
        TIFFBinaryReader input;
        byte[] buf = new byte[0x4000];
        int vbits;
        int load_flags;
        internal PanaBitpump(TIFFBinaryReader _input, uint load)
        {
            input = _input;
            vbits = 0;
            load_flags = (int)load;
        }

        public void SkipBytes(int bytes)
        {
            int blocks = (bytes / 0x4000) * 0x4000;
            input.ReadBytes(blocks);
            for (int i = blocks; i < bytes; i++)
                GetBits(8);
        }

        public UInt32 GetBits(int nbits)
        {
            int vbyte;

            if (vbits == 0)
            {
                /* On truncated readers this routine will just return just for the truncated
                * part of the reader. Since there is no chance of affecting output buffer
                * size we allow the decoder to decode this
                */
                if (input.GetRemainSize() < 0x4000 - load_flags)
                {
                    Common.Memcopy(buf, input.ReadBytes(input.GetRemainSize()), (uint)input.GetRemainSize(), load_flags, 0);
                    input.ReadBytes(input.GetRemainSize());
                }
                else
                {
                    Common.Memcopy(buf, input.ReadBytes(0x4000 - load_flags), (uint)(0x4000 - load_flags), load_flags, 0);
                    input.ReadBytes(0x4000 - load_flags);
                    if (input.GetRemainSize() < load_flags)
                    {
                        Common.Memcopy(buf, input.ReadBytes(input.GetRemainSize()), (uint)input.GetRemainSize());
                        input.ReadBytes(input.GetRemainSize());
                    }
                    else
                    {
                        Common.Memcopy(buf, input.ReadBytes(load_flags), (uint)load_flags);
                        input.ReadBytes(load_flags);
                    }
                }
            }
            vbits = (vbits - nbits) & 0x1ffff;
            vbyte = vbits >> 3 ^ 0x3ff0;
            return (uint)((buf[vbyte] | buf[vbyte + 1] << 8) >> (vbits & 7) & ~(-1 << nbits));
        }
    }
}
