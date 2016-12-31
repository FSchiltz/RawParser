using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
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
    public sealed partial class LoadBar : UserControl
    {
        private uint displayMutex = 0;

        public LoadBar()
        {
            this.InitializeComponent();
        }

        public async void ShowLoad()
        {
            displayMutex++;
            if (displayMutex > 0)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressDisplay.Visibility = Visibility.Visible;
                    //ProgressDisplay.IsActive = true;
                });
            }
        }

        public async void HideLoad()
        {
            displayMutex--;
            if (displayMutex <= 0)
            {
                displayMutex = 0;
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     ProgressDisplay.Visibility = Visibility.Collapsed;
                    //ProgressDisplay.IsActive = false;
                });
            }
        }
    }
}
