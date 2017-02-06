namespace RawEditor.Settings
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
        Crop,
        WB,
        ReverseGamma,
        HistoEqualisation
    }

    public class HistoryObject
    {
        public object value;
        public object oldValue;
        public EffectObject target;
        public string Target
        {
            get { return target.ToString(); }
        }
        //TODO improve aand replace by localisation
        public string ValueAsString { get { return "Old:" + oldValue + " new:" + value; } }
    }
}
