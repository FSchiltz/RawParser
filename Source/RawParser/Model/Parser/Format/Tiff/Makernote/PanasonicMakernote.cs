using System;

namespace RawNet.Format.Tiff
{
    internal class PanasonicMakernote : Makernote
    {
        public PanasonicMakernote(byte[] data, Endianness endian, int depth):base(endian, depth)
        {
            throw new NotImplementedException();            
        }
    }
}