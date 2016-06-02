using System;
using System.Collections;
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
                uint maxheight = (uint)(histogramCanvas.Height/imageHeight);
                uint widthstep = (uint)(histogramCanvas.Width / value.Length);
                await CoreApplication.MainView.CoreWindow.Dispatcher
                     .RunAsync(CoreDispatcherPriority.Normal, () =>
                     {
                         line = new Line();
                         line.Stroke = new SolidColorBrush(Colors.Black);

                         line.X1 = 1;
                         line.X2 = 50;
                         line.Y1 = 1;
                         line.Y2 = 50;

                         line.StrokeThickness = 1;
                         histogramCanvas.Children.Add(line);
                     });                
            }
        }
    }
}
