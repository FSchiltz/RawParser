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
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;

namespace RawEditor
{
    /// <summary>
    /// The main class of the appliation
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public RawImage raw;
        public bool ImageSelected { set; get; }
        public Size dim;//for auto preview
        bool cameraWB = true;
        public Thumbnail thumbnail;
        private uint displayMutex = 0;
        private bool userAppliedModif = false;

        public MainPage()
        {
            InitializeComponent(); var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel * 1.2;
            dim = new Size(bounds.Width * scaleFactor, bounds.Height * scaleFactor);
            this.NavigationCacheMode = NavigationCacheMode.Required;
            /*if (null == metadata)
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
            }*/

            NavigationCacheMode = NavigationCacheMode.Enabled;
            ImageSelected = false;
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
        }

        public async void DisplayLoad()
        {
            displayMutex++;
            if (displayMutex > 0)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    progressDisplay.Visibility = Visibility.Visible;
                });
            }
        }

        public async void StopLoadDisplay()
        {
            displayMutex--;
            if (displayMutex <= 0)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    progressDisplay.Visibility = Visibility.Collapsed;
                });
            }
        }

        private async void AppBarImageChooseClick(object sender, RoutedEventArgs e)
        {
            if (!ImageSelected)
            {
                FileOpenPicker filePicker = new FileOpenPicker()
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.ComputerFolder
                };
                filePicker.FileTypeFilter.Add(".tiff");
                filePicker.FileTypeFilter.Add(".tif");
                filePicker.FileTypeFilter.Add(".jpg");
                filePicker.FileTypeFilter.Add(".jpeg");
                filePicker.FileTypeFilter.Add(".png");

                //raw
                filePicker.FileTypeFilter.Add(".nef");
                filePicker.FileTypeFilter.Add(".dng");
                filePicker.FileTypeFilter.Add(".cr2");
                filePicker.FileTypeFilter.Add(".pef");
                filePicker.FileTypeFilter.Add(".arw");
                StorageFile file = await filePicker.PickSingleFileAsync();
                if (file != null)
                {
                    // Application now has read/write access to the picked file
                    try
                    {
                        ImageSelected = true;
                        OpenFile(file);
                    }
                    catch (Exception ex)
                    {
                        ExceptionDisplay.display(ex.Message + ex.StackTrace);
                        ImageSelected = false;
                    }
                }
                else
                {
                }
            }
        }

        //Always call in the UI thread
        private async void EmptyImage()
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
                        EnableEditingControl(false);
                        //free the histogram
                        histogramCanvas.Children.Clear();
                        //set back editing control to default value
                        ResetControls();
                    });
        }

        private async void ResetControls()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher
                   .RunAsync(CoreDispatcherPriority.Normal, () =>
                   {
                       exposureSlider.Value = 0;
                       ShadowSlider.Value = 0;
                       HighLightSlider.Value = 0;
                       //gammaSlider.IsEnabled = v;
                       contrastSlider.Value = 10;
                       //brightnessSlider.IsEnabled = v;
                       saturationSlider.Value = 0;
                       ResetButton.IsEnabled = false;
                       userAppliedModif = false;
                       //set white balance if any
                       SetWB();
                   });
        }

        private async void SetWB()
        {
            int rValue = 255, bValue = 255, gValue = 255;
            if (raw != null && raw.metadata != null)
            {
                //calculate the coeff
                double r = raw.metadata.wbCoeffs[0], b = raw.metadata.wbCoeffs[2], g = raw.metadata.wbCoeffs[1];
                rValue = (int)(r * 255);
                bValue = (int)(b * 255);
                gValue = (int)(g * 255);
                if (rValue > 510) rValue = 510;
                else if (rValue < 0) rValue = 0;
                if (bValue > 510) bValue = 510;
                else if (bValue < 0) bValue = 0;
                if (gValue > 510) gValue = 510;
                else if (gValue < 0) gValue = 0;

            }
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                colorTempSlider.Value = rValue;
                colorTintSlider.Value = gValue;
                colorTintBlueSlider.Value = bValue;
            });
        }

        private async void EnableEditingControl(bool v)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     colorTempSlider.IsEnabled = v;
                     colorTintSlider.IsEnabled = v;
                     colorTintBlueSlider.IsEnabled = v;
                     exposureSlider.IsEnabled = v;
                     ShadowSlider.IsEnabled = v;
                     HighLightSlider.IsEnabled = v;
                     //gammaSlider.IsEnabled = v;
                     contrastSlider.IsEnabled = v;
                     //brightnessSlider.IsEnabled = v;
                     saturationSlider.IsEnabled = v;
                     saveButton.IsEnabled = v;
                 });
        }

        /*
         * For the zoom of the image
         * 
         */
        /*
        private void PageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            pageWidth = e.NewSize.Width;
            pageHeight = e.NewSize.Height;
            SetScrollProperty();
        }*/

        private void OpenFile(StorageFile file)
        {
            //Add a loading screen
            DisplayLoad();
            EmptyImage();
            Task t = Task.Run(async () =>
            {
                try
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();

                    Stream stream = (await file.OpenReadAsync()).AsStreamForRead();

                    //Does not improve speed
                    /*
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, (int)stream.Length);
                    stream = new MemoryStream(data);
                    stream.Position = 0;*/

                    //change decoder detection with file extension
                    RawDecoder decoder = RawParser.GetDecoder(ref stream, file.FileType);
                    // decoder.checkSupport();
                    thumbnail = decoder.DecodeThumb();
                    if (thumbnail != null)
                    {
                        //read the thumbnail
                        Task.Run(() =>
                        {
                            try
                            {
                                if (thumbnail.type == ThumbnailType.JPEG)
                                {
                                    DisplayImage(JpegHelper.getJpegInArrayAsync(thumbnail.data), true);

                                }
                                else if (thumbnail.type == ThumbnailType.RAW)
                                {
                                    //this is a raw image in an array
                                    JpegHelper.getThumbnailAsSoftwareBitmap(thumbnail);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("Error in thumb " + e.Message);
                            }
                        });
                    }

                    decoder.DecodeRaw();
                    decoder.DecodeMetaData();
                    raw = decoder.rawImage;
                    raw.metadata.fileName = file.DisplayName;
                    raw.metadata.fileNameComplete = file.Name;

                    stream.Dispose();
                    decoder = null;
                    
                    DisplayExifAsync();

                    //demos
                    if (raw.cfa != null && raw.cpp == 1)
                    {
                        //get the algo from the settings
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
                    CreatePreview();
                    UpdatePreview(true);
                    thumbnail = null;

                    //activate the editing control
                    SetWB();
                    EnableEditingControl(true);
                    //dispose
                    file = null;
                    watch.Stop();
                    Debug.WriteLine("Parsed done in " + watch.ElapsedMilliseconds + "ms");
                }
                catch (FormatException e)
                {
                    file = null;
                    raw = null;
                    EmptyImage();
                    var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                    var str = loader.GetString("ExceptionText");
                    Debug.WriteLine(e.Message);
                    ExceptionDisplay.display(str);
                   
                }

                StopLoadDisplay();
                ImageSelected = false;
            });
        }

        private void CreatePreview()
        {
            //create a small image from raw to display
            FactorValue factor = SettingStorage.PreviewFactor;

            //image will be size of windows
            int previewFactor = 0;
            if (factor == FactorValue.Auto)
            {
                if (raw.dim.y > raw.dim.x)
                {
                    previewFactor = (int)(raw.dim.y / dim.Height);
                }
                else
                {
                    previewFactor = (int)(raw.dim.x / dim.Width);
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
                    catch (Exception ex)
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

        private void AppbarSettingClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), null);
        }

        /*private void SetScrollProperty()
        {                       
            if (raw.previewDim.x > 0 && raw.previewDim.y > 0)
            {
                float x = 0;
                double relativeBorder = SettingStorage.ImageBoxBorder;
                if ((raw.previewDim.x / raw.previewDim.y) < (ImageDisplay.ActualWidth / ImageDisplay.ActualHeight))
                {
                    x = (float)(ImageDisplay.ViewportWidth /
                        (raw.previewDim.x +
                            (relativeBorder * raw.previewDim.x)
                        ));
                }
                else
                {
                    x = (float)(ImageDisplay.ViewportHeight /
                        (raw.previewDim.y +
                            (relativeBorder * raw.previewDim.y)
                        ));
                }
                if (x < 0.1) x = 0.1f;
                else if (x > 1) x = 1;
                ImageDisplay.MinZoomFactor = 0.1f;
                ImageDisplay.MaxZoomFactor = x + 10;
                ImageDisplay.ChangeView(null, null, x);
        }
        }*/

        public async void DisplayExifAsync()
        {
            //TODO add localized exifs name
            if (raw != null && raw.metadata != null)
            {
                //create a list from the metadata object
                Dictionary<string, string> exif = new Dictionary<string, string>();
                exif.Add("File", raw.metadata.fileNameComplete);
                if (raw.metadata.make != null && raw.metadata.make.Trim() != "")
                    exif.Add("Maker", raw.metadata.make);
                if (raw.metadata.model != null && raw.metadata.model.Trim() != "")
                    exif.Add("Model", raw.metadata.model);
                if (raw.metadata.mode != null && raw.metadata.mode.Trim() != "")
                    exif.Add("Image mode", raw.metadata.mode);

                exif.Add("Size", "" + ((raw.dim.x * raw.dim.y) / 1000000.0).ToString("F") + " MPixels");
                exif.Add("Width", "" + raw.dim.x);
                exif.Add("Height", "" + raw.dim.y);
                exif.Add("Uncropped height", "" + raw.uncroppedDim.x);
                exif.Add("Uncropped width", "" + raw.uncroppedDim.y);

                if (raw.metadata.isoSpeed > 0)
                    exif.Add("ISO", "" + raw.metadata.isoSpeed);
                if (raw.metadata.aperture > 0)
                    exif.Add("Aperture", "" + raw.metadata.aperture.ToString("F"));
                if (raw.metadata.exposure > 0)
                    exif.Add("Exposure time", "" + raw.metadata.ExposureAsString());

                if (raw.metadata.timeTake != null)
                    exif.Add("Time of capture", "" + raw.metadata.timeTake);
                if (raw.metadata.timeModify != null)
                    exif.Add("Time modified", "" + raw.metadata.timeModify);

                if (raw.metadata.gps != null)
                {
                    exif.Add("Longitude", raw.metadata.gps.LongitudeToString());
                    exif.Add("lattitude", raw.metadata.gps.LattitudeToString());
                    exif.Add("altitude", raw.metadata.gps.AltitudeToString());
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            //Do some UI-code that must be run on the UI thread.
                            //set exif datasource
                            //TODO add
                            exifDisplay.ItemsSource = exif;
                        });

            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO reimplement correclty
            //Just for testing purpose for now
            if (raw?.rawData != null)
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = raw.metadata.fileName
                };
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add("Jpeg image file", new List<string>() { ".jpg" });
                savePicker.FileTypeChoices.Add("PNG image file", new List<string>() { ".png" });
                savePicker.FileTypeChoices.Add("PPM image file", new List<string>() { ".ppm" });
                savePicker.FileTypeChoices.Add("TIFF image file", new List<string>() { ".tiff" });
                savePicker.FileTypeChoices.Add("BitMap image file", new List<string>() { ".bmp" });
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                DisplayLoad();
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
                    ApplyUserModif(ref raw.rawData, raw.dim, raw.ColorDepth, ref bitmap);
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

                    else if (file.FileType == ".tiff")
                    {
                        using (var filestream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            int[] t = new int[3];
                            var propertySet = new BitmapPropertySet();
                            var compressionValue = new BitmapTypedValue(
                                TiffCompressionMode.None, // no compression
                                PropertyType.UInt8
                                );
                            propertySet.Add("TiffCompressionMethod", compressionValue);
                            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, filestream, propertySet);

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
                        PpmEncoder.WriteToFile(str, ref copyOfimage, raw.dim.y, raw.dim.x, raw.ColorDepth);
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
                    StopLoadDisplay();
                });
            }
        }

        private void DisplayImage(SoftwareBitmap image, bool reset)
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
                                    if (reset)
                                        SetScrollProperty(bitmap.PixelWidth, bitmap.PixelHeight);
                                });
                    }
                });
            }
        }

        private void SetScrollProperty(int w, int h)
        {
                float x = 0;
                double relativeBorder = 1 + SettingStorage.ImageBoxBorder ;
                if (w > h)
                {
                    x = (float)(ImageDisplay.ActualWidth / (w * relativeBorder));
                }
                else
                {
                    x = (float)(ImageDisplay.ActualHeight / (h * relativeBorder));
                }
                if (x < 0.1) x = 0.1f;
                else if (x > 1) x = 1;
                ImageDisplay.MinZoomFactor = 0.1f;
                ImageDisplay.MaxZoomFactor = x + 10;
                ImageDisplay.ChangeView(null, null, x);
            
        }

        private void UpdatePreview(bool reset)
        {
            //display the histogram                    
            Task histoTask = Task.Run(async () =>
            {
                SoftwareBitmap bitmap = null;
                //Needs to run in UI thread
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, raw.previewDim.x, raw.previewDim.y);
            });
                int[] value = ApplyUserModif(ref raw.previewData, raw.previewDim, raw.ColorDepth, ref bitmap);
                DisplayImage(bitmap, reset);
                Histogram.Create(value, raw.ColorDepth, (uint)raw.previewDim.y, (uint)raw.previewDim.x, histogramCanvas);
            });
        }

        /**
         * Apply the change over the image preview
         */
        private int[] ApplyUserModif(ref ushort[] image, Point2D dim, ushort colorDepth, ref SoftwareBitmap bitmap)
        {
            ImageEffect effect = new ImageEffect();
            //get all the value 
            Task t = Task.Run(async () =>
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    effect.exposure = exposureSlider.Value;
                    effect.rMul = colorTempSlider.Value;
                    effect.gMul = colorTintSlider.Value;
                    effect.bMul = colorTintBlueSlider.Value;
                    effect.contrast = contrastSlider.Value / 10;
                    //effect.gamma = gammaSlider.Value;
                    //effect.brightness = (1 << colorDepth) * (brightnessSlider.Value / 100);
                    effect.shadow = ShadowSlider.Value * 2;
                    effect.hightlight = HighLightSlider.Value * 3;
                    effect.saturation = 1 + saturationSlider.Value / 100;
                });
            });
            t.Wait();
            effect.mul = raw.metadata.wbCoeffs;
            effect.cameraWB = cameraWB;
            effect.exposure = Math.Pow(2, effect.exposure);
            effect.camCurve = raw.curve;

            //get the softwarebitmap buffer
            return effect.applyModification(image, dim, colorDepth, ref bitmap);
        }

        private void ApplyUserModif(ref ushort[] image, Point2D dim, ushort colorDepth)
        {
            ImageEffect effect = new ImageEffect();
            //get all the value 
            Task t = Task.Run(async () =>
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    effect.exposure = exposureSlider.Value;
                    effect.rMul = colorTempSlider.Value;
                    effect.gMul = colorTintSlider.Value;
                    effect.bMul = colorTintBlueSlider.Value;
                    effect.contrast = contrastSlider.Value / 10;
                    /*
                    effect.gamma = gammaSlider.Value;
                    effect.brightness = (1 << colorDepth) * (brightnessSlider.Value / 100);*/
                    effect.shadow = ShadowSlider.Value;
                    effect.hightlight = HighLightSlider.Value;
                    effect.saturation = 1 + saturationSlider.Value / 100;
                });
            });
            t.Wait();
            effect.mul = raw.metadata.wbCoeffs;
            effect.cameraWB = cameraWB;
            effect.exposure = Math.Pow(2, effect.exposure);
            effect.camCurve = raw.curve;
            effect.applyModification(image, dim, colorDepth);
        }

        #region WBSlider
        private void WBSlider_DragStop(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                cameraWB = false;
                cameraWBCheck.IsEnabled = true;
                EnableReset();
                UpdatePreview(false);
            }
        }

        private async void EnableReset()
        {
            if (!userAppliedModif)
            {
                userAppliedModif = true;
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ResetButton.IsEnabled = true;
                });
            }
        }

        private void cameraWBCheck_Click(object sender, RoutedEventArgs e)
        {
            cameraWB = true;
            cameraWBCheck.IsEnabled = false;
            //TODO move slider to the camera WB
            SetWB();
            UpdatePreview(false);
        }
        #endregion

        private void Slider_PointerCaptureLost(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                EnableReset();
                UpdatePreview(false);
            }
        }

        private void ResetButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ResetControls();
            UpdatePreview(false);
        }
    }
}
