namespace RawEditor
{
    public enum EffectObject
    {
        reset,
        exposure,
        red, blue, green,
        contrast,
        shadow, hightlight,
        rotate,
        zoom,
        saturation
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
