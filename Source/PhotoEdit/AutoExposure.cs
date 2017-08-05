using PhotoNet.Common;
using System;
using Windows.UI.Xaml.Media;

namespace PhotoNet
{
    public static class AutoExposure
    {
        public static ImageEffect Get(ImageComponent<ushort> preview, PointCollection histo)
        {
            //find the exposure shift needed
            //caclute the mean
            double mean = 0, count = 0;
            for (int i = 0; i < histo.Count; i++)
            {
                mean += histo[i].Y * i;
                count += histo[i].Y;
            }
            mean /= count;
            var shift = 78 - mean;
            var sign = Math.Sign(shift);
            //get the shift until the mean

            //find the shadow shift

            //find the higlight shift

            //find the contrast 
            return new ImageEffect() { Exposure = Math.Log(Math.Abs(shift) / 8, 2) * sign };
        }
    }
}
