using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;

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

        unsafe public SoftwareBitmap getImageRawAs8bitsBitmap(int width, int height, object[] curve)
        {
            //mode is BGRA because microsoft only work correctly wih this            
            SoftwareBitmap image = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height,BitmapAlphaMode.Ignore);
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
                    int diff = (int)(colorDepth) - 8;
                    for (int i = 0; i < bufferLayout.Width * bufferLayout.Height; i++)
                    {
                        //get the pixel
                        ushort temp = 0;
                        //todo check if correct
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
                         * 
                         * */
                        tempByteArray[bufferLayout.StartIndex + (i * 4)] = (byte)(temp >> diff);
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = (byte)(temp >> diff);
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = (byte)(temp >> diff);
                        tempByteArray[bufferLayout.StartIndex+(i * 4) + 3] = 255;
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
                ushort temp = 0;
                for (int k = 0; k < 8; k++)
                {
                    bool xy = imageData[(i * (int)colorDepth) + k];
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
