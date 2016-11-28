using System;

namespace RawNet
{


    class CiffParserException : Exception
    {
        public CiffParserException(string _msg) : base(_msg) { }
    };
}
