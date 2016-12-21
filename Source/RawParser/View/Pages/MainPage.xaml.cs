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
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using RawEditor.View.UIHelper;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using RawNet;
using System.Diagnostics;
using Windows.Graphics.Display;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using Microsoft.Services.Store.Engagement;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace RawEditor
{
    // Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array
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
        public bool ImageSelected { set; get; }
        public Size ViewDim;//for auto preview
        bool cameraWB = true;
        public Thumbnail thumbnail;
        private uint displayMutex = 0;
        private bool userAppliedModif = false;
        public ObservableCollection<HistoryObject> history = new ObservableCollection<HistoryObject>();
        private StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
#if DEBUG
        string debug = " Debug";
#else
        string debug =  "";
#endif
        public MainPage()
        {
            InitializeComponent();
            var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
            var scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel * 1.2;
            ViewDim = new Size(bounds.Width * scaleFactor, bounds.Height * scaleFactor);
            if (StoreServicesFeedbackLauncher.IsSupported())
            {
                FeedbackButton.Visibility = Visibility.Visible;
            }
            NavigationCacheMode = NavigationCacheMode.Required;
            //HistoryDisplay.ItemsSource = history;
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

        public void DisplayLoad()
        {
            displayMutex++;
            if (displayMutex > 0)
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    progressDisplay.Visibility = Visibility.Visible;
                });
            }
        }

        public void StopLoadDisplay()
        {
            displayMutex--;
            if (displayMutex <= 0)
            {
                displayMutex = 0;
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    progressDisplay.Visibility = Visibility.Collapsed;
                });
            }
        }

        private async void ImageChooseClickAsync(object sender, RoutedEventArgs e)
        {
            if (!ImageSelected)
            {
                FileOpenPicker filePicker = new FileOpenPicker()
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.ComputerFolder
                };
                // Dropdown of file types the user can open
                foreach (string format in FormatHelper.ReadSupportedFormat)
                {
                    filePicker.FileTypeFilter.Add(format);
                }
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
                        ExceptionDisplay.DisplayAsync(ex.Message + ex.StackTrace);
                        raw = null;
                        ImageSelected = false;
                    }
                }
            }
        }

        //Always call in the UI thread
        private async void EmptyImageAsync()
        {
            //empty the previous image data
            raw = null;
            history.Clear();
            await CoreApplication.MainView.CoreWindow.Dispatcher
                    .RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //empty the image display
                        ImageBox.Source = null;
                        //empty the exif data
                        ExifDisplay.ItemsSource = null;
                        //empty the histogram
                        EnableEditingControlAsync(false);
                        //free the histogram
                        histogramCanvas.Children.Clear();
                        //set back editing control to default value
                        ResetControlsAsync();
                    });
        }

        private async void ResetControlsAsync()
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
                       SetWBAsync();
                   });
        }

        private async void SetWBAsync()
        {
            int rValue = 255, bValue = 255, gValue = 255;
            if (raw != null && raw.metadata != null)
            {
                //calculate the coeff
                double r = raw.metadata.wbCoeffs[0], b = raw.metadata.wbCoeffs[2], g = raw.metadata.wbCoeffs[1];
                rValue = (int)(r * 255);
                bValue = (int)(b * 255);
                gValue = (int)(g * 255);
                /* if (rValue > 510) rValue = 765;
                 if (bValue > 510) bValue = 765;
                 if (gValue > 510) gValue = 765;*/

            }
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                colorTempSlider.Value = rValue;
                colorTintSlider.Value = gValue;
                colorTintBlueSlider.Value = bValue;
            });
        }

        private async void EnableEditingControlAsync(bool v)
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
                     SaveButton.IsEnabled = v;
                     ZoomSlider.IsEnabled = v;
                     RotateLeftButton.IsEnabled = v;
                     RotateRightButton.IsEnabled = v;
                     ShareButton.IsEnabled = v;
                 });
        }

        private void OpenFile(StorageFile file)
        {
            //Add a loading screen
            DisplayLoad();
            EmptyImageAsync();
            ImageSelected = true;
            Task.Run(async () =>
            {
                try
                {
                    Stream stream = (await file.OpenReadAsync()).AsStreamForRead();

                    //Does not improve speed
                    /*
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, (int)stream.Length);
                    stream = new MemoryStream(data);
                    stream.Position = 0;*/

                    var watch = Stopwatch.StartNew();
                    RawDecoder decoder = RawParser.GetDecoder(ref stream, file.FileType);
                    thumbnail = decoder.DecodeThumb();
                    if (thumbnail != null)
                    {
                        //read the thumbnail
                        Task.Run(() =>
                        {
                            try
                            {
                                DisplayImage(thumbnail.GetSoftwareBitmap(), true);
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("Error in thumb " + e.Message);
                            }
                        });
                    }

                    decoder.DecodeRaw();
                    decoder.DecodeMetadata();
                    raw = decoder.rawImage;
                    raw.metadata.FileName = file.DisplayName;
                    raw.metadata.FileNameComplete = file.Name;

                    watch.Stop();
                    raw.metadata.ParsingTime = watch.ElapsedMilliseconds;
                    Debug.WriteLine("Parsed done in " + watch.ElapsedMilliseconds + "ms");
                    stream.Dispose();
                    file = null;
                    decoder = null;
                    DisplayExif();
                    if (raw.isCFA)
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
                        Demosaic.Demos(ref raw, algo);
                    }
                    CreatePreview();
                    UpdatePreview(true);
                    thumbnail = null;

                    //activate the editing control
                    SetWBAsync();
                    EnableEditingControlAsync(true);
                    //dispose
                    //send an event with file extension, camera model and maker 
                    logger.Log("SuccessOpening " + file?.FileType + " " + raw?.metadata?.make + " " + raw?.metadata?.model + debug);
                    file = null;
                }
                catch (FormatException e)
                {
                    raw = null;
                    EmptyImageAsync();
                    var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                    var str = loader.GetString("ExceptionText");
                    //send an event with file extension and camer modeland maker if any                   
                    logger.Log("FailOpening " + file?.FileType + " " + raw?.metadata?.make + " " + raw?.metadata?.model + debug);
                    Debug.WriteLine(e.Message);
                    ExceptionDisplay.DisplayAsync(str);
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
                if (raw.dim.height > raw.dim.width)
                {
                    previewFactor = (int)(raw.dim.height / ViewDim.Height);
                }
                else
                {
                    previewFactor = (int)(raw.dim.width / ViewDim.Width);
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
                var file = (StorageFile)fileArgs.Files[0];
                args = null;
                if (file != null)
                {
                    // Application now has read/write access to the picked file
                    try
                    {
                        OpenFile(file);
                    }
                    catch (Exception ex)
                    {
                        ExceptionDisplay.DisplayAsync(ex.Message + ex.StackTrace);
                    }
                }
                else
                {
                    ExceptionDisplay.DisplayAsync("No file selected");
                }
            }
        }

        private void SettingClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), null);
        }

        public void DisplayExif()
        {
            if (raw != null && raw.metadata != null)
            {
                //create a list from the metadata object
                Dictionary<string, string> exif = ExifHelper.ParseExif(ref raw);
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ExifDisplay.ItemsSource = exif;
                }).AsTask().Wait();
            }
        }

        private async void SaveButtonClickAsync(object sender, RoutedEventArgs e)
        {
            if (raw?.rawData != null)
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = raw.metadata.FileName
                };

                foreach (KeyValuePair<string, List<string>> format in FormatHelper.SaveSupportedFormat)
                {
                    savePicker.FileTypeChoices.Add(format.Key, format.Value);
                }
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                DisplayLoad();
                var task = Task.Run(async () =>
                {
                    var result = await ApplyUserModifAsync(raw.rawData, raw.dim, raw.ColorDepth);
                    try
                    {
                        FormatHelper.SaveAsync(file, result.Item2);
                    }
                    catch (IOException ex)
                    {
                        ExceptionDisplay.DisplayAsync(ex.Message);
                    }
                    StopLoadDisplay();
                });
            }
        }

        private void DisplayImage(SoftwareBitmap image, bool reset)
        {
            if (image != null)
            {
                Task.Run(async () =>
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
            double relativeBorder = SettingStorage.ImageBoxBorder;
            if (w / h > ImageDisplay.ActualWidth / ImageDisplay.ActualHeight)
            {
                x = (float)(ImageDisplay.ActualWidth / (w * (1 + relativeBorder)));
            }
            else
            {
                x = (float)(ImageDisplay.ActualHeight / (h * (1 + relativeBorder)));
            }
            if (x < 0.1) x = 0.1f;
            else if (x > 1) x = (float)(1 - relativeBorder);
            ImageDisplay.MinZoomFactor = 0.1f;
            ImageDisplay.MaxZoomFactor = 2;
            ImageDisplay.ChangeView(null, null, x);
            ZoomSlider.Value = x;
        }

        private void UpdatePreview(bool reset)
        {
            //display the histogram                    
            Task.Run(async () =>
            {
                var result = await ApplyUserModifAsync(raw.previewData, raw.previewDim, raw.ColorDepth);
                DisplayImage(result.Item2, reset);
                Histogram.CreateAsync(result.Item1, raw.ColorDepth, (uint)raw.previewDim.height, (uint)raw.previewDim.width, histogramCanvas);
            });
        }

        /**
         * Apply the change over the image preview
         */
        async private Task<Tuple<int[], SoftwareBitmap>> ApplyUserModifAsync(ushort[] image, Point2D dim, ushort colorDepth)
        {
            ImageEffect effect = new ImageEffect();
            //get all the value 
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
            effect.mul = raw.metadata.wbCoeffs;
            effect.cameraWB = cameraWB;
            effect.exposure = Math.Pow(2, effect.exposure);
            effect.camCurve = raw.curve;
            effect.rotation = raw.rotation;
            SoftwareBitmap bitmap = null;
            //Needs to run in UI thread
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (raw.rotation == 1 || raw.rotation == 3)
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, dim.height, dim.width);
                }
                else
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, dim.width, dim.height);
                }
            });
            var histo = effect.ApplyModification(image, dim, colorDepth, ref bitmap);

            return Tuple.Create(histo, bitmap);
        }

        #region WBSlider
        private void WBSlider_DragStop(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                cameraWB = false;
                history.Add(new HistoryObject() { oldValue = 0, value = colorTempSlider.Value, target = EffectObject.red });
                cameraWBCheck.IsEnabled = true;
                EnableResetAsync();
                UpdatePreview(false);
            }
        }

        private async void EnableResetAsync()
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

        private void CameraWBCheck_Click(object sender, RoutedEventArgs e)
        {
            cameraWB = true;
            cameraWBCheck.IsEnabled = false;
            //TODO move slider to the camera WB
            SetWBAsync();
            UpdatePreview(false);
        }
        #endregion

        private void Slider_PointerCaptureLost(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.previewData != null)
            {
                history.Add(new HistoryObject() { oldValue = 0, value = saturationSlider.Value, target = EffectObject.saturation });
                EnableResetAsync();
                UpdatePreview(false);
            }
        }

        private void ResetButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            history.Add(new HistoryObject() { oldValue = 0, value = 1, target = EffectObject.reset });
            ResetControlsAsync();
            UpdatePreview(false);
        }

        private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ImageDisplay.ChangeView(null, null, (float)e.NewValue);
        }

        private void RotateRightButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var t = new HistoryObject() { oldValue = raw.rotation, target = EffectObject.rotate };
            raw.rotation++;
            raw.rotation = raw.rotation % 4;
            t.value = raw.rotation;
            history.Add(t);
            UpdatePreview(false);
        }

        private void RotateLeftButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var t = new HistoryObject() { oldValue = raw.rotation, target = EffectObject.rotate };
            if (raw.rotation == 0) raw.rotation = 3;
            else raw.rotation--;
            t.value = raw.rotation;
            history.Add(t);
            UpdatePreview(false);
        }

        private void ImageDisplay_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ZoomSlider.Value = ImageDisplay.ZoomFactor;
        }

        private async void FeedbackButton_TappedAsync(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var launcher = StoreServicesFeedbackLauncher.GetDefault();
            await launcher.LaunchAsync();
        }

        private void ShareButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            DataTransferManager manager = DataTransferManager.GetForCurrentView();
            manager.DataRequested += DataTransferManager_DataRequestedAsync;

            DataTransferManager.ShowShareUI();
        }

        private async void DataTransferManager_DataRequestedAsync(DataTransferManager manager, DataRequestedEventArgs args)
        {
            try
            {

                DataRequest request = args.Request;
                request.Data.Properties.Title = "Share image";
                request.Data.Properties.Description = "";
                var deferal = request.GetDeferral();
                //TODO regionalise text
                //generate the bitmap
                DisplayLoad();
                var result = await ApplyUserModifAsync(raw.rawData, raw.dim, raw.ColorDepth);
                InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                //Needs to run in the UI thread because fuck performance
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    encoder.SetSoftwareBitmap(result.Item2);
                });
                await encoder.FlushAsync();
                encoder = null;
                result.Item2.Dispose();
                StopLoadDisplay();

                request.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                deferal.Complete();

            }
            catch (Exception e)
            {
                ExceptionDisplay.DisplayAsync(e.Message);
            }
        }
    }
}
