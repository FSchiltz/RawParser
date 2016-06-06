using RawParser.Model.Settings;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace RawParser.View.Pages
{
    public sealed partial class Settings : Page
    {
        public Settings()
        {
            InitializeComponent();
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            SystemNavigationManager.GetForCurrentView().BackRequested += (s, a) =>
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                    a.Handled = true;
                }
            };
        }

        private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            SettingStorage.imageBoxBorder = e.NewValue / 100;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var t = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();
            if (t == "Auto")
            {
                SettingStorage.autoPreviewFactor = true;
            }
            else
            {
                SettingStorage.autoPreviewFactor = false;
                SettingStorage.previewFactor = int.Parse(t);
            }
        }

        private void ComboBoxFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingStorage.saveFormat = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();
        }
    }
}
