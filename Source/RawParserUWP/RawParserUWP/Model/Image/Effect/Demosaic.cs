using RawParserUWP.Model.Format.Image;

namespace RawParserUWP.Model.Parser.Demosaic
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
                    Demosaic.NearNeighbour(ref image, (int)image.height, (int)image.width, image.colorDepth, image.cfa);
                    break;
            }
        }

        private static void NearNeighbour(ref RawImage image, int height, int width, ushort colorDepth, byte[] cfa)
        {
            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    var pixeltype = cfa[((row % 2) * 2) + col % 2];
                    if (pixeltype == 1)
                    {
                        //if green
                        //get the red(                        
                        image[row, col, 0] = (ushort)((image[row - 1, col, 0] + image[row + 1, col, 0]) >> 1);
                        //get the blue (left)
                        image[row, col, 2] = (ushort)((image[row, col - 1, 2] + image[row, col + 1, 2]) >> 1);
                    }
                    else
                    {
                        //get the green value from around
                        pixeltype ^= 2;
                        image[row, col, 1] = (ushort)((image[row - 1, col, 1] + image[row + 1, col, 1] + image[row, col - 1, 1] + image[row, col + 1, 1]) >> 2);

                        image[row, col, pixeltype] = (ushort)((image[row - 1, col - 1, pixeltype] + image[row - 1, col + 1, pixeltype] + image[row + 1, col - 1, pixeltype] + image[row + 1, col + 1, pixeltype]) >> 2);
                    }
                }
            }
        }
    }
}
