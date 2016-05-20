using RawParser.Model.Format;
using RawParserUWP.Model.Exception;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;

namespace RawParser.Model.ImageDisplay
{

    class RawImage
    {
        private string fileName {get;set;}
        public Dictionary<ushort, Tag> exif;
        public byte[] imageData { set; get; }
        public byte[] imagePreviewData { get; set; }

        public RawImage(Dictionary<ushort, Tag> e ,byte[] d, byte[] p)
        {
            exif = e;
            imageData = d;
            imagePreviewData = p;           
        }

        internal SoftwareBitmap getImageAsBitmap()
        {
            MemoryStream ms = new MemoryStream();
            ms.Write(imagePreviewData, 0, imagePreviewData.Length);
            ms.Position = 0; //reset the stream after populate
            var decoder = BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
            
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
                                     
            return bitmapasync.GetResults(); ;            
        }
    }
}
