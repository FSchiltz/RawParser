using System.Threading.Tasks;
using RawNet;
using RawEditor.View.UIHelper;
namespace RawEditor.Effect
{
    static class HistogramHelper
    {
        internal static HistoRaw CalculateHistogram(ImageComponent<int> image, uint colorDepth2)
        {
            var maxValue = 1 << (int)colorDepth2;
            HistoRaw histogram = new HistoRaw()
            {
                luma = new int[maxValue],
                red = new int[maxValue],
                blue = new int[maxValue],
                green = new int[maxValue]
            };
            //calculte the shift between colordepth input and output
            int shift = (int)(image.ColorDepth - colorDepth2);

            for (int y = 0; y < image.dim.Height; y++)
            {
                long realY = (y + image.offset.Height) * image.UncroppedDim.Width;
                for (int x = 0; x < image.dim.Width; x++)
                {
                    long realPix = realY + x + image.offset.Width;
                    //TODO add back luminance hsitogram
                    histogram.red[image.red[realPix] >> shift]++;
                    histogram.green[image.green[realPix] >> shift]++;
                    histogram.blue[image.blue[realPix] >> shift]++;
                }
            }
            return histogram;
        }

        internal static void HistogramEqualisation(ImageComponent<int> image, HistoRaw histogram)
        {
            //TODO change and correct

            //apply histogram equalisation if needed using the histogram
            //create a lookup table
            var lut = new int[uint.MaxValue];
            double pixelCount = image.dim.Height * image.dim.Width;

            int sum = 0;
            // build a LUT containing scale factor
            for (int i = 0; i < lut.Length; ++i)
            {
                sum += histogram.luma[i];
                lut[i] = (int)(sum * 255 / pixelCount);
            }
            // transform image using sum histogram as a LUT
            Parallel.For(0, image.dim.Height, y =>
            {
                long realY = y * image.dim.Width;
                for (int x = 0; x < image.dim.Width; x++)
                {
                    long realX = realY + x;
                    image.red[realX] = lut[image.red[realX]];
                    image.green[realX] = lut[image.green[realX]];
                    image.blue[realX] = lut[image.blue[realX]];
                }
            });
        }
    }
}
