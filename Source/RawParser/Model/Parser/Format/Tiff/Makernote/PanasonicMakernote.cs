﻿using System;

namespace RawNet.Format.TIFF
{
    internal class PanasonicMakernote : Makernote
    {
        public PanasonicMakernote(byte[] data, Endianness endian, int depth):base(endian, depth)
        {
            throw new NotImplementedException();            
        }
    }
}