namespace RawEditor
{
    public enum EffectObject
    {
        Reset,
        Exposure,
        Red, Blue, Green,
        Contrast,
        Shadow,
        Hightlight,
        Rotate,
        Zoom,
        Saturation,
        Crop
    }
    public class HistoryObject
    {
        public double value;
        public double oldValue;
        public EffectObject target;
        public string Target
        {
            get { return target.ToString(); }
        }
        public string ValueAsString { get { return "Old:" + oldValue + " new:" + value; } }
    }
}
