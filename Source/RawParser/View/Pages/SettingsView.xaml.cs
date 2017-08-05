using PhotoNet;
using PhotoNet.Common;
using RawEditor.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Data.Pdf;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RawEditor.View.Pages
{
    public sealed partial class SettingsView : Page
    {
        public List<BitmapImage> PdfPages
        {
            get;
        } = new List<BitmapImage>();

        public string Version
        {
            get
            {
                return string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
            }
        }

        public SettingsView()
        {
            InitializeComponent();

            var enumval = Enum.GetValues(typeof(DemosaicAlgorithm)).Cast<DemosaicAlgorithm>();
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

        public async void Donate()
        {
            var context = StoreContext.GetDefault();
            //do all the donation logic here
            MessageDialog dialog = new MessageDialog("");

            //now check if the user has already doanted
            StoreAppLicense appLicense = await context.GetAppLicenseAsync();
            if (appLicense.AddOnLicenses.TryGetValue("Support", out var license) && license.IsActive)
            {
                dialog.Content = "You've already donated for the app. Thank you for your support";
                dialog.Commands.Add(new UICommand { Label = "Cancel", Id = 0 });
            }
            else
            {
                //get info about the donation

                // Specify the kinds of add-ons to retrieve.
                string[] productKinds = { "Durable" };
                List<String> filterList = new List<string>(productKinds);
                // Specify the Store IDs of the products to retrieve.
                string[] storeIds = new string[] { "9pjwtct8t8x1" };
                StoreProductQueryResult queryResult = await context.GetStoreProductsAsync(filterList, storeIds);

                if (queryResult.ExtendedError != null)
                {
                    // The user may be offline or there might be some other server failure.
                    await new MessageDialog("The purchase was unsuccessful, maybe try again latter", "").ShowAsync();
                    return;
                }

                dialog.Content = "You can donate to support the development of the app. This donation doesn't unlock additionnal feature (at least for now). Would you like to continue ?";
                //Do some UI-code that must be run on the UI thread.               
                dialog.Commands.Add(new UICommand { Label = "Donate " + queryResult.Products["9pjwtct8t8x1"].Price.FormattedBasePrice, Id = 0 });
                dialog.Commands.Add(new UICommand { Label = "Cancel", Id = 1 });
            }
            dialog.CancelCommandIndex = 1;
            var pressed = await dialog.ShowAsync();

            //if pressed ok donate
            if ((int)pressed.Id == 0)
            {
                try
                {
                    // The customer doesn't own this feature, so
                    // show the purchase dialog.
                    var result = await context.RequestPurchaseAsync("9pjwtct8t8x1");

                    //Check the license state to determine if the in-app purchase was successful.
                    switch (result.Status)
                    {
                        case StorePurchaseStatus.Succeeded:
                            await new MessageDialog("The donation was successful, thank you very much for your support", "").ShowAsync();
                            break;
                        case StorePurchaseStatus.AlreadyPurchased:
                            await new MessageDialog("You've already donated for the app. Thank you for your support", "").ShowAsync();
                            break;
                        case StorePurchaseStatus.NetworkError:
                        case StorePurchaseStatus.ServerError:
                            await new MessageDialog("The purchase was unsuccessful, please check your internet connection", "").ShowAsync();
                            break;
                        case StorePurchaseStatus.NotPurchased:
                            //user closed, do not bother him more
                            break;
                        default:
                            await new MessageDialog("The purchase was unsuccessful, maybe try again later", "").ShowAsync();
                            break;
                    }
                }
                catch (Exception)
                {
                    await new MessageDialog("The purchase was unsuccessful, maybe try again latter", "").ShowAsync();
                }
            }
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
            SettingStorage.DemosAlgo = ((DemosaicAlgorithm)e.AddedItems[0]);
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

