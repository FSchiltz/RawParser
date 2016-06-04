using System;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace RawParserUWP.View.UIHelper
{
    class Histogram
    {
        //TODO simplify if memory saver mode
        internal static async void Create(int[] value, ushort colorDepth,uint imageHeight, Canvas histogramCanvas)
        {
            //create the histogram
            for (int i = 0; i < value.Length; i++)
            {
                Line line = null;
                await CoreApplication.MainView.CoreWindow.Dispatcher
                     .RunAsync(CoreDispatcherPriority.Normal, () =>
                     {
                         uint maxheight = (uint)(histogramCanvas.Height / imageHeight);
                         uint widthstep = (uint)(histogramCanvas.Width / value.Length);
                         line = new Line();
                         line.Stroke = new SolidColorBrush(Colors.Black);
                         line.StrokeThickness = 1;
                         line.X1 = line.X2 = i*widthstep;                         
                         line.Y1 = 0;
                         line.Y2 = maxheight * value[i];

                         Canvas.SetTop(line, 50);
                         Canvas.SetLeft(line, 50);
                         histogramCanvas.Children.Add(line);
                     });                
            }
        }
    }
}
