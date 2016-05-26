using RawParser.Model.Format;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace RawParser.Model.ImageDisplay
{

    class RawImage
    {
        private string fileName { get; set; }
        public Dictionary<ushort, Tag> exif { get; set; }
        public BitArray imageData { set; get; }
        public ushort colorDepth;
        public byte[] imagePreviewData { get; set; }
        public uint height;
        public uint width;

        public RawImage(Dictionary<ushort, Tag> e, BitArray d, byte[] p, uint h, uint w, ushort c)
        {
            exif = e;
            imageData = d;
            imagePreviewData = p;
            height = h;
            width = w;
            colorDepth = c;
        }

        public SoftwareBitmap getImagePreviewAsBitmap()
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(imagePreviewData, 0, imagePreviewData.Length);

            ms.Position = 0; //reset the stream after populate
            return getBitmapFromStream(ms.AsRandomAccessStream());
        }

        public SoftwareBitmap getImageAsBitmap()
        {
            SoftwareBitmap image = new SoftwareBitmap(BitmapPixelFormat.Gray16, (int)width, (int)height);
            Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(width * height * 2);

            byte[] tempByteArray = new byte[width * height * 2];
            throw new NotImplementedException();
            for (int i = 0; i < width * height * 2; i++)
            {
                //get the pixel
                byte firstBits;
                byte lastBits;

                int k = 0;
                for (; k < 8; k++)
                {

                }

                for (; k < colorDepth; k++)
                {

                }

                tempByteArray[i] = firstBits;
                tempByteArray[++i] = lastBits;

            }


            image.CopyFromBuffer(buffer);
            return image;
        }

        internal SoftwareBitmap getBitmapFromStream(IRandomAccessStream ms)
        {
            var decoder = BitmapDecoder.CreateAsync(ms);

            Task t = Task.Run(() =>
            {
                while (decoder.Status == AsyncStatus.Started) { }
            });
            t.Wait();
            if (decoder.Status == AsyncStatus.Error)
            {
                throw decoder.ErrorCode;
            }

            var bitmapasync = decoder.GetResults().GetSoftwareBitmapAsync();
            t = Task.Run(() =>
            {
                while (bitmapasync.Status == AsyncStatus.Started) { }
            });
            t.Wait();
            if (bitmapasync.Status == AsyncStatus.Error)
            {
                throw bitmapasync.ErrorCode;
            }

            return bitmapasync.GetResults();
        }
    }
}
