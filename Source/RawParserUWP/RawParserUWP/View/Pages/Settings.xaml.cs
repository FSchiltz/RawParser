using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace RawParserUWP.View.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Settings : Page
    {

        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
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
            localSettings.Values["imageBoxBorder"] = e.NewValue/100;            
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            localSettings.Values["previewFactor"] = (int)((ComboBoxItem)e.AddedItems[0]).Content;
        }

        private void ComboBoxFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {            
            localSettings.Values["saveFormat"] = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();
        }
    }
}
