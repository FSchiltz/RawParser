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
            if (image.isFujiTrans)
            {
                switch (algo)
                {
                    case DemosaicAlgorithm.Deflate:
                        break;
                    default:
                        FujiDemos.Demosaic(image);
                        break;
                }
            }
            else if (image.colorFilter.ToString() != "RGBG")
            {
                switch (algo)
                {
                    case DemosaicAlgorithm.Bilinear:
                        Bilinear.Demosaic(image);
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
        }

        private static void Deflate(RawImage image)
        {
            image.raw.red = new ushort[image.raw.dim.width * image.raw.dim.height];
            image.raw.green = new ushort[image.raw.dim.width * image.raw.dim.height];
            image.raw.blue = new ushort[image.raw.dim.width * image.raw.dim.height];

            Parallel.For(0, image.raw.dim.height, row =>
            {
                long realRow = (row + image.raw.offset.height) * image.raw.uncroppedDim.width;
                long cfarow = (row % image.colorFilter.Size.height) * image.colorFilter.Size.width;
                for (int col = 0; col < image.raw.dim.width; col++)
                {
                    long realCol = (col + image.raw.offset.width) + realRow;
                    CFAColor pixeltype = image.colorFilter.cfa[cfarow + (col % image.colorFilter.Size.width)];
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
    }
}
