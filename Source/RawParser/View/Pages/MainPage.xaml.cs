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
using RawParser.View.UIHelper;
using RawParser.Image;
using RawParser.Parser;
using RawParser.Effect;
using RawParser.Model.Settings;
using RawParser.Model.Encoder;

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
        public RawImage raw;
        public bool imageSelected { set; get; }
        public double pageWidth;
        public double pageHeight;
        private int currentImageDisplayedHeight;
        private int currentImageDisplayedWidth;

        bool cameraWB = true;
        private bool ExposuredragStarted;
        private bool WBdragStarted;

        public MainPage()
        {
            InitializeComponent();
            SettingStorage.init();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            imageSelected = false;

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
                     gammaSlider.IsEnabled = v;
                     //contrastSlider.IsEnabled = v;
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

                    Demosaic.demos(ref raw, demosAlgorithm.NearNeighbour);

                    //activate the editing control
                    enableEditingControl(true);

                    //create a small image from raw to display
                    bool autoFactor = SettingStorage.autoPreviewFactor;
                    int previewFactor = 0;
                    if (autoFactor)
                    {
                        if (raw.height > raw.width)
                        {
                            previewFactor = (int)(raw.height / 720);
                        }
                        else
                        {
                            previewFactor = (int)(raw.width / 1080);
                        }
                        int start = 1;
                        for (; previewFactor > (start << 1); start <<= 1) ;
                        if ((previewFactor - start) < ((start << 1) - previewFactor)) previewFactor = start;
                        else previewFactor <<= 1;
                    }
                    else
                    {
                        previewFactor = SettingStorage.previewFactor;
                    }
                    raw.previewHeight = (uint)(raw.height / previewFactor);
                    raw.previewWidth = (uint)(raw.width / previewFactor);
                    raw.previewData = new ushort[raw.previewHeight * raw.previewWidth * 3];
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

        private void setScrollProperty()
        {
            if (currentImageDisplayedWidth > 0 && currentImageDisplayedHeight > 0)
            {
                float x = 0;
                double relativeBorder = SettingStorage.imageBoxBorder;
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
            //TODO reimplement correclty
            //Just for testing purpose for now
            if (raw?.rawData != null)
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = raw.fileName
                };
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add("Image file", new List<string>() { ".jpg", ".png", ".ppm", ".tiff", ".bmp" });
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null) return;


                progressDisplay.Visibility = Visibility.Visible;
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                var exposure = exposureSlider.Value;
                int temperature = (int)colorTempSlider.Value;
                var temp = (int)colorTempSlider.Value;
                var task = Task.Run(async () =>
                {
                    ushort[] copyOfimage = new ushort[raw.rawData.Length];
                    for (int i = 0; i < raw.rawData.Length; i++) copyOfimage[i] = raw.rawData[i];
                    applyUserModif(ref copyOfimage, raw.height, raw.width, raw.colorDepth);

                    // write to file
                    if (file.FileType == ".jpg")
                    {
                        using (var filestream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            int[] t = new int[3];
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, filestream);
                            var x = encoder.BitmapProperties;
                            SoftwareBitmap bitmap = null;
                            //Needs to run in the UI thread because fuck performance
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
                                bitmap = RawImage.getImageAs8bitsBitmap(ref copyOfimage, raw.height, raw.width, raw.colorDepth, null, ref t, false, false);
                                encoder.SetSoftwareBitmap(bitmap);
                            });
                            await encoder.FlushAsync();
                            encoder = null;
                            bitmap.Dispose();
                        }
                    }
                    else if (file.FileType == ".png")
                    {
                        using (var filestream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            int[] t = new int[3];
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, filestream);
                            SoftwareBitmap bitmap = null;
                            //Needs to run in the UI thread because fuck performance
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
                                bitmap = RawImage.getImageAs8bitsBitmap(ref copyOfimage, raw.height, raw.width, raw.colorDepth, null, ref t, false, false);
                                encoder.SetSoftwareBitmap(bitmap);
                            });
                            await encoder.FlushAsync();
                            encoder = null;
                            bitmap.Dispose();
                        }
                    }
                    else if (file.FileType == ".bmp")
                    {
                        using (var filestream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            int[] t = new int[3];
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, filestream);
                            SoftwareBitmap bitmap = null;
                            //Needs to run in the UI thread because fuck performance
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
                                bitmap = RawImage.getImageAs8bitsBitmap(ref copyOfimage, raw.height, raw.width, raw.colorDepth, null, ref t, false, false);
                                encoder.SetSoftwareBitmap(bitmap);
                            });
                            await encoder.FlushAsync();
                            encoder = null;
                            bitmap.Dispose();
                        }
                    }
                    else if (file.FileType == ".ppm")
                    {
                        Stream str = await file.OpenStreamForWriteAsync();
                        PpmEncoder.WriteToFile(str, ref copyOfimage, raw.height, raw.width, raw.colorDepth);
                    }
                    else throw new FormatException("Format not supported: " + file.FileType);
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

        private void displayImage(SoftwareBitmap image)
        {
            if (image != null)
            {
                Task t = Task.Run(async () =>
                {
                    using (image)
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    //Do some UI-code that must be run on the UI thread.
                                    //display the image preview
                                    imageBox.Source = null;
                                    WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                                    image.CopyToBuffer(bitmap.PixelBuffer);
                                    imageBox.Source = bitmap;
                                    currentImageDisplayedHeight = bitmap.PixelHeight;
                                    currentImageDisplayedWidth = bitmap.PixelWidth;
                                    setScrollProperty();
                                });
                    }
                });
            }
        }
        public void applyUserModif(ref ushort[] data, uint height, uint width, int colordepth)
        {
            double exposure = 0;
            double temperature = 0;
            double gamma = 0;
            double contrast = 0;
            Task t = Task.Run(async () =>
            {
                //get all the value               
                await CoreApplication.MainView.CoreWindow.Dispatcher
                .RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    exposure = exposureSlider.Value;
                    temperature = colorTempSlider.Value;
                    gamma = gammaSlider.Value;
                    contrast = contrastSlider.Value;
                });
            });
            t.Wait();

            //aply all thetransformation on it
            if (cameraWB)
            {
                Balance.scaleColor(ref data, height, width, raw.dark, raw.saturation, raw.camMul, raw.colorDepth);
            }
            else
            {
                double[] mul = new double[4];
                Balance.calculateRGB((int)temperature, out mul[0], out mul[2], out mul[1]);
                Balance.scaleColor(ref data, height, width, raw.dark, raw.saturation, mul, colordepth);
            }
            Balance.scaleGamma(ref data, height, width, colordepth, gamma);
            Luminance.Contraste(ref data, height, width, contrast, colordepth);
            Luminance.Exposure(ref data, height, width, exposure, colordepth);


        }

        private void updatePreview()
        {
            //display the histogram                    
            Task histoTask = Task.Run(async () =>
            {
                int[] value = new int[256];
                ushort[] copyofpreview = new ushort[raw.previewData.Length];
                for (int i = 0; i < copyofpreview.Length; i++) copyofpreview[i] = raw.previewData[i];
                applyUserModif(ref copyofpreview, raw.previewHeight, raw.previewWidth, raw.colorDepth);
                SoftwareBitmap bitmap = null;
                //Needs to run in UI thread because fuck it
                await CoreApplication.MainView.CoreWindow.Dispatcher
                 .RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     histoLoadingBar.Visibility = Visibility.Visible;
                     //Writeablebitmap use BGRA (don't know why )
                     bitmap = RawImage.getImageAs8bitsBitmap(ref copyofpreview, raw.previewHeight, raw.previewWidth, raw.colorDepth, null, ref value, true, true);
                 });
                displayImage(bitmap);
                Histogram.Create(value, raw.colorDepth,raw.previewHeight, raw.previewWidth, histogramCanvas);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    histoLoadingBar.Visibility = Visibility.Collapsed;
                });
            });
        }

        #region ExposureSlider
        private void exposureSlider_DragStart(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ExposuredragStarted = true;
        }

        private void exposureSlider_DragStop(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                updatePreview();
                ExposuredragStarted = false;
            }
        }

        private void exposureSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (raw?.previewData != null && ExposuredragStarted)
            {
                updatePreview();
            }
        }
        #endregion

        #region WBSlider
        private void WBSlider_DragStart(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            WBdragStarted = true;
        }

        private void WBSlider_DragStop(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                WBdragStarted = false;
                cameraWB = false;
                cameraWBCheck.IsEnabled = true;
                updatePreview();
            }
        }

        private void colorTempSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (raw?.previewData != null && WBdragStarted)
            {
                cameraWB = false;
                cameraWBCheck.IsEnabled = true;
                updatePreview();
            }
        }

        private void cameraWBCheck_Click(object sender, RoutedEventArgs e)
        {
            cameraWB = true;
            cameraWBCheck.IsEnabled = false;
            updatePreview();
        }
        #endregion

        private void Slider_PointerCaptureLost(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                updatePreview();
            }
        }
    }
}
