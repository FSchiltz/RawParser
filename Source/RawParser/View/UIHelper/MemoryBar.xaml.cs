using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
