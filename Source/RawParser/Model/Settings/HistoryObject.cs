using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawEditor
{
    public enum EffectObject
    {
        saturation,
        exposure,
        red, blue, green,
        contrast,
        shadow, hightlight,
        rotate,
        zoom
    }
    public class HistoryObject
    {
        public double value;
        public double oldValue;
        public EffectObject target;
    }
}
