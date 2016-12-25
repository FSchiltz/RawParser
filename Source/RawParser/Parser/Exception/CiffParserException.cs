using System;

namespace RawNet
{
    class CiffParserException : Exception
    {
        protected CiffParserException(string _msg) : base(_msg) { }
    };
}
