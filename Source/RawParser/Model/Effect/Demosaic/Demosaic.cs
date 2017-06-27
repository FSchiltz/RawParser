using RawNet;
using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace RawEditor.Effect
{
    public static class Demosaic
    {
        public static void Demos(RawImage<ushort> image, DemosaicAlgorithm algorithm)
        {
            Debug.Assert(image?.raw?.rawView != null);
            Debug.Assert(image.raw.dim.Area > 4);
            image.raw.cpp = 3;
            image.raw.red = new ushort[image.raw.dim.width * image.raw.dim.height];
            image.raw.green = new ushort[image.raw.dim.width * image.raw.dim.height];
            image.raw.blue = new ushort[image.raw.dim.width * image.raw.dim.height];
            Deflate(image);
            image.raw.rawView = null;
            if (algorithm == DemosaicAlgorithm.None) return;
            if (image.isFujiTrans)
            {
                switch (algorithm)
                {
                    case DemosaicAlgorithm.FastAdams:
                    case DemosaicAlgorithm.Adams:
                    case DemosaicAlgorithm.SSDD:
                    default:
                        new FastAdamsDemosaic().Demosaic(image);
                        //new FujiDemos().Demosaic(image);
                        break;
                }
            }
            else if (image.colorFilter.ToString() != "RGBG")
            {
                switch (algorithm)
                {
                    case DemosaicAlgorithm.Bilinear:
                        Bilinear.Demosaic(image);
                        break;
                    case DemosaicAlgorithm.Adams:
                        new AdamsDemosaic().Demosaic(image);
                        break;
                    case DemosaicAlgorithm.FastAdams:
                        new FastAdamsDemosaic().Demosaic(image);
                        break;
                    case DemosaicAlgorithm.Malvar:
                        Malvar.Demosaic(image);
                        break;
                    case DemosaicAlgorithm.SSDD:
                        new SSDDemosaic().Demosaic(image);
                        break;
                }
            }

            //set correct dim
            image.raw.offset = new Point2D();
            image.raw.UncroppedDim = new Point2D(image.raw.dim.width, image.raw.dim.height);
        }

        private static void Deflate(RawImage<ushort> image)
        {
            Parallel.For(0, image.raw.dim.height, row =>
            {
                long realRow = (row + image.raw.offset.height) * image.raw.UncroppedDim.width;
                long cfarow = (row % image.colorFilter.Size.height) * image.colorFilter.Size.width;
                for (int col = 0; col < image.raw.dim.width; col++)
                {
                    long realCol = (col + image.raw.offset.width) + realRow;
                    CFAColor pixeltype = image.colorFilter.cfa[cfarow + (col % image.colorFilter.Size.width)];
                    switch (pixeltype)
                    {
                        case CFAColor.Green:
                            image.raw.green[(row * image.raw.dim.width) + col] = image.raw.rawView[realCol];
                            break;
                        case CFAColor.Red:
                            image.raw.red[(row * image.raw.dim.width) + col] = image.raw.rawView[realCol];
                            break;
                        case CFAColor.Blue:
                            image.raw.blue[(row * image.raw.dim.width) + col] = image.raw.rawView[realCol];
                            break;
                    }
                }
            });
        }
    }
}
