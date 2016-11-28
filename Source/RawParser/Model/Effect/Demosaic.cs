using RawNet;

namespace RawEditor.Effect
{
    enum demosAlgorithm
    {
        NearNeighbour,
        Bicubic,
        Spline,
        Bilinear
    }

    class Demosaic
    {
        public static void demos(ref RawImage image, demosAlgorithm algo)
        {
            switch (algo)
            {
                case demosAlgorithm.Bilinear:
                    break;
                case demosAlgorithm.Bicubic:
                    break;
                case demosAlgorithm.Spline:
                    break;
                case demosAlgorithm.NearNeighbour:
                default:
                    Demosaic.NearNeighbour(ref image, image.dim.y, image.dim.x, image.colorDepth, image.cfa);
                    break;
            }
        }

        private static void NearNeighbour(ref RawImage image, int height, int width, ushort colorDepth, ColorFilterArray cfa)
        {
            ushort[] deflated = new ushort[image.rawData.Length * 3];
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    int pixeltype = (int)cfa.cfa[((row % 2) * 2) + col % 2];
                    if (pixeltype == 1)
                    {
                        //if green
                        //get the red(                        
                        deflated[((row * width) + col) * 3] = (ushort)((image[row - 1, col] + image[row + 1, col]) >> 1);
                        //get the blue (left)
                        deflated[(((row * width) + col) * 3) + 2] = (ushort)((image[row, col - 1] + image[row, col + 1]) >> 1);
                    }
                    else
                    {
                        //get the green value from around
                        pixeltype ^= 2;
                        deflated[(((row * width) + col) * 3) + 1] = (ushort)((image[row - 1, col] + image[row + 1, col] + image[row, col - 1] + image[row, col + 1]) >> 2);

                        deflated[(((row * width) + col) * 3) + pixeltype] = (ushort)((image[row - 1, col - 1] + image[row - 1, col + 1] + image[row + 1, col - 1] + image[row + 1, col + 1]) >> 2);
                    }
                }
            }
            image.rawData = deflated;

        }
    }
}
