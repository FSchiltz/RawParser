﻿using System;
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
                    if (area.Offset + area.Size > image.raw.UncroppedDim.height)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than height of image");
                    for (uint y = area.Offset; y < area.Offset + area.Size; y++)
                    {
                        ushort[] pixel = image.preview.rawView.Skip((int)(image.raw.offset.width + image.raw.dim.width * y)).ToArray();
                        int[] localhist = histogram.Skip((int)(y & 1) * (65536 * 2)).ToArray();
                        for (uint x = image.raw.offset.width; x < image.raw.dim.width + image.raw.offset.width; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * image.raw.dim.width;
                }

                // Process vertical area 
                if (area.IsVertical)
                {
                    if (area.Offset + area.Size > image.raw.UncroppedDim.width)
                        throw new RawDecoderException("RawImageData::calculateBlackAreas: Offset + size is larger than width of image");
                    for (uint y = image.raw.offset.height; y < image.raw.dim.height + image.raw.offset.height; y++)
                    {
                        ushort[] pixel = image.preview.rawView.Skip((int)(area.Offset + image.raw.dim.width * y)).ToArray();
                        int[] localhist = histogram.Skip((int)(y & 1) * (65536 * 2)).ToArray();
                        for (uint x = area.Offset; x < area.Size + area.Offset; x++)
                        {
                            localhist[((x & 1) << 16) + pixel[0]]++;
                        }
                    }
                    totalpixels += area.Size * image.raw.dim.height;
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
                Parallel.For(image.raw.offset.height, image.raw.dim.height + image.raw.offset.height, y =>
                {
                    long pos = y * image.raw.UncroppedDim.width;
                    for (uint x = image.raw.offset.width; x < (image.raw.offset.width + image.raw.dim.width) * image.raw.cpp; x++)
                    {
                        long value = (long)((image.raw.rawView[pos + x] - image.black) * factor);
                        if (value > image.whitePoint) value = maxValue;
                        else if (value < 0) value = 0;
                        image.raw.rawView[x + pos] = (ushort)value;
                    }
                });
            }
        }

        /**
      * Create a preview of the raw image using the scaling factor
      * The X and Y dimension will be both divided by the image
      * 
      */
        public static void CreatePreview(FactorValue factor, double viewHeight, double viewWidth, RawImage<ushort> image)
        {
            //image will be size of windows
            uint previewFactor = 0;
            if (factor == FactorValue.Auto)
            {
                if (image.raw.dim.height > image.raw.dim.width)
                {
                    previewFactor = (uint)((image.raw.dim.height / viewHeight) * 0.9);
                }
                else
                {
                    previewFactor = (uint)((image.raw.dim.width / viewWidth) * 0.9);
                }
                if (previewFactor < 1)
                {
                    previewFactor = 1;
                }
            }
            else
            {
                previewFactor = (uint)factor;
            }

            image.preview = new ImageComponent<ushort>(new Point2D(image.raw.dim.width / previewFactor, image.raw.dim.height / previewFactor), image.raw.ColorDepth);
            uint doubleFactor = previewFactor * previewFactor;
            ushort maxValue = (ushort)((1 << image.raw.ColorDepth) - 1);
            //loop over each block
            Parallel.For(0, image.preview.dim.height, y =>
            {
                var posY = (y * image.preview.dim.width);
                var rY = (y * previewFactor);
                for (int x = 0; x < image.preview.dim.width; x++)
                {
                    var posX = posY + x;
                    //find the mean of each block
                    long r = 0, g = 0, b = 0;
                    var rX = (x * previewFactor);
                    for (int i = 0; i < previewFactor; i++)
                    {
                        long realY = (image.raw.dim.width * (rY + i)) + rX;
                        for (int k = 0; k < previewFactor; k++)
                        {
                            long realX = realY + k;
                            r += image.raw.red[realX];
                            g += image.raw.green[realX];
                            b += image.raw.blue[realX];
                        }
                    }
                    image.preview.red[posX] = (ushort)(r / doubleFactor);
                    image.preview.green[posX] = (ushort)(g / doubleFactor);
                    image.preview.blue[posX] = (ushort)(b / doubleFactor);
                }
            });
        }
    }
}
