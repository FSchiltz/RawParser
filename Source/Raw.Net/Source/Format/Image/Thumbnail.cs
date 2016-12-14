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
        public Point2D dim { get; set; }
        public ThumbnailType type { get; set; }
    }
}
