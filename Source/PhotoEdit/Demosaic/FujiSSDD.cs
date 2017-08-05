using PhotoNet.Common;
using System;
using System.Threading.Tasks;

namespace PhotoNet
{
    class FujiSSDD
    {
        static byte GREENPOSITION = 1;
        static byte REDPOSITION = 0;
        static byte BLUEPOSITION = 2;

        //static int MAX(int i, int j) { return ((i) < (j) ? (j) : (i)); }
        //static int MIN(int i, int j) { return ((i) < (j) ? (i) : (j)); }

        //static double fTiny = 0.00000001;

        //static double COEFF_YR = 0.299;
        //static double COEFF_YG = 0.587;
        //static double COEFF_YB = 0.114;
        // static double LUTMAX = 30.0;
        //static double LUTMAXM1 = 29.0;
        //static double LUTPRECISION = 1000.0;
        static double threshold = 2.0;


        public static void Demosaic(Image<ushort> image)
        {
            // Mask of color per pixel
            byte[] mask = new byte[image.fullSize.dim.width * image.fullSize.dim.height];
            uint cfaWidth = image.colorFilter.Size.width;
            uint cfaHeight = image.colorFilter.Size.height;
            Parallel.For(0, image.fullSize.dim.width, x =>
            {
                for (int y = 0; y < image.fullSize.dim.height; y++)
                {
                    mask[y * image.fullSize.dim.width + x] = (byte)image.colorFilter.cfa[((y % cfaHeight) * cfaWidth) + (x % cfaWidth)];
                }
            });

            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            //TODO optimiseby removing unnessecary increment and check
            Parallel.For(0, image.fullSize.dim.width, x =>
            {
                for (int y = 0; y < image.fullSize.dim.height; y++)
                {
                    if ((mask[y * image.fullSize.dim.width + x] != GREENPOSITION) && (x < 3 || y < 3 || x >= image.fullSize.dim.width - 3 || y >= image.fullSize.dim.height - 3))
                    {
                        long gn, gs, ge, gw;

                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < image.fullSize.dim.height - 1) gs = y + 1; else gs = image.fullSize.dim.height - 2;
                        if (x < image.fullSize.dim.width - 1) ge = x + 1; else ge = image.fullSize.dim.width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        image.fullSize.green[y * image.fullSize.dim.width + x] = (ushort)((
                           image.fullSize.green[gn * image.fullSize.dim.width + x] +
                           image.fullSize.green[gs * image.fullSize.dim.width + x] +
                           image.fullSize.green[y * image.fullSize.dim.width + gw] +
                           image.fullSize.green[y * image.fullSize.dim.width + ge]) / 4.0);
                    }
                }
            });

            // Interpolate the green by Adams algorithm inside the image    
            // First interpolate green directionally
            Parallel.For(3, image.fullSize.dim.width - 3, x =>
            {
                for (int y = 3; y < image.fullSize.dim.height - 3; y++)
                {
                    if (mask[y * image.fullSize.dim.width + x] != GREENPOSITION)
                    {
                        long l = y * image.fullSize.dim.width + x;
                        long lp1 = (y + 1) * image.fullSize.dim.width + x;
                        long lp2 = (y + 2) * image.fullSize.dim.width + x;
                        long lm1 = (y - 1) * image.fullSize.dim.width + x;
                        long lm2 = (y - 2) * image.fullSize.dim.width + x;

                        // Compute vertical and horizontal gradients in the green channel
                        double adv = Math.Abs(image.fullSize.green[lp1] - image.fullSize.green[lm1]);
                        double adh = Math.Abs(image.fullSize.green[l - 1] - image.fullSize.green[l + 1]);
                        double dh0, dv0;

                        // If current pixel is blue, we compute the horizontal and vertical blue second derivatives
                        // else is red, we compute the horizontal and vertical red second derivatives
                        if (mask[l] == BLUEPOSITION)
                        {
                            dh0 = 2.0 * image.fullSize.blue[l] - image.fullSize.blue[l + 2] - image.fullSize.blue[l - 2];
                            dv0 = 2.0 * image.fullSize.blue[l] - image.fullSize.blue[lp2] - image.fullSize.blue[lm2];
                        }
                        else
                        {
                            dh0 = 2.0 * image.fullSize.red[l] - image.fullSize.red[l + 2] - image.fullSize.red[l - 2];
                            dv0 = 2.0 * image.fullSize.red[l] - image.fullSize.red[lp2] - image.fullSize.red[lm2];
                        }

                        // Add vertical and horizontal differences
                        adh = adh + Math.Abs(dh0);
                        adv = adv + Math.Abs(dv0);

                        // If vertical and horizontal differences are similar, compute an isotropic average
                        if (Math.Abs(adv - adh) < threshold)
                            image.fullSize.green[l] = (ushort)(
                                (image.fullSize.green[lm1] +
                                image.fullSize.green[lp1] +
                                image.fullSize.green[l - 1] +
                                image.fullSize.green[l + 1]) / 4.0 + (dh0 + dv0) / 8.0);

                        // Else If horizontal differences are smaller, compute horizontal average
                        else if (adh < adv)
                        {
                            image.fullSize.green[l] = (ushort)((image.fullSize.green[l - 1] + image.fullSize.green[l + 1]) / 2.0 + (dh0) / 4.0);
                        }

                        // Else If vertical differences are smaller, compute vertical average			
                        else if (adv < adh)
                        {
                            image.fullSize.green[l] = (ushort)((image.fullSize.green[lp1] + image.fullSize.green[lm1]) / 2.0 + (dv0) / 4.0);
                        }
                    }
                }
            });

            demosaicking_bilinearSimple_red_blue(image.fullSize, mask, image.fullSize.red, REDPOSITION);
            demosaicking_bilinearSimple_red_blue(image.fullSize, mask, image.fullSize.blue, BLUEPOSITION);

        }

        static void demosaicking_bilinearSimple_red_blue(ImageComponent<ushort> image, byte[] mask, ushort[] input, int COLORPOSITION)
        {
            // Interpolate the red differences making the average of possible values depending on the CFA structure
            Parallel.For(0, image.dim.width, x =>
            {
                for (int y = 0; y < image.dim.height; y++)
                {
                    if (mask[y * image.dim.width + x] != COLORPOSITION)
                    {
                        long gn, gs, ge, gw;
                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < image.dim.height - 1) gs = y + 1; else gs = image.dim.height - 2;
                        if (x < image.dim.width - 1) ge = x + 1; else ge = image.dim.width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        if (mask[y * image.dim.width + x] == GREENPOSITION && y % 2 == 0)
                            input[y * image.dim.width + x] = (ushort)((input[y * image.dim.width + ge] + input[y * image.dim.width + gw]) / 2.0);
                        else if (mask[y * image.dim.width + x] == GREENPOSITION && x % 2 == 0)
                            input[y * image.dim.width + x] = (ushort)((input[gn * image.dim.width + x] + input[gs * image.dim.width + x]) / 2.0);
                        else
                        {
                            input[y * image.dim.width + x] = (ushort)((input[gn * image.dim.width + ge] +
                                input[gn * image.dim.width + gw] +
                                input[gs * image.dim.width + ge] +
                                input[gs * image.dim.width + gw]) / 4.0);
                        }
                    }
                }
            });
        }
    }
}
