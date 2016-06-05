using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using RawParser.View.Exception;
using Windows.Storage.Provider;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using System.Runtime.InteropServices;
using System.Text;
using RawParser.View.UIHelper;
using RawParser.Image;
using RawParser.Parser;
using RawParser.Effect;

namespace RawParser
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
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public RawImage raw;
        public bool imageSelected { set; get; }
        public double pageWidth;
        public double pageHeight;
        private int currentImageDisplayedHeight;
        private int currentImageDisplayedWidth;
        private bool colorTempchanged;
        private int[] value = new int[256];

        //TODO move
        bool dragStarted = false;
        bool dragEnded = false;
        double oldValue = 0;

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
            if (localSettings.Values["imageBoxBorder"] == null)
                localSettings.Values["imageBoxBorder"] = 0.05;
            if (localSettings.Values["previewFactor"] == null)
                localSettings.Values["previewFactor"] = 4;
            if (localSettings.Values["saveFormat"] == null)
                localSettings.Values["saveFormat"] = ".jpg";
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
                        raw = null;
                        //empty the image display
                        imageBox.Source = null;
                        imageBox.UpdateLayout();
                        //empty the exif data
                        exifDisplay.ItemsSource = null;
                        //empty the histogram
                        enableEditingControl(false);
                    });
        }

        private async void enableEditingControl(bool v)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher
                 .RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     colorTempSlider.IsEnabled = v;
                     exposureSlider.IsEnabled = v;
                 });
        }

        /*
         * For the zoom of the image
         * 
         */

        private void PageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            pageWidth = e.NewSize.Width;
            pageHeight = e.NewSize.Height;
            setScrollProperty();
        }

        private void OpenFile(StorageFile file)
        {
            //Open the file with the correct parser
            AParser parser;
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
                    raw = new RawImage();
                    raw.fileName = file.DisplayName;

                    //Set the stream
                    parser.setStream(stream);

                    //read the thumbnail
                    raw.thumbnail = parser.parseThumbnail();
                    displayImage(JpegHelper.getJpegInArray(raw.thumbnail));

                    //read the preview
                    parser.parsePreview();
                    //displayImage(RawImage.getImageAsBitmap(parser.parsePreview()));

                    //read the exifi
                    raw.exif = parser.parseExif();
                    displayExif();
                    //read the data 
                    raw.height = parser.height;
                    raw.width = parser.width;
                    raw.colorDepth = parser.colorDepth;
                    raw.cfa = parser.cfa;
                    raw.camMul = parser.camMul;
                    raw.rawData = parser.parseRAWImage();
                    raw.camMul = parser.camMul;
                    Balance.scaleColor(ref raw, raw.dark, raw.saturation, raw.camMul);

                    Demosaic.demos(ref raw, demosAlgorithm.NearNeighbour);

                    //create a small image from raw to display
                    int previewFactor = (int)localSettings.Values["previewFactor"];
                    //previewFactor = 4;

                    raw.previewHeight = (uint)(raw.height / previewFactor);
                    raw.previewWidth = (uint)(raw.width / previewFactor);
                    raw.previewData = new uint[raw.previewHeight * raw.previewWidth * 3];
                    for (int i = 0; i < raw.previewHeight; i++)
                    {
                        for (int j = 0; j < raw.previewWidth; j++)
                        {
                            raw.previewData[((i * raw.previewWidth) + j) * 3] = raw.rawData[((i * previewFactor * raw.previewWidth) + j) * 3 * previewFactor];
                            raw.previewData[(((i * raw.previewWidth) + j) * 3) + 1] = raw.rawData[(((i * previewFactor * raw.previewWidth) + j) * 3 * previewFactor) + 1];
                            raw.previewData[(((i * raw.previewWidth) + j) * 3) + 2] = raw.rawData[(((i * previewFactor * raw.previewWidth) + j) * 3 * previewFactor) + 2];
                        }
                    }
                    updatePreview();
                    //activate the editing control
                    enableEditingControl(true);
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
                                currentImageDisplayedHeight = bitmap.PixelHeight;
                                currentImageDisplayedWidth = bitmap.PixelWidth;
                                setScrollProperty();
                            });
                }
            }
        }

        private void setScrollProperty()
        {
            if (currentImageDisplayedWidth > 0 && currentImageDisplayedHeight > 0)
            {
                float x = 0;
                double relativeBorder = (double)localSettings.Values["imageBoxBorder"];
                if ((currentImageDisplayedWidth / currentImageDisplayedHeight) < (imageDisplayScroll.ActualWidth / imageDisplayScroll.ActualHeight))
                {
                    x = (float)(imageDisplayScroll.ViewportWidth /
                        (currentImageDisplayedWidth +
                            (relativeBorder * currentImageDisplayedWidth)
                        ));
                }
                else
                {
                    x = (float)(imageDisplayScroll.ViewportHeight /
                        (currentImageDisplayedHeight +
                            (relativeBorder * currentImageDisplayedHeight)
                        ));
                }
                if (x < 0.1) x = 0.1f;
                else if (x > 1) x = 1;
                imageDisplayScroll.MinZoomFactor = x;
                imageDisplayScroll.MaxZoomFactor = x + 10;
                //imageDisplayScroll.ZoomToFactor(x);
                imageDisplayScroll.InvalidateMeasure();
                imageDisplayScroll.InvalidateArrange();
                imageDisplayScroll.InvalidateScrollInfo();
            }
        }

        public async void displayExif()
        {
            //*
            if (raw.exif != null)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    //Do some UI-code that must be run on the UI thread.
                    //set exif datasource
                    exifDisplay.ItemsSource = raw.exif.Values;
                });
            }
            //*/
        }

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            string format = (string)localSettings.Values["saveFormat"];

            //TODO reimplement correclty
            //Just for testing purpose for now
            if (raw.rawData != null)
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = raw.fileName
                };
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add("Image file", new List<string>() { format });
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                progressDisplay.Visibility = Visibility.Visible;
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                var exposure = exposureSlider.Value;
                var temp = (int)colorTempSlider.Value;
                var task = Task.Run(async () =>
                {

                    //TODO apply to the real image the correction
                    //apply the exposure
                    Luminance.Exposure(ref raw.rawData, raw.height, raw.width, exposure);
                    //apply the temperature (not yet because slider is not set to correct temp)
                    if (colorTempchanged)
                        Balance.WhiteBalance(ref raw.rawData, raw.colorDepth, raw.height, raw.width, temp);
                    colorTempchanged = false;
                    //Check if clipping
                    Luminance.Clip(ref raw.rawData, raw.height, raw.width, (ushort)Math.Pow(2, raw.colorDepth));

                    // write to file
                    if (format == ".jpg")
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
                            BitmapEncoder.JpegEncoderId,
                            await file.OpenAsync(FileAccessMode.ReadWrite));
                        int[] t = new int[3];
                        //Needs to run in the UI thread because fuck performance
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            //Do some UI-code that must be run on the UI thread.
                            encoder.SetSoftwareBitmap(raw.getImageRawAs8bitsBitmap(null, ref t));
                            await encoder.FlushAsync();
                        });
                    }
                    else if (format == ".ppm")
                    {
                        var str = await file.OpenStreamForWriteAsync();
                        var stream = new StreamWriter(str, Encoding.ASCII);
                        stream.Write("P3\r\n" + raw.width + " " + raw.height + " 255 \r\n");
                        for (int i = 0; i < raw.height; i++)
                        {
                            for (int j = 0; j < raw.width; j++)
                            {
                                ushort x = raw.rawData[(int)(((i * raw.width) + j) * 3)];
                                byte y = (byte)(x >> 6);
                                stream.Write(y + " ");
                                x = raw.rawData[(int)(((i * raw.width) + j) * 3) + 1];
                                y = (byte)(x >> 6);
                                stream.Write(y + " ");
                                x = raw.rawData[(int)(((i * raw.width) + j) * 3) + 2];
                                y = (byte)(x >> 6);
                                stream.Write(y + " ");
                            }
                            stream.Write("\r\n");
                        }
                        str.Dispose();
                    }
                    else throw new FormatException("Format not supported: " + format);
                    // Let Windows know that we're finished changing the file so
                    // the other app can update the remote version of the file.
                    // Completing updates may require Windows to ask for user input.
                    FileUpdateStatus status =
                            await CachedFileManager.CompleteUpdatesAsync(file);

                    if (status != FileUpdateStatus.Complete)
                    {
                        ExceptionDisplay.display("File could not be saved");
                    }
                    await CoreApplication.MainView.CoreWindow.Dispatcher
                    .RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //Do some UI-code that must be run on the UI thread.
                        //Hide the loading screen
                        progressDisplay.Visibility = Visibility.Collapsed;
                    });

                });
            }
        }

        private void colorTempSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                colorTempchanged = true;
                double oldvalue = e.OldValue;
                double value = e.NewValue; //get the value as a stop
                value -= oldvalue;
                Task t = Task.Run(() =>
                {
                    lock (raw.previewData)
                    {
                        Balance.WhiteBalance(ref raw.previewData, raw.colorDepth, raw.previewHeight, raw.previewWidth, (int)value);
                        updatePreview();
                    }
                });
            }
        }

        private void updatePreview()
        {
            //display the histogram                    
            Task histoTask = Task.Run(async () =>
            {
                SoftwareBitmap bitmap = null;
                //Needs to run in UI thread because fuck it
                await CoreApplication.MainView.CoreWindow.Dispatcher
                 .RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     histoLoadingBar.Visibility = Visibility.Visible;
                     bitmap = raw.getImagePreviewAs8bitsBitmap(null, ref value);
                 });
                displayImage(bitmap);
                Histogram.Create(value, raw.colorDepth, raw.previewHeight, histogramCanvas);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    histoLoadingBar.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void exposureSlider_DragEnter(object sender, DragEventArgs e)
        {
            //store the value
            oldValue = exposureSlider.Value;
            dragStarted = true;
        }

        private void exposureSlider_DragLeave(object sender, DragEventArgs e)
        {
            if (raw?.previewData != null )
            {
                colorTempchanged = true;
                double value = exposureSlider.Value; //get the value as a stop
                value -= oldValue;
                Task t = Task.Run(() =>
                {
                    lock (raw.previewData)
                    {
                        Luminance.Exposure(ref raw.previewData, raw.previewHeight, raw.previewWidth, value);
                        updatePreview();
                    }
                });
                dragStarted = false;
            }
        }

        private void exposureSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (raw?.previewData != null && !dragStarted)
            {
                colorTempchanged = true;
                double value = e.NewValue; //get the value as a stop
                value -= e.OldValue;
                Task t = Task.Run(() =>
                {
                    lock (raw.previewData)
                    {
                        Luminance.Exposure(ref raw.previewData, raw.previewHeight, raw.previewWidth, value);
                        updatePreview();
                    }
                });
            }
        }
    }
}
