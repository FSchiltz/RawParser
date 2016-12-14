using RawNet;
using System;
using System.Collections.Generic;
using System.Linq;
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
        } = new List<BitmapImage>();

        public SettingsView()
        {
            InitializeComponent();
            var enumval = Enum.GetValues(typeof(DemosAlgorithm)).Cast<DemosAlgorithm>();
            DemosComboBox.ItemsSource = enumval.ToList();

            var enumval2 = Enum.GetValues(typeof(FactorValue)).Cast<FactorValue>();
            ScaleComboBox.ItemsSource = enumval2.ToList();

            var enumval3 = Enum.GetValues(typeof(ThemeEnum)).Cast<ThemeEnum>();
            ThemeComboBox.ItemsSource = enumval3.ToList();

            UpdateView();
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

        private void UpdateView()
        {
            //set value of allcombox to current choosen settings
            //for scale
            ScaleComboBox.SelectedItem = SettingStorage.PreviewFactor;
            //for demos
            DemosComboBox.SelectedItem = SettingStorage.DemosAlgo;

            ThemeComboBox.SelectedItem = SettingStorage.SelectedTheme;

            BorderSlider.Value = SettingStorage.ImageBoxBorder * 100;
        }

        private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            SettingStorage.ImageBoxBorder = e.NewValue / 100;
        }

        private void PreviewFactor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingStorage.PreviewFactor = (FactorValue)e.AddedItems[0];
        }

        private void Algo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingStorage.DemosAlgo = ((DemosAlgorithm)e.AddedItems[0]);
        }

        private async void Button_TappedAsync(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
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
                scroll.Width = Window.Current.Bounds.Width;
                scroll.ChangeView(null, null, (float)((Window.Current.Bounds.Width) / PdfPages[0].PixelWidth));
                pop.ItemsSource = PdfPages;
            }
            PopUp.IsOpen = true;
        }

        private void PolicyButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            PopUp.IsOpen = false;
        }

        private void Reset_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            //reset settings
            SettingStorage.Reset();
            //update view
            UpdateView();
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingStorage.SelectedTheme = ((ThemeEnum)e.AddedItems[0]);
        }
    }
}

