using RawNet;
using System.Threading.Tasks;
using System;

namespace RawEditor.Effect
{
    public static class Demosaic
    {
        public static void Demos(RawImage image, DemosaicAlgorithm algo)
        {
            image.cpp = 3;
            //first we deflate the image
            Deflate(image);
            switch (algo)
            {
                case DemosaicAlgorithm.AHD:
                //AHD(image);
                //break;
                //break;
                case DemosaicAlgorithm.Bicubic:
                //Bicubic(image);
                //break;
                case DemosaicAlgorithm.Spline:
                //break;
                case DemosaicAlgorithm.Bilinear:
                    if (image.isFujiTrans)
                        FujiBilinear(image);
                    else
                        Bilinear.Demosaic(image);
                    break;
                case DemosaicAlgorithm.Deflate:
                    //already done
                    break;
                case DemosaicAlgorithm.SSDD:
                    SSDD.Demosaic(image, false);
                    break;
                case DemosaicAlgorithm.SimpleAdams:
                    SSDD.Demosaic(image, true);
                    break;
                case DemosaicAlgorithm.Malvar:
                    Malvar.Demosaic(image);
                    break;
            }
        }

        private static void AHD(RawImage image)
        {
            throw new NotImplementedException();
        }

        private static void Bicubic(RawImage image)
        {
            throw new NotImplementedException();
        }

        private static void Deflate(RawImage image)
        {
            uint height = image.colorFilter.Size.height;
            uint width = image.colorFilter.Size.width;

            image.raw.red = new ushort[image.raw.dim.width * image.raw.dim.height];
            image.raw.green = new ushort[image.raw.dim.width * image.raw.dim.height];
            image.raw.blue = new ushort[image.raw.dim.width * image.raw.dim.height];

            Parallel.For(0, image.raw.dim.height, row =>
            {
                long realRow = row + image.raw.offset.height;
                for (int col = 0; col < image.raw.dim.width; col++)
                {
                    long realCol = (col + image.raw.offset.width) + realRow * image.raw.uncroppedDim.width;
                    CFAColor pixeltype = image.colorFilter.cfa[((row % height) * width) + col % width];
                    switch (pixeltype)
                    {
                        case CFAColor.GREEN:
                            image.raw.green[(row * image.raw.dim.width) + col] = image.raw.rawView[realCol];
                            break;
                        case CFAColor.RED:
                            image.raw.red[(row * image.raw.dim.width) + col] = image.raw.rawView[realCol];
                            break;
                        case CFAColor.BLUE:
                            image.raw.blue[(row * image.raw.dim.width) + col] = image.raw.rawView[realCol];
                            break;
                    }
                }
            });
            image.raw.rawView = null;
            //set correct dim
            image.raw.offset = new Point2D();
            image.raw.uncroppedDim = new Point2D(image.raw.dim.width, image.raw.dim.height);
        }

        private static void FujiBilinear(RawImage image)
        {
            uint height = image.colorFilter.Size.height;
            uint width = image.colorFilter.Size.width;

            Parallel.For(0, image.raw.dim.height, row =>
            {
                for (int col = 0; col < image.raw.dim.width; col++)
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
        }

    }
}
