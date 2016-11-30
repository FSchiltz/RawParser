using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RawNet
{
    public class JPGParser : RawDecoder
    {
        public JPGParser(ref TIFFBinaryReader file) : base(ref file)
        {
            /*
            image = new WriteableBitmap(1, 1);
            image.SetSource(file.BaseStream);
            IBuffer buffer = image.PixelBuffer;
            ushort[] raw = new ushort[image.PixelHeight * image.PixelWidth * 3];
            for (int i = 0; i < image.PixelWidth * image.PixelHeight * 3; i++)
            {
                 //raw[i] = buffer;
            }*/
        }
    }
}