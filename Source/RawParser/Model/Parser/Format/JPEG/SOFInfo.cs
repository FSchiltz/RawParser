namespace RawNet.JPEG
{
    internal class SOFInfo
    {
        public uint width;   // Width
        public uint height;    // Height
        public uint numComponents;  // Components
        public int precision;  // Precision
        public JpegComponentInfo[] ComponentInfo { get; set; } = new JpegComponentInfo[4];
        public bool Initialized { get; set; }
    };
}

