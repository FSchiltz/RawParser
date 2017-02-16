namespace RawNet.Jpeg
{
    internal class SOFInfo
    {
        public uint width;   // Width
        public uint height;    // Height
        public uint numComponents;  // Components
        public int precision;  // Precision
        public JpegComponentInfo[] ComponentInfo { get; set; } = new JpegComponentInfo[4];
        public bool Initialized { get; set; }

        public override string ToString()
        {
            string t = "";
            foreach (JpegComponentInfo info in ComponentInfo)
            {
                t += " " + info.ToString();
            }
            return "Width: " + width + " Height: " + height + " comps: " + numComponents + " precision" + precision;
        }
    };
}

