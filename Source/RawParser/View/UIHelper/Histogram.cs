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
        public static async void Create(int[] value, ushort colorDepth, uint height, uint width, Canvas histogramCanvas)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher
                 .RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     histogramCanvas.Children.Clear();
                 });
            for (int i = 0; i < value.Length; i++)
            {
                Line line = null;
                await CoreApplication.MainView.CoreWindow.Dispatcher
                     .RunAsync(CoreDispatcherPriority.Normal, () =>
                     {
                         int widthstep = (int)(value.Length / histogramCanvas.ActualWidth);
                         value[i] = (int)(value[i] / ((height * width) / (256 * 10)));
                         line = new Line();
                         line.Stroke = new SolidColorBrush(Colors.Black);
                         line.StrokeThickness = 1;

                         line.X1 = line.X2 = (i * widthstep);
                         line.Y1 = histogramCanvas.Height;
                         line.Y2 = (int)(histogramCanvas.Height - value[i]);

                         histogramCanvas.Children.Add(line);
                     });
            }
        }

    }
}
