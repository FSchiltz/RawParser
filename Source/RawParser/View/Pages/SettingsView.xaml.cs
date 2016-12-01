using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace RawEditor
{
    public sealed partial class SettingsView : Page
    {
        public SettingsView()
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
    }
}
