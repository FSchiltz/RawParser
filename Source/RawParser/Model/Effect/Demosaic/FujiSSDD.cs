using RawNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawEditor.Model.Effect.Demosaic
{
    class FujiSSDD
    {
        static byte GREENPOSITION = 1;
        static byte REDPOSITION = 0;
        static byte BLUEPOSITION = 2;

        static int MAX(int i, int j) { return ((i) < (j) ? (j) : (i)); }
        static int MIN(int i, int j) { return ((i) < (j) ? (i) : (j)); }

        static double fTiny = 0.00000001;

        //static double COEFF_YR = 0.299;
        //static double COEFF_YG = 0.587;
        //static double COEFF_YB = 0.114;
        static double LUTMAX = 30.0;
        static double LUTMAXM1 = 29.0;
        static double LUTPRECISION = 1000.0;
        static double threshold = 2.0;


        public static unsafe void Demosaic(RawImage image)
        {
            // Mask of color per pixel
            byte[] mask = new byte[image.raw.dim.width * image.raw.dim.height];
            uint cfaWidth = image.colorFilter.Size.width;
            uint cfaHeight = image.colorFilter.Size.height;
            Parallel.For(0, image.raw.dim.width, x =>
            {
                for (int y = 0; y < image.raw.dim.height; y++)
                {
                    mask[y * image.raw.dim.width + x] = (byte)image.colorFilter.cfa[((y % cfaHeight) * cfaWidth) + (x % cfaWidth)];
                }
            });

            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            Parallel.For(0, image.raw.dim.width, x =>
            {
                for (int y = 0; y < image.raw.dim.height; y++)
                {
                    if ((mask[y * image.raw.dim.width + x] != GREENPOSITION) && (x < 3 || y < 3 || x >= image.raw.dim.width - 3 || y >= image.raw.dim.height - 3))
                    {
                        long gn, gs, ge, gw;

                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < image.raw.dim.height - 1) gs = y + 1; else gs = image.raw.dim.height - 2;
                        if (x < image.raw.dim.width - 1) ge = x + 1; else ge = image.raw.dim.width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        image.raw.green[y * image.raw.dim.width + x] = (ushort)((
                           image.raw.green[gn * image.raw.dim.width + x] +
                           image.raw.green[gs * image.raw.dim.width + x] +
                           image.raw.green[y * image.raw.dim.width + gw] +
                           image.raw.green[y * image.raw.dim.width + ge]) / 4.0);
                    }
                }
            });

            // Interpolate the green by Adams algorithm inside the image    
            // First interpolate green directionally
            Parallel.For(3, image.raw.dim.width - 3, x =>
            {
                for (int y = 3; y < image.raw.dim.height - 3; y++)
                {
                    if (mask[y * image.raw.dim.width + x] != GREENPOSITION)
                    {
                        long l = y * image.raw.dim.width + x;
                        long lp1 = (y + 1) * image.raw.dim.width + x;
                        long lp2 = (y + 2) * image.raw.dim.width + x;
                        long lm1 = (y - 1) * image.raw.dim.width + x;
                        long lm2 = (y - 2) * image.raw.dim.width + x;

                        // Compute vertical and horizontal gradients in the green channel
                        double adv = Math.Abs(image.raw.green[lp1] - image.raw.green[lm1]);
                        double adh = Math.Abs(image.raw.green[l - 1] - image.raw.green[l + 1]);
                        double dh0, dv0;

                        // If current pixel is blue, we compute the horizontal and vertical blue second derivatives
                        // else is red, we compute the horizontal and vertical red second derivatives
                        if (mask[l] == BLUEPOSITION)
                        {
                            dh0 = 2.0 * image.raw.blue[l] - image.raw.blue[l + 2] - image.raw.blue[l - 2];
                            dv0 = 2.0 * image.raw.blue[l] - image.raw.blue[lp2] - image.raw.blue[lm2];
                        }
                        else
                        {
                            dh0 = 2.0 * image.raw.red[l] - image.raw.red[l + 2] - image.raw.red[l - 2];
                            dv0 = 2.0 * image.raw.red[l] - image.raw.red[lp2] - image.raw.red[lm2];
                        }

                        // Add vertical and horizontal differences
                        adh = adh + Math.Abs(dh0);
                        adv = adv + Math.Abs(dv0);

                        // If vertical and horizontal differences are similar, compute an isotropic average
                        if (Math.Abs(adv - adh) < threshold)
                            image.raw.green[l] = (ushort)(
                                (image.raw.green[lm1] +
                                image.raw.green[lp1] +
                                image.raw.green[l - 1] +
                                image.raw.green[l + 1]) / 4.0 + (dh0 + dv0) / 8.0);

                        // Else If horizontal differences are smaller, compute horizontal average
                        else if (adh < adv)
                        {
                            image.raw.green[l] = (ushort)((image.raw.green[l - 1] + image.raw.green[l + 1]) / 2.0 + (dh0) / 4.0);
                        }

                        // Else If vertical differences are smaller, compute vertical average			
                        else if (adv < adh)
                        {
                            image.raw.green[l] = (ushort)((image.raw.green[lp1] + image.raw.green[lm1]) / 2.0 + (dv0) / 4.0);
                        }
                    }
                }
            });

            demosaicking_bilinearSimple_red_blue(image.raw, mask, image.raw.red, REDPOSITION);
            demosaicking_bilinearSimple_red_blue(image.raw, mask, image.raw.blue, BLUEPOSITION);

        }

        static unsafe void demosaicking_bilinearSimple_red_blue(ImageComponent image, byte[] mask, ushort[] input, int COLORPOSITION)
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
