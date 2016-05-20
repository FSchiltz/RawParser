using RawParser.Model.ImageDisplay;
using RawParser.Model.Parser;
using RawParserUWP.Model.Exception;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;


namespace RawParserUWP
{
    /// <summary>
    /// The main class of the appliation
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private RawImage currentRawImage { set; get; }
        private bool imageSelected { set; get; }

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            appBarImageChoose.Click += new RoutedEventHandler(appBarImageChooseClick);
            imageSelected = false;
        }

        private async void appBarImageChooseClick(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.FileTypeFilter.Add(".nef");
            filePicker.FileTypeFilter.Add(".tiff");
            filePicker.FileTypeFilter.Add(".dng");
            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                // Application now has read/write access to the picked file
                try
                {
                    //Open the file with the correct parser
                    Parser parser;
                    switch (file.FileType.ToUpper())
                    {
                        case ".NEF":
                            parser = new NEFParser();
                            break;
                        case ".DNG":
                            parser = new DNGParser();
                            break;
                        case ".TIFF":
                            parser = new DNGParser();
                            break;
                        default: throw new Exception("File not supported");//todo change exception types
                    }

                    //TODO Add a loading screen
                    progressDisplay.IsActive = true;
                    progressDisplay.Visibility = Visibility.Visible;
                    Stream stream = (await file.OpenReadAsync()).AsStreamForRead();
                    Task t = Task.Run(async() =>
                    {
                        currentRawImage = parser.parse(stream);
                        SoftwareBitmap image = currentRawImage.getImageAsBitmap();
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            //Do some UI-code that must be run on the UI thread.
                            //display the image
                            WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                            image.CopyToBuffer(bitmap.PixelBuffer);
                            imageBox.Source = bitmap;
                            //TODO Hide the loading screen
                            progressDisplay.Visibility = Visibility.Collapsed;
                            progressDisplay.IsActive = false;
                        });       
                    });                    
                }
                catch (Exception ex)
                {
                    ExceptionDisplay.display(ex.Message + ex.StackTrace);
                }
            }
            else
            {
                //TODO
            }
        }


        private void appbarAboutClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(View.Pages.About), null);
        }

        private void appbarSettingClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(View.Pages.Settings), null);
        }
    }
}
