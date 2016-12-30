namespace RawNet
{
    internal class NokiaMakernote : Makernote
    {
        public NokiaMakernote(byte[] data, uint offset, Endianness endian, int depth, int parentOffset) : base(data, offset, endian, depth, parentOffset)
        {

        }
    }
}