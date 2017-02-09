namespace RawNet
{
    public abstract partial class RawDecoder
    {
        internal class RawSlice
        {
            public uint h = 0;
            public uint offset = 0;
            public uint count = 0;
            public uint offsetY = 0;

            public RawSlice() { }
        };
    }
}
