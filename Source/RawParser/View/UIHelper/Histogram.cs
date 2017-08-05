using PhotoNet;
using System;
using System.Linq;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace RawEditor.View.UIHelper
{
    public class Histogram
    {
        public PointCollection PointsL { get; } = new PointCollection();
        public PointCollection PointsR { get; } = new PointCollection();
        public PointCollection PointsG { get; } = new PointCollection();
        public PointCollection PointsB { get; } = new PointCollection();

        public async void FillAsync(HistoRaw value)
        {
            ClearAsync();
            //smooth the histogramm
            value.luma = SmoothHistogram(value.luma);
            value.red = SmoothHistogram(value.red);
            value.green = SmoothHistogram(value.green);
            value.blue = SmoothHistogram(value.blue);
            //create a collection point
            int max = value.luma.Max();
            // first point (lower-left corner)
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PointsL.Add(new Point(0, max));
                PointsR.Add(new Point(0, max));
                PointsG.Add(new Point(0, max));
                PointsB.Add(new Point(0, max));
                // middle points
                for (int i = 0; i < value.luma.Length; i++)
                {
                    PointsL.Add(new Point(i, max - value.luma[i]));
                    PointsR.Add(new Point(i, max - value.red[i]));
                    PointsG.Add(new Point(i, max - value.green[i]));
                    PointsB.Add(new Point(i, max - value.blue[i]));
                }
                // last point (lower-right corner)
                PointsL.Add(new Point(value.luma.Length - 1, max));
                PointsR.Add(new Point(value.luma.Length - 1, max));
                PointsG.Add(new Point(value.luma.Length - 1, max));
                PointsB.Add(new Point(value.luma.Length - 1, max));
            });
        }

        public async void ClearAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (PointsL.Count > 0)
                {
                    PointsL.Clear();
                    PointsR.Clear();
                    PointsG.Clear();
                    PointsB.Clear();
                }
            });
        }

        public Histogram() { }

        private static int[] SmoothHistogram(int[] originalValues)
        {
            int[] smoothedValues = new int[originalValues.Length];
            double[] mask = new double[] { 0.25, 0.5, 0.25 };

            for (int bin = 1; bin < originalValues.Length - 1; bin++)
            {
                double smoothedValue = 0;
                for (int i = 0; i < mask.Length; i++)
                {
                    smoothedValue += originalValues[bin - 1 + i] * mask[i];
                }
                smoothedValues[bin] = (int)smoothedValue;
            }
            return smoothedValues;
        }
    }
}
