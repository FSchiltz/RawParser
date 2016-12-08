using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;

namespace RawNet
{
    internal class JpegHelper
    {
        protected static SoftwareBitmap getJpegInArray(byte[] im)
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

    }
}
