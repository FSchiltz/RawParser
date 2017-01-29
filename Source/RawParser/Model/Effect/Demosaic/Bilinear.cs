using RawNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawEditor.Effect
{
    static class Bilinear
    {
        /** 
 * @brief Bilinear demosaicing
 * @param Output pointer to memory to store the demosaiced image
 * @param Input the input image as a flattened 2D array
 * @param image.raw.dim.width, image.raw.dim.height the image dimensions
 * @param redx, redy the coordinates of the upper-rightmost red pixel
 *
 * Bilinear demosaicing is considered to be the simplest demosaicing method and
 * is used as a baseline for comparing more sophisticated methods.
 *
 * The Input image is a 2D float array of the input RGB values of size 
 * image.raw.dim.width*image.raw.dim.height in row-major order.  redx, redy are the coordinates of the 
 * upper-rightmost red pixel to specify the CFA pattern.
 */
        static public void Demosaic(RawImage image)
        {
            Parallel.For(1, image.raw.dim.height - 1, row =>
            {
                for (int col = 1; col < image.raw.dim.width - 1; col++)
                {
                    CFAColor pixeltype = image.colorFilter.cfa[((row % 2) * 2) + col % 2];
                    if (pixeltype == CFAColor.GREEN)
                    {
                        //get the red                      
                        image.raw.red[(row * image.raw.dim.width) + col] =
                  (ushort)(image.raw.red[((row - 1) * image.raw.dim.width) + col] + image.raw.red[((row + 1) * image.raw.dim.width) + col] >> 1);
                        //get the blue (left) //get the red                      
                        image.raw.blue[(row * image.raw.dim.width) + col] =
                  (ushort)(image.raw.blue[(row * image.raw.dim.width) + col - 1] + image.raw.blue[(row * image.raw.dim.width) + col + 1] >> 1);
                    }
                    else
                    {

                        //get the red                      
                        image.raw.green[(row * image.raw.dim.width) + col] =
                  (ushort)(image.raw.green[((row - 1) * image.raw.dim.width) + col] + image.raw.green[((row + 1) * image.raw.dim.width) + col] >> 1);
                        if (pixeltype == CFAColor.BLUE)
                        {
                            //get the other value
                            image.raw.red[(row * image.raw.dim.width) + col] =
                    (ushort)(image.raw.red[((row - 1) * image.raw.dim.width) + col - 1] + image.raw.red[((row - 1) * image.raw.dim.width) + col + 1] >> 1);
                        }
                        else
                        {
                            //get the other value
                            image.raw.blue[(row * image.raw.dim.width) + col] =
                    (ushort)(image.raw.blue[((row - 1) * image.raw.dim.width) + col - 1] + image.raw.blue[((row - 1) * image.raw.dim.width) + col + 1] >> 1);

                        }
                    }
                }
            });

            /*
            ushort AverageH, AverageV, AverageC, AverageX;
            int i, x, y;
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
            int Green = 1 - ((redx + redy) & 1);

            for (y = 0; y < image.raw.dim.height; y++)
            {
                for (x = 0; x < image.raw.dim.width; x++)
                {
                    if (y == 0)
                    {
                        AverageV = Input[image.raw.dim.width];

                        if (x == 0)
                        {
                            AverageH = Input[i + 1];
                            AverageC = (Input[i + 1] + Input[i + image.raw.dim.width]) / 2;
                            AverageX = Input[i + 1 + image.raw.dim.width];
                        }
                        else if (x < image.raw.dim.width - 1)
                        {
                            AverageH = (Input[i - 1] + Input[i + 1]) / 2;
                            AverageC = (Input[i - 1] + Input[i + 1]
                                + Input[i + image.raw.dim.width]) / 3;
                            AverageX = (Input[i - 1 + image.raw.dim.width]
                                + Input[i + 1 + image.raw.dim.width]) / 2;
                        }
                        else
                        {
                            AverageH = Input[i - 1];
                            AverageC = (Input[i - 1] + Input[i + image.raw.dim.width]) / 2;
                            AverageX = Input[i - 1 + image.raw.dim.width];
                        }
                    }
                    else if (y < image.raw.dim.height - 1)
                    {
                        AverageV = (Input[i - image.raw.dim.width] + Input[i + image.raw.dim.width]) / 2;

                        if (x == 0)
                        {
                            AverageH = Input[i + 1];
                            AverageC = (Input[i + 1] +
                                Input[i - image.raw.dim.width] + Input[i + image.raw.dim.width]) / 3;
                            AverageX = (Input[i + 1 - image.raw.dim.width]
                                + Input[i + 1 + image.raw.dim.width]) / 2;
                        }
                        else if (x < image.raw.dim.width - 1)
                        {
                            AverageH = (Input[i - 1] + Input[i + 1]) / 2;
                            AverageC = (AverageH + AverageV) / 2;
                            AverageX = (Input[i - 1 - image.raw.dim.width] + Input[i + 1 - image.raw.dim.width]
                                + Input[i - 1 + image.raw.dim.width] + Input[i + 1 + image.raw.dim.width]) / 4;
                        }
                        else
                        {
                            AverageH = Input[i - 1];
                            AverageC = (Input[i - 1] +
                                Input[i - image.raw.dim.width] + Input[i + image.raw.dim.width]) / 3;
                            AverageX = (Input[i - 1 - image.raw.dim.width]
                                + Input[i - 1 + image.raw.dim.width]) / 2;
                        }
                    }
                    else
                    {
                        AverageV = Input[i - image.raw.dim.width];

                        if (x == 0)
                        {
                            AverageH = Input[i + 1];
                            AverageC = (Input[i + 1] + Input[i - image.raw.dim.width]) / 2;
                            AverageX = Input[i + 1 - image.raw.dim.width];
                        }
                        else if (x < image.raw.dim.width - 1)
                        {
                            AverageH = (Input[i - 1] + Input[i + 1]) / 2;
                            AverageC = (Input[i - 1]
                                + Input[i + 1] + Input[i - image.raw.dim.width]) / 3;
                            AverageX = (Input[i - 1 - image.raw.dim.width]
                                + Input[i + 1 - image.raw.dim.width]) / 2;
                        }
                        else
                        {
                            AverageH = Input[i - 1];
                            AverageC = (Input[i - 1] + Input[i - image.raw.dim.width]) / 2;
                            AverageX = Input[i - 1 - image.raw.dim.width];
                        }
                    }

                    if (((x + y) & 1) == Green)
                    {
                        // Center pixel is green 
            image.raw.green[i] = Input[i];

                        if ((y & 1) == redy)
                        {
                            /* Left and right neighbors are red 
                            image.raw.red[i] = AverageH;
                            image.raw.blue[i] = AverageV;
                        }
                        else
                        {
                            /* Left and right neighbors are blue 
                            image.raw.red[i] = AverageV;
                            image.raw.blue[i] = AverageH;
                        }
                    }
                    else
                    {
                        image.raw.green[i] = AverageC;

                        if ((y & 1) == redy)
                        {
                            /* Center pixel is red 
                            image.raw.red[i] = Input[i];
                            image.raw.blue[i] = AverageX;
                        }
                        else
                        {
                            /* Center pixel is blue *
                            image.raw.red[i] = AverageX;
                            image.raw.blue[i] = Input[i];
                        }
                    }
                }*/
        }
    }
}
