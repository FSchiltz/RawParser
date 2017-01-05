using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RawEditor.View.UIHelper
{
    public sealed partial class LoadBar : UserControl
    {
        private int displayMutex = 0;

        public LoadBar()
        {
            InitializeComponent();
        }

        public void ShowLoad()
        {
            displayMutex++;
            if (displayMutex > 0)
            {
                ProgressDisplay.Visibility = Visibility.Visible;
            }
        }

        public void HideLoad()
        {
            displayMutex--;
            if (displayMutex <= 0)
            {
                displayMutex = 0;
                ProgressDisplay.Visibility = Visibility.Collapsed;
            }
        }

        public async void HideLoadAsync()
        {
            displayMutex--;
            if (displayMutex <= 0)
            {
                displayMutex = 0;
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressDisplay.Visibility = Visibility.Collapsed;
                });
            }
        }
    }
}
