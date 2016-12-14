using System;

namespace RawNet
{
    public class CameraMetadataException : Exception
    {
        public CameraMetadataException(string msg) : base(msg) { }
        public CameraMetadataException() { }
        public CameraMetadataException(string msg, Exception innerException):base(msg,innerException){ }
    }
}

