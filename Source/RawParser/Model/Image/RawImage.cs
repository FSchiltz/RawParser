using RawParser.Format.IFD;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Windows.Graphics.Imaging;

namespace RawParser.Image
{

    public class RawImage
    {
        public byte[] thumbnail;
        public uint[] previewData;
        public uint previewHeight;
        public uint previewWidth;
        public string fileName { get; set; }
        public Dictionary<ushort, Tag> exif;
        public ushort[] rawData;
        public ushort colorDepth;
        public uint height;
        public uint width;
        public byte[] cfa;
        public int saturation;
        public int dark;
        public double[] camMul;

        /*
         * Should be allows if possible
         * not efficient but allows more concise code
         * 
         */
        public ushort this[int row, int col, int k]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var a = (((row * width) + col) * 3) + k;
                if (row < 0 || row >= height || col < 0 || col >= width)
                {
                    return 0;
                }
                else return rawData[a];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                rawData[(((row * width) + col) * 3) + k] = value;
            }
        }

       
        public unsafe SoftwareBitmap getImageRawAs8bitsBitmap(object[] curve, ref int[] value)
        {
            SoftwareBitmap image = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)width, (int)height, BitmapAlphaMode.Ignore);
            using (BitmapBuffer buffer = image.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* tempByteArray;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out tempByteArray, out capacity);

                    // Fill-in the BGRA plane
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    //calculte diff between colordepth and 8
                    int diff = (colorDepth) - 8;

                    for (int i = 0; i < bufferLayout.Width * bufferLayout.Height; i++)
                    {
                        //get the pixel                    
                        ushort blue = (ushort)(rawData[(i * 3)] >> diff),
                        green = (ushort)(rawData[(i * 3) + 1] >> diff),
                        red = (ushort)(rawData[(i * 3) + 2] >> (diff));
                        if (blue > 255) blue = 255;
                        if (red > 255) red = 255;
                        if (green > 255) green = 255;
                        value[(byte)((red << 1) + (green << 2) + green + blue) >> 3]++;
                        tempByteArray[bufferLayout.StartIndex + (i * 4)] = (byte)red;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = (byte)green;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = (byte)blue;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 3] = 255;
                    }
                }
            }
            return image;
        }

        unsafe public SoftwareBitmap getImagePreviewAs8bitsBitmap(object[] curve, ref int[] value)
        {
            SoftwareBitmap image = new SoftwareBitmap(BitmapPixelFormat.Rgba8, (int)previewWidth, (int)previewHeight, BitmapAlphaMode.Ignore);
            using (BitmapBuffer buffer = image.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* tempByteArray;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out tempByteArray, out capacity);

                    // Fill-in the BGRA plane
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    //calculte diff between colordepth and 8
                    int diff = (colorDepth) - 8;

                    for (int i = 0; i < bufferLayout.Width * bufferLayout.Height; i++)
                    {
                        //get the pixel                    
                        uint blue = (previewData[(i * 3)] >> diff),
                        green = (previewData[(i * 3) + 1] >> diff),
                        red = (previewData[(i * 3) + 2] >> (diff));
                        if (blue > 255) blue = 255;
                        if (red > 255) red = 255;
                        if (green > 255) green = 255;
                        value[(byte)((red << 1) + (green << 2) + green + blue) >> 3]++;
                        tempByteArray[bufferLayout.StartIndex + (i * 4)] = (byte)red;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = (byte)green;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = (byte)blue;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 3] = 255;
                    }
                }
            }
            return image;
        }

        /*
         * For testing
         */
        public ushort[] getImageAsByteArray()
        {
            ushort[] tempByteArray = new ushort[width * height];
            for (int i = 0; i < width * height; i++)
            {
                //get the pixel
                ushort temp = rawData[(i * (int)colorDepth)];
                /*
            for (int k = 0; k < 8; k++)
            {
                bool xy = rawData[(i * (int)colorDepth) + k];
                if (xy)
                {
                    temp |= (ushort)(1 << k);
                }
            }*/
                tempByteArray[i] = temp;
            }
            return tempByteArray;
        }
    }
}
