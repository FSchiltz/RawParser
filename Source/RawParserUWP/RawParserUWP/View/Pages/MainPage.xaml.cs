using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using RawParserUWP.Model.Format.Image;
using RawParserUWP.Model.Parser;
using RawParserUWP.View.Exception;
using Windows.Storage.Provider;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using System.Runtime.InteropServices;
using RawParserUWP.Model.Parser.Demosaic;
using RawParserUWP.View.UIHelper;

namespace RawParserUWP
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
    /// <summary>
    /// The main class of the appliation
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private RawImage currentRawImage { set; get; }
        public bool imageSelected { set; get; }
        public double pageWidth;
        public double pageHeight;

        public MainPage()
        {
            InitializeComponent();
            InitSettings();
            NavigationCacheMode = NavigationCacheMode.Enabled;

            imageSelected = false;

        }

        private void InitSettings()
        {
            //checkif settings already exists
            if (localSettings.Values["imageBoxBorder"] == null) localSettings.Values["imageBoxBorder"] = 10;
        }

        private async void appBarImageChooseClick(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            filePicker.FileTypeFilter.Add(".nef");
            filePicker.FileTypeFilter.Add(".tiff");
            filePicker.FileTypeFilter.Add(".dng");
            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                // Application now has read/write access to the picked file
                try
                {
                    OpenFile(file);
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

        //Always call in the UI thread
        private async void emptyImage()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher
                    .RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //empty the previous image data
                        currentRawImage = null;
                        //empty the image display
                        imageBox.Source = null;
                        imageBox.UpdateLayout();
                        //empty the exif data
                        exifDisplay.ItemsSource = null;
                        //empty the histogram
                    });
        }

        /*
         * For the zoom of the image
         * 
         */

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            pageWidth = e.NewSize.Width;
            pageHeight = e.NewSize.Height;
        }

        private void OpenFile(StorageFile file)
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
                default:
                    throw new System.Exception("File not supported"); //todo change exception types
            }

            //Add a loading screen
            progressDisplay.Visibility = Visibility.Visible;
            histoLoadingBar.Visibility = Visibility.Visible;
            emptyImage();

            Task t = Task.Run(async () =>
            {
                using (Stream stream = (await file.OpenReadAsync()).AsStreamForRead())
                {
                    currentRawImage = new RawImage();
                    currentRawImage.fileName = file.DisplayName;

                    //Set the stream
                    parser.setStream(stream);

                    //read the thumbnail
                    currentRawImage.thumbnail = parser.parseThumbnail();
                    displayImage(RawImage.getImageAsBitmap(currentRawImage.thumbnail));

                    //read the preview
                    parser.parsePreview();
                    //displayImage(RawImage.getImageAsBitmap(parser.parsePreview()));

                    //read the exif
                    currentRawImage.exif = parser.parseExif();
                    displayExif();
                    //read the data 
                    currentRawImage.height = parser.height;
                    currentRawImage.width = parser.width;
                    currentRawImage.colorDepth = parser.colorDepth;
                    currentRawImage.imageData = Demosaic.demos(parser.parseRAWImage(), currentRawImage.height, currentRawImage.width, currentRawImage.colorDepth, demosAlgorithm.NearNeighbour);


                    //Needs to run in UI thread because fuck it
                    int[] value = new int[(int)Math.Pow(2, currentRawImage.colorDepth)];
                    await CoreApplication.MainView.CoreWindow.Dispatcher
                     .RunAsync(CoreDispatcherPriority.Normal, () =>
                     {
                         displayImage(currentRawImage.getImageRawAs8bitsBitmap((int)currentRawImage.width, (int)currentRawImage.height, null, ref value));
                     });
                    //display the histogram
                    Task histoTask = Task.Run(async () =>
                    {
                        Histogram.Create(value, currentRawImage.colorDepth, currentRawImage.height, histogramCanvas);
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                         {
                             histoLoadingBar.Visibility = Visibility.Collapsed;
                         });
                    });
                    //dispose
                    file = null;
                    parser = null;
                }
                await CoreApplication.MainView.CoreWindow.Dispatcher
                            .RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
                                //Hide the loading screen
                                progressDisplay.Visibility = Visibility.Collapsed;
                            });


                //For testing
                //emptyImage();
                //see if memory leak
                //memory should beat 25 mega after this.
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var args = e.Parameter as Windows.ApplicationModel.Activation.IActivatedEventArgs;
            if (args?.Kind == Windows.ApplicationModel.Activation.ActivationKind.File)
            {
                var fileArgs = args as Windows.ApplicationModel.Activation.FileActivatedEventArgs;
                string strFilePath = fileArgs.Files[0].Path;
                var file = (StorageFile)fileArgs.Files[0];
                if (file != null)
                {
                    // Application now has read/write access to the picked file
                    try
                    {
                        OpenFile(file);
                    }
                    catch (System.Exception ex)
                    {
                        ExceptionDisplay.display(ex.Message + ex.StackTrace);
                    }
                }
                else
                {
                    //TODO
                }
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

        private void appbarShowSplitClick(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }

        public async void displayImage(SoftwareBitmap image)
        {
            if (image != null)
            {
                //Display preview Image
                //*
                using (image)
                {
                    await
                        CoreApplication.MainView.CoreWindow.Dispatcher
                            .RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
                                //display the image preview
                                WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                                image.CopyToBuffer(bitmap.PixelBuffer);
                                imageBox.Source = bitmap;
                                setScrollProperty(bitmap.PixelHeight, bitmap.PixelWidth);
                            });
                    //display the exif

                }
                //*/
            }
        }

        private void setScrollProperty(int pixelHeight, int pixelWidth)
        {
            //TODO call when changed state
            if ((pixelWidth / pixelHeight) > (imageDisplayScroll.ActualWidth / imageDisplayScroll.ActualHeight))
            {
                imageDisplayScroll.MinZoomFactor = (float)(imageDisplayScroll.ViewportWidth / (pixelWidth + (int)localSettings.Values["imageBoxBorder"]));
            }
            else
            {
                imageDisplayScroll.MinZoomFactor = (float)(imageDisplayScroll.ViewportHeight / (pixelHeight + (int)localSettings.Values["imageBoxBorder"]));
            }
            imageDisplayScroll.ZoomToFactor(imageDisplayScroll.MinZoomFactor);
        }

        public async void displayExif()
        {
            //*
            if (currentRawImage.exif != null)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //Do some UI-code that must be run on the UI thread.
                    //set exif datasource
                    exifDisplay.ItemsSource = currentRawImage.exif.Values;
                });
            }
            //*/
        }

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO reimplement correclty
            //Just for testing purpose for now
            if (imageBox.Source != null)
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = currentRawImage.fileName
                };
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add("Image file", new List<string>() { ".jpg" });
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null) return;
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                // write to file
                WriteableBitmap bitmapImage = (WriteableBitmap)imageBox.Source;

                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
                    BitmapEncoder.JpegEncoderId,
                    await file.OpenAsync(FileAccessMode.ReadWrite));
                using (Stream pixelStream = bitmapImage.PixelBuffer.AsStream())
                {

                    byte[] pixels = new byte[pixelStream.Length];

                    await pixelStream.ReadAsync(pixels, 0, pixels.Length);


                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)bitmapImage.PixelWidth,
                        (uint)bitmapImage.PixelHeight, 96.0, 96.0, pixels);

                    await encoder.FlushAsync();
                    // Let Windows know that we're finished changing the file so
                    // the other app can update the remote version of the file.
                    // Completing updates may require Windows to ask for user input.
                    FileUpdateStatus status =
                        await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status != FileUpdateStatus.Complete)
                    {
                        ExceptionDisplay.display("File could not be saved");
                    }
                }
            }
        }
    }
}
