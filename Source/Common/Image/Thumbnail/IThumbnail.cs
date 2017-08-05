using Windows.Graphics.Imaging;

namespace PhotoNet.Common
{
    public interface Thumbnail
    {
        SoftwareBitmap GetBitmap();
    }
}