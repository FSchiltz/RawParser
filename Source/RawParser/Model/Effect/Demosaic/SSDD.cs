using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RawNet;

namespace RawEditor.Effect
{
    static class SSDD
    {
        static int MAX(int i, int j) { return ((i) < (j) ? (j) : (i)); }
        static int MIN(int i, int j) { return ((i) < (j) ? (i) : (j)); }

        static double fTiny = 0.00000001;

        static double LUTMAX = 30.0;
        static double LUTMAXM1 = 29.0;
        static double LUTPRECISION = 1000.0;
        static double threshold = 2.0;

        public static void Demosaic(RawImage image, bool simple)
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
            CFAColor[] mask = new CFAColor[image.raw.dim.width * image.raw.dim.height];
            Parallel.For(0, image.raw.dim.width, x =>
            {
                for (int y = 0; y < image.raw.dim.height; y++)
                {
                    mask[y * image.raw.dim.width + x] = image.colorFilter.cfa[((y % 2) * 2) + (x % 2)];
                }
            });

            demosaicking_adams(redx, redy, image.raw, simple, mask);
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

        #region nlMean

        /**
         * \brief  Iterate median filter on chromatic components of the image
         *
         *
         * @param[in]  ired, igreen, iblue  initial  image
         * @param[in]  iter  number of iteracions
         * @param[out] ored, ogreen, oblue  filtered output 
         * @param[in]  (redx, redy)  coordinates of the red pixel: (0,0), (0,1), (1,0), (1,1)
         * @param[in]  side  median in a (2*side+1) x (2*side+1) window
         * @param[in]  projflag if not zero, values of the original CFA are kept 
         * @param[in]  width, height size of the image
         *
         */
        static void chromatic_median(int iter, int redx, int redy, bool projflag, double side, ImageComponent image)
        {
            uint size = image.dim.height * image.dim.width;
            // Auxiliary variables for computing chromatic components
            float[] y = new float[size];
            float[] u = new float[size];
            float[] v = new float[size];
            float[] u0 = new float[size];
            float[] v0 = new float[size];

            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // For each iteration
            for (int i = 1; i <= iter; i++)
            {

                // Transform to YUV
                // wxRgb2Yuv(image, iblue, y, u, v);

                // Perform a Median on YUV component
                //wxMedian(u, u0, side, 1, image.dim.width, image.dim.height);
                //wxMedian(v, v0, side, 1, image.dim.width, image.dim.height);

                // Transform back to RGB
                //wxYuv2Rgb(image, y, u0, v0);*/
            }
        }

        /**
         * \brief Compute Exp(-x)
         *
         *
         * @param[in]  dif    value of x
         * @param[in]  lut    table of Exp(-x)
         *
         */
        static double sLUT(double dif, double[] lut)
        {
            if (dif >= (float)LUTMAXM1) return 0.0;
            int x = (int)Math.Floor((double)dif * (float)LUTPRECISION);
            double y1 = lut[x];
            double y2 = lut[x + 1];
            return y1 + (y2 - y1) * (dif * LUTPRECISION - x);
        }

        /**
         * \brief  NLmeans based demosaicking
         *
         * For each value to be filled, a weigthed average of original CFA values of the same channel is performed.
         * The weight depends on the difference of a 3x3 color patch 
         *
         * @param[in]  image.red, image.green, image.blue  initial demosaicked image
         * @param[out] image.red, image.green, image.blue  demosaicked output 
         * @param[in]  (redx, redy)  coordinates of the red pixel: (0,0), (0,1), (1,0), (1,1)
         * @param[in]  bloc  research block of size (2+bloc+1) x (2*bloc+1)
         * @param[in]  h kernel bandwidth 
         * @param[in]  width, height size of the image
         *
         */
        static void demosaicking_nlmeans(int bloc, double h, int redx, int redy, ImageComponent image, CFAColor[] mask)
        {
            // Initializations
            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // Tabulate the function Exp(-x) for x>0.
            int luttaille = (int)(LUTMAX * LUTPRECISION);
            double[] lut = new double[luttaille];

            for (int i = 0; i < luttaille; i++) lut[i] = Math.Exp(-(float)i / LUTPRECISION);

            // for each pixel
            for (int y = 2; y < image.dim.height - 2; y++)
                for (int x = 2; x < image.dim.width - 2; x++)
                {
                    // index of current pixel
                    long l = y * image.dim.width + x;


                    // Learning zone depending on the window size
                    int imin = MAX(x - bloc, 1);
                    int jmin = MAX(y - bloc, 1);

                    int imax = MIN(x + bloc, (int)image.dim.width - 2);
                    int jmax = MIN(y + bloc, (int)image.dim.height - 2);


                    // auxiliary variables for computing average
                    double red = 0.0;
                    double green = 0.0;
                    double blue = 0.0;

                    double rweight = 0.0;
                    double gweight = 0.0;
                    double bweight = 0.0;


                    // for each pixel in the neighborhood
                    for (int j = jmin; j <= jmax; j++)
                        for (int i = imin; i <= imax; i++)
                        {

                            // index of neighborhood pixel
                            long l0 = j * image.dim.width + i;

                            // We only interpolate channels differents of the current pixel channel
                            if (mask[l] != mask[l0])
                            {
                                // Distances computed on color
                                double some = 0.0;

                                unsafe
                                {
                                    fixed (ushort* redPtr = image.red)
                                    {
                                        some = l2_distance_r1(redPtr, x, y, i, j, image.dim.width);
                                    }
                                    fixed (ushort* greenPtr = image.red)
                                    {
                                        some += l2_distance_r1(greenPtr, x, y, i, j, image.dim.width);
                                    }
                                    fixed (ushort* bluePtr = image.red)
                                    {
                                        some += l2_distance_r1(bluePtr, x, y, i, j, image.dim.width);
                                    }
                                }
                                // Compute weight
                                some = some / (27.0 * h);
                                double weight = sLUT(some, lut);

                                // Add pixel to corresponding channel average
                                if (mask[l0] == CFAColor.Green)
                                {
                                    green += weight * image.green[l0];
                                    gweight += weight;
                                }
                                else if (mask[l0] == CFAColor.Red)
                                {
                                    red += weight * image.red[l0];
                                    rweight += weight;
                                }
                                else
                                {
                                    blue += weight * image.blue[l0];
                                    bweight += weight;
                                }

                            }

                        }

                    // Set value to current pixel
                    if (mask[l] != CFAColor.Green && gweight > fTiny) image.green[l] = (ushort)(green / gweight);
                    else image.green[l] = image.green[l];

                    if (mask[l] != CFAColor.Red && rweight > fTiny) image.red[l] = (ushort)(red / rweight);
                    else image.red[l] = image.red[l];

                    if (mask[l] != CFAColor.Blue && bweight > fTiny) image.blue[l] = (ushort)(blue / bweight);
                    else image.blue[l] = image.blue[l];


                }
        }

        //TODO change into real code
        /**
         * \brief  Compute Euclidean distance of a 3x3 patch centered at (i0,j0), (u1,j1) of the same image
         *
         *
         * @param[in]  u0  image
         * @param[in]  (i0,j0)  center of first window
         * @param[in]  (i1,j1)  center of second window
         * @param[in]  width    width of the image
         *
         */
        unsafe static double l2_distance_r1(ushort* u0, int i0, int j0, int i1, int j1, uint width)
        {

            double diff, dist = 0.0;

            ushort* ptr0, ptr1;

            ptr0 = u0 + (j0 - 1) * width + i0 - 1;
            ptr1 = u0 + (j1 - 1) * width + i1 - 1;

            /* first line */

            diff = *ptr0++ - *ptr1++;
            dist += diff * diff;
            diff = *ptr0++ - *ptr1++;
            dist += diff * diff;
            diff = *ptr0 - *ptr1;
            dist += diff * diff;

            /* second line */
            ptr0 += width - 2;
            ptr1 += width - 2;

            diff = *ptr0++ - *ptr1++;
            dist += diff * diff;
            diff = *ptr0++ - *ptr1++;
            dist += diff * diff;
            diff = *ptr0 - *ptr1;
            dist += diff * diff;

            /* third line */
            ptr0 += width - 2;
            ptr1 += width - 2;

            diff = *ptr0++ - *ptr1++;
            dist += diff * diff;
            diff = *ptr0++ - *ptr1++;
            dist += diff * diff;
            diff = *ptr0 - *ptr1;
            dist += diff * diff;

            return dist;
        }
        #endregion

        /**
         * \brief  Classical Adams-Hamilton demosaicking algorithm
         *
         *  The green channel is interpolated directionally depending on the green first and red and blue second directional derivatives.
         *  The red and blue differences with the green channel are interpolated bilinearly.
         *
         * @param[in]  image.red, image.green, image.blue  original cfa image
         * @param[out]  image.red,  image.green,  image.blue  demosaicked output 
         * @param[in]  (redx, redy)  coordinates of the red pixel: (0,0), (0,1), (1,0), (1,1)
         * @param[in]  threshold value to consider horizontal and vertical variations equivalent and average both estimates
         * @param[in]  image.dim.width, image.dim.height size of the image
         *
         */
        static unsafe void demosaicking_adams(int redx, int redy, ImageComponent image, bool simple, CFAColor[] mask)
        {
            // Initializations
            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            Parallel.For(0, image.dim.height, row =>
            {
                long realRow = row + image.offset.height;
                for (long col = 0; col < image.dim.width; col++)
                {
                    long realCol = col + image.offset.width;
                    if (!(col < 3 || row < 3 || col >= image.dim.width - 3 || row >= image.dim.height - 3))
                    {
                        //skip to the end of line to reduce calculation
                        col = image.dim.width - 4;
                    }
                    else if ((mask[row * image.dim.width + col] != (CFAColor)CFAColor.Green))
                    {
                        long gn, gs, ge, gw;

                        if (row > 0) gn = realRow - 1; else gn = 1;
                        if (row < image.dim.height - 1) gs = realRow + 1; else gs = image.uncroppedDim.height - 2;
                        if (col < image.dim.width - 1) ge = realCol + 1; else ge = image.uncroppedDim.width - 2;
                        if (col > 0) gw = realCol - 1; else gw = 1;

                        image.green[row * image.dim.width + col] = (ushort)((
                            image.rawView[gn * image.uncroppedDim.width + realCol] +
                            image.rawView[gs * image.uncroppedDim.width + realCol] +
                            image.rawView[realRow * image.uncroppedDim.width + gw] +
                            image.rawView[realRow * image.uncroppedDim.width + ge]) / 4.0);
                    }
                }
            });

            // Interpolate the green by Adams algorithm inside the image    
            // First interpolate green directionally
            Parallel.For(3, image.dim.height - 3, row =>
            {
                long realRow = row + image.offset.height;
                for (int col = 3; col < image.dim.width - 3; col++)
                {
                    long realCol = col + image.offset.width;

                    long l1 = row * image.dim.width + col;
                    if (mask[l1] != CFAColor.Green)
                    {
                        long l = realRow * image.uncroppedDim.width + realCol;
                        long lp1 = (realRow + 1) * image.uncroppedDim.width + realCol;
                        long lp2 = (realRow + 2) * image.uncroppedDim.width + realCol;
                        long lm1 = (realRow - 1) * image.uncroppedDim.width + realCol;
                        long lm2 = (realRow - 2) * image.uncroppedDim.width + realCol;

                        // Compute vertical and horizontal gradients in the green channel
                        double adv = Math.Abs(image.rawView[lp1] - image.rawView[lm1]);
                        double adh = Math.Abs(image.rawView[l - 1] - image.rawView[l + 1]);
                        double dh0, dv0;

                        // If current pixel is blue, we compute the horizontal and vertical blue second derivatives
                        // else is red, we compute the horizontal and vertical red second derivatives
                        // if (mask[l] == CFAColor.Blue)
                        // {
                        dh0 = 2.0 * image.rawView[l] - image.rawView[l + 2] - image.rawView[l - 2];
                        dv0 = 2.0 * image.rawView[l] - image.rawView[lp2] - image.rawView[lm2];
                        //}
                        /*
                        else
                        {
                            dh0 = 2.0 * image.red[l] - image.red[l + 2] - image.red[l - 2];
                            dv0 = 2.0 * image.red[l] - image.red[lp2] - image.red[lm2];
                        }*/

                        // Add vertical and horizontal differences
                        adh = adh + Math.Abs(dh0);
                        adv = adv + Math.Abs(dv0);

                        if (Math.Abs(adv - adh) < threshold)
                        {
                            // If vertical and horizontal differences are similar, compute an isotropic average
                            image.green[l1] = (ushort)((image.rawView[lm1] + image.rawView[lp1] + image.rawView[l - 1] +
                                image.rawView[l + 1]) / 4.0 + (dh0 + dv0) / 8.0);
                        }
                        else if (adh < adv)
                        {
                            // Else If horizontal differences are smaller, compute horizontal average
                            image.green[l1] = (ushort)((image.rawView[l - 1] + image.rawView[l + 1]) / 2.0 + (dh0) / 4.0);
                        }
                        else if (adv < adh)
                        {
                            // Else If vertical differences are smaller, compute vertical average	
                            image.green[l1] = (ushort)((image.rawView[lp1] + image.rawView[lm1]) / 2.0 + (dv0) / 4.0);
                        }
                    }
                }
            });

            if (!simple)
            {
                // compute the bilinear on the differences of the red and blue with the already interpolated green
                demosaicking_bilinear_red_blue(redx, redy, image, mask, image.red, CFAColor.Red);
                demosaicking_bilinear_red_blue(bluex, bluey, image, mask, image.blue, CFAColor.Blue);
            }
            else
            {
                demosaicking_bilinearSimple_red_blue(redx, redy, image, mask, image.red, CFAColor.Red);
                demosaicking_bilinearSimple_red_blue(bluex, bluey, image, mask, image.blue, CFAColor.Blue);
            }
        }

        /**
         * \brief  Classical bilinear interpolation of red and blue differences with the green channel
         *
         *
         * @param[in]   image.red,  image.green,  image.blue  original cfa image with green interpolated
         * @param[out]  image.red,  image.green,  image.blue  demosaicked output 
         * @param[in]  (redx, redy)  coordinates of the red pixel: (0,0), (0,1), (1,0), (1,1)
         * @param[in]  image.dim.width, image.dim.height size of the image
         *
         */
        static unsafe void demosaicking_bilinear_red_blue(int colorX, int colorY, ImageComponent image, CFAColor[] mask, ushort[] output, CFAColor COLORPOSITION)
        {
            long[] red = new long[image.dim.width * image.dim.height];
            // Compute the differences  
            Parallel.For(0, image.dim.width * image.dim.height, i =>
            {
                red[i] = output[i] - image.green[i];
            });

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
                       if (y < image.dim.height - 1) gs = y + 1; else gs = (int)image.dim.height - 2;
                       if (x < image.dim.width - 1) ge = x + 1; else ge = (int)image.dim.width - 2;
                       if (x > 0) gw = x - 1; else gw = 1;

                       if (mask[y * image.dim.width + x] == CFAColor.Green && y % 2 == colorY)
                           red[y * image.dim.width + x] = (int)((red[y * image.dim.width + ge] + red[y * image.dim.width + gw]) / 2.0);
                       else if (mask[y * image.dim.width + x] == CFAColor.Green && x % 2 == colorX)
                           red[y * image.dim.width + x] = (int)((red[gn * image.dim.width + x] + red[gs * image.dim.width + x]) / 2.0);
                       else
                       {
                           red[y * image.dim.width + x] = (int)((red[gn * image.dim.width + ge] +
                               red[gn * image.dim.width + gw] +
                               red[gs * image.dim.width + ge] +
                               red[gs * image.dim.width + gw]) / 4.0);
                       }
                   }
               }
           });
            // Make back the differences
            Parallel.For(0, image.dim.width * image.dim.height, i =>
            {
                output[i] = (ushort)(red[i] + image.green[i]);

            });
            red = null;
        }

        static unsafe void demosaicking_bilinearSimple_red_blue(int colorX, int colorY, ImageComponent image, CFAColor[] mask, ushort[] output, CFAColor COLORPOSITION)
        {
            // Interpolate the red differences making the average of possible values depending on the CFA structure
            Parallel.For(0, image.dim.height, row =>
            {
                long realRow = row + image.offset.height;
                for (int col = 0; col < image.dim.width; col++)
                {
                    long realCol = col + image.offset.width;
                    if (mask[row * image.dim.width + col] != COLORPOSITION)
                    {
                        long gn, gs, ge, gw;
                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (row > 0) gn = realRow - 1; else gn = 1;
                        gn *= image.uncroppedDim.width;
                        if (row < image.dim.height - 1) gs = realRow + 1; else gs = image.uncroppedDim.height - 2;
                        gs *= image.uncroppedDim.width;
                        if (col < image.dim.width - 1) ge = realCol + 1; else ge = image.uncroppedDim.width - 2;
                        if (col > 0) gw = realCol - 1; else gw = 1;

                        long pos = row * image.dim.width + col;

                        if (mask[pos] == CFAColor.Green && row % 2 == colorY)
                        {
                            output[pos] = (ushort)((image.rawView[realRow * image.uncroppedDim.width + ge] + image.rawView[realRow * image.uncroppedDim.width + gw]) / 2.0);
                        }
                        else if (mask[pos] == CFAColor.Green && col % 2 == colorX)
                        {
                            output[pos] = (ushort)((image.rawView[gn + realCol] + image.rawView[gs + realCol]) / 2.0);
                        }
                        else
                        {
                            output[pos] = (ushort)((image.rawView[gn + ge] + image.rawView[gn + gw] + image.rawView[gs + ge] + image.rawView[gs + gw]) / 4.0);
                        }
                    }
                }
            });
        }
    }
}
