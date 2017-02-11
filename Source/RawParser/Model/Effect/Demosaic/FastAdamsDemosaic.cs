using RawNet;
using System.Threading.Tasks;

namespace RawEditor.Effect
{
    class FastAdamsDemosaic : AdamsDemosaic
    {

        protected new void DemosaickingAdams(int redx, int redy, ImageComponent image, CFAColor[] mask)
        {
            // Initializations
            int bluex = 1 - redx;
            int bluey = 1 - redy;

            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            Parallel.For(0, image.dim.Height, row =>
            {
                for (long col = 0; col < image.dim.Width; col++)
                {
                    if ((mask[row * image.dim.Width + col] != CFAColor.Green))
                    {
                        long gn, gs, ge, gw;
                        if (row > 0) gn = row - 1; else gn = 1;
                        if (row < image.dim.Height - 1) gs = row + 1; else gs = image.dim.Height - 2;
                        if (col < image.dim.Width - 1) ge = col + 1; else ge = image.dim.Width - 2;
                        if (col > 0) gw = col - 1; else gw = 1;

                        image.green[row * image.dim.Width + col] = (ushort)((
                            image.green[gn * image.dim.Width + col] +
                            image.green[gs * image.dim.Width + col] +
                            image.green[row * image.dim.Width + gw] +
                            image.green[row * image.dim.Width + ge]) / 4.0);
                    }
                }
            });
        }

        protected new void DemosaickingBilinearRedBlue(int colorX, int colorY, ImageComponent image, CFAColor[] mask, ushort[] output, CFAColor COLORPOSITION)
        {
            Parallel.For(0, image.dim.Height, row =>
            {
                for (int col = 0; col < image.dim.Width; col++)
                {
                    if (mask[row * image.dim.Width + col] != COLORPOSITION)
                    {
                        long gn, gs, ge, gw;
                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        if (row > 0) gn = row - 1; else gn = 1;
                        if (row < image.dim.Height - 1) gs = row + 1; else gs = image.dim.Height - 2;
                        if (col < image.dim.Width - 1) ge = col + 1; else ge = image.dim.Width - 2;
                        if (col > 0) gw = col - 1; else gw = 1;

                        if (mask[row * image.dim.Width + col] == CFAColor.Green && row % 2 == colorY)
                            output[row * image.dim.Width + col] = (ushort)((output[row * image.dim.Width + ge] + output[row * image.dim.Width + gw]) / 2.0);
                        else if (mask[row * image.dim.Width + col] == CFAColor.Green && col % 2 == colorX)
                            output[row * image.dim.Width + col] = (ushort)((output[gn * image.dim.Width + col] + output[gs * image.dim.Width + col]) / 2.0);
                        else
                        {
                            output[row * image.dim.Width + col] = (ushort)((output[gn * image.dim.Width + ge] +
                                output[gn * image.dim.Width + gw] +
                                output[gs * image.dim.Width + ge] +
                                output[gs * image.dim.Width + gw]) / 4.0);
                        }
                    }
                }
            });
        }

    }
}
