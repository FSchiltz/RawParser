using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RawNet;

namespace RawEditor.Effect
{
    class AdamsDemosaic
    {
        static protected double threshold = 2.0;

        public void Demosaic(RawImage image)
        {
            int redx, redy;
            switch (image.colorFilter.ToString())
            {
                case "RGGB":
                    redx = 0;
                    redy = 0;
                    break;
                case "GRBG":
                    redx = 1;
                    redy = 0;
                    break;
                case "GBRG":
                    redx = 0;
                    redy = 1;
                    break;
                case "BGGR":
                    redx = 1;
                    redy = 1;
                    break;
                default:
                    throw new FormatException("Pattern " + image.colorFilter.ToString() + " is not supported");
            }

            // Mask of color per pixel
            CFAColor[] mask = new CFAColor[image.raw.dim.Width * image.raw.dim.Height];
            Parallel.For(0, image.raw.dim.Width, x =>
            {
                for (int y = 0; y < image.raw.dim.Height; y++)
                {
                    mask[y * image.raw.dim.Width + x] = image.colorFilter.cfa[((y % 2) * 2) + (x % 2)];
                }
            });

            // Initializations
            int bluex = 1 - redx;
            int bluey = 1 - redy;
            DemosaickingAdams(redx, redy, image.raw, mask);
            // compute the bilinear on the differences of the red and blue with the already interpolated green
            DemosaickingBilinearRedBlue(redx, redy, image.raw, mask, image.raw.red, CFAColor.Red);
            DemosaickingBilinearRedBlue(bluex, bluey, image.raw, mask, image.raw.blue, CFAColor.Blue);

            /*
            h = 16.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, image.raw);
            //chromatic_median(iter, redx, redy, projflag, side, image.raw);

            h = 4.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, image.raw);
           // chromatic_median(iter, redx, redy, projflag, side, image.raw);

            h = 1.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, image.raw);
            //chromatic_median(iter, redx, redy, projflag, side, image.arw);*/
        }

        protected void DemosaickingAdams(int redx, int redy, ImageComponent image, CFAColor[] mask)
        {
            // Initializations
            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            Parallel.For(0, image.dim.Height, row =>
            {
                for (long col = 0; col < image.dim.Width; col++)
                {
                    if (!(col < 3 || row < 3 || col >= image.dim.Width - 3 || row >= image.dim.Height - 3))
                    {
                        //skip to the end of line to reduce calculation
                        col = image.dim.Width - 4;
                    }
                    else if ((mask[row * image.dim.Width + col] != CFAColor.Green))
                    {
                        long gn, gs, ge, gw;
                        if (row > 0) gn = row - 1; else gn = 1;
                        if (row < image.dim.Height - 1) gs = row + 1; else gs = image.dim.Height - 2;
                        if (col < image.dim.Width - 1) ge = col + 1; else ge = image.dim.Width - 2;
                        if (col > 0) gw = col - 1; else gw = 1;

                        image.green[row * image.dim.Width + col] = (ushort)((
                            image.green[gn * image.dim.Width + col] +
                            image.green[gs * image.dim.Width + col] +
                            image.green[row * image.dim.Width + gw] +
                            image.green[row * image.dim.Width + ge]) / 4.0);
                    }
                }
            });

            // Interpolate the green by Adams algorithm inside the image    
            // First interpolate green directionally
            Parallel.For(3, image.dim.Height - 3, row =>
            {
                for (int col = 3; col < image.dim.Width - 3; col++)
                {
                    if (mask[row * image.dim.Width + col] != CFAColor.Green)
                    {
                        long l = row * image.dim.Width + col;
                        long lp1 = (row + 1) * image.dim.Width + col;
                        long lp2 = (row + 2) * image.dim.Width + col;
                        long lm1 = (row - 1) * image.dim.Width + col;
                        long lm2 = (row - 2) * image.dim.Width + col;

                        // Compute vertical and horizontal gradients in the green channel
                        double adv = Math.Abs(image.green[lp1] - image.green[lm1]);
                        double adh = Math.Abs(image.green[l - 1] - image.green[l + 1]);
                        double dh0, dv0;

                        // If current pixel is blue, we compute the horizontal and vertical blue second derivatives
                        // else is red, we compute the horizontal and vertical red second derivatives
                        if (mask[l] == CFAColor.Blue)
                        {
                            dh0 = 2.0 * image.blue[l] - image.blue[l + 2] - image.blue[l - 2];
                            dv0 = 2.0 * image.blue[l] - image.blue[lp2] - image.blue[lm2];
                        }
                        else
                        {
                            dh0 = 2.0 * image.red[l] - image.red[l + 2] - image.red[l - 2];
                            dv0 = 2.0 * image.red[l] - image.red[lp2] - image.red[lm2];
                        }

                        // Add vertical and horizontal differences
                        adh = adh + Math.Abs(dh0);
                        adv = adv + Math.Abs(dv0);
                        double val = 0;
                        if (Math.Abs(adv - adh) < threshold)
                        {
                            // If vertical and horizontal differences are similar, compute an isotropic average
                            val = (image.green[lm1] + image.green[lp1] + image.green[l - 1] + image.green[l + 1]) / 4.0 + (dh0 + dv0) / 8.0;
                        }
                        else if (adh < adv)
                        {
                            // Else If horizontal differences are smaller, compute horizontal average
                            val = (image.green[l - 1] + image.green[l + 1]) / 2.0 + (dh0) / 4.0;
                        }
                        else if (adv < adh)
                        {
                            // Else If vertical differences are smaller, compute vertical average	
                            val = (image.green[lp1] + image.green[lm1]) / 2.0 + (dv0) / 4.0;
                        }

                        if (val < 0)
                        {
                            val = 0;
                        }
                        image.green[l] = (ushort)(val);
                    }
                }
            });
        }

        protected void DemosaickingBilinearRedBlue(int colorX, int colorY, ImageComponent image, CFAColor[] mask, ushort[] output, CFAColor COLORPOSITION)
        {
            var dim = image.dim;
            int[] red = new int[dim.Width * dim.Height];
            // Compute the differences  
            Parallel.For(0, dim.Width * dim.Height, i =>
            {
                red[i] = output[i] - image.green[i];
            });

            // Interpolate the red differences making the average of possible values depending on the CFA structure
            Parallel.For(0, dim.Width, x =>
            {
                for (int y = 0; y < dim.Height; y++)
                {
                    if (mask[y * dim.Width + x] != COLORPOSITION)
                    {
                        long gn, gs, ge, gw;
                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < dim.Height - 1) gs = y + 1; else gs = dim.Height - 2;
                        if (x < dim.Width - 1) ge = x + 1; else ge = dim.Width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        if (mask[y * dim.Width + x] == CFAColor.Green && y % 2 == colorY)
                            red[y * dim.Width + x] = (int)((red[y * dim.Width + ge] + red[y * dim.Width + gw]) / 2.0);
                        else if (mask[y * dim.Width + x] == CFAColor.Green && x % 2 == colorX)
                            red[y * dim.Width + x] = (int)((red[gn * dim.Width + x] + red[gs * dim.Width + x]) / 2.0);
                        else
                        {
                            red[y * dim.Width + x] = (int)((red[gn * dim.Width + ge] +
                                red[gn * dim.Width + gw] +
                                red[gs * dim.Width + ge] +
                                red[gs * dim.Width + gw]) / 4.0);
                        }
                    }
                }
            });

            // Make back the differences
            Parallel.For(0, dim.Width * dim.Height, i =>
            {
                var val = red[i] + image.green[i];
                if (val < 0) val = 0;
                output[i] = (ushort)(val);
            });
        }
    }
}
