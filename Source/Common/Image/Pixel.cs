using System;

namespace PhotoNet.Common
{
    public struct Pixel
    {
        public double R;
        public double G;
        public double B;

        Pixel(Pixel p)
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

    public struct PixelHSL
    {
        public double H;
        public double S;
        public double L;

        PixelHSL(Pixel p)
        {
            H = p.R;
            S = p.G;
            L = p.B;
        }

        public bool IsZero()
        {
            return H == 0 && S == 0 && L == 0;
        }
    }
}
