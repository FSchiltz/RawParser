namespace RawNet
{
    public class BlackArea
    {
        public uint Offset { get; set; } // Offset in bayer pixels.
        public uint Size { get; set; }   // Size in bayer pixels.
        public bool IsVertical { get; set; }  // Otherwise horizontal

        public BlackArea(uint offset, uint size, bool isVertical)
        {
            Offset = offset;
            Size = size;
            IsVertical = isVertical;
        }
    }
}
