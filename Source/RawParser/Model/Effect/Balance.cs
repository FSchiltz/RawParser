using System;

namespace RawParser.Effect
{
    public class Balance
    {
        private Balance() { }

        public static void calculateRGB(int temp, out double rRefer, out double gRefer, out double bRefer)
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
            bRefer /= (255 );
            gRefer /= (255 );
            rRefer /= (255 );
        }

        /*
         * Start with a temperature, in Kelvin, somewhere between 1000 and 40000.  (Other values may work,
                 but I can't make any promises about the quality of the algorithm's estimates above 40000 K.)
            Note also that the temperature and color variables need to be declared as floating - point.
        */
        //TODO correct
        /*
        public static void WhiteBalance(ref ushort[] image, int colorDepth, uint h, uint w, int temp)
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

        /*
         * Does not clip,beware
         * 
         */
        public static void scaleColor(ref ushort[] data, uint height, uint width, int dark, int saturation, double[] mul, int colorDepth)
        {
            uint maxValue = (uint)(1 << colorDepth) - 1;
            for (int i = 0; i < height * width; i++)
            {
                double r = (data[i * 3] * mul[0]);
                double g = (data[(i * 3) + 1] * mul[1]);
                double b = (data[(i * 3) + 2] * mul[2]);
                if (r > maxValue) r = maxValue;
                if (g > maxValue) g = maxValue;
                if (b > maxValue) b = maxValue;
                data[i * 3] = (ushort)r;
                data[(i * 3) + 1] = (ushort)g;
                data[(i * 3) + 2] = (ushort)b;
            }
        }

        public static void scaleGamma(ref ushort [] image, uint height,uint width, int colorDepth, double gamma)
        {
            double maxValue = (int)Math.Pow(2, colorDepth) - 1;
            for (int i = 0; i < height * width; i++)
            {
                double r = maxValue * Math.Pow(image[i * 3] / maxValue, gamma);
                double g = maxValue * Math.Pow(image[(i * 3) + 1] / maxValue, gamma);
                double b = maxValue * Math.Pow(image[(i * 3) + 2] / maxValue, gamma);
                if (r > maxValue) r = maxValue;
                if (g > maxValue) g = maxValue;
                if (b > maxValue) b = maxValue;
                image[i * 3] = (ushort)r;
                image[(i * 3) + 1] = (ushort)g;
                image[(i * 3) + 2] = (ushort)b;
            }
        }
    }
}