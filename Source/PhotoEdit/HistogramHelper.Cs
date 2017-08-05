using System.Threading.Tasks;
using System;
using PhotoNet.Common;

namespace PhotoNet
{
    static class HistogramHelper
    {
        internal static HistoRaw CalculateHistogram(ImageComponent<int> image)
        {
            var maxValue = 1 << image.ColorDepth;
            HistoRaw histogram = new HistoRaw()
            {
                luma = new int[maxValue],
                red = new int[maxValue],
                blue = new int[maxValue],
                green = new int[maxValue]
            };

            for (int y = 0; y < image.dim.height; y++)
            {
                long realY = (y + image.offset.height) * image.UncroppedDim.width;
                for (int x = 0; x < image.dim.width; x++)
                {
                    long realPix = realY + x + image.offset.width;
                    histogram.luma[(int)(0.299 * image.red[realPix] + 0.587 * image.green[realPix] + 0.114 * image.blue[realPix])]++;
                    histogram.red[image.red[realPix]]++;
                    histogram.green[image.green[realPix]]++;
                    histogram.blue[image.blue[realPix]]++;
                }
            }
            return histogram;
        }

        internal static HistoRaw CalculateLumaHistogram(ImageComponent<int> image)
        {
            HistoRaw histogram = new HistoRaw()
            {
                luma = new int[(1 << image.ColorDepth)]
            };

            for (int y = 0; y < image.dim.height; y++)
            {
                long realY = (y + image.offset.height) * image.UncroppedDim.width;
                for (int x = 0; x < image.dim.width; x++)
                {
                    long realPix = realY + x + image.offset.width;

                    histogram.luma[(int)((Math.Max(Math.Max(image.red[realPix], image.green[realPix]), image.blue[realPix])
                        + Math.Min(Math.Min(image.red[realPix], image.green[realPix]), image.blue[realPix])) / 2.0)]++;
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
            double pixelCount = image.dim.height * image.dim.width;

            int sum = 0;
            // build a LUT containing scale factor
            for (int i = 0; i < lut.Length; ++i)
            {
                sum += histogram.luma[i];
                lut[i] = (int)(sum * 255 / pixelCount);
            }
            // transform image using sum histogram as a LUT
            Parallel.For(0, image.dim.height, y =>
            {
                long realY = y * image.dim.width;
                for (int x = 0; x < image.dim.width; x++)
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
