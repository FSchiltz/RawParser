using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace RawParserUWP.Model.Format.Image
{

    public class RawImage
    {
        public byte[] thumbnail, previewImage;
        public string fileName { get; set; }
        public Dictionary<ushort, Tag> exif { get; set; }
        public BitArray imageData { set; get; }
        public ushort colorDepth;
        public uint height;
        public uint width;

        public static SoftwareBitmap getImageAsBitmap(byte[] im)
        {
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

        public SoftwareBitmap getImageRawAs8bitsBitmap()
        {
            SoftwareBitmap image = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)width, (int)height, BitmapAlphaMode.Ignore);
            byte[] tempByteArray = new byte[width * height * 4];
            //R + G + B + A => 4 bytes per pixel

            //calculte diff between colordepth and 8
            int diff = colorDepth - 7;

            for (int i = 0; i < width * height; i++)
            {
                //get the pixel
                ushort temp = 0;
                for (int k = 0; k < colorDepth; k++)
                {
                    if (imageData[(i * colorDepth) + k])
                    {
                        temp |= (ushort)(1 << k);
                    }
                }

                /*
                 * For the moment no curve
                 * TODO apply a curve given in input
                 * */
                tempByteArray[(i * 4)] = (byte)(temp >> diff);
                tempByteArray[(i * 4) + 1] = 255;
                tempByteArray[(i * 4) + 2] = 255;
                tempByteArray[(i * 4) + 3] = 255;
            }
            image.CopyFromBuffer(tempByteArray.AsBuffer());
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
                ushort temp = 0;
                for (int k = 0; k < 8; k++)
                {
                    bool xy = imageData[(i * colorDepth) + k];
                    if (xy)
                    {
                        temp |= (ushort)(1 << k);
                    }
                }
                tempByteArray[i] = temp;
            }
            return tempByteArray;
        }
    }
}
