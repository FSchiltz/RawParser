namespace PhotoNet.Common
{
    public enum ExifGroup
    {
        Parser,
        Camera,
        Image,
        Lens,
        Shot,
        GPS,
        Various
    }

    public class ExifValue
    {
        public string Name { get; set; }
        public string Data { get; set; }
        public ExifValue(string key, string value, ExifGroup group)
        {
            Name = key;
            Data = value;
            Group = group;
        }
        public ExifGroup Group { get; }

    }
}