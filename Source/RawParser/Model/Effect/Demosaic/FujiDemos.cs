using RawNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawEditor.Effect
{
    static class FujiDemos
    {
        static public void Demosaic(RawImage<ushort>  image)
        {
            //short[,] greenPosition = new short[,] { { 0, 0 }, { 0, 2 }, { 2, 0 }, { 2, 2 } };//position for green whithout the center
            //short[,] colorPosition = new short[,] { { 0, 1 }, { 2, 1 }, { 1, 0 }, { 1, 2 } };

            //demosaic by group of 3x3
            Parallel.For(0, image.raw.dim.height / 3, t =>
            {
                long top = t * 3;
                for (long left = 0; left < image.raw.dim.width - 19; left += 3)
                {
                    //interpolate the green
                    long pos = (top * image.raw.dim.width) + left;
                    image.raw.green[pos + 1] = (ushort)((image.raw.green[pos + 2] + image.raw.green[pos]) / 2);
                    pos = ((top + 2) * image.raw.dim.width) + left;
                    image.raw.green[pos + 1] = (ushort)((image.raw.green[pos + 2] + image.raw.green[pos]) / 2);

                    pos = ((top + 1) * image.raw.dim.width) + left;
                    image.raw.green[pos] = (ushort)(
                         (image.raw.green[((top) * image.raw.dim.width) + left] + image.raw.green[((top + 2) * image.raw.dim.width) + left]) / 2);
                    image.raw.green[pos] = (ushort)(
                         (image.raw.green[((top) * image.raw.dim.width) + left + 2] + image.raw.green[((top + 2) * image.raw.dim.width) + left + 2]) / 2);

                    //interpolate the red and blue for the center pixel
                    pos = ((top + 1) * image.raw.dim.width) + left + 1;
                    long topcolor = ((top) * image.raw.dim.width) + left + 1;
                    long bottomcolor = ((top + 2) * image.raw.dim.width) + left + 1;
                    long leftcolor = ((top + 1) * image.raw.dim.width) + left;
                    long rightcolor = ((top + 1) * image.raw.dim.width) + left + 2;
                    if (image.colorFilter.cfa[((top % 6) * 6) + ((left + 1) % 6)] == CFAColor.Red)
                    {
                        //top pixel is red
                        image.raw.red[pos] = (ushort)((image.raw.red[topcolor] + image.raw.red[bottomcolor]) / 2);
                        image.raw.blue[pos] = (ushort)((image.raw.blue[leftcolor] + image.raw.blue[rightcolor]) / 2);

                        //interpolate the red and blue for the other pixel
                        pos = top * image.raw.dim.width + left;
                        image.raw.red[pos] = image.raw.red[topcolor];
                        image.raw.blue[pos] = image.raw.blue[leftcolor];

                        pos += 2;
                        image.raw.red[pos] = image.raw.red[topcolor];
                        image.raw.blue[pos] = image.raw.blue[rightcolor];

                        pos = (top + 2) * image.raw.dim.width + left;
                        image.raw.red[pos] = image.raw.red[bottomcolor];
                        image.raw.blue[pos] = image.raw.blue[leftcolor];

                        pos += 2;
                        image.raw.red[pos] = image.raw.red[bottomcolor];
                        image.raw.blue[pos] = image.raw.blue[rightcolor];

                    }
                    else
                    {
                        //top pixel is blue
                        image.raw.blue[pos] = (ushort)((image.raw.blue[topcolor] + image.raw.blue[bottomcolor]) / 2);
                        image.raw.red[pos] = (ushort)((image.raw.red[leftcolor] + image.raw.red[rightcolor]) / 2);

                        //interpolate the red and blue for the other pixel
                        pos = top * image.raw.dim.width + left;
                        image.raw.blue[pos] = image.raw.blue[topcolor];
                        image.raw.red[pos] = image.raw.red[leftcolor];

                        pos += 2;
                        image.raw.blue[pos] = image.raw.blue[topcolor];
                        image.raw.red[pos] = image.raw.red[rightcolor];

                        pos = (top + 2) * image.raw.dim.width + left;
                        image.raw.blue[pos] = image.raw.blue[bottomcolor];
                        image.raw.red[pos] = image.raw.red[leftcolor];

                        pos += 2;
                        image.raw.blue[pos] = image.raw.blue[bottomcolor];
                        image.raw.red[pos] = image.raw.red[rightcolor];
                    }
                }
            });
        }
    }
}
