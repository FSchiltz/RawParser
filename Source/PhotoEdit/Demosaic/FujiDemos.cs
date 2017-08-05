using PhotoNet.Common;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PhotoNet
{
    class FujiDemos
    {
        protected int redx, redy, bluex, bluey;
        protected CFAColor[] mask;
        public void Demosaic(Image<ushort> image)
        {
            // Mask of color per pixel
            mask = new CFAColor[image.fullSize.dim.width * image.fullSize.dim.height];
            Parallel.For(0, image.fullSize.dim.height, row =>
            {
                for (long col = 0; col < image.fullSize.dim.width; col++)
                {
                    mask[row * image.fullSize.dim.width + col] = image.colorFilter.cfa[((row % image.colorFilter.Size.width) * image.colorFilter.Size.height) + (col % image.colorFilter.Size.height)];
                }
            });
            DemosaickingAdams(image.fullSize, image.colorFilter);
            // compute the bilinear on the differences of the red and blue with the already interpolated green
            //DemosaickingBilinearRedBlue(redx, redy, image.fullSize, image.fullSize.red, CFAColor.Red);
            //DemosaickingBilinearRedBlue(bluex, bluey, image.fullSize, image.fullSize.blue, CFAColor.Blue);
        }

        protected virtual void DemosaickingAdams(ImageComponent<ushort> image, ColorFilterArray cfa)
        {
            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            Parallel.For(0, image.dim.height, row =>
            {
                for (long col = 0; col < image.dim.width; col++)
                {
                    var color = mask[row * image.dim.width + col];


                    //take the pixel and put it around
                    if (color == CFAColor.Green)
                    {
                        Interpolate(CFAColor.Green, image.green, col, row, image.dim);
                    }
                    else if (color == CFAColor.Red)
                    {
                        Interpolate(CFAColor.Red, image.red, col, row, image.dim);
                    }
                    else
                    {
                        Interpolate(CFAColor.Blue, image.blue, col, row, image.dim);
                    }
                    /*if (!(col < 3 || row < 3 || col >= image.dim.width - 3 || row >= image.dim.height - 3))
                    {
                        //skip to the end of line to reduce calculation
                        col = image.dim.width - 4;
                    }
                    else if (((mask[row * image.dim.width + col] = cfa.cfa[((row % cfa.Size.width) * cfa.Size.height) + (col % cfa.Size.height)]) != CFAColor.Green))
                    {
                        long gn, gs, ge, gw;
                        if (row > 0) gn = row - 1; else gn = 1;
                        if (row < image.dim.height - 1) gs = row + 1; else gs = image.dim.height - 2;
                        if (col < image.dim.width - 1) ge = col + 1; else ge = image.dim.width - 2;
                        if (col > 0) gw = col - 1; else gw = 1;

                        image.green[row * image.dim.width + col] = (ushort)((
                            image.green[gn * image.dim.width + col] +
                            image.green[gs * image.dim.width + col] +
                            image.green[row * image.dim.width + gw] +
                            image.green[row * image.dim.width + ge]) / 4.0);
                    }*/
                }
            });
        }

        private void Interpolate(CFAColor color, ushort[] output, long x, long y, Point2D dim)
        {
            long gn, gs, ge, gw, pos = y * dim.width + x;
            // Compute north, south, west, east positions
            // taking a mirror symmetry at the boundaries
            if (y > 0) gn = y - 1; else gn = 1;
            if (y < dim.height - 1) gs = y + 1; else gs = dim.height - 2;
            if (x < dim.width - 1) ge = x + 1; else ge = dim.width - 2;
            if (x > 0) gw = x - 1; else gw = 1;

            if (mask[gn * dim.width + ge] != color) output[gn * dim.width + ge] = output[pos];
            if (mask[gn * dim.width + gw] != color) output[gn * dim.width + gw] = output[pos];
            if (mask[gn * dim.width + x] != color) output[gn * dim.width + x] = output[pos];
            if (mask[gs * dim.width + ge] != color) output[gs * dim.width + ge] = output[pos];
            if (mask[gs * dim.width + gw] != color) output[gs * dim.width + gw] = output[pos];
            if (mask[gs * dim.width + x] != color) output[gs * dim.width + x] = output[pos];
            if (mask[y * dim.width + gw] != color) output[y * dim.width + gw] = output[pos];
            if (mask[y * dim.width + ge] != color) output[y * dim.width + ge] = output[pos];
        }

        protected virtual void DemosaickingBilinearRedBlue(int colorX, int colorY, ImageComponent<ushort> image, ushort[] output, CFAColor COLORPOSITION)
        {
            var dim = image.dim;
            var red = new ushort[image.dim.Area];
            // Interpolate the red differences making the average of possible values depending on the CFA structure
            Parallel.For(0, dim.width, x =>
            {
                for (int y = 0; y < dim.height; y++)
                {
                    if (mask[y * dim.width + x] != COLORPOSITION)
                    {
                        long gn, gs, ge, gw;
                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (y > 0) gn = y - 1; else gn = 1;
                        if (y < dim.height - 1) gs = y + 1; else gs = dim.height - 2;
                        if (x < dim.width - 1) ge = x + 1; else ge = dim.width - 2;
                        if (x > 0) gw = x - 1; else gw = 1;

                        //if green take all pixel around (there are 2 of each color around)
                        if (mask[y * dim.width + x] == CFAColor.Green)
                        {
                            int nb = 0;
                            if (COLORPOSITION == mask[gn * dim.width + ge]) nb++;
                            if (COLORPOSITION == mask[gn * dim.width + gw]) nb++;
                            if (COLORPOSITION == mask[gn * dim.width + x]) nb++;
                            if (COLORPOSITION == mask[gs * dim.width + ge]) nb++;
                            if (COLORPOSITION == mask[gs * dim.width + gw]) nb++;
                            if (COLORPOSITION == mask[gs * dim.width + x]) nb++;
                            if (COLORPOSITION == mask[y * dim.width + gw]) nb++;
                            if (COLORPOSITION == mask[y * dim.width + ge]) nb++;
                            Debug.Assert(nb == 2);

                            red[y * dim.width + x] = (ushort)((
                                output[gn * dim.width + ge] +
                                output[gn * dim.width + gw] +
                                output[gn * dim.width + x] +
                                output[gs * dim.width + ge] +
                                output[gs * dim.width + gw] +
                                output[gs * dim.width + x] +
                                output[y * dim.width + gw] +
                               output[y * dim.width + ge]) / 2.0);
                        }
                        else
                        {
                            int nb = 0;
                            if (COLORPOSITION == mask[gn * dim.width + ge]) nb++;
                            if (COLORPOSITION == mask[gn * dim.width + gw]) nb++;
                            if (COLORPOSITION == mask[gn * dim.width + x]) nb++;
                            if (COLORPOSITION == mask[gs * dim.width + ge]) nb++;
                            if (COLORPOSITION == mask[gs * dim.width + gw]) nb++;
                            if (COLORPOSITION == mask[gs * dim.width + x]) nb++;
                            if (COLORPOSITION == mask[y * dim.width + gw]) nb++;
                            if (COLORPOSITION == mask[y * dim.width + ge]) nb++;
                            Debug.Assert(nb == 3);

                            //else take all pixel around (there are 3 of the other color around)
                            red[y * dim.width + x] = (ushort)((
                                output[gn * dim.width + ge] +
                               output[gn * dim.width + gw] +
                               output[gn * dim.width + x] +
                               output[gs * dim.width + ge] +
                               output[gs * dim.width + gw] +
                              output[gs * dim.width + x] +
                               output[y * dim.width + gw] +
                               output[y * dim.width + ge]) / 3.0);
                        }
                    }
                }
            });
            output = red;
        }
    }
}
