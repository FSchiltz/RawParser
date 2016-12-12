using System;
namespace RawNet
{
    internal class PanasonicMakernote : Makernote
    {
        private byte[] data;

        public PanasonicMakernote(byte[] data)
        {
            throw new NotImplementedException();
            this.data = data;
        }
    }
}