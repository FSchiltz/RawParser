using System.Threading.Tasks;
using System.Diagnostics;
using PhotoNet.Common;

namespace PhotoNet
{
    public static class Demosaic
    {
        public static void Demos(Image<ushort> image, DemosaicAlgorithm algorithm)
        {
            Debug.Assert(image?.fullSize?.rawView != null);
            Debug.Assert(image.fullSize.dim.Area > 4);
            image.fullSize.cpp = 3;
            image.fullSize.red = new ushort[image.fullSize.dim.width * image.fullSize.dim.height];
            image.fullSize.green = new ushort[image.fullSize.dim.width * image.fullSize.dim.height];
            image.fullSize.blue = new ushort[image.fullSize.dim.width * image.fullSize.dim.height];
            Deflate(image);
            image.fullSize.rawView = null;
            if (algorithm == DemosaicAlgorithm.None) return;
            if (image.colorFilter.Size.width % 4 != 0)
            {
                //non conventionnal bayer filter
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
            image.fullSize.offset = new Point2D();
            image.fullSize.UncroppedDim = new Point2D(image.fullSize.dim.width, image.fullSize.dim.height);
        }

        private static void Deflate(Image<ushort> image)
        {
            Parallel.For(0, image.fullSize.dim.height, row =>
            {
                long realRow = (row + image.fullSize.offset.height) * image.fullSize.UncroppedDim.width;
                long cfarow = (row % image.colorFilter.Size.height) * image.colorFilter.Size.width;
                for (int col = 0; col < image.fullSize.dim.width; col++)
                {
                    long realCol = (col + image.fullSize.offset.width) + realRow;
                    CFAColor pixeltype = image.colorFilter.cfa[cfarow + (col % image.colorFilter.Size.width)];
                    switch (pixeltype)
                    {
                        case CFAColor.Green:
                            image.fullSize.green[(row * image.fullSize.dim.width) + col] = image.fullSize.rawView[realCol];
                            break;
                        case CFAColor.Red:
                            image.fullSize.red[(row * image.fullSize.dim.width) + col] = image.fullSize.rawView[realCol];
                            break;
                        case CFAColor.Blue:
                            image.fullSize.blue[(row * image.fullSize.dim.width) + col] = image.fullSize.rawView[realCol];
                            break;
                    }
                }
            });
        }
    }
}
