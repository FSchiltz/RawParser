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
            image.raw.red = new ushort[image.raw.dim.Width * image.raw.dim.Height];
            image.raw.green = new ushort[image.raw.dim.Width * image.raw.dim.Height];
            image.raw.blue = new ushort[image.raw.dim.Width * image.raw.dim.Height];
            Deflate(image);
            image.raw.rawView = null;
            if (image.isFujiTrans)
            {
                switch (algo)
                {
                    case DemosaicAlgorithm.None:
                        // Deflate(image);
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
                    case DemosaicAlgorithm.None:
                        //Deflate(image);
                        break;
                    case DemosaicAlgorithm.Bilinear:
                        Bilinear.Demosaic(image);
                        break;
                    case DemosaicAlgorithm.Adams:
                        SSDD.Demosaic(image, false);
                        break;
                    case DemosaicAlgorithm.FastAdams:
                        SSDD.Demosaic(image, true);
                        break;
                    case DemosaicAlgorithm.Malvar:
                        Malvar.Demosaic(image);
                        break;
                }
            }

            //set correct dim
            image.raw.offset = new Point2D();
            image.raw.uncroppedDim = new Point2D(image.raw.dim.Width, image.raw.dim.Height);
        }

        private static void Deflate(RawImage image)
        {
            Parallel.For(0, image.raw.dim.Height, row =>
            {
                long realRow = (row + image.raw.offset.Height) * image.raw.uncroppedDim.Width;
                long cfarow = (row % image.colorFilter.Size.Height) * image.colorFilter.Size.Width;
                for (int col = 0; col < image.raw.dim.Width; col++)
                {
                    long realCol = (col + image.raw.offset.Width) + realRow;
                    CFAColor pixeltype = image.colorFilter.cfa[cfarow + (col % image.colorFilter.Size.Width)];
                    switch (pixeltype)
                    {
                        case CFAColor.Green:
                            image.raw.green[(row * image.raw.dim.Width) + col] = image.raw.rawView[realCol];
                            break;
                        case CFAColor.Red:
                            image.raw.red[(row * image.raw.dim.Width) + col] = image.raw.rawView[realCol];
                            break;
                        case CFAColor.Blue:
                            image.raw.blue[(row * image.raw.dim.Width) + col] = image.raw.rawView[realCol];
                            break;
                    }
                }
            });
        }
    }
}
