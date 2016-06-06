using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace RawParser.View.UIHelper
{
    class Histogram
    {
        //TODO simplify if memory saver mode
        public static async void Create(int[] value, ushort colorDepth, Canvas histogramCanvas)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher
                 .RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     histogramCanvas.Children.Clear();
                 });
            //create the histogram
            int max = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (max < value[i]) max = value[i];
            }
            for (int i = 0; i < value.Length; i++)
            {
                Line line = null;
                await CoreApplication.MainView.CoreWindow.Dispatcher
                     .RunAsync(CoreDispatcherPriority.Normal, () =>
                     {
                         double maxheight = (histogramCanvas.Height / max);
                         int widthstep = (int)(value.Length / histogramCanvas.ActualWidth);
                         line = new Line();
                         line.Stroke = new SolidColorBrush(Colors.Black);
                         line.StrokeThickness = 1;
                         line.X1 = line.X2 = (int)(i * widthstep);
                         line.Y1 = histogramCanvas.Height;
                         line.Y2 = (int)(histogramCanvas.Height - (maxheight * value[i]));
                         histogramCanvas.Children.Add(line);
                     });
            }
        }

    }
}
