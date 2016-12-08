using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    abstract internal class Decoder
    {
        protected Decoder()
        {

        }

        protected uint height;
        protected uint width;
        protected ushort colorDepth;
        protected byte[] cfa;
        protected double[] camMul = { 1, 1, 1, 1 };
        protected double[] black = new double[4];
        protected double[] curve;
        //this replace call back because something in .Net was causing more than 400 mb of ram
        abstract protected void Parse(Stream s);
        abstract protected byte[] parseThumbnail();
        abstract protected byte[] parsePreview();
        abstract protected Dictionary<ushort, Tag> parseExif();
        abstract protected ushort[] parseRAWImage();
    }
}
