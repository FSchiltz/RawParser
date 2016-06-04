using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace RawParserUWP.Model.Format.Image
{

    public class RawImage
    {
        public byte[] thumbnail, previewImage;
        public string fileName { get; set; }
        public Dictionary<ushort, Tag> exif;
        public ushort[] imageData;
        public ushort colorDepth;
        public uint height;
        public uint width;
        public byte[] cfa;

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
                if (a < imageData.Length
                    && a >= 0)
                {
                    return imageData[a];
                }
                else return 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                imageData[((row * width) + col) * 3] = value;
            }
        }

        public static SoftwareBitmap getImageAsBitmap(byte[] im)
        {
            if (im == null) return null;
            Task t;
            IAsyncOperation<BitmapDecoder> decoder;
            IAsyncOperation<SoftwareBitmap> bitmapasync;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(im, 0, im.Length);
                ms.Position = 0; //reset the stream after populate

                decoder = BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());

                t = Task.Run(() =>
                {
                    while (decoder.Status == AsyncStatus.Started)
                    {
                    }
                });
                t.Wait();
                if (decoder.Status == AsyncStatus.Error)
                {
                    throw decoder.ErrorCode;
                }

                bitmapasync = decoder.GetResults().GetSoftwareBitmapAsync();

                t = Task.Run(() =>
                {
                    while (bitmapasync.Status == AsyncStatus.Started)
                    {
                    }
                });
                t.Wait();
                if (bitmapasync.Status == AsyncStatus.Error)
                {
                    throw bitmapasync.ErrorCode;
                }
            }
            return bitmapasync.GetResults();
        }

        unsafe public SoftwareBitmap getImageRawAs8bitsBitmap(object[] curve, ref int[] value)
        {
            //mode is BGRA because microsoft only work correctly wih this            
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
                        ushort blue = 0, green = 0, red = 0;
                        blue = imageData[(i * 3)];
                        green = imageData[(i * 3) + 1];
                        red = imageData[(i * 3) + 2];
                        /*
                        for (int k = 0; k < colorDepth; k++)
                        {
                            if (imageData[(i * 3 * colorDepth) + k])
                            {
                                red |= (ushort)(1 << k);
                            }
                        }
                        for (int k = 0; k < colorDepth; k++)
                        {
                            if (imageData[(i * 3 * colorDepth) + colorDepth + k])
                            {
                                green |= (ushort)(1 << k);
                            }
                        }
                        for (int k = 0; k < colorDepth; k++)
                        {
                            if (imageData[(i * 3 * colorDepth) + (2 * colorDepth) + k])
                            {
                                blue |= (ushort)(1 << k);
                            }
                        }
                        */
                        //TODO get correct luminance
                        //value[(blue + red + green) / 3] += 1;

                        /*
                         * For the moment no curve
                         * TODO apply a curve given in input
                         * 
                         * */
                        /*
                       byte redB, greenB, blueB;
                       if (red < 255) redB = (byte)(red >> 4);
                       else if (red < 4095) redB =(byte)( red >> 5);
                       else redB = (byte)(red >> 6);

                       if (green < 255) greenB = (byte)(green >> 4);
                       else if (green < 4095) greenB = (byte)(green >> 5);
                       else greenB = (byte)(green >> 6);

                       if (blue < 255) blueB = (byte)(blue >> 4);
                       else if (red < 4095) blueB = (byte)(blue >> 5);
                       else blueB = (byte)(blue >> 6);
                       */
                        double redD = red / (255 * 64), greenD = green / (255 * 64), blueD = blue / (255 * 64);

                        //Center pixel values at 0, so that the range is -0.5 to 0.5
                        redD -= 0.5f;
                        greenD -= 0.5f;
                        blueD -= 0.5f;
                        //Multiply and just by the contrast ratio, this distances the color
                        //distributing right at the center....see histogram for further details
                        double contrast = 2.25;
                        redD *= contrast;
                        greenD *= contrast;
                        blueD *= contrast;
                        //change back to a 0-1 range
                        redD += 0.5f;
                        greenD += 0.5f;
                        blueD += 0.5f;
                        //and back to 0-255                         
                        redD *= 255;
                        greenD *= 255;
                        blueD *= 255;

                        if (redD > 255)
                            redD = 255;
                        else if (redD < 0)
                            redD = 0;

                        if (greenD > 255)
                            greenD = 255;
                        else if (greenD < 0)
                            greenD = 0;

                        if (blueD > 255)
                            blueD = 255;
                        else if (blueD < 0)
                            blueD = 0;
                        int redI = (int)redD, greenI = (int)greenD, blueI = (int)blueD;

                        tempByteArray[bufferLayout.StartIndex + (i * 4)] = (byte)redI;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = (byte)greenI;
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = (byte)blueI;
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
                ushort temp = imageData[(i * (int)colorDepth)];
                /*
            for (int k = 0; k < 8; k++)
            {
                bool xy = imageData[(i * (int)colorDepth) + k];
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
