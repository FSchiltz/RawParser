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
        internal static ImageComponent<int> Apply(ImageComponent<int> buffer, double sharpness)
        {
            //create a buffer
            ImageComponent<int> noisebuffer = new ImageComponent<int>(buffer.dim, buffer.ColorDepth);
            //sharpen using a unsharp mask
            Parallel.For(1, buffer.dim.Height - 1, y =>
            {
                var realY = y * buffer.dim.Width;
                for (int x = 1; x < buffer.dim.Width - 1; x++)
                {
                    var realX = realY + x;
                    var beforeRow = ((y - 1) * buffer.dim.Width) + x;
                    var afterRow = ((y + 1) * buffer.dim.Width) + x;
                    noisebuffer.red[realX] = (9 * buffer.red[realX])
                       + (-1 * buffer.red[(realX + 1)])
                       + (-1 * buffer.red[(realX - 1)])
                       + (-1 * buffer.red[afterRow])
                       + (-1 * buffer.red[afterRow + 1])
                       + (-1 * buffer.red[afterRow - 1])
                       + (-1 * buffer.red[beforeRow])
                       + (-1 * buffer.red[beforeRow + 1])
                       + (-1 * buffer.red[beforeRow - 1]);

                    noisebuffer.green[realX] = (9 * buffer.green[realX])
                       + (-1 * buffer.green[realY + x + 1])
                       + (-1 * buffer.green[realY + x - 1])
                       + (-1 * buffer.green[afterRow])
                       + (-1 * buffer.green[afterRow + 1])
                       + (-1 * buffer.green[afterRow - 1])
                       + (-1 * buffer.green[beforeRow])
                       + (-1 * buffer.green[beforeRow + 1])
                       + (-1 * buffer.green[beforeRow - 1]);

                    noisebuffer.blue[realX] = (9 * buffer.blue[realX])
                       + (-1 * buffer.blue[realX + 1])
                       + (-1 * buffer.blue[realX - 1])
                       + (-1 * buffer.blue[afterRow])
                       + (-1 * buffer.blue[afterRow + 1])
                       + (-1 * buffer.blue[afterRow - 1])
                       + (-1 * buffer.blue[beforeRow])
                       + (-1 * buffer.blue[beforeRow + 1])
                       + (-1 * buffer.blue[beforeRow - 1]);
                }
            });

            return noisebuffer;
        }
    }
}
