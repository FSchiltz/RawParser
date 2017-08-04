using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace RawNet
{
    public class JPEGThumbnail : Thumbnail
    {
        public byte[] data;

        public JPEGThumbnail(byte[] v)
        {
            data = v;
        }

        public SoftwareBitmap GetBitmap()
        {
            if (data == null) return null;
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
            var bitmapasync = decoder.Result.GetSoftwareBitmapAsync().AsTask();
            bitmapasync.Wait();
            if (bitmapasync.Status == TaskStatus.Faulted)
            {
                ms.Dispose();
                throw bitmapasync.Exception;
            }
            ms.Dispose();
            return bitmapasync.Result;
        }
    }
}
