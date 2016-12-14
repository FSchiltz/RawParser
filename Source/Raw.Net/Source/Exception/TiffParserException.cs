using System;

namespace RawNet
{
    public class TiffParserException : Exception
    {
        public TiffParserException(string msg) : base(msg)
        {
            //_RPT1(0, "TIFF Exception: %s\n", _msg.c_str());
        }
    }
}
