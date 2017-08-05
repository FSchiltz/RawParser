using System;

namespace PhotoNet.Common
{
    public class RawDecoderException : Exception
    {
        public RawDecoderException(string message) : base(message) { }
        public RawDecoderException() { }
        public RawDecoderException(string message, Exception innerException) : base(message, innerException) { }
    }
}
