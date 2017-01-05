using System;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RawEditor.View.UIHelper
{
    public sealed partial class MemoryBar : UserControl
    {
        public MemoryBar()
        {
            this.InitializeComponent();
            UpdateMemoryBar(null, null);
            var dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += UpdateMemoryBar;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 2);
            dispatcherTimer.Start();
        }

        public void UpdateMemoryBar(object e, object a)
        {
            double var = (MemoryManager.AppMemoryUsage / (double)MemoryManager.AppMemoryUsageLimit) * 100;
            if (var < 1) var = 1;
            Memory.Value = var;
        }
    }
}
