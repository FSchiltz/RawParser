using RawNet;
using System.Threading.Tasks;
using System;

namespace RawEditor.Effect
{
    public enum DemosAlgorithm
    {
        Bicubic,
        Spline,
        Bilinear,
        Deflate,
        AHD
    }

    public static class Demosaic
    {
        public static void Demos(RawImage image, DemosAlgorithm algo)
        {
            ushort[] deflated = new ushort[image.raw.dim.width * image.raw.dim.height * 3];
            image.cpp = 3;
            switch (algo)
            {
                case DemosAlgorithm.AHD:
                    AHD(image, deflated);
                    break;
                //break;
                case DemosAlgorithm.Bicubic:
                    Bicubic(image, deflated);
                    break;
                case DemosAlgorithm.Spline:
                //break;
                case DemosAlgorithm.Bilinear:
                    if (image.isFujiTrans)
                        FujiBilinear(image, deflated);
                    else
                        Bilinear(image, deflated);
                    break;
                case DemosAlgorithm.Deflate:
                    Deflate(image, deflated);
                    break;
            }
            //set correct dim
            image.raw.offset = new Point2D();
            image.raw.uncroppedDim = new Point2D(image.raw.dim.width, image.raw.dim.height);
            image.raw.data = deflated;
        }

        private unsafe static void AHD(RawImage image, ushort[] deflated)
        {
            throw new NotImplementedException();
        }

        private static void Bicubic(RawImage image, ushort[] deflated)
        {
            throw new NotImplementedException();
        }

        private static void Deflate(RawImage image, ushort[] deflated)
        {
            int height = image.colorFilter.Size.height;
            int width = image.colorFilter.Size.width;
            Parallel.For(0, image.raw.dim.height, row =>
            {
                int realRow = row + image.raw.offset.height;
                for (int col = 0; col < image.raw.dim.width; col++)
                {
                    int realCol = col + image.raw.offset.width;
                    int pixeltype = (int)image.colorFilter.cfa[((row % height) * width) + col % width];

                    deflated[(((row * image.raw.dim.width) + col) * 3) + pixeltype] = image[realRow, realCol];
                }
            });
        }

        private static void FujiBilinear(RawImage image, ushort[] deflated)
        {
            int height = image.colorFilter.Size.height;
            int width = image.colorFilter.Size.width;
            //first deflate
            Deflate(image, deflated);
            Parallel.For(0, image.raw.dim.height, row =>
            {
                int realRow = row + image.raw.offset.height;
                for (int col = 0; col < image.raw.dim.width; col++)
                {
                    int realCol = col + image.raw.offset.width;
                    CFAColor pixeltype = image.colorFilter.cfa[((row % height) * width) + col % width];
                    if (pixeltype == CFAColor.GREEN)
                    {
                        //if green
                        //get the green
                        deflated[(((row * image.raw.dim.width) + col) * 3) + 1] = image[realRow, realCol];

                        //get the red(                        
                        deflated[((row * image.raw.dim.width) + col) * 3] = (ushort)((image[realRow - 1, realCol] + image[realRow + 1, realCol]) >> 1);
                        //get the blue (left)
                        deflated[(((row * image.raw.dim.width) + col) * 3) + 2] = (ushort)((image[realRow, realCol - 1] + image[realRow, realCol + 1]) >> 1);
                    }
                    else
                    {
                        deflated[(((row * image.raw.dim.width) + col) * 3) + (int)pixeltype] = image[realRow, realCol];

                        //get the green value from around
                        pixeltype = (CFAColor)((int)pixeltype ^ 2);
                        deflated[(((row * image.raw.dim.width) + col) * 3) + 1] = (ushort)((image[realRow - 1, realCol] + image[realRow + 1, realCol] + image[realRow, realCol - 1] + image[realRow, realCol + 1]) >> 2);

                        //get the other value
                        deflated[(((row * image.raw.dim.width) + col) * 3) + (int)pixeltype] = (ushort)((image[realRow - 1, realCol - 1] + image[realRow - 1, realCol + 1] + image[realRow + 1, realCol - 1] + image[realRow + 1, realCol + 1]) >> 2);
                    }
                }
            });
            image.raw.data = deflated;
        }

        private static void Bilinear(RawImage image, ushort[] deflated)
        {
            Parallel.For(0, image.raw.dim.height, row =>
            {
                int realRow = row + image.raw.offset.height;
                for (int col = 0; col < image.raw.dim.width; col++)
                {
                    int realCol = col + image.raw.offset.width;
                    CFAColor pixeltype = image.colorFilter.cfa[((row % 2) * 2) + col % 2];
                    if (pixeltype == CFAColor.GREEN)
                    {
                        //if green
                        //get the green
                        deflated[(((row * image.raw.dim.width) + col) * 3) + 1] = image[realRow, realCol];

                        //get the red(                        
                        deflated[((row * image.raw.dim.width) + col) * 3] = (ushort)((image[realRow - 1, realCol] + image[realRow + 1, realCol]) >> 1);
                        //get the blue (left)
                        deflated[(((row * image.raw.dim.width) + col) * 3) + 2] = (ushort)((image[realRow, realCol - 1] + image[realRow, realCol + 1]) >> 1);
                    }
                    else
                    {
                        deflated[(((row * image.raw.dim.width) + col) * 3) + (int)pixeltype] = image[realRow, realCol];

                        //get the green value from around
                        pixeltype = (CFAColor)((int)pixeltype ^ 2);
                        deflated[(((row * image.raw.dim.width) + col) * 3) + 1] = (ushort)((image[realRow - 1, realCol] + image[realRow + 1, realCol] + image[realRow, realCol - 1] + image[realRow, realCol + 1]) >> 2);

                        //get the other value
                        deflated[(((row * image.raw.dim.width) + col) * 3) + (int)pixeltype] = (ushort)((image[realRow - 1, realCol - 1] + image[realRow - 1, realCol + 1] + image[realRow + 1, realCol - 1] + image[realRow + 1, realCol + 1]) >> 2);
                    }
                }
            });
            image.raw.data = deflated;
        }
    }
}
