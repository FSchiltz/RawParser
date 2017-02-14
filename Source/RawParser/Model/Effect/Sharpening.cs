using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RawNet;

namespace RawEditor.Effect
{
    static class Sharpening
    {
        internal static ImageComponent<int> Apply(ImageComponent<int> image, int sharpness)
        {
            //create a buffer
            ImageComponent<int> buffer = new ImageComponent<int>(image.dim, image.ColorDepth);
            int factor = 11 - sharpness;
            int mul = 8 + factor;
            //sharpen using a unsharp mask
            Parallel.For(1, image.dim.Height - 1, y =>
            {
                var realY = y * image.dim.Width;
                for (int x = 1; x < image.dim.Width - 1; x++)
                {
                    var realX = realY + x;
                    var beforeRow = ((y - 1) * image.dim.Width) + x;
                    var afterRow = ((y + 1) * image.dim.Width) + x;
                    buffer.red[realX] = ((mul * image.red[realX])
                        - image.red[(realX + 1)]
                        - image.red[(realX - 1)]
                        - image.red[afterRow]
                        - image.red[afterRow + 1]
                        - image.red[afterRow - 1]
                        - image.red[beforeRow]
                        - image.red[beforeRow + 1]
                        - image.red[beforeRow - 1]) / factor;

                    buffer.green[realX] = ((mul * image.green[realX])
                        - image.green[realY + x + 1]
                        - image.green[realY + x - 1]
                        - image.green[afterRow]
                        - image.green[afterRow + 1]
                        - image.green[afterRow - 1]
                        - image.green[beforeRow]
                        - image.green[beforeRow + 1]
                        - image.green[beforeRow - 1]) / factor;

                    buffer.blue[realX] = ((mul * image.blue[realX])
                        - image.blue[realX + 1]
                        - image.blue[realX - 1]
                        - image.blue[afterRow]
                        - image.blue[afterRow + 1]
                        - image.blue[afterRow - 1]
                        - image.blue[beforeRow]
                        - image.blue[beforeRow + 1]
                        - image.blue[beforeRow - 1]) / factor;
                }
            });

            //fill in the edge
            Parallel.For(0, image.dim.Height, y =>
            {
                var pos = y * image.dim.Width;
                buffer.red[pos] = image.red[pos];
                buffer.green[pos] = image.green[pos];
                buffer.blue[pos] = image.blue[pos];
                pos += image.dim.Width - 1;
                buffer.red[pos] = image.red[pos];
                buffer.green[pos] = image.green[pos];
                buffer.blue[pos] = image.blue[pos];
            });

            var p = (image.dim.Height - 1) * image.dim.Width;
            Parallel.For(0, image.dim.Width, x =>
            {
                buffer.red[x] = image.red[x];
                buffer.green[x] = image.green[x];
                buffer.blue[x] = image.blue[x];

                buffer.red[p] = image.red[p];
                buffer.green[p] = image.green[p];
                buffer.blue[p] = image.blue[p];
                p++;
            });

            return buffer;
        }
    }
}
