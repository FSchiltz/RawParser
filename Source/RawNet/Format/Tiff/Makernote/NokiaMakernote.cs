using PhotoNet.Common;

namespace RawNet.Format.Tiff
{
    internal class NokiaMakernote : Makernote
    {
        public NokiaMakernote(byte[] data, uint offset, Endianness endian, int depth, int parentOffset) : base(data, offset, endian, depth, parentOffset)
        {

        }
    }
}