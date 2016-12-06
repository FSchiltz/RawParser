using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RawEditor
{
    public sealed partial class SettingsView : Page
    {

        public List<BitmapImage> PdfPages
        {
            get;
            set;
        } = new List<BitmapImage>();

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

        private async void Button_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (PdfPages.Count == 0)
            {
                //load the pdf
                StorageFile f = await
                    StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/PrivacyPolicy.pdf"));
                PdfDocument doc = await PdfDocument.LoadFromFileAsync(f);

                PdfPages.Clear();

                for (uint i = 0; i < doc.PageCount; i++)
                {
                    BitmapImage image = new BitmapImage();

                    var page = doc.GetPage(i);

                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                    {
                        await page.RenderToStreamAsync(stream);
                        await image.SetSourceAsync(stream);
                    }

                    PdfPages.Add(image);
                }
                scroll.Height = Window.Current.Bounds.Height;
                scroll.ChangeView(null, null, (float)((Window.Current.Bounds.Width - 20) / PdfPages[0].PixelWidth));
                pop.ItemsSource = PdfPages;
            }
            PopUp.IsOpen = true;
        }

        private void Button_Tapped_1(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            PopUp.IsOpen = false;
        }
    }
}

