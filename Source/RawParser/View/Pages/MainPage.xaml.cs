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
using RawEditor.View.Exception;
using Windows.Storage.Provider;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using RawEditor.View.UIHelper;
using RawEditor.Effect;
using RawEditor.Model.Encoder;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using RawNet;
using System.Diagnostics;

namespace RawEditor
{
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
        CameraMetaData metadata = null;
        public Thumbnail thumbnail;

        public MainPage()
        {
            InitializeComponent();
            if (null == metadata)
            {
                try
                {
                    StorageFolder installationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    var f = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/cameras.xml")).AsTask();
                    f.Wait();
                    var t = f.Result.OpenStreamForReadAsync();
                    t.Wait();
                    metadata = new CameraMetaData(t.Result);
                }
                catch (CameraMetadataException e)
                {
                    ExceptionDisplay.display(e.Message);
                }
            }
            SettingStorage.Init();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            imageSelected = false;
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
        }

        private async void appBarImageChooseClick(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker()
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            filePicker.FileTypeFilter.Add(".nef");
            // filePicker.FileTypeFilter.Add(".tiff");
            // filePicker.FileTypeFilter.Add(".tif");
            filePicker.FileTypeFilter.Add(".dng");
            // filePicker.FileTypeFilter.Add(".cr2");
            filePicker.FileTypeFilter.Add(".jpg");
            //filePicker.FileTypeFilter.Add(".jpeg");
            //filePicker.FileTypeFilter.Add(".png");
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
                        ImageBox.Source = null;
                        ImageBox.UpdateLayout();
                        //empty the exif data
                        exifDisplay.ItemsSource = null;
                        //empty the histogram
                        enableEditingControl(false);
                        //free the histogram

                    });
        }

        private async void enableEditingControl(bool v)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     colorTempSlider.IsEnabled = v;
                     colorTintSlider.IsEnabled = v;
                     exposureSlider.IsEnabled = v;

                     //ShadowSlider.IsEnabled = v;
                     //HighLightSlider.IsEnabled = v;
                     //gammaSlider.IsEnabled = v;
                     contrastSlider.IsEnabled = v;
                     //brightnessSlider.IsEnabled = v;
                     saturationSlider.IsEnabled = v;
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
            //Add a loading screen
            progressDisplay.Visibility = Visibility.Visible;
            histoLoadingBar.Visibility = Visibility.Visible;
            emptyImage();
            Task t = Task.Run(async () =>
            {
                try
                {
                    Stream stream = (await file.OpenReadAsync()).AsStreamForRead();
                    RawParser parser = new RawParser(ref stream);
                    RawDecoder decoder = parser.decoder;
                    decoder.failOnUnknown = false;
                    decoder.checkSupport(metadata);


                    thumbnail = decoder.decodeThumb();
                    if (thumbnail != null)
                    {
                        //read the thumbnail
                        Task.Run(() =>
                        {
                            if (thumbnail.type == ThumbnailType.JPEG)
                            {
                                displayImage(JpegHelper.getJpegInArrayAsync(thumbnail.data));
                            }
                            else if (thumbnail.type == ThumbnailType.RAW)
                            {
                                //this is a raw image in an array
                                JpegHelper.getThumbnailAsSoftwareBitmap(thumbnail);
                            }
                        });
                    }

                    raw = decoder.decodeRaw();
                    decoder.decodeMetaData(metadata);
                    raw.fileName = file.DisplayName;
                    //read the exifs
                    displayExif();
                    //scale the value
                    //raw.scaleValues();

                    //correctWB
                    //raw.CorrectWB();

                    //demos
                    if (raw.cfa != null && raw.cpp == 1)
                    {
                        //get th ealgo from the settings
                        DemosAlgorithm algo;
                        try
                        {
                            algo = SettingStorage.DemosAlgo;
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            algo = DemosAlgorithm.Deflate;
                        }
                        Demosaic.demos(ref raw, algo);
                    }
                    createPreview();
                    updatePreview();

                    //activate the editing control
                    enableEditingControl(true);
                    //dispose
                    file = null;
                    parser = null;
                }
                catch (FormatException e)
                {
                    var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                    var str = loader.GetString("ExceptionText");
                    ExceptionDisplay.display(str);
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                            {
                                                //Hide the loading screen
                                                progressDisplay.Visibility = Visibility.Collapsed;
                                            });
            });
        }

        private void createPreview()
        {
            //create a small image from raw to display
            FactorValue factor = SettingStorage.PreviewFactor;

            //image will be size of windows
            int previewFactor = 0;
            if (factor == FactorValue.Auto)
            {
                if (raw.dim.y > raw.dim.x)
                {
                    previewFactor = (int)(raw.dim.y / 720);
                }
                else
                {
                    previewFactor = (int)(raw.dim.x / 1080);
                }
                int start = 1;
                for (; previewFactor > (start << 1); start <<= 1) ;
                if ((previewFactor - start) < ((start << 1) - previewFactor)) previewFactor = start;
                else previewFactor <<= 1;
            }
            else
            {
                previewFactor = (int)factor;
            }
            raw.CreatePreview(previewFactor);
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
                    ExceptionDisplay.display("No file selected");
                }
            }
        }

        private void appbarSettingClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), null);
        }

        private void setScrollProperty()
        {
            if (currentImageDisplayedWidth > 0 && currentImageDisplayedHeight > 0)
            {
                float x = 0;
                double relativeBorder = SettingStorage.ImageBoxBorder;
                if ((currentImageDisplayedWidth / currentImageDisplayedHeight) < (ImageDisplay.ActualWidth / ImageDisplay.ActualHeight))
                {
                    x = (float)(ImageDisplay.ViewportWidth /
                        (currentImageDisplayedWidth +
                            (relativeBorder * currentImageDisplayedWidth)
                        ));
                }
                else
                {
                    x = (float)(ImageDisplay.ViewportHeight /
                        (currentImageDisplayedHeight +
                            (relativeBorder * currentImageDisplayedHeight)
                        ));
                }
                if (x < 0.1) x = 0.1f;
                else if (x > 1) x = 1;
                ImageDisplay.MinZoomFactor = 0.1f;
                ImageDisplay.MaxZoomFactor = x + 10;
                //ImageDisplay.ZoomToFactor(x);
                ImageDisplay.InvalidateMeasure();
                ImageDisplay.InvalidateArrange();
                ImageDisplay.InvalidateScrollInfo();
            }
        }

        public async void displayExif()
        {
            //TODO add localized exifs name
            if (raw != null && raw.metadata != null)
            {
                //create a list from the metadata object

                Dictionary<string, string> exif = new Dictionary<string, string>();
                //iso
                exif.Add("ISO", "" + raw.metadata.isoSpeed);
                //maker
                exif.Add("Maker", raw.metadata.make);
                //model
                exif.Add("Model", raw.metadata.model);
                //mode
                exif.Add("Image mode", raw.metadata.mode);
                //dimension
                exif.Add("Width", "" + raw.dim.x);
                exif.Add("Height", "" + raw.dim.y);
                //uncropped dim
                exif.Add("Uncropped height", "" + raw.uncroppedDim.x);
                exif.Add("Uncropped width", "" + raw.uncroppedDim.y);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            //Do some UI-code that must be run on the UI thread.
                            //set exif datasource
                            //TODO add
                            exifDisplay.ItemsSource = exif;
                        });

            }
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
                savePicker.FileTypeChoices.Add("Jpeg image file", new List<string>() { ".jpg" });
                savePicker.FileTypeChoices.Add("PNG image file", new List<string>() { ".png" });
                savePicker.FileTypeChoices.Add("PPM image file", new List<string>() { ".ppm" });
                savePicker.FileTypeChoices.Add("TIFF image file", new List<string>() { ".tiff" });
                savePicker.FileTypeChoices.Add("BitMap image file", new List<string>() { ".bmp" });
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
                    SoftwareBitmap bitmap = null;
                    //Needs to run in UI thread
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, raw.dim.x, raw.dim.y);
                    });
                    applyUserModif(ref raw.rawData, raw.dim, raw.mOffset, raw.colorDepth, ref bitmap);
                    // write to file
                    if (file.FileType == ".jpg")
                    {
                        using (var filestream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            int[] t = new int[3];
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, filestream);
                            var x = encoder.BitmapProperties;

                            //Needs to run in the UI thread because fuck performance
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
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

                            //Needs to run in the UI thread because fuck performance
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
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

                            //Needs to run in the UI thread because fuck performance
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                //Do some UI-code that must be run on the UI thread.
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
                        //create a copy
                        ushort[] copyOfimage = new ushort[raw.rawData.Length];
                        Parallel.For(raw.mOffset.y, raw.dim.y, y =>
                        {
                            int realY = y * raw.dim.x * 3;
                            for (int x = raw.mOffset.x; x < raw.dim.x; x++)
                            {
                                int realPix = realY + (3 * x);
                                copyOfimage[realPix] = (ushort)(raw.rawData[realPix] * raw.metadata.wbCoeffs[0]);
                                copyOfimage[realPix + 1] = (ushort)(raw.rawData[realPix + 1] * raw.metadata.wbCoeffs[1]);
                                copyOfimage[realPix + 2] = (ushort)(raw.rawData[realPix + 2] * raw.metadata.wbCoeffs[2]);
                            }
                        });
                        //apply white balance
                        PpmEncoder.WriteToFile(str, ref copyOfimage, raw.dim.y, raw.dim.x, raw.colorDepth);
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
                                    ImageBox.Source = null;
                                    WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                                    image.CopyToBuffer(bitmap.PixelBuffer);
                                    ImageBox.Source = bitmap;
                                    currentImageDisplayedHeight = bitmap.PixelHeight;
                                    currentImageDisplayedWidth = bitmap.PixelWidth;
                                    setScrollProperty();
                                });
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
                //Needs to run in UI thread
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    histoLoadingBar.Visibility = Visibility.Visible;
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, raw.previewDim.x, raw.previewDim.y);
                });
                int[] value = applyUserModif(ref raw.previewData, raw.previewDim, new iPoint2D(), raw.colorDepth, ref bitmap);
                displayImage(bitmap);
                Histogram.Create(value, raw.colorDepth, (uint)raw.previewDim.y, (uint)raw.previewDim.x, histogramCanvas);
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    histoLoadingBar.Visibility = Visibility.Collapsed;
                });
            });
        }

        /**
         * Apply the change over the image preview
         */
        private int[] applyUserModif(ref ushort[] image, iPoint2D dim, iPoint2D offset, ushort colorDepth, ref SoftwareBitmap bitmap)
        {
            ImageEffect effect = new ImageEffect();
            //get all the value 
            Task t = Task.Run(async () =>
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    effect.exposure = exposureSlider.Value;
                    effect.temperature = colorTempSlider.Value - 1;
                    effect.tint = colorTintSlider.Value - 1;

                    effect.contrast = contrastSlider.Value / 10;
                    /*effect.gamma = gammaSlider.Value;
                    //effect.brightness = (1 << colorDepth) * (brightnessSlider.Value / 100);
                    //effect.shadow = ShadowSlider.Value;
                    //effect.hightlight = HighLightSlider.Value;*/
                    effect.saturation = 1 + saturationSlider.Value / 100;
                });
            });
            t.Wait();
            effect.mul = raw.metadata.wbCoeffs;
            effect.cameraWB = cameraWB;
            effect.exposure = Math.Pow(2, effect.exposure);
            effect.camCurve = raw.curve;

            //get the softwarebitmap buffer
            return effect.applyModification(image, dim, offset, colorDepth, ref bitmap);
        }

        private void applyUserModif(ref ushort[] image, iPoint2D dim, iPoint2D offset, ushort colorDepth)
        {
            ImageEffect effect = new ImageEffect();
            //get all the value 
            Task t = Task.Run(async () =>
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    effect.exposure = exposureSlider.Value;
                    effect.temperature = colorTempSlider.Value - 1;
                    effect.tint = colorTintSlider.Value - 1;

                    effect.contrast = contrastSlider.Value / 10;
                    /*
                    effect.gamma = gammaSlider.Value;
                    effect.brightness = (1 << colorDepth) * (brightnessSlider.Value / 100);
                    effect.shadow = ShadowSlider.Value;
                    effect.hightlight = HighLightSlider.Value;*/
                    effect.saturation = 1 + saturationSlider.Value / 100;
                });
            });
            t.Wait();
            effect.mul = raw.metadata.wbCoeffs;
            effect.cameraWB = cameraWB;
            effect.exposure = Math.Pow(2, effect.exposure);
            effect.camCurve = raw.curve;
            effect.applyModification(image, dim, offset, colorDepth);
        }

        #region WBSlider
        private void WBSlider_DragStop(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
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
            //TODO move slider to the camera WB
            MoveWB();
            updatePreview();
        }

        private void MoveWB()
        {

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
