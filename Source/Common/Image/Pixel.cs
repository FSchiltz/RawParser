using System;

namespace PhotoNet.Common
{
    public struct Pixel
    {
        public double R;
        public double G;
        public double B;
        public double balance;

        public Pixel(Pixel p)
        {
            R = p.R;
            G = p.G;
            B = p.B;
            balance = 1;
        }

        public bool IsZero()
        {
            return R == 0 && G == 0 && B == 0;
        }
    }
}
