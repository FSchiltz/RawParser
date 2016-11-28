using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    abstract public class Decoder
    {
        public Decoder()
        {

        }

        public uint height;
        public uint width;
        public ushort colorDepth;
        public byte[] cfa;
        public double[] camMul = { 1, 1, 1, 1 };
        public double[] black = new double[4];
        public double[] curve;
        //this replace call back because something in .Net was causing more than 400 mb of ram
        abstract public void Parse(Stream s);
        abstract public byte[] parseThumbnail();
        abstract public byte[] parsePreview();
        abstract public Dictionary<ushort, Tag> parseExif();
        abstract public ushort[] parseRAWImage();
    }
}
