using PhotoNet.Common;
using System.Threading.Tasks;

namespace PhotoNet
{
    class FastAdamsDemosaic : AdamsDemosaic
    {
        protected override void DemosaickingAdams(ImageComponent<ushort> image, ColorFilterArray cfa)
        {
            SuperSimple(image, cfa);
            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            /* Parallel.For(0, image.dim.Height, row =>
             {
                 var cfapos = (row % 2) * 2;
                 var posRow = row * image.dim.Width;

                 long gn = 1, gs;
                 if (row > 0) gn = row - 1;
                 if (row < image.dim.Height - 1) gs = row + 1; else gs = image.dim.Height - 2;
                 gs *= image.dim.Width;
                 gn *= image.dim.Width;

                 for (long col = 0; col < image.dim.Width; col++)
                 {
                     var pos = posRow + col;
                     var color = mask[pos] = cfa.cfa[cfapos + (col % 2)];
                     long gw = 1, ge;
                     if (col < image.dim.Width - 1) ge = col + 1; else ge = image.dim.Width - 2;
                     if (col > 0) gw = col - 1;

                     if ((color != CFAColor.Green))
                     {
                         //interpolate green                   
                         image.green[pos] = (ushort)((image.green[gn + col] + image.green[gs + col] + image.green[posRow + gw] + image.green[posRow + ge]) / 4);
                     }
                 }
             });*/
        }

        protected override void DemosaickingBilinearRedBlue(int colorX, int colorY, ImageComponent<ushort> image, ushort[] output, CFAColor COLORPOSITION)
        {
            /*Parallel.For(0, image.dim.Height, row =>
            {
                var cfapos = (row % 2) * 2;
                var posRow = row * image.dim.Width;

                long gn = 1, gs;
                if (row > 0) gn = row - 1;
                if (row < image.dim.Height - 1) gs = row + 1; else gs = image.dim.Height - 2;
                gs *= image.dim.Width;
                gn *= image.dim.Width;

                for (int col = 0; col < image.dim.Width; col++)
                {
                    var pos = posRow + col;
                    var color = mask[pos];
                    if (color != COLORPOSITION)
                    {
                        // Compute north, south, west, east positions
                        // taking a mirror symmetry at the boundaries
                        long gw = 1, ge;
                        if (col < image.dim.Width - 1) ge = col + 1; else ge = image.dim.Width - 2;
                        if (col > 0) gw = col - 1;

                        if (color == CFAColor.Green && row % 2 == colorY)
                        {
                            output[pos] = (ushort)((output[posRow + ge] + output[posRow + gw]) / 2);
                        }
                        else if (color == CFAColor.Green && col % 2 == colorX)
                        {
                            output[pos] = (ushort)((output[gn + col] + output[gs + col]) / 2);
                        }
                        else
                        {
                            output[pos] = (ushort)((output[gn + ge] + output[gn + gw] + output[gs + ge] + output[gs + gw]) / 4);
                        }
                    }
                }
            });*/
        }

        protected void SuperSimple(ImageComponent<ushort> image, ColorFilterArray cfa)
        {
            // Interpolate the green channel by bilinear on the boundaries  
            // make the average of four neighbouring green pixels: Nourth, South, East, West
            Parallel.For(0, image.dim.height, row =>
            {
                var cfapos = (row % cfa.Size.height) * cfa.Size.width;
                var posRow = row * image.dim.width;

                long gn = 1, gs;
                if (row > 0) gn = row - 1;
                if (row < image.dim.height - 1) gs = row + 1; else gs = image.dim.height - 2;
                gs *= image.dim.width;
                gn *= image.dim.width;

                for (long col = 0; col < image.dim.width; col++)
                {
                    var pos = posRow + col;
                    var color = cfa.cfa[cfapos + (col % cfa.Size.width)];
                    long gw = 1, ge;
                    if (col < image.dim.width - 1) ge = col + 1; else ge = image.dim.width - 2;
                    if (col > 0) gw = col - 1;

                    if ((color != CFAColor.Green))
                    {
                        //interpolate green                   
                        image.green[pos] = (ushort)((image.green[gn + col] + image.green[gs + col] + image.green[posRow + gw] + image.green[posRow + ge]) / 4);
                        if (color == CFAColor.Red)
                        {
                            image.blue[pos] = (ushort)((image.blue[gn + ge] + image.blue[gn + gw] + image.blue[gs + ge] + image.blue[gs + gw]) / 4);
                        }
                        else
                        {
                            image.red[pos] = (ushort)((image.red[gn + ge] + image.red[gn + gw] + image.red[gs + ge] + image.red[gs + gw]) / 4);
                        }
                    }
                    else
                    {
                        if (row % 2 == bluey)
                        {
                            image.blue[pos] = (ushort)((image.blue[posRow + ge] + image.blue[posRow + gw]) / 2);
                            image.red[pos] = (ushort)((image.red[gn + col] + image.red[gs + col]) / 2);
                        }
                        else
                        {
                            image.blue[pos] = (ushort)((image.blue[gn + col] + image.blue[gs + col]) / 2);
                            image.red[pos] = (ushort)((image.red[posRow + ge] + image.red[posRow + gw]) / 2);
                        }
                    }
                }
            });
        }
    }
}
