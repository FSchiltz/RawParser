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
            image.cpp = 3;
            image.raw.red = new ushort[image.raw.dim.Width * image.raw.dim.Height];
            image.raw.green = new ushort[image.raw.dim.Width * image.raw.dim.Height];
            image.raw.blue = new ushort[image.raw.dim.Width * image.raw.dim.Height];
            Deflate(image);
            image.raw.rawView = null;
            if (image.isFujiTrans)
            {
                switch (algorithm)
                {
                    case DemosaicAlgorithm.None:
                        break;
                    case DemosaicAlgorithm.FastAdams:
                    case DemosaicAlgorithm.Adams:
                    case DemosaicAlgorithm.SSDD:
                        FujiSSDD.Demosaic(image);
                        break;
                    default:
                        FujiDemos.Demosaic(image);
                        break;
                }
            }
            else if (image.colorFilter.ToString() != "RGBG")
            {
                switch (algorithm)
                {
                    case DemosaicAlgorithm.None:
                        //Deflate(image);
                        break;
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
            image.raw.UncroppedDim = new Point2D(image.raw.dim.Width, image.raw.dim.Height);
        }

        private static void Deflate(RawImage<ushort> image)
        {
            Parallel.For(0, image.raw.dim.Height, row =>
            {
                long realRow = (row + image.raw.offset.Height) * image.raw.UncroppedDim.Width;
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
