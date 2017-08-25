using System;

namespace PhotoNet.Common
{
    public struct Pixel
    {
        public double R;
        public double G;
        public double B;

        public Pixel(Pixel p)
        {
            R = p.R;
            G = p.G;
            B = p.B;
        }

        public bool IsZero()
        {
            return R == 0 && G == 0 && B == 0;
        }
    }
}
