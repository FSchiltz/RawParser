using System.Runtime.CompilerServices;

namespace RawEditor.Effect
{
    class Luminance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clip(ref double red, ref double green, ref double blue, uint maxValue)
        {
            if (red > maxValue)
                red = maxValue;
            else if (red < 0)
                red = 0;

            if (green > maxValue)
                green = maxValue;
            if (green < 0)
                green = 0;

            if (blue > maxValue)
                blue = maxValue;
            else if (blue < 0)
                blue = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Clip(ref double l)
        {
            if (l > 1) l = 1;
            else if (l < 0) l = 0;
        }
    }
}
