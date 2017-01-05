using System;

namespace RawEditor.Effect
{
    public static class Balance
    {
        
        public static void SRGBToRGB(ref double value, double maxValue)
        {
            value /= maxValue;
            if (value < 0.04045)
            {
                value /= 12.92;
            }
            else
            {
                value = Math.Pow(((value + 0.055) / 1.055), 2.4);
            }
            value *= maxValue;
        }

        public static void CalculateRGB(int temp, out double rRefer, out double gRefer, out double bRefer)
        {
            ushort maxValue = 255;
            temp /= 100;
            if (temp >= 66)
            {
                //red
                rRefer = (ushort)(351.97690566805693
                    + (0.114206453784165 * (temp - 55))
                    + ((-40.25366309332127) * Math.Log(temp - 55)));
                if (rRefer < 0) rRefer = 0;
                else if (rRefer > maxValue) rRefer = maxValue;

                //green
                gRefer = (ushort)((325.4494125711974)
                    + (0.07943456536662342 * (temp - 50))
                    + ((-28.0852963507957) * Math.Log(temp - 50)));
                if (gRefer < 0) gRefer = 0;
                else if (gRefer > maxValue) gRefer = maxValue;

                //blue
                bRefer = maxValue;
            }
            else
            {
                //red                    
                rRefer = maxValue;

                //green
                gRefer = (ushort)(((-155.25485562709179))
                    + ((-0.44596950469579133) * (temp - 2))
                    + (104.49216199393888 * Math.Log(temp - 2)));
                if (gRefer < 0) gRefer = 0;
                else if (gRefer > maxValue) gRefer = maxValue;

                //blue
                if (temp <= 19) bRefer = 0;
                else
                {
                    bRefer = (ushort)(((-254.76935184120902))
                        + (0.8274096064007395 * (temp - 10))
                        + (115.67994401066147 * Math.Log(temp - 10)));
                    if (bRefer < 0) bRefer = 0;
                    else if (bRefer > maxValue) bRefer = maxValue;
                }
            }
            //TODO fix
            bRefer = 255 / bRefer;
            gRefer = 255 / gRefer;
            rRefer = 255 / rRefer;
        }

        /*
         * Start with a temperature, in Kelvin, somewhere between 1000 and 40000.  (Other values may work,
                 but I can't make any promises about the quality of the algorithm's estimates above 40000 K.)
            Note also that the temperature and color variables need to be declared as floating - point.
        */
        //TODO correct
        /*
        public static void WhiteBalance(ushort[] image, int colorDepth, uint h, uint w, int temp)
        {
            //TODO caclute the real value and remove transforming to 8 bit
            ushort rRefer = 0, gRefer = 0, bRefer = 0;
            ushort factor = (ushort)(colorDepth - 8);
            calculateRGB(temp, out rRefer, out gRefer, out bRefer);
            rRefer <<= factor;
            gRefer <<= factor;
            bRefer <<= factor;
            double aplhablend = 0.005;
            for (int i = 0; i < w * h; i++)
            {
                //red
                var r = image[i * 3];
                image[i * 3] = (ushort)((image[i * 3] * (1 - aplhablend)) + (rRefer * aplhablend));
                var r2 = image[i * 3];
                //green
                var g = image[(i * 3) + 1];
                image[(i * 3) + 1] = (ushort)((image[(i * 3) + 1] * (1 - aplhablend)) + (gRefer * aplhablend));
                var g2 = image[(i * 3) + 1];
                //blue                
                var b = image[(i * 3) + 1];
                image[(i * 3) + 2] = (ushort)((image[(i * 3) + 2] * (1 - aplhablend)) + (bRefer * aplhablend));
                var b2 = image[(i * 3) + 1];
            }
        }*/

        public static void ScaleGamma(ref double r, ref double g, ref double b, double gamma, uint maxValue)
        {
            r = maxValue * Math.Pow(r / maxValue, gamma);
            g = maxValue * Math.Pow(g / maxValue, gamma);
            b = maxValue * Math.Pow(b / maxValue, gamma);
        }

        public static double[] GammaCurve(double pwr, double ts, int mode, int imax)
        {
            int i;
            double[] g = new double[6], bnd = { 0, 0 };
            double r;
            double[] curve = new double[0x10000];

            g[0] = pwr;
            g[1] = ts;
            g[2] = g[3] = g[4] = 0;
            bnd[Convert.ToInt32(g[1] >= 1)] = 1;

            if (g[1] != 0 && (g[1] - 1) * (g[0] - 1) <= 0)
            {
                for (i = 0; i < 48; i++)
                {
                    g[2] = (bnd[0] + bnd[1]) / 2;
                    if (g[0] != 0) bnd[Convert.ToInt32((Math.Pow(g[2] / g[1], -g[0]) - 1) / g[0] - 1 / g[2] > -1)] = g[2];
                    else bnd[Convert.ToInt32(g[2] / Math.Exp(1 - 1 / g[2]) < g[1])] = g[2];
                }
                g[3] = g[2] / g[1];
                if (g[0] != 0) g[4] = g[2] * (1 / g[0] - 1);
            }
            if (g[0] != 0) g[5] = 1 / (g[1] * (g[3] * g[3]) / 2 - g[4] * (1 - g[3]) +
                    (1 - Math.Pow(g[3], 1 + g[0])) * (1 + g[4]) / (1 + g[0])) - 1;
            else g[5] = 1 / (g[1] * (g[3] * g[3]) / 2 + 1
             - g[2] - g[3] - g[2] * g[3] * (Math.Log(g[3]) - 1)) - 1;
            if (!Convert.ToBoolean(mode--))
            {
                //memcpy(gamm, g, sizeof gamm);
                return null;
            }
            for (i = 0; i < 0x10000; i++)
            {
                curve[i] = 0xffff;
                if ((r = (double)i / imax) < 1)
                    curve[i] = 0x10000 * (Convert.ToBoolean(mode)
                  ? (r < g[3] ? r * g[1] : (Convert.ToBoolean(g[0]) ? Math.Pow(r, g[0]) * (1 + g[4]) - g[4] : Math.Log(r) * g[2] + 1))
                  : (r < g[2] ? r / g[1] : (Convert.ToBoolean(g[0]) ? Math.Pow((r + g[4]) / (1 + g[4]), 1 / g[0]) : Math.Exp((r - 1) / g[2]))));
            }
            return curve;
        }
    }
}