using System;
using System.Runtime.CompilerServices;
using RawNet;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace RawEditor.Effect
{
    static class Luminance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clip(ref double red, ref double green, ref double blue, uint maxValue)
        {
            if (red > maxValue)
                red = maxValue;
            else if (red < 0)
                red = 0;

            if (green > maxValue)
                green = maxValue;
            if (green < 0)
                green = 0;

            if (blue > maxValue)
                blue = maxValue;
            else if (blue < 0)
                blue = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Clip(ref double l)
        {
            if (l > 1) l = 1;
            else if (l < 0) l = 0;
        }

        internal static void Clip(ImageComponent<int> image)
        {
            var maxValue = (1 << image.ColorDepth) - 1;
            Parallel.For(0, image.dim.Height, y =>
            {
                long realY = y * image.dim.Width;
                for (int x = 0; x < image.dim.Width; x++)
                {
                    long realPix = realY + x;
                    var red = image.red[realPix];
                    var green = image.green[realPix];
                    var blue = image.blue[realPix];
                    if (red < 0) red = 0;
                    else if (red > maxValue) red = maxValue;

                    if (green < 0) green = 0;
                    else if (green > maxValue) green = maxValue;

                    if (blue < 0) blue = 0;
                    else if (blue > maxValue) blue = maxValue;

                    image.red[realPix] = red;
                    image.green[realPix] = green;
                    image.blue[realPix] = blue;
                }
            });
        }

        internal static void Clip(ImageComponent<int> image, ushort colorDepth)
        {
            var maxValue = (1 << colorDepth) - 1;
            var shift = image.ColorDepth - colorDepth;
            image.ColorDepth = colorDepth;
            Parallel.For(0, image.dim.Height, y =>
            {
                long realY = y * image.dim.Width;
                for (int x = 0; x < image.dim.Width; x++)
                {
                    long realPix = realY + x;
                    var red = image.red[realPix] >> shift;
                    var green = image.green[realPix] >> shift;
                    var blue = image.blue[realPix] >> shift;
                    if (red < 0) red = 0;
                    else if (red > maxValue) red = maxValue;

                    if (green < 0) green = 0;
                    else if (green > maxValue) green = maxValue;

                    if (blue < 0) blue = 0;
                    else if (blue > maxValue) blue = maxValue;

                    image.red[realPix] = red;
                    image.green[realPix] = green;
                    image.blue[realPix] = blue;
                }
            });
        }

        internal static void CalculateBlackArea(RawImage<ushort> image)
        {
            int[] histogram = new int[4 * 65536 * sizeof(int)];
            uint totalpixels = 0;
            image.black = 2048;
            return;
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

        }

        //TODO Move to the RawNet Namespace
        public static void ScaleValues(RawImage<ushort> image)
        {
            Debug.Assert(image.raw.cpp == 1);
            Debug.Assert(Convert.ToInt32(image.whitePoint) > 0);
            long maxValue = 1 << image.raw.ColorDepth;
            if (image.whitePoint == 0) image.whitePoint = maxValue - 1;
            if (image.black == 0) CalculateBlackArea(image);
            double factor = maxValue / (double)(image.whitePoint - image.black);
            maxValue--;
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
