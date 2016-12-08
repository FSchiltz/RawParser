using System;

namespace RawNet
{
    class TiffParserException : Exception
    {
        public TiffParserException(string _msg) : base(_msg)
        {
            //_RPT1(0, "TIFF Exception: %s\n", _msg.c_str());
        }

        protected static void ThrowTPE(string fmt)
        {
            /*va_list val;
            va_start(val, fmt);
            char buf[8192];
            vsprintf_s(buf, 8192, fmt, val);
            va_end(val);
            _RPT1(0, "EXCEPTION: %s\n", buf);*/
            throw new TiffParserException(fmt);
        }
    }
}
