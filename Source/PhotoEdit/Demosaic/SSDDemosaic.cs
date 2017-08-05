using PhotoNet.Common;
using System;

namespace PhotoNet
{
    class SSDDemosaic : AdamsDemosaic
    {
        protected static int Max(int i, int j) { return ((i) < (j) ? (j) : (i)); }
        protected static int Min(int i, int j) { return ((i) < (j) ? (i) : (j)); }

        static protected double fTiny = 0.00000001;
        static protected double LUTMAX = 30.0;
        static protected double LUTMAXM1 = 29.0;
        static protected double LUTPRECISION = 1000.0;

        public new void Demosaic(Image<ushort> image)
        {
            base.Demosaic(image);
            /*
            var h = 16.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, image.fullSize);
            chromatic_median(iter, redx, redy, projflag, side, image.fullSize);

            h = 4.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, image.fullSize);
            chromatic_median(iter, redx, redy, projflag, side, image.fullSize);

            h = 1.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, image.fullSize);
            chromatic_median(iter, redx, redy, projflag, side, image.fullSize);*/
        }

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
        static void ChromaticMedian(int iter, int redx, int redy, double side, ImageComponent<ushort> image)
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
                //wxMedian(u, u0, side, 1, image.dim.Width, image.dim.Height);
                //wxMedian(v, v0, side, 1, image.dim.Width, image.dim.Height);

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
        static double LUT(double dif, double[] lut)
        {
            if (dif >= (float)LUTMAXM1) return 0.0;
            int x = (int)Math.Floor(dif * (float)LUTPRECISION);
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
        void DemosaickingNlmeans(int bloc, double h, ImageComponent<ushort> image, CFAColor[] mask)
        {

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
                    int iMin = Max(x - bloc, 1);
                    int jMin = Max(y - bloc, 1);

                    int iMax = Min(x + bloc, (int)image.dim.width - 2);
                    int jMax = Min(y + bloc, (int)image.dim.height - 2);


                    // auxiliary variables for computing average
                    double red = 0.0;
                    double green = 0.0;
                    double blue = 0.0;

                    double rweight = 0.0;
                    double gweight = 0.0;
                    double bweight = 0.0;


                    // for each pixel in the neighborhood
                    for (int j = jMin; j <= jMax; j++)
                        for (int i = iMin; i <= iMax; i++)
                        {

                            // index of neighborhood pixel
                            long l0 = j * image.dim.width + i;

                            // We only interpolate channels differents of the current pixel channel
                            if (mask[l] != mask[l0])
                            {
                                // Distances computed on color
                                double some = 0.0;
                                /*
                                unsafe
                                {
                                    fixed (ushort* redPtr = image.red)
                                    {
                                        some = L2DistanceR1(redPtr, x, y, i, j, image.dim.width);
                                    }
                                    fixed (ushort* greenPtr = image.red)
                                    {
                                        some += L2DistanceR1(greenPtr, x, y, i, j, image.dim.width);
                                    }
                                    fixed (ushort* bluePtr = image.red)
                                    {
                                        some += L2DistanceR1(bluePtr, x, y, i, j, image.dim.width);
                                    }
                                }*/
                                // Compute weight
                                some = some / (27.0 * h);
                                double weight = LUT(some, lut);

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
        /*
       static double L2DistanceR1(ushort* u0, int i0, int j0, int i1, int j1, uint width)
       {
           double diff, dist = 0.0;
           ushort* ptr0, ptr1;

           ptr0 = u0 + (j0 - 1) * width + i0 - 1;
           ptr1 = u0 + (j1 - 1) * width + i1 - 1;

           /* first line 
           diff = *ptr0++ - *ptr1++;
           dist += diff * diff;
           diff = *ptr0++ - *ptr1++;
           dist += diff * diff;
           diff = *ptr0 - *ptr1;
           dist += diff * diff;

           /* second line 
           ptr0 += width - 2;
           ptr1 += width - 2;
           diff = *ptr0++ - *ptr1++;
           dist += diff * diff;
           diff = *ptr0++ - *ptr1++;
           dist += diff * diff;
           diff = *ptr0 - *ptr1;
           dist += diff * diff;

           /* third line 
           ptr0 += width - 2;
           ptr1 += width - 2;
           diff = *ptr0++ - *ptr1++;
           dist += diff * diff;
           diff = *ptr0++ - *ptr1++;
           dist += diff * diff;
           diff = *ptr0 - *ptr1;
           dist += diff * diff;

           return dist;
       }*/
    }
}
