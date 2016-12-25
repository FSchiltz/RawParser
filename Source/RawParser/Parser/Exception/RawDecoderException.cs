using System;

namespace RawNet
{
    public class RawDecoderException : Exception
    {
        public RawDecoderException(string msg) : base(msg) { }
        public RawDecoderException() { }
        public RawDecoderException(string msg, Exception innerException) : base(msg, innerException) { }
    }
}
