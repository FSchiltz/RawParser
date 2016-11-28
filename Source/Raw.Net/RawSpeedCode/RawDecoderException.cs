using System;

namespace RawSpeed
{

    public class RawDecoderException : Exception
    {
        public RawDecoderException(string msg) : base(msg)
        {
            // _RPT1(0, "RawDecompressor Exception: %s\n", _msg.c_str());
        }

        public static void ThrowRDE(string fmt)
        {
            /*
            va_list val;
            va_start(val, fmt);
            char buf[8192];
            vsprintf_s(buf, 8192, fmt, val);
            va_end(val);
            _RPT1(0, "EXCEPTION: %s\n", buf);*/
            throw new RawDecoderException(fmt);
        }
    }
}
