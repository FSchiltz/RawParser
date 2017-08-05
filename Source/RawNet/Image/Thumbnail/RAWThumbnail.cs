using PhotoNet.Common;
using RawNet.Decoder;
using System;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Imaging;
using Windows.UI.Core;

namespace RawNet
{
    class RAWThumbnail : Thumbnail
    {
        public byte[] data;
        public Point2D dim;
        public uint cpp;

        public SoftwareBitmap GetBitmap()
        {
            SoftwareBitmap bitmap = null;
            //Needs to run in UI thread
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)dim.width, (int)dim.height);
            }).AsTask().Wait();

            using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Write))
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
                        tempByteArray[bufferLayout.StartIndex + (i * 4) + 2] = data[(i * cpp)];
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
            return bitmap;
        }
    }
}
