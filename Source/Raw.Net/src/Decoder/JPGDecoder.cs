using Windows.UI.Xaml.Media.Imaging;

namespace RawNet
{
    internal class JPGParser : RawDecoder
    {
        WriteableBitmap image;

        public JPGParser(ref TIFFBinaryReader file) : base(ref file)
        {

        }

        /*
* Use the built in jpeg reader
*/

        /*
        public override ushort[] parseRAWImage(Stream s)
        {
        image = new WriteableBitmap(1, 1);
            image.SetSource(s.AsRandomAccessStream());
            IBuffer buffer = image.PixelBuffer;
            ushort[] raw = new ushort[image.PixelHeight * image.PixelWidth * 3];
            for (int i = 0; i < image.PixelWidth * image.PixelHeight * 3; i++)
            {
                // raw[i] = buffer;
            }
            return null;
        }
*/
    }
}