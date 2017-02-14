using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RawNet;

namespace RawEditor.Effect
{
    static class Denoising
    {
        internal static ImageComponent<int> Apply(ImageComponent<int> image, double denoise)
        {
            //create a buffer
            ImageComponent<int> buffer = new ImageComponent<int>(image.dim, image.ColorDepth);
            int mul = 10 - (int)denoise;
            int factor = 8 + mul;

            //apply a median filtering
            Parallel.For(1, image.dim.Height - 1, y =>
            {
                long realY = y * image.dim.Width;
                for (int x = 1; x < image.dim.Width - 1; x++)
                {
                    long realX = realY + x;
                    var beforeRow = ((y - 1) * image.dim.Width) + x;
                    var afterRow = ((y + 1) * image.dim.Width) + x;
                    buffer.red[realX] = ((mul * image.red[realX])
                    + image.red[realX + 1]
                    + image.red[realX - 1]
                    + image.red[afterRow]
                    + image.red[afterRow + 1]
                    + image.red[afterRow - 1]
                    + image.red[beforeRow]
                    + image.red[beforeRow + 1]
                    + image.red[beforeRow - 1]) / 9;

                    buffer.green[realX] = ((mul * image.green[realX])
                    + image.green[realX + 1]
                    + image.green[realX - 1]
                    + image.green[afterRow]
                    + image.green[afterRow + 1]
                    + image.green[afterRow - 1]
                    + image.green[beforeRow]
                    + image.green[beforeRow + 1]
                    + image.green[beforeRow - 1]) / 9;

                    buffer.blue[realX] = ((mul * image.blue[realX])
                    + image.blue[realX + 1]
                    + image.blue[realX - 1]
                    + image.blue[afterRow]
                    + image.blue[afterRow + 1]
                    + image.blue[afterRow - 1]
                    + image.blue[beforeRow]
                    + image.blue[beforeRow + 1]
                    + image.blue[beforeRow - 1]) / 9;
                }
            });

            return buffer;
        }
    }
}
