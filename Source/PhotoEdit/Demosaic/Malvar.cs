using PhotoNet.Common;
using System;
using System.Threading.Tasks;

namespace PhotoNet
{
    static class Malvar
    {
        /** 
       * @brief Demosaicing using the 5x5 linear method of Malvar et al.
       * @param Output pointer to memory to store the demosaiced image
       * @param Input the input image as a flattened 2D array
       * @param image.fullSize.dim.Width, image.fullSize.dim.Height the image dimensions
       * @param redx, redy the coordinates of the upper-rightmost red pixel
       *
       * Malvar, He, and Cutler considered the design of a high quality linear
       * demosaicing method using 5x5 filters.  The method is essentially the 
       * bilinear demosaicing method that is "gradient-corrected" by adding the
       * Laplacian from another channel.  This enables the method to take 
       * advantage of correlation among the RGB channels.
       *
       * The Input image is a 2D float array of the input RGB values of size 
       * image.fullSize.dim.Width*image.fullSize.dim.Height in row-major order.  redx, redy are the coordinates of the 
       * upper-rightmost red pixel to specify the CFA pattern.
       */
        public static void Demosaic(Image<ushort>  image)
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

            int BlueX = 1 - redx;
            int BlueY = 1 - redy;
            // Neigh holds a copy of the 5x5 neighborhood around the current point 
            long[,] Neigh = new long[5, 5];
            // NeighPresence is used for boundary handling.  It is set to 0 if the       neighbor is beyond the boundaries of the image and 1 otherwise. 
            byte[,] NeighPresence = new byte[5, 5];
            int i = 0;
            Parallel.For(0, image.fullSize.dim.height, y =>
            {
                for (long x = 0; x < image.fullSize.dim.width; x++, i++)
                {
                    /* 5x5 neighborhood around the point (x,y) is copied into Neigh */
                    for (long ny = -2, j = x + image.fullSize.dim.width * (y - 2); ny <= 2; ny++, j += image.fullSize.dim.width)
                    {
                        for (int nx = -2; nx <= 2; nx++)
                        {
                            if (x + nx >= 0 && x + nx < image.fullSize.dim.width && y + ny >= 0 && y + ny < image.fullSize.dim.height)
                            {
                                Neigh[2 + nx, 2 + ny] = image.fullSize.green[j + nx] + image.fullSize.blue[j + nx] + image.fullSize.red[j + nx];
                                NeighPresence[2 + nx, 2 + ny] = 1;
                            }
                            else
                            {
                                Neigh[2 + nx, 2 + ny] = 0;
                                NeighPresence[2 + nx, 2 + ny] = 0;
                            }
                        }
                    }

                    if ((x & 1) == redx && (y & 1) == redy)
                    {
                        /* Center pixel is red */
                        image.fullSize.green[i] = (ushort)((2 * (Neigh[2, 1] + Neigh[1, 2]
                            + Neigh[3, 2] + Neigh[2, 3])
                            + (NeighPresence[0, 2] + NeighPresence[4, 2]
                            + NeighPresence[2, 0] + NeighPresence[2, 4]) * Neigh[2, 2]
                            - Neigh[0, 2] - Neigh[4, 2]
                            - Neigh[2, 0] - Neigh[2, 4])
                            / (2 * (NeighPresence[2, 1] + NeighPresence[1, 2]
                            + NeighPresence[3, 2] + NeighPresence[2, 3])));
                        image.fullSize.blue[i] = (ushort)((4 * (Neigh[1, 1] + Neigh[3, 1]
                            + Neigh[1, 3] + Neigh[3, 3]) +
                            3 * ((NeighPresence[0, 2] + NeighPresence[4, 2]
                            + NeighPresence[2, 0] + NeighPresence[2, 4]) * Neigh[2, 2]
                            - Neigh[0, 2] - Neigh[4, 2]
                            - Neigh[2, 0] - Neigh[2, 4]))
                            / (4 * (NeighPresence[1, 1] + NeighPresence[3, 1]
                            + NeighPresence[1, 3] + NeighPresence[3, 3])));
                    }
                    else if ((x & 1) == BlueX && (y & 1) == BlueY)
                    {
                        /* Center pixel is blue */
                        image.fullSize.green[i] = (ushort)((2 * (Neigh[2, 1] + Neigh[1, 2]
                            + Neigh[3, 2] + Neigh[2, 3])
                            + (NeighPresence[0, 2] + NeighPresence[4, 2]
                            + NeighPresence[2, 0] + NeighPresence[2, 4]) * Neigh[2, 2]
                            - Neigh[0, 2] - Neigh[4, 2]
                            - Neigh[2, 0] - Neigh[2, 4])
                            / (2 * (NeighPresence[2, 1] + NeighPresence[1, 2]
                            + NeighPresence[3, 2] + NeighPresence[2, 3])));
                        image.fullSize.red[i] = (ushort)((4 * (Neigh[1, 1] + Neigh[3, 1]
                            + Neigh[1, 3] + Neigh[3, 3]) +
                            3 * ((NeighPresence[0, 2] + NeighPresence[4, 2]
                            + NeighPresence[2, 0] + NeighPresence[2, 4]) * Neigh[2, 2]
                            - Neigh[0, 2] - Neigh[4, 2]
                            - Neigh[2, 0] - Neigh[2, 4]))
                            / (4 * (NeighPresence[1, 1] + NeighPresence[3, 1]
                            + NeighPresence[1, 3] + NeighPresence[3, 3])));
                    }
                    else
                    {
                        /* Center pixel is green */
                        if ((y & 1) == redy)
                        {
                            /* Left and right neighbors are red */
                            image.fullSize.red[i] = (ushort)((8 * (Neigh[1, 2] + Neigh[3, 2])
                                + (2 * (NeighPresence[1, 1] + NeighPresence[3, 1]
                                + NeighPresence[0, 2] + NeighPresence[4, 2]
                                + NeighPresence[1, 3] + NeighPresence[3, 3])
                                - NeighPresence[2, 0] - NeighPresence[2, 4]) * Neigh[2, 2]
                                - 2 * (Neigh[1, 1] + Neigh[3, 1]
                                + Neigh[0, 2] + Neigh[4, 2]
                                + Neigh[1, 3] + Neigh[3, 3])
                                + Neigh[2, 0] + Neigh[2, 4])
                                / (8 * (NeighPresence[1, 2] + NeighPresence[3, 2])));
                            image.fullSize.blue[i] = (ushort)((8 * (Neigh[2, 1] + Neigh[2, 3])
                                + (2 * (NeighPresence[1, 1] + NeighPresence[3, 1]
                                + NeighPresence[2, 0] + NeighPresence[2, 4]
                                + NeighPresence[1, 3] + NeighPresence[3, 3])
                                - NeighPresence[0, 2] - NeighPresence[4, 2]) * Neigh[2, 2]
                                - 2 * (Neigh[1, 1] + Neigh[3, 1]
                                + Neigh[2, 0] + Neigh[2, 4]
                                + Neigh[1, 3] + Neigh[3, 3])
                                + Neigh[0, 2] + Neigh[4, 2])
                                / (8 * (NeighPresence[2, 1] + NeighPresence[2, 3])));
                        }
                        else
                        {
                            /* Left and right neighbors are blue */
                            image.fullSize.red[i] = (ushort)((8 * (Neigh[2, 1] + Neigh[2, 3])
                                + (2 * (NeighPresence[1, 1] + NeighPresence[3, 1]
                                + NeighPresence[2, 0] + NeighPresence[2, 4]
                                + NeighPresence[1, 3] + NeighPresence[3, 3])
                                - NeighPresence[0, 2] - NeighPresence[4, 2]) * Neigh[2, 2]
                                - 2 * (Neigh[1, 1] + Neigh[3, 1]
                                + Neigh[2, 0] + Neigh[2, 4]
                                + Neigh[1, 3] + Neigh[3, 3])
                                + Neigh[0, 2] + Neigh[4, 2])
                                / (8 * (NeighPresence[2, 1] + NeighPresence[2, 3])));
                            image.fullSize.blue[i] = (ushort)((8 * (Neigh[1, 2] + Neigh[3, 2])
                                + (2 * (NeighPresence[1, 1] + NeighPresence[3, 1]
                                + NeighPresence[0, 2] + NeighPresence[4, 2]
                                + NeighPresence[1, 3] + NeighPresence[3, 3])
                                - NeighPresence[2, 0] - NeighPresence[2, 4]) * Neigh[2, 2]
                                - 2 * (Neigh[1, 1] + Neigh[3, 1]
                                + Neigh[0, 2] + Neigh[4, 2]
                                + Neigh[1, 3] + Neigh[3, 3])
                                + Neigh[2, 0] + Neigh[2, 4])
                                / (8 * (NeighPresence[1, 2] + NeighPresence[3, 2])));
                        }
                    }
                }
            });
        }
    }
}
