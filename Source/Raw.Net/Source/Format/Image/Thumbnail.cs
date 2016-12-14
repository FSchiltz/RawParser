using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Imaging;
using Windows.UI.Core;

namespace RawNet
{
    public enum ThumbnailType
    {
        JPEG,
        RAW
    }

    public class Thumbnail
    {
        public byte[] data;
        public Point2D dim;
        public uint cpp;

        public ThumbnailType Type { get; set; }

        public SoftwareBitmap GetSoftwareBitmap()
        {
            if (data == null) return null;
            else if (Type == ThumbnailType.JPEG)
            {
                MemoryStream ms = new MemoryStream();
                ms.Write(data, 0, data.Length);
                ms.Position = 0; //reset the stream after populate

                var decoder = BitmapDecoder.CreateAsync(ms.AsRandomAccessStream()).AsTask();
                decoder.Wait();
                if (decoder.Status == TaskStatus.Faulted)
                {
                    ms.Dispose();
                    throw decoder.Exception;
                }
                ms.Dispose();
                var bitmapasync = decoder.Result.GetSoftwareBitmapAsync().AsTask();
                bitmapasync.Wait();
                if (bitmapasync.Status == TaskStatus.Faulted)
                {
                    throw bitmapasync.Exception;
                }
                ms.Dispose();
                return bitmapasync.Result;
            }
            else if (Type == ThumbnailType.RAW)
            {
                SoftwareBitmap bitmap = null;
                //Needs to run in UI thread
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, dim.width, dim.height);
                }).AsTask().Wait();

                using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
                {
                    using (var reference = buffer.CreateReference())
                    {
                        unsafe
                        {
                            ((IMemoryBufferByteAccess)reference).GetBuffer(out var tempByteArray, out uint capacity);

                            // Fill-in the BGRA plane
                            BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                            for (int i = 0; i < bufferLayout.Width * bufferLayout.Height; i++)
                            {
                                tempByteArray[bufferLayout.StartIndex + (i * 4)] = data[(i * cpp) + 2];
                                tempByteArray[bufferLayout.StartIndex + (i * 4) + 1] = data[(i * cpp) + 1];
                                tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = data[(i * cpp) ];
                                if (cpp == 4)
                                {
                                    tempByteArray[bufferLayout.StartIndex + (i * 4) + 3] = data[(i * 4) + 3];
                                }
                                else
                                {
                                    tempByteArray[bufferLayout.StartIndex + (i * 4) + 3] = 255;
                                }
                            }
                        }
                    }
                }

                return bitmap;
            }
            else return null;
        }
    }
}
