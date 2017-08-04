using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

        public void Show()
        {
            displayMutex++;
            if (displayMutex > 0)
            {
                ProgressDisplay.Visibility = Visibility.Visible;
            }
        }

        public void Hide()
        {
            displayMutex--;
            if (displayMutex <= 0)
            {
                displayMutex = 0;
                ProgressDisplay.Visibility = Visibility.Collapsed;
            }
        }
    }
}
