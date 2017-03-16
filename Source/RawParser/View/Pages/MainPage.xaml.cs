using Microsoft.Services.Store.Engagement;
using RawEditor.Base;
using RawEditor.Effect;
using RawEditor.Model;
using RawEditor.Settings;
using RawEditor.View.UIHelper;
using RawNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RawEditor.View.Pages
{
    /// <summary>
    /// The main class of the appliation
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public bool ImageSelected { set; get; } = false;
        public Thumbnail thumbnail;
        public Bindable<bool> ResetButtonVisibility = new Bindable<bool>(false);
        public Bindable<bool> ControlVisibilty = new Bindable<bool>(false);
        public CollectionViewSource ExifSource = new CollectionViewSource() { IsSourceGrouped = true };
        public Bindable<bool> feedbacksupport = new Bindable<bool>(StoreServicesFeedbackLauncher.IsSupported());
        public Histogram Histo { get; set; } = new Histogram();
        public ImageEffect EditionValue = new ImageEffect();
        public ImageEffect DefaultValue = new ImageEffect();
        public ImageEffect OldValue = new ImageEffect();
        public HistoryList History = new HistoryList();
        private Bindable<Boolean> selectManualWB = new Bindable<bool>(false);
        private int rotation;
        private bool isBindingEnabled = true;

        public static RawImage<ushort> rawImage;
#if !DEBUG
        private StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
#endif

        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
            History.HistoryChanged += new PropertyChangedEventHandler((e, d) =>
            {
                EditionValue.Copy(History.GetCurrent().effect);
                UpdatePreview(false);
            });
            History.Default = new HistoryObject(EffectType.Unkown, DefaultValue);

            if (!SettingStorage.EnableDebug) PivotGrid.Items.Remove(ToolsPivot); //Super ugly (visibilty.collapsed no work for pivot..
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
        private void EmptyImage()
        {
            //empty the previous image data
            rawImage = null;
            ImageBoxPlain.Source = null;
            CropUI.SetThumbAsync(null);
            //empty the image display
            ImageBox.Source = null;
            //empty the exif data
            History?.Clear();
            ExifSource.Source = null;
            //empty the histogram
            ControlVisibilty.Value = false;
            ResetControls();
            DisplayImage(null, false); ;
            Histo.ClearAsync();
            GC.Collect();
        }

        private void ResetControls()
        {
            EditionValue.Copy(DefaultValue);
            CropUI.ResetCrop();
            CropUI.SetThumbAsync(null);
            rawImage?.ResetCrop();
            SetImageSizeText();
            ImageBoxPlain.Visibility = Visibility.Collapsed;
            ResetButtonVisibility.Value = false;
            BeforeToggle.IsChecked = false;
            HideCropUI();
        }

        private void ResetUpdateControls()
        {
            ResetControls();

            if (rawImage == null || isBindingEnabled == false) return;

            var old = new ImageEffect();
            old.Copy(EditionValue);
            History.Add(new HistoryObject(EffectType.Reset, EditionValue.GetCopy()) { oldValue = old });
            UpdatePreview(true);
        }

        private void SetWBUpdate()
        {
            //add an history object
            if (EditionValue != null && DefaultValue != null)
            {
                History.Add(new HistoryObject(EffectType.WhiteBalance, EditionValue.GetCopy())
                {
                    oldValue = new double[] { EditionValue.RMul, EditionValue.GMul, EditionValue.BMul },
                    value = new double[] { rawImage?.metadata?.WbCoeffs.Red ?? 1, rawImage?.metadata?.WbCoeffs.Green ?? 1, rawImage?.metadata?.WbCoeffs.Blue ?? 1 }
                });
                EditionValue.RMul = DefaultValue.RMul;
                EditionValue.GMul = DefaultValue.GMul;
                EditionValue.BMul = DefaultValue.BMul;
                UpdatePreview(false);
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
                RawHelper.OpenFile(file);
            }
        }

        private void SettingClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsView), null);
        }

        private void OpenFile(StorageFile file)
        {
            //Add a loading screen
            if (rawImage != null)
            {
                EmptyImage();
            }
            Load.Show();
            ImageSelected = true;
            isBindingEnabled = false;

            Task.Run(async () =>
            {
                try
                {
                    var watchTotal = Stopwatch.StartNew();
                    using (Stream stream = (await file.OpenReadAsync()).AsStreamForRead())
                    {
                        var watchdecode = Stopwatch.StartNew();
                        RawDecoder decoder = RawParser.GetDecoder(stream, file);
                        try
                        {
                            thumbnail = decoder.DecodeThumb();
                            Task.Run(() =>
                            {
                                var result = thumbnail?.GetSoftwareBitmap();
                                DisplayImage(result, true);
                            });
                        }
                        //since thumbnail are optionnal, we ignore all errors           
                        catch (Exception) { }

                        decoder.DecodeRaw();
                        decoder.DecodeMetadata();
                        rawImage = decoder.rawImage;

                        watchdecode.Stop();
                        Debug.WriteLine("Decoding done in " + watchdecode.ElapsedMilliseconds + " ms");

                        var watchLook = Stopwatch.StartNew();
                        rawImage.table?.ApplyTableLookUp(rawImage.raw);
                        watchLook.Stop();
                        Debug.WriteLine("Lookup done in " + watchLook.ElapsedMilliseconds + " ms");

                        var watchScale = Stopwatch.StartNew();
                        ImageHelper.ScaleValues(rawImage);
                        watchScale.Stop();
                        Debug.WriteLine("Scale done in " + watchScale.ElapsedMilliseconds + " ms");
                    }

                    rawImage.metadata.SetFileMetatdata(file);

                    if (rawImage.isCFA)
                    {
                        var watchDemos = Stopwatch.StartNew();
                        //get the algo from the settings
                        DemosaicAlgorithm algo;
                        try
                        {
                            algo = SettingStorage.DemosAlgo;
                        }
                        catch (Exception)
                        {
                            algo = DemosaicAlgorithm.FastAdams;
                        }
                        Demosaic.Demos(rawImage, algo);

                        watchDemos.Stop();
                        Debug.WriteLine("Demos done in " + watchDemos.ElapsedMilliseconds + " ms");
                    }

                    if (rawImage.convertionM != null)
                    {
                        var watchConvert = Stopwatch.StartNew();
                        rawImage.ConvertRGB();
                        watchConvert.Stop();
                        Debug.WriteLine("ConvertRGB done in " + watchConvert.ElapsedMilliseconds + " ms");
                    }

                    var watchPreview = Stopwatch.StartNew();
                    ImageHelper.CreatePreview(SettingStorage.PreviewFactor, ImageDisplay.ViewportHeight, ImageDisplay.ViewportWidth, rawImage);
                    watchPreview.Stop();
                    Debug.WriteLine("Preview done in " + watchPreview.ElapsedMilliseconds + " ms");

                    watchTotal.Stop();
                    rawImage.metadata.ParsingTime = watchTotal.ElapsedMilliseconds;
                    GC.Collect();
                    //check if enough memory
                    if (MemoryManager.AppMemoryUsageLimit - MemoryManager.AppMemoryUsage < ((ulong)rawImage.raw.green.Length * 6) || MemoryManager.AppMemoryUsageLevel == AppMemoryUsageLevel.High)
                    {
                        TextDisplay.DisplayWarning("The image is bigger than what your device support, this application may fail when saving. Only " + ((MemoryManager.AppMemoryUsageLimit - MemoryManager.AppMemoryUsage) / (1024 * 1024)) + "Mb left of memory for this app to use");
                    }
#if !DEBUG
                    //send an event with file extension, camera model and make
                    logger.Log("SuccessOpening " + rawImage?.metadata?.FileExtension.ToLower() + " " + rawImage?.metadata?.Make + " " + rawImage?.metadata?.Model);
#endif
                    DefaultValue.Rotation = rawImage.metadata.OriginalRotation;
                    DefaultValue.ReverseGamma = rawImage.IsGammaCorrected;
                    DefaultValue.RMul = rawImage?.metadata.WbCoeffs?.Red ?? 1;
                    DefaultValue.GMul = rawImage?.metadata.WbCoeffs?.Green ?? 1;
                    DefaultValue.BMul = rawImage?.metadata.WbCoeffs?.Blue ?? 1;
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        var exif = rawImage.ParseExif();
                        //Group the data
                        var groups = from x in exif group x by x.Group into grp orderby grp.Key select grp;
                        //Set the grouped data to CollectionViewSource
                        ExifSource.Source = groups;
                        ResetControls();
                        UpdatePreview(true);
                        ControlVisibilty.Value = true;
                        SetImageSizeText();
                    });
                    if (rawImage.errors.Count > 0)
                    {
                        var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                        TextDisplay.DisplayWarning(loader.GetString("ErrorOnLoadWarning"));
#if !DEBUG
                        //send an event with file extension and camera model and make if any                   
                        logger.Log("ErrorOnOpen " + file?.FileType.ToLower() + " " + rawImage?.metadata?.Make + " " + rawImage?.metadata?.Model + "" + rawImage.errors.Count);
#endif
                    }
                    isBindingEnabled = true;
                    thumbnail = null;
                }
                catch (Exception)
                {
#if !DEBUG
                    //send an event with file extension and camera model and make if any                   
                    logger.Log("FailOpening " + file?.FileType.ToLower() + " " + rawImage?.metadata?.Make + " " + rawImage?.metadata?.Model);
#endif

                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        EmptyImage();
                    });
                    ImageSelected = false;
                    var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                    TextDisplay.DisplayError(loader.GetString("ExceptionText"));
                }

                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Blur(false);
                    Load.Hide();
                });
                ImageSelected = false;
            });
        }

        private async void SaveButtonClickAsync(object sender, RoutedEventArgs e)
        {
            if (rawImage?.raw != null)
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = rawImage.metadata.FileName
                };

                foreach (KeyValuePair<string, List<string>> format in FormatHelper.SaveSupportedFormat)
                {
                    savePicker.FileTypeChoices.Add(format.Key, format.Value);
                }
                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                Load.Show();

                //TODO
                //show an option Ui
                //when user click ok save the properties to a bitmapProperies
                //check isuser chose 16bits or not
                //save

                var task = Task.Run(() =>
                {
                    try
                    {
                        var watchPreview = Stopwatch.StartNew();
                        var result = ApplyImageEffect8bitsNoHistoAsync(rawImage.raw, EditionValue);
                        watchPreview.Stop();
                        Debug.WriteLine("Apply done in " + watchPreview.ElapsedMilliseconds + " ms");


                        watchPreview = Stopwatch.StartNew();
                        //call GC here (GC does not empty automatically native object)
                        GC.Collect();
                        FormatHelper.SaveAsync(file, result);
                        watchPreview.Stop();
                        Debug.WriteLine("Save done in " + watchPreview.ElapsedMilliseconds + " ms");
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        TextDisplay.DisplayError(ex.Message);
#else
                        TextDisplay.DisplayError("An error occured while saving");
#endif
                    }

                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //Blur(false);
                        Load.Hide();
                    });
                });
            }
        }

        private void DisplayImage(SoftwareBitmap image, bool move)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ImageBox.Source = null;
                if (image != null)
                {
                    WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                    image.CopyToBuffer(bitmap.PixelBuffer);
                    ImageBox.Source = bitmap;
                    if (move) CenterImage(image.PixelWidth, image.PixelHeight);
                    image.Dispose();
                }
            });
        }

        private void CenterImageBindable()
        {
            CenterImage((int)ImageBox.ActualWidth, (int)ImageBox.ActualHeight);
        }

        private void CenterImage(int width, int height)
        {
            if (width == 0 || height == 0) return;
            float ZeroFactor = 0;
            double relativeBorder = SettingStorage.ImageBoxBorder;
            if (width / height > ImageDisplay.ActualWidth / ImageDisplay.ActualHeight)
            {
                ZeroFactor = (float)(ImageDisplay.ActualWidth / (width * (1 + relativeBorder)));
            }
            else
            {
                ZeroFactor = (float)(ImageDisplay.ActualHeight / (height * (1 + relativeBorder)));
            }
            if (ZeroFactor < 0.1) ZeroFactor = 0.1f;
            else if (ZeroFactor > 1) ZeroFactor = (float)(1 - relativeBorder);
            ImageDisplay.MinZoomFactor = 0.1f;
            ImageDisplay.MaxZoomFactor = 10;
            ZoomSlider.Value = ZeroFactor;
            ImageDisplay.ChangeView(0, 0, ZeroFactor);
        }

        public void TestFunc()
        {
            Debug.Assert(false);
        }

        private void UpdatePreview(bool move)
        {
            if (rawImage?.preview == null) return;

            Task.Run(() =>
            {
                var watchScale = Stopwatch.StartNew();
                var result = ApplyImageEffect8bits(rawImage.preview, EditionValue);
                DisplayImage(result.Item2, move);
                Histo.FillAsync(result.Item1);
                watchScale.Stop();
                Debug.WriteLine("Update done in " + watchScale.ElapsedMilliseconds + " ms");
            });
        }

        private SoftwareBitmap CreateBitmap(BitmapPixelFormat format, int rotation, Point2D dim)
        {
            SoftwareBitmap bitmap = null;
            //Needs to run in UI thread
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (rotation == 1 || rotation == 3)
                {
                    bitmap = new SoftwareBitmap(format, (int)dim.height, (int)dim.width);
                }
                else
                {
                    bitmap = new SoftwareBitmap(format, (int)dim.width, (int)dim.height);
                }
            }).AsTask().Wait();
            return bitmap;
        }

        //Apply the change over the image preview       
        private Tuple<HistoRaw, SoftwareBitmap> ApplyImageEffect8bits(ImageComponent<ushort> image, ImageEffect edition)
        {
            SoftwareBitmap bitmap = CreateBitmap(BitmapPixelFormat.Bgra8, edition.Rotation, image.dim);
            var tmp = edition.ApplyTo8Bits(image, bitmap, true);
            return Tuple.Create(tmp, bitmap);
        }

        //Apply the change over the image preview       
        private SoftwareBitmap ApplyImageEffect8bitsNoHistoAsync(ImageComponent<ushort> image, ImageEffect edition)
        {
            SoftwareBitmap bitmap = CreateBitmap(BitmapPixelFormat.Bgra8, edition.Rotation, image.dim);
            edition.ApplyTo8Bits(image, bitmap, false);
            return bitmap;
        }

        private void EditingControlChanged()
        {
            //find the changed value
            //TODO remove this
            if (!isBindingEnabled || rawImage?.raw == null || EditionValue == null) return;
            History.Add(OldValue.GetHistory(EditionValue));
            OldValue.Copy(EditionValue);
            if (LowGamma.IsChecked == true)
            {
                EditionValue.Gamma = 1.8;
            }
            else if (HighGamma.IsChecked == true)
            {
                EditionValue.Gamma = 2.4;
            }
            else
            {
                EditionValue.Gamma = 2.2;
            }
            ResetButtonVisibility.Value = true;
            UpdatePreview(false);
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            ImageDisplay.ChangeView(null, null, (float)e.NewValue);
        }

        private void RotateRightButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (rawImage != null)
            {
                var t = new HistoryObject(EffectType.Rotate, EditionValue.GetCopy()) { oldValue = EditionValue.Rotation };
                EditionValue.Rotation++;
                t.value = EditionValue.Rotation;
                History.Add(t);
                ResetButtonVisibility.Value = true;
                UpdatePreview(false);
            }
        }

        private void RotateLeftButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (rawImage != null)
            {
                var t = new HistoryObject(EffectType.Rotate, EditionValue.GetCopy()) { oldValue = EditionValue.Rotation };
                EditionValue.Rotation--;
                t.value = EditionValue.Rotation;
                History.Add(t);
                ResetButtonVisibility.Value = true;
                UpdatePreview(false);
            }
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
                Load.Show();
                var result = ApplyImageEffect8bitsNoHistoAsync(rawImage.raw, EditionValue);
                InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);

                //Needs to run in the UI thread because fuck performance
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    encoder.SetSoftwareBitmap(result);
                });
                await encoder.FlushAsync();
                encoder = null;
                result.Dispose();
                Load.Hide();

                request.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                deferal.Complete();
            }
            catch (Exception e)
            {
                TextDisplay.DisplayError(e.Message);
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
            if (rawImage?.raw != null)
            {
                CropUI.SetThumbAsync(null);
                Load.Show();
                // Blur(true);
                ControlVisibilty.Value = false;
                //display the crop UI
                CropGrid.Visibility = Visibility.Visible;
                //wait for accept or reset pressed

                uint h, w;
                if (EditionValue.Rotation == 1 || EditionValue.Rotation == 3)
                {
                    h = rawImage.preview.UncroppedDim.width;
                    w = rawImage.preview.UncroppedDim.height;
                }
                else
                {
                    h = rawImage.preview.UncroppedDim.height;
                    w = rawImage.preview.UncroppedDim.width;
                }
                double factor;
                if (w > h)
                {
                    factor = ImageDisplay.ActualWidth / (w);
                }
                else
                {
                    factor = ImageDisplay.ActualHeight / (h + 160);
                }
                CropUI.SetSize((int)(w * factor * 0.7), (int)(h * factor * 0.7), EditionValue.Rotation);
                ImageComponent<ushort> img = new ImageComponent<ushort>(rawImage.preview) { offset = new Point2D(), dim = new Point2D(rawImage.preview.UncroppedDim) };
                rawImage.preview.dim = new Point2D(rawImage.preview.UncroppedDim);
                //create a preview of the image             
                SoftwareBitmap image = await Task.Run(() =>
                {
                    return ApplyImageEffect8bitsNoHistoAsync(img, EditionValue);
                });
                //display the preview
                CropUI.SetThumbAsync(image);
            }
        }

        private void CropAccept_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            HideCropUI();
            double top = CropUI.Top;
            double left = CropUI.Left;
            double right = CropUI.Right;
            double bottom = CropUI.Bottom;
            if (rawImage?.raw != null && rawImage?.preview != null)
            {
                rawImage.raw.offset = new Point2D((uint)(rawImage.raw.UncroppedDim.width * left), (uint)(rawImage.raw.UncroppedDim.height * top));
                rawImage.raw.dim = new Point2D((uint)(rawImage.raw.UncroppedDim.width * right), (uint)(rawImage.raw.UncroppedDim.height * bottom));
                rawImage.preview.offset = new Point2D((uint)(rawImage.preview.UncroppedDim.width * left), (uint)(rawImage.preview.UncroppedDim.height * top));
                rawImage.preview.dim = new Point2D((uint)(rawImage.preview.UncroppedDim.width * right), (uint)(rawImage.preview.UncroppedDim.height * bottom));
                UpdatePreview(true);
                //set the display size
                SetImageSizeText();
            }
            var t = new HistoryObject(EffectType.Crop, EditionValue.GetCopy()) { oldValue = 0 };
            History.Add(t);
            ResetButtonVisibility.Value = true;
        }

        //TODO replace by binding if possible
        private void SetImageSizeText()
        {
            ImageHeight.Text = rawImage.raw.dim.height + "px";
            ImageWidth.Text = rawImage.raw.dim.width + "px";
        }

        private void HideCropUI()
        {
            if (CropGrid.Visibility == Visibility.Visible)
            {
                Load.Hide();
                CropGrid.Visibility = Visibility.Collapsed;
                CropUI.isRightDragging = CropUI.isTopDragging = false;
                ControlVisibilty.Value = true;
            }
        }

        private void ListView_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            History.SetCurrent(((ListView)sender).SelectedIndex);
        }

        private async void ShowBeforeDisplayAsync()
        {
            //if first time create the image
            if (ImageBoxPlain.Source == null || rotation != EditionValue.Rotation)
            {
                var edit = DefaultValue.GetCopy();
                rotation = edit.Rotation = EditionValue.Rotation;
                SoftwareBitmap image = await Task.Run(() =>
                {
                    return ApplyImageEffect8bitsNoHistoAsync(rawImage.preview, edit);
                });
                WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                image.CopyToBuffer(bitmap.PixelBuffer);
                ImageBoxPlain.Source = bitmap;
                image.Dispose();
            }
            //toggle visibility
            ImageBoxPlain.Visibility = Visibility.Visible;
        }

        private void HideBeforeDisplay()
        {
            ImageBoxPlain.Visibility = Visibility.Collapsed;
        }

        private void ChooseNeutralPoint()
        {
            //enable selection mode over the image
            selectManualWB.Value = true;
            ControlVisibilty.Value = false;
            //save old editing value
            OldValue.Copy(EditionValue);
        }


        private void ImageBox_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (selectManualWB.Value)
            {
                //get the correct pixel
                var position = e.GetPosition(ImageBox);
                uint width = (uint)((position.X / ImageBox.ActualWidth) * rawImage.raw.dim.width) + rawImage.raw.offset.width;
                uint height = (uint)((position.Y / ImageBox.ActualHeight) * rawImage.raw.dim.height) + rawImage.raw.offset.height;
                long pixelPos = height * rawImage.raw.UncroppedDim.width + width;
                //Calculate the multiplier
                double gMul = rawImage.raw.green[pixelPos] + 1;
                double rMul = gMul / (rawImage.raw.red[pixelPos] + 1);
                double bMul = gMul / (rawImage.raw.blue[pixelPos] + 1);

                //apply them
                EditionValue.RMul = rMul;
                EditionValue.BMul = bMul;
                EditionValue.GMul = 1;
                //update preview
                UpdatePreview(false);
            }
        }

        void ChooseNeutralPointAccept()
        {
            //add an history object
            History.Add(new HistoryObject(EffectType.WhiteBalance, EditionValue.GetCopy())
            {
                value = new double[] { EditionValue.RMul, EditionValue.GMul, EditionValue.BMul },
                oldValue = new double[] { OldValue.RMul, OldValue.GMul, OldValue.BMul }
            });
            ResetButtonVisibility.Value = true;
            //selection back to disable
            ControlVisibilty.Value = true;
            selectManualWB.Value = false;
        }

        void ChooseNeutralPointReject()
        {
            //revert change
            EditionValue.Copy(OldValue);
            UpdatePreview(false);

            //selection back to disable
            ControlVisibilty.Value = true;
            selectManualWB.Value = false;
        }

        void AutoExpose()
        {
            //get an editing object
            var autoval = AutoExposure.Get(rawImage.preview, Histo.PointsL);
            //add history object
            History.Add(new HistoryObject(EffectType.AutoExposure, EditionValue.GetCopy()));

            //apply it
            EditionValue.Copy(autoval);
            UpdatePreview(false);
        }
    }
}
