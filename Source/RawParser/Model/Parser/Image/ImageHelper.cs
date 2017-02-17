using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RawNet
{
    static class ImageHelper
    {

        internal static void CalculateBlackArea(RawImage<ushort> image)
        {
            Debug.Assert(image.black == 0);
            Debug.Assert(image.blackAreas.Count > 0);
            int[] histogram = new int[4 * 65536 * sizeof(int)];
            uint totalpixels = 0;
            for (int i = 0; i < image.blackAreas.Count; i++)
            {
                BlackArea area = image.blackAreas[i];

                // Make sure area sizes are multiple of two, 
                //  so we have the same amount of pixels for each CFA group 
                area.Size = area.Size - (area.Size & 1);

                // Process horizontal area 
                if (!area.IsVertical)
                {
                    if (area.Offset + area.Size > image.raw.UncroppedDim.Height)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than height of image");
                    for (uint y = area.Offset; y < area.Offset + area.Size; y++)
                    {
                        ushort[] pixel = image.preview.rawView.Skip((int)(image.raw.offset.Width + image.raw.dim.Width * y)).ToArray();
                        int[] localhist = histogram.Skip((int)(y & 1) * (65536 * 2)).ToArray();
                        for (uint x = image.raw.offset.Width; x < image.raw.dim.Width + image.raw.offset.Width; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * image.raw.dim.Width;
                }

                // Process vertical area 
                if (area.IsVertical)
                {
                    if (area.Offset + area.Size > image.raw.UncroppedDim.Width)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than width of image");
                    for (uint y = image.raw.offset.Height; y < image.raw.dim.Height + image.raw.offset.Height; y++)
                    {
                        ushort[] pixel = image.preview.rawView.Skip((int)(area.Offset + image.raw.dim.Width * y)).ToArray();
                        int[] localhist = histogram.Skip((int)(y & 1) * (65536 * 2)).ToArray();
                        for (uint x = area.Offset; x < area.Size + area.Offset; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * image.raw.dim.Height;
                }
            }

            int acc_pixels = histogram[0];
            int pixel_value = 0;
            while (acc_pixels <= totalpixels && pixel_value < 65535)
            {
                pixel_value++;
                acc_pixels += histogram[pixel_value];
            }
            image.black = pixel_value;
            Debug.Assert(image.black <= image.whitePoint);

        }

        //TODO Move to the RawNet Namespace
        public static void ScaleValues(RawImage<ushort> image)
        {
            Debug.Assert(image.raw.cpp == 1);
            Debug.Assert(Convert.ToInt32(image.whitePoint) > 0);

            long maxValue = (1 << image.raw.ColorDepth) - 1;

            //calculate the black level
            if (image.black == 0 && image.blackAreas.Count > 0) CalculateBlackArea(image);
            if (image.whitePoint == 0 || image.whitePoint > maxValue) image.whitePoint = maxValue;
            double factor = maxValue / (double)(image.whitePoint - image.black);

            Debug.Assert(image.black < image.whitePoint);
            Debug.Assert(image.whitePoint <= maxValue);

            if (image.black != 0 || image.whitePoint != maxValue)
            {
                Parallel.For(image.raw.offset.Height, image.raw.dim.Height + image.raw.offset.Height, y =>
                {
                    long pos = y * image.raw.UncroppedDim.Width;
                    for (uint x = image.raw.offset.Width; x < (image.raw.offset.Width + image.raw.dim.Width) * image.raw.cpp; x++)
                    {
                        long value = (long)((image.raw.rawView[pos + x] - image.black) * factor);
                        if (value > image.whitePoint) value = maxValue;
                        else if (value < 0) value = 0;
                        image.raw.rawView[x + pos] = (ushort)value;
                    }
                });
            }
        }
    }
}
