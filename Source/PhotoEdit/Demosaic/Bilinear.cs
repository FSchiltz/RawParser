using PhotoNet.Common;
using System.Threading.Tasks;

namespace PhotoNet
{
    static class Bilinear
    {
        /** 
 * @brief Bilinear demosaicing
 * @param Output pointer to memory to store the demosaiced image
 * @param Input the input image as a flattened 2D array
 * @param image.fullSize.dim.Width, image.fullSize.dim.Height the image dimensions
 * @param redx, redy the coordinates of the upper-rightmost red pixel
 *
 * Bilinear demosaicing is considered to be the simplest demosaicing method and
 * is used as a baseline for comparing more sophisticated methods.
 *
 * The Input image is a 2D float array of the input RGB values of size 
 * image.fullSize.dim.Width*image.fullSize.dim.Height in row-major order.  redx, redy are the coordinates of the 
 * upper-rightmost red pixel to specify the CFA pattern.
 */
        static public void Demosaic(Image<ushort>  image)
        {
            Parallel.For(1, image.fullSize.dim.height - 1, row =>
            {
                for (int col = 1; col < image.fullSize.dim.width - 1; col++)
                {
                    CFAColor pixeltype = image.colorFilter.cfa[((row % 2) * 2) + col % 2];
                    if (pixeltype == CFAColor.Green)
                    {
                        //get the red                      
                        image.fullSize.red[(row * image.fullSize.dim.width) + col] =
                  (ushort)(image.fullSize.red[((row - 1) * image.fullSize.dim.width) + col] + image.fullSize.red[((row + 1) * image.fullSize.dim.width) + col] >> 1);
                        //get the blue (left) //get the red                      
                        image.fullSize.blue[(row * image.fullSize.dim.width) + col] =
                  (ushort)(image.fullSize.blue[(row * image.fullSize.dim.width) + col - 1] + image.fullSize.blue[(row * image.fullSize.dim.width) + col + 1] >> 1);
                    }
                    else
                    {

                        //get the red                      
                        image.fullSize.green[(row * image.fullSize.dim.width) + col] =
                  (ushort)(image.fullSize.green[((row - 1) * image.fullSize.dim.width) + col] + image.fullSize.green[((row + 1) * image.fullSize.dim.width) + col] >> 1);
                        if (pixeltype == CFAColor.Blue)
                        {
                            //get the other value
                            image.fullSize.red[(row * image.fullSize.dim.width) + col] =
                    (ushort)(image.fullSize.red[((row - 1) * image.fullSize.dim.width) + col - 1] + image.fullSize.red[((row - 1) * image.fullSize.dim.width) + col + 1] >> 1);
                        }
                        else
                        {
                            //get the other value
                            image.fullSize.blue[(row * image.fullSize.dim.width) + col] =
                    (ushort)(image.fullSize.blue[((row - 1) * image.fullSize.dim.width) + col - 1] + image.fullSize.blue[((row - 1) * image.fullSize.dim.width) + col + 1] >> 1);

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

            for (y = 0; y < image.fullSize.dim.Height; y++)
            {
                for (x = 0; x < image.fullSize.dim.Width; x++)
                {
                    if (y == 0)
                    {
                        AverageV = Input[image.fullSize.dim.Width];

                        if (x == 0)
                        {
                            AverageH = Input[i + 1];
                            AverageC = (Input[i + 1] + Input[i + image.fullSize.dim.Width]) / 2;
                            AverageX = Input[i + 1 + image.fullSize.dim.Width];
                        }
                        else if (x < image.fullSize.dim.Width - 1)
                        {
                            AverageH = (Input[i - 1] + Input[i + 1]) / 2;
                            AverageC = (Input[i - 1] + Input[i + 1]
                                + Input[i + image.fullSize.dim.Width]) / 3;
                            AverageX = (Input[i - 1 + image.fullSize.dim.Width]
                                + Input[i + 1 + image.fullSize.dim.Width]) / 2;
                        }
                        else
                        {
                            AverageH = Input[i - 1];
                            AverageC = (Input[i - 1] + Input[i + image.fullSize.dim.Width]) / 2;
                            AverageX = Input[i - 1 + image.fullSize.dim.Width];
                        }
                    }
                    else if (y < image.fullSize.dim.Height - 1)
                    {
                        AverageV = (Input[i - image.fullSize.dim.Width] + Input[i + image.fullSize.dim.Width]) / 2;

                        if (x == 0)
                        {
                            AverageH = Input[i + 1];
                            AverageC = (Input[i + 1] +
                                Input[i - image.fullSize.dim.Width] + Input[i + image.fullSize.dim.Width]) / 3;
                            AverageX = (Input[i + 1 - image.fullSize.dim.Width]
                                + Input[i + 1 + image.fullSize.dim.Width]) / 2;
                        }
                        else if (x < image.fullSize.dim.Width - 1)
                        {
                            AverageH = (Input[i - 1] + Input[i + 1]) / 2;
                            AverageC = (AverageH + AverageV) / 2;
                            AverageX = (Input[i - 1 - image.fullSize.dim.Width] + Input[i + 1 - image.fullSize.dim.Width]
                                + Input[i - 1 + image.fullSize.dim.Width] + Input[i + 1 + image.fullSize.dim.Width]) / 4;
                        }
                        else
                        {
                            AverageH = Input[i - 1];
                            AverageC = (Input[i - 1] +
                                Input[i - image.fullSize.dim.Width] + Input[i + image.fullSize.dim.Width]) / 3;
                            AverageX = (Input[i - 1 - image.fullSize.dim.Width]
                                + Input[i - 1 + image.fullSize.dim.Width]) / 2;
                        }
                    }
                    else
                    {
                        AverageV = Input[i - image.fullSize.dim.Width];

                        if (x == 0)
                        {
                            AverageH = Input[i + 1];
                            AverageC = (Input[i + 1] + Input[i - image.fullSize.dim.Width]) / 2;
                            AverageX = Input[i + 1 - image.fullSize.dim.Width];
                        }
                        else if (x < image.fullSize.dim.Width - 1)
                        {
                            AverageH = (Input[i - 1] + Input[i + 1]) / 2;
                            AverageC = (Input[i - 1]
                                + Input[i + 1] + Input[i - image.fullSize.dim.Width]) / 3;
                            AverageX = (Input[i - 1 - image.fullSize.dim.Width]
                                + Input[i + 1 - image.fullSize.dim.Width]) / 2;
                        }
                        else
                        {
                            AverageH = Input[i - 1];
                            AverageC = (Input[i - 1] + Input[i - image.fullSize.dim.Width]) / 2;
                            AverageX = Input[i - 1 - image.fullSize.dim.Width];
                        }
                    }

                    if (((x + y) & 1) == Green)
                    {
                        // Center pixel is green 
            image.fullSize.green[i] = Input[i];

                        if ((y & 1) == redy)
                        {
                            /* Left and right neighbors are red 
                            image.fullSize.red[i] = AverageH;
                            image.fullSize.blue[i] = AverageV;
                        }
                        else
                        {
                            /* Left and right neighbors are blue 
                            image.fullSize.red[i] = AverageV;
                            image.fullSize.blue[i] = AverageH;
                        }
                    }
                    else
                    {
                        image.fullSize.green[i] = AverageC;

                        if ((y & 1) == redy)
                        {
                            /* Center pixel is red 
                            image.fullSize.red[i] = Input[i];
                            image.fullSize.blue[i] = AverageX;
                        }
                        else
                        {
                            /* Center pixel is blue *
                            image.fullSize.red[i] = AverageX;
                            image.fullSize.blue[i] = Input[i];
                        }
                    }
                }*/
        }
    }
}
