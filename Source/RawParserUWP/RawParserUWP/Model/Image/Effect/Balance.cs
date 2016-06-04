using System;
using RawParserUWP.Model.Format.Image;

namespace RawParserUWP
{
    public class Balance
    {
        /*
         * Start with a temperature, in Kelvin, somewhere between 1000 and 40000.  (Other values may work,
                 but I can't make any promises about the quality of the algorithm's estimates above 40000 K.)
    Note also that the temperature and color variables need to be declared as floating - point.
    */
        public static void whiteBalance(ref RawImage image, int temp, int tint)
        {
            //TODO caclute the real value andremove transofrmingto8 bit
            ushort maxValue = 255;
            temp /= 100;
            ushort factor = (ushort)(image.colorDepth - 8);
            ushort refer;
            float tempStrength = 0.5f;
            if (temp >= 66)
            {
                for (int i = 0; i < image.width * image.height; i++)
                {
                    //red
                    refer = (ushort)(351.97690566805693
                        + (0.114206453784165 * (temp - 55))
                        + ((-40.25366309332127) * Math.Log(temp - 55)));
                    if (refer < 0) refer = 0;
                    else if (refer > maxValue) refer = maxValue;
                    //apply
                    image.imageData[i * 3] = (ushort)(image.imageData[i * 3] * (1 - tempStrength) + (refer << factor) * tempStrength);

                    //green
                    refer = (ushort)((325.4494125711974)
                        + (0.07943456536662342 * (temp - 50))
                        + ((-28.0852963507957) * Math.Log(temp - 50)));
                    if (refer < 0) refer = 0;
                    else if (refer > maxValue) refer = maxValue;
                    image.imageData[(i * 3) + 1] = (ushort)(image.imageData[(i * 3) + 1] * (1 - tempStrength) + (refer << factor) * tempStrength);

                    //blue
                    image.imageData[(i * 3) + 2] = (ushort)(image.imageData[(i * 3) + 2] * (1 - tempStrength) + (maxValue << factor) * tempStrength);
                }
            }
            else
            {
                for (int i = 0; i < image.width * image.height; i++)
                {
                    //red                    
                    image.imageData[i * 3] = (ushort)(image.imageData[i * 3] * (1 - tempStrength) + (maxValue << factor) * tempStrength);

                    //green
                    refer = (ushort)(((-155.25485562709179))
                        + ((-0.44596950469579133) * (temp - 2))
                        + (104.49216199393888 * Math.Log(temp - 2)));
                    if (refer < 0) refer = 0;
                    else if (refer > maxValue) refer = maxValue;
                    image.imageData[(i * 3) + 1] = (ushort)(image.imageData[(i * 3) + 1] * (1 - tempStrength) + (refer << factor) * tempStrength);

                    //blue
                    if (temp <= 19) refer = 0;
                    else
                    {
                        refer = (ushort)(((-254.76935184120902))
                            + (0.8274096064007395 * (temp - 10))
                            + (115.67994401066147 * Math.Log(temp - 10)));
                        if (refer < 0) refer = 0;
                        else if (refer > maxValue) refer = maxValue;
                    }
                    image.imageData[(i * 3) + 2] = (ushort)(image.imageData[(i * 3) + 2] * (1 - tempStrength) + (refer << factor) * tempStrength);
                }
            }
        }
    }
}