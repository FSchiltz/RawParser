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
        static byte GREENPOSITION = 0;
        static byte REDPOSITION = 1;
        static byte BLUEPOSITION = 2;

        public static void Demosaic(RawImage image)
        {
            int redx, redy;
            //(int redx, int redy, float* ired, float* igreen, float* iblue, float* ored, float* ogreen, float* oblue, int width, int height
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

            float h;
            int dbloc = 7;
            double side = 1.5;
            int iter = 1;
            int projflag = 1;
            double threshold = 2.0;
            /*
            demosaicking_adams(threshold, redx, redy, ired, igreen, iblue, ored, ogreen, oblue, width, height);

            h = 16.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, ored, ogreen, oblue, ired, igreen, iblue, width, height);
            chromatic_median(iter, redx, redy, projflag, side, ired, igreen, iblue, ored, ogreen, oblue, width, height);

            h = 4.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, ored, ogreen, oblue, ired, igreen, iblue, width, height);
            chromatic_median(iter, redx, redy, projflag, side, ired, igreen, iblue, ored, ogreen, oblue, width, height);

            h = 1.0;
            demosaicking_nlmeans(dbloc, h, redx, redy, ored, ogreen, oblue, ired, igreen, iblue, width, height);
            chromatic_median(iter, redx, redy, projflag, side, ired, igreen, iblue, ored, ogreen, oblue, width, height);*/
        }


        /**
         * \brief  Classical Adams-Hamilton demosaicking algorithm
         *
         *  The green channel is interpolated directionally depending on the green first and red and blue second directional derivatives.
         *  The red and blue differences with the green channel are interpolated bilinearly.
         *
         * @param[in]  ired, igreen, iblue  original cfa image
         * @param[out] ored, ogreen, oblue  demosaicked output 
         * @param[in]  (redx, redy)  coordinates of the red pixel: (0,0), (0,1), (1,0), (1,1)
         * @param[in]  threshold value to consider horizontal and vertical variations equivalent and average both estimates
         * @param[in]  width, height size of the image
         *
         */
        static unsafe void demosaicking_adams(float threshold, int redx, int redy, float* ired, float* igreen, float* iblue, float* ored, float* ogreen, float* oblue, int width, int height)
        {
            // Initializations
            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // CFA Mask of color per pixel
            byte[] mask = new byte[width * height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (x % 2 == redx && y % 2 == redy) mask[y * width + x] = REDPOSITION;
                    else if (x % 2 == bluex && y % 2 == bluey) mask[y * width + x] = BLUEPOSITION;
                    else mask[y * width + x] = GREENPOSITION;
                }
            }

            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if ((mask[y * width + x] != GREENPOSITION) && (x < 3 || y < 3 || x >= width - 3 || y >= height - 3))
                    {
                        int gn, gs, ge, gw;

                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < height - 1) gs = y + 1; else gs = height - 2;
                        if (x < width - 1) ge = x + 1; else ge = width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        ogreen[y * width + x] = (ogreen[gn * width + x] + ogreen[gs * width + x] + ogreen[y * width + gw] + ogreen[y * width + ge]) / 4.0f;
                    }
                }
            }

            // Interpolate the green by Adams algorithm inside the image    
            // First interpolate green directionally
            for (int x = 3; x < width - 3; x++)
            {
                for (int y = 3; y < height - 3; y++)
                {
                    if (mask[y * width + x] != GREENPOSITION)
                    {
                        int l = y * width + x;
                        int lp1 = (y + 1) * width + x;
                        int lp2 = (y + 2) * width + x;
                        int lm1 = (y - 1) * width + x;
                        int lm2 = (y - 2) * width + x;

                        // Compute vertical and horizontal gradients in the green channel
                       // float adv = fabsf(ogreen[lp1] - ogreen[lm1]);
                        //float adh = fabsf(ogreen[l - 1] - ogreen[l + 1]);
                        float dh0, dv0;

                        // If current pixel is blue, we compute the horizontal and vertical blue second derivatives
                        // else is red, we compute the horizontal and vertical red second derivatives
                        if (mask[l] == BLUEPOSITION)
                        {
                            dh0 = 2.0f * oblue[l] - oblue[l + 2] - oblue[l - 2];
                            dv0 = 2.0f * oblue[l] - oblue[lp2] - oblue[lm2];
                        }
                        else
                        {
                            dh0 = 2.0f * ored[l] - ored[l + 2] - ored[l - 2];
                            dv0 = 2.0f * ored[l] - ored[lp2] - ored[lm2];
                        }

                        // Add vertical and horizontal differences
                       // adh = adh + fabsf(dh0);
                       // adv = adv + fabsf(dv0);

                        // If vertical and horizontal differences are similar, compute an isotropic average
                      //  if (fabsf(adv - adh) < threshold)
                            ogreen[l] = (ogreen[lm1] + ogreen[lp1] + ogreen[l - 1] + ogreen[l + 1]) / 4.0f + (dh0 + dv0) / 8.0f;

                        // Else If horizontal differences are smaller, compute horizontal average
                      //  else if (adh < adv)
                            ogreen[l] = (ogreen[l - 1] + ogreen[l + 1]) / 2.0f + (dh0) / 4.0f;

                        // Else If vertical differences are smaller, compute vertical average			
                       // else if (adv < adh)
                            ogreen[l] = (ogreen[lp1] + ogreen[lm1]) / 2.0f + (dv0) / 4.0f;
                    }
                }
            }
            // compute the bilinear on the differences of the red and blue with the already interpolated green
            demosaicking_bilinear_red_blue(redx, redy, ored, ogreen, oblue, width, height);            
        }

        /**
         * \brief  Classical bilinear interpolation of red and blue differences with the green channel
         *
         *
         * @param[in]  ored, ogreen, oblue  original cfa image with green interpolated
         * @param[out] ored, ogreen, oblue  demosaicked output 
         * @param[in]  (redx, redy)  coordinates of the red pixel: (0,0), (0,1), (1,0), (1,1)
         * @param[in]  width, height size of the image
         *
         */
        static unsafe void demosaicking_bilinear_red_blue(int redx, int redy, float* ored, float* ogreen, float* oblue, int width, int height)
        {
            //Initializations 
            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // Mask of color per pixel
            byte[] mask = new byte[width * height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (x % 2 == redx && y % 2 == redy) mask[y * width + x] = REDPOSITION;
                    else if (x % 2 == bluex && y % 2 == bluey) mask[y * width + x] = BLUEPOSITION;
                    else mask[y * width + x] = GREENPOSITION;
                }
            }

            // Compute the differences  
            for (int i = 0; i < width * height; i++)
            {
                ored[i] -= ogreen[i];
                oblue[i] -= ogreen[i];
            }

            // Interpolate the blue differences making the average of possible values depending on the CFA structure 
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (mask[y * width + x] != BLUEPOSITION)
                    {

                        int gn, gs, ge, gw;

                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < height - 1) gs = y + 1; else gs = height - 2;
                        if (x < width - 1) ge = x + 1; else ge = width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        if (mask[y * width + x] == GREENPOSITION && y % 2 == bluey)
                            oblue[y * width + x] = (oblue[y * width + ge] + oblue[y * width + gw]) / 2.0f;
                        else if (mask[y * width + x] == GREENPOSITION && x % 2 == bluex)
                            oblue[y * width + x] = (oblue[gn * width + x] + oblue[gs * width + x]) / 2.0f;
                        else
                        {
                            oblue[y * width + x] = (oblue[gn * width + ge] + oblue[gn * width + gw] + oblue[gs * width + ge] + oblue[gs * width + gw]) / 4.0f;
                        }

                    }
                }
            }

            // Interpolate the blue differences making the average of possible values depending on the CFA structure
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (mask[y * width + x] != REDPOSITION)
                    {

                        int gn, gs, ge, gw;

                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < height - 1) gs = y + 1; else gs = height - 2;
                        if (x < width - 1) ge = x + 1; else ge = width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        if (mask[y * width + x] == GREENPOSITION && y % 2 == redy)
                            ored[y * width + x] = (ored[y * width + ge] + ored[y * width + gw]) / 2.0f;
                        else if (mask[y * width + x] == GREENPOSITION && x % 2 == redx)
                            ored[y * width + x] = (ored[gn * width + x] + ored[gs * width + x]) / 2.0f;
                        else
                        {
                            ored[y * width + x] = (ored[gn * width + ge] + ored[gn * width + gw] + ored[gs * width + ge] + ored[gs * width + gw]) / 4.0f;
                        }

                    }
                }
            }

            // Make back the differences
            for (int i = 0; i < width * height; i++)
            {
                ored[i] += ogreen[i];
                oblue[i] += ogreen[i];
            }
        }
    }
}
