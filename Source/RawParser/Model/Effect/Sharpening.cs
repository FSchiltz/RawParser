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
            Parallel.For(1, buffer.dim.Height - 1, y =>
            {
                var realY = y * buffer.dim.Width;
                var beforeY = ((y - 1) * buffer.dim.Width);
                var afterY = ((y + 1) * buffer.dim.Width);
                //TODO add cache for first column (loop 2 by 2 pixel)
                for (int x = 1; x < buffer.dim.Width - 1; x++)
                {
                    var realX = realY + x;
                    var beforeRow = beforeY + x;
                    var afterRow = afterY + x;
                    buffer.red[realX] = ((mul * image.red[realX])
                        - image.red[beforeRow]
                        - image.red[afterRow]
                        - image.red[realX - 1]
                        - image.red[afterRow - 1]
                        - image.red[beforeRow - 1]
                        - image.red[realX + 1]
                        - image.red[afterRow + 1]
                        - image.red[beforeRow + 1]
                       ) / factor;

                    buffer.green[realX] = ((mul * image.green[realX])
                        - image.green[realX + 1]
                        - image.green[realX - 1]
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
            Parallel.For(0, buffer.dim.Height, y =>
            {
                var pos = y * buffer.dim.Width;

                buffer.red[pos] = image.red[pos];
                buffer.green[pos] = image.green[pos];
                buffer.blue[pos] = image.blue[pos];

                pos += image.dim.Width - 1;
                buffer.red[pos] = image.red[pos];
                buffer.green[pos] = image.green[pos];
                buffer.blue[pos] = image.blue[pos];
            });

            var p = (buffer.dim.Height - 1) * buffer.dim.Width;
            Parallel.For(0, buffer.dim.Width, x =>
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
