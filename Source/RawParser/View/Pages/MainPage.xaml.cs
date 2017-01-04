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
using Windows.UI.ViewManagement;
using Windows.Foundation;
using RawNet;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Microsoft.Services.Store.Engagement;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using RawEditor.Effect;
using Windows.System;

namespace RawEditor
{
    /// <summary>
    /// The main class of the appliation
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public RawImage raw;
        public bool ImageSelected { set; get; } = false;
        bool cameraWB = true;
        public Thumbnail thumbnail;
        private bool userAppliedModif = false;
        //public ObservableCollection<HistoryObject> history = new ObservableCollection<HistoryObject>();
        public Bindable<bool> ResetButtonVisibility { get; set; } = new Bindable<bool>(false);
        public Bindable<bool> ControlVisibilty { get; set; } = new Bindable<bool>(false);
        public ObservableCollection<ExifValue> ExifSource = new ObservableCollection<ExifValue>();
#if !DEBUG
        private StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
#endif
        public MainPage()
        {
            InitializeComponent();
            if (StoreServicesFeedbackLauncher.IsSupported())
            {
                FeedbackButton.Visibility = Visibility.Visible;
            }
            NavigationCacheMode = NavigationCacheMode.Required;
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
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
                    OpenFile(file);
            }
        }

        //Always call in the UI thread
        private async void EmptyImageAsync()
        {
            //empty the previous image data
            raw = null;
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                CropUI.SetThumbAsync(null);
                //empty the image display
                ImageBox.Source = null;
                //empty the exif data
                //history?.Clear();
                ExifSource?.Clear();
                //empty the histogram
                EnableEditingControlAsync(false);
                LumaHisto.Points = null;
                RedHisto.Points = null;
                GreenHisto.Points = null;
                BlueHisto.Points = null;
            });
            GC.Collect();
        }

        private async void ResetControlsAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                exposureSlider.Value = 0;
                ShadowSlider.Value = 0;
                HighLightSlider.Value = 0;
                contrastSlider.Value = 0;
                saturationSlider.Value = 0;
                userAppliedModif = false;
                CropUI.ResetCrop();
                CropUI.SetThumbAsync(null);
                VignetSlider.Value = 0;
                GammaToggle.IsChecked = raw?.IsGammaCorrected ?? true;
                if (raw != null)
                {
                    raw.raw.offset = new Point2D(0, 0);
                    raw.raw.dim = new Point2D(raw.raw.uncroppedDim.width, raw.raw.uncroppedDim.height);
                    raw.preview.offset = new Point2D(0, 0);
                    raw.preview.dim = new Point2D(raw.preview.uncroppedDim.width, raw.preview.uncroppedDim.height);
                    raw.Rotation = raw.metadata.OriginalRotation;
                }
                SetWBAsync();
                ResetButtonVisibility.Value = false;
            });
            if (raw != null)
            {
                UpdatePreview(false);
            }
        }

        private async void SetWBAsync()
        {
            int rValue = 255, bValue = 255, gValue = 255;
            if (raw != null && raw.metadata != null)
            {
                //calculate the coeff
                double r = raw.metadata.WbCoeffs[0], b = raw.metadata.WbCoeffs[2], g = raw.metadata.WbCoeffs[1];
                rValue = (int)(r * 255);
                bValue = (int)(b * 255);
                gValue = (int)(g * 255);

            }
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                colorTempSlider.Value = rValue;
                colorTintSlider.Value = gValue;
                colorTintBlueSlider.Value = bValue;
            });
        }

        private void EnableEditingControlAsync(bool v)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                colorTempSlider.IsEnabled = v;
                colorTintSlider.IsEnabled = v;
                colorTintBlueSlider.IsEnabled = v;
                exposureSlider.IsEnabled = v;
                ShadowSlider.IsEnabled = v;
                HighLightSlider.IsEnabled = v;
                contrastSlider.IsEnabled = v;
                saturationSlider.IsEnabled = v;
                SaveButton.IsEnabled = v;
                ZoomSlider.IsEnabled = v;
                RotateLeftButton.IsEnabled = v;
                RotateRightButton.IsEnabled = v;
                ShareButton.IsEnabled = v;
                CropButton.IsEnabled = v;
                VignetSlider.IsEnabled = v;
                GammaToggle.IsEnabled = v;
            });
        }

        private void OpenFile(StorageFile file)
        {
            try
            {
                ExifSource.Add(new ExifValue("Test", "Test"));
                //Add a loading screen
                Load.ShowLoad();
                EmptyImageAsync();
                Task.Run(async () =>
                {
                    try
                    {
                        ImageSelected = true;
                        using (Stream stream = (await file.OpenReadAsync()).AsStreamForRead())
                        {
                            var watch = Stopwatch.StartNew();
                            RawDecoder decoder = RawParser.GetDecoder(stream, file.FileType);
                            try
                            {
                                thumbnail = decoder.DecodeThumb();
                                //read the thumbnail
                                Task.Run(() =>
                            {
                                try
                                {
                                    DisplayImage(thumbnail?.GetSoftwareBitmap(), true);
                                }
                                catch (Exception e)
                                {
                                    Debug.WriteLine("Error in thumb " + e.Message);
                                }
                            });
                            }
                            catch (Exception)
                            {                                    //since thumbnail are optionnal, we ignore all errors 
                            }

                            decoder.DecodeRaw();
                            decoder.DecodeMetadata();
                            raw = decoder.rawImage;
                            //if (decoder.ScaleValue)
                            //raw.ScaleValues();
                            raw.metadata.FileName = file.DisplayName;
                            raw.metadata.FileNameComplete = file.Name;
                            raw.metadata.FileExtension = file.FileType;
                            if (raw.errors.Count > 0)
                            {
                                ExceptionDisplay.Display("This file is not fully supported, it may appear incorrectly");
#if !DEBUG
                                //send an event with file extension and camera model and make if any                   
                                logger.Log("ErrorOnOpen " + file?.FileType.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model + ""+raw.errors.Count);
#endif
                            }
                            watch.Stop();
                            raw.metadata.ParsingTime = watch.ElapsedMilliseconds;
                        }

                        //create a list from the metadata object
                        CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            raw.ParseExif(ExifSource);
                        });
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
                            Demosaic.Demos(raw, algo);
                        }
                        raw.CreatePreview(SettingStorage.PreviewFactor, ImageDisplay.ViewportHeight, ImageDisplay.ViewportWidth);

                        //check if enough memory
                        if (MemoryManager.AppMemoryUsageLimit - MemoryManager.AppMemoryUsage < (ulong)raw.raw.data.Length || MemoryManager.AppMemoryUsageLevel == AppMemoryUsageLevel.High)
                        {
                            ExceptionDisplay.Display("The image is bigger than what your device support, this application may fail when saving. Only " + ((MemoryManager.AppMemoryUsageLimit - MemoryManager.AppMemoryUsage) / (1024 * 1024)) + "Mb left of memory for this app to use");
                        }
#if !DEBUG
                    //send an event with file extension, camera model and make
                    logger.Log("SuccessOpening " + raw?.metadata?.FileExtension.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model);
#endif
                        UpdatePreview(true);
                        thumbnail = null;
                        ResetControlsAsync();
                        EnableEditingControlAsync(true);
                    }
                    catch (Exception e)
                    {
                        raw = null;
                        EmptyImageAsync();
                        ImageSelected = false;

#if DEBUG
                        Debug.WriteLine(e.Message);
#else
                                                //send an event with file extension and camera model and make if any                   
                    logger.Log("FailOpening " + file?.FileType.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model);                       
#endif
                        var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                        var str = loader.GetString("ExceptionText");
                        ExceptionDisplay.Display(str);
                    }
                    Load.HideLoad();
                    ImageSelected = false;
                });
            }
            catch (Exception e)
            {
                ExceptionDisplay.Display("Somethig wrong happened sorry. (" + e.GetType() + ")");
#if DEBUG
                Debug.WriteLine(e.Message + " " + e.StackTrace);
#endif
            }
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
                OpenFile(file);
            }
        }

        private void SettingClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), null);
        }

        private async void SaveButtonClickAsync(object sender, RoutedEventArgs e)
        {
            if (raw?.raw.data != null)
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

                Load.ShowLoad();
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ApplyUserModifAsync(raw.raw.data, raw.raw.dim, raw.raw.offset, raw.raw.uncroppedDim, raw.ColorDepth, false);
                        FormatHelper.SaveAsync(file, result.Item2);
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        ExceptionDisplay.Display(ex.Message);
#else
                        ExceptionDisplay.Display("An error occured while saving");
#endif
                    }
                    Load.HideLoad();
                });
            }
        }

        private void DisplayImage(SoftwareBitmap image, bool reset)
        {
            if (image != null)
            {
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Do some UI-code that must be run on the UI thread.
                    //display the image preview
                    ImageBox.Source = null;
                    WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                    image.CopyToBuffer(bitmap.PixelBuffer);
                    image.Dispose();
                    ImageBox.Source = bitmap;
                    if (reset)
                        SetScrollProperty(bitmap.PixelWidth, bitmap.PixelHeight);
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
                var result = await ApplyUserModifAsync(raw.preview.data, raw.preview.dim, raw.preview.offset, raw.preview.uncroppedDim, raw.ColorDepth, true);
                DisplayImage(result.Item2, reset);

                var histo = new Histogram();
                histo.FillAsync(result.Item1, (uint)raw.preview.dim.height, (uint)raw.preview.dim.width);
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    LumaHisto.Points = histo.PointsL;
                    RedHisto.Points = histo.PointsR;
                    GreenHisto.Points = histo.PointsG;
                    BlueHisto.Points = histo.PointsB;
                });
            });
        }

        /**
         * Apply the change over the image preview
         */
        async private Task<Tuple<HistoRaw, SoftwareBitmap>> ApplyUserModifAsync(ushort[] image, Point2D dim, Point2D offset, Point2D uncrop, int colorDepth, bool histo)
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
                effect.shadow = ShadowSlider.Value;
                effect.hightlight = HighLightSlider.Value;
                effect.saturation = 1 + saturationSlider.Value / 100;
                effect.vignet = VignetSlider.Value;
                effect.ReverseGamma = (bool)GammaToggle.IsChecked;
            });

            effect.mul = raw.metadata.WbCoeffs;
            effect.cameraWB = cameraWB;
            effect.exposure = Math.Pow(2, effect.exposure);
            //effect.camCurve = raw.curve;
            effect.rotation = raw.Rotation;
            SoftwareBitmap bitmap = null;

            //Needs to run in UI thread
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (raw.Rotation == 1 || raw.Rotation == 3)
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, dim.height, dim.width);
                }
                else
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, dim.width, dim.height);
                }
            });
            if (histo)
            {
                var tmp = effect.ApplyModification(image, dim, offset, uncrop, colorDepth, bitmap, histo);
                return Tuple.Create(tmp, bitmap);
            }
            else
            {
                effect.ApplyModification(image, dim, offset, uncrop, colorDepth, bitmap, false);
                return Tuple.Create(new HistoRaw(), bitmap);
            }
        }

        #region WBSlider
        private void WBSlider_DragStop(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (raw?.preview.data != null)
            {
                cameraWB = false;
                //history.Add(new HistoryObject() { oldValue = 0, value = colorTempSlider.Value, target = EffectObject.red });
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
                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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

        private void EditingControlChanged()
        { //history.Add(new HistoryObject() { oldValue = 0, value = saturationSlider.Value, target = EffectObject.saturation });
            EnableResetAsync();
            UpdatePreview(false);
        }

        private void ResetButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (CropUI.Visibility == Visibility.Visible)
            {
                HideCropUI();
                Load.HideLoad();
            }
            ResetControlsAsync();
            //history.Add(new HistoryObject() { oldValue = 0, value = 1, target = EffectObject.reset });

        }

        private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ImageDisplay.ChangeView(null, null, (float)e.NewValue);
        }

        private void RotateRightButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            raw.Rotation++;
            /*var t = new HistoryObject() { oldValue = raw.Rotation, target = EffectObject.rotate };
            
            t.value = raw.Rotation;
            history.Add(t);*/
            EnableResetAsync();
            UpdatePreview(false);
        }

        private void RotateLeftButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            raw.Rotation--;
            /*
            var t = new HistoryObject() { oldValue = raw.Rotation, target = EffectObject.rotate };
            
            t.value = raw.Rotation;
            history.Add(t);*/
            EnableResetAsync();
            UpdatePreview(false);
        }        

        private void FeedbackButton_TappedAsync(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            StoreServicesFeedbackLauncher.GetDefault().LaunchAsync();
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
                Load.ShowLoad();
                var result = await ApplyUserModifAsync(raw.raw.data, raw.raw.dim, raw.raw.offset, raw.raw.uncroppedDim, raw.ColorDepth, false);
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
                Load.HideLoad();

                request.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                deferal.Complete();
            }
            catch (Exception e)
            {
                ExceptionDisplay.Display(e.Message);
            }
        }

        private void ReportButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri(@"https://github.com/arimhan/RawParser/issues"));
        }

        private void GitterButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri(@"https://gitter.im/RawParser/Lobby"));
        }

        private async void CropButton_TappedAsync(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            CropUI.SetThumbAsync(null);
            Load.ShowLoad();
            EnableEditingControlAsync(false);
            //display the crop UI
            CropGrid.Visibility = Visibility.Visible;
            //wait for accept or reset pressed

            int h, w;
            if (raw.Rotation == 1 || raw.Rotation == 3)
            {
                h = raw.preview.uncroppedDim.width;
                w = raw.preview.uncroppedDim.height;
            }
            else
            {
                h = raw.preview.uncroppedDim.height;
                w = raw.preview.uncroppedDim.width;
            }
            double factor;
            if (w > h)
            {
                factor = ImageDisplay.ActualWidth / (w + 160);
            }
            else
            {
                factor = ImageDisplay.ActualHeight / (h + 160);
            }
            CropUI.SetSize((int)(w * factor), (int)(h * factor), raw.Rotation);
            //create a preview of the image
            var result = await ApplyUserModifAsync(raw.preview.data, raw.preview.uncroppedDim, new Point2D(0, 0), raw.preview.uncroppedDim, raw.ColorDepth, false);
            //display the preview
            CropUI.SetThumbAsync(result.Item2);
        }

        private void CropAccept_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            //hide Crop UI
            HideCropUI();
            double top = CropUI.Top;
            double left = CropUI.Left;
            double right = CropUI.Right;
            double bottom = CropUI.Bottom;

            raw.raw.offset = new Point2D((int)(raw.raw.uncroppedDim.width * left), (int)(raw.raw.uncroppedDim.height * top));
            raw.raw.dim = new Point2D((int)(raw.raw.uncroppedDim.width * right), (int)(raw.raw.uncroppedDim.height * bottom));

            raw.preview.offset = new Point2D((int)(raw.preview.uncroppedDim.width * left), (int)(raw.preview.uncroppedDim.height * top));
            raw.preview.dim = new Point2D((int)(raw.preview.uncroppedDim.width * right), (int)(raw.preview.uncroppedDim.height * bottom));

            UpdatePreview(true);
            //var t = new HistoryObject() { oldValue = 0, target = EffectObject.crop };
            //history.Add(t)
            EnableResetAsync();
        }

        private void HideCropUI()
        {
            Load.HideLoad();
            CropGrid.Visibility = Visibility.Collapsed;
            EnableEditingControlAsync(true);
        }
    }
}