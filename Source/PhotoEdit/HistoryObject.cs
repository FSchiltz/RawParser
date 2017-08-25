namespace PhotoNet
{
    public enum EffectType
    {
        Unkown = 0,
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
        WhiteBalance,
        ReverseGamma,
        HistoEqualisation,
        AutoExposure,
        Gamma,
        Sharpness,
        Denoise,
        SplitTone

    }

    public class HistoryObject
    {
        public object value;
        public object oldValue;
        public EffectType target;
        public ImageEffect effect;

        public HistoryObject(EffectType target, ImageEffect effect)
        {
            this.target = target;
            this.effect = effect;
            value = 0.0;
            oldValue = 0.0;
        }

        public string Target
        {
            get { return target.ToString(); }
        }

        //TODO improve and replace by localisation
        public string ValueAsString
        {
            get
            {
                switch (target)
                {
                    case EffectType.WhiteBalance:
                        return "Set to default";
                    case EffectType.Crop:
                    case EffectType.Rotate:
                    case EffectType.AutoExposure:
                    case EffectType.Zoom:
                    case EffectType.Reset:
                        return "";
                    case EffectType.HistoEqualisation:
                    case EffectType.ReverseGamma:
                        return "from " + oldValue + " to " + value;
                    default:
                        return "from " + ((double)oldValue).ToString("F") + " to " + ((double)value).ToString("F");
                }
            }
        }
    }
}
