using Microsoft.Services.Store.Engagement;
using RawEditor.Base;
using RawEditor.Effect;
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
        public RawImage<ushort> raw;
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
            raw = null;
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
            if (raw != null)
            {
                raw.raw.offset = new Point2D(0, 0);
                raw.raw.dim = new Point2D(raw.raw.UncroppedDim);
                raw.preview.offset = new Point2D(0, 0);
                raw.preview.dim = new Point2D(raw.preview.UncroppedDim);
                SetImageSizeText();
            }
            ImageBoxPlain.Visibility = Visibility.Collapsed;
            ResetButtonVisibility.Value = false;
            BeforeToggle.IsChecked = false;
            HideCropUI();
        }

        private void ResetUpdateControls()
        {
            ResetControls();

            if (raw != null)
            {
                var old = new ImageEffect();
                old.Copy(EditionValue);
                History.Add(new HistoryObject(EffectType.Reset, EditionValue.GetCopy()) { oldValue = old });
                UpdatePreview(true);
            }
        }

        private void SetWBUpdate()
        {
            //add an history object
            History.Add(new HistoryObject(EffectType.WhiteBalance, EditionValue.GetCopy())
            {
                oldValue = new double[] { EditionValue.RMul, EditionValue.GMul, EditionValue.BMul },
                value = new double[] { raw?.metadata.WbCoeffs.Red ?? 1, raw?.metadata.WbCoeffs.Green ?? 1, raw?.metadata.WbCoeffs.Blue ?? 1 }
            });
            EditionValue.RMul = DefaultValue.RMul;
            EditionValue.GMul = DefaultValue.GMul;
            EditionValue.BMul = DefaultValue.BMul;
            UpdatePreview(false);
        }

        private void OpenFile(StorageFile file)
        {
            //Add a loading screen
            if (raw != null)
            {
                EmptyImage();
            }
            Load.Show();
            // Blur(true);
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
                        watch.Stop();
                        raw = decoder.rawImage;
                        raw.metadata.ParsingTime = watch.ElapsedMilliseconds;
                        //if (decoder.ScaleValue) raw.ScaleValues();
                    }
                    raw.metadata.FileName = file.DisplayName;
                    raw.metadata.FileNameComplete = file.Name;
                    raw.metadata.FileExtension = file.FileType;

                    if (raw.isCFA)
                    {
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
                        Demosaic.Demos(raw, algo);
                    }
                    /*
                    if (raw.convertionM != null)
                    {
                        raw.ConvertRGB();
                    }*/
                    raw.CreatePreview(SettingStorage.PreviewFactor, ImageDisplay.ViewportHeight, ImageDisplay.ViewportWidth);

                    GC.Collect();
                    //check if enough memory
                    if (MemoryManager.AppMemoryUsageLimit - MemoryManager.AppMemoryUsage < ((ulong)raw.raw.green.Length * 3) || MemoryManager.AppMemoryUsageLevel == AppMemoryUsageLevel.High)
                    {
                        TextDisplay.DisplayWarning("The image is bigger than what your device support, this application may fail when saving. Only " + ((MemoryManager.AppMemoryUsageLimit - MemoryManager.AppMemoryUsage) / (1024 * 1024)) + "Mb left of memory for this app to use");
                    }
#if !DEBUG
                    //send an event with file extension, camera model and make
                    logger.Log("SuccessOpening " + raw?.metadata?.FileExtension.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model);
#endif
                    DefaultValue.Rotation = raw.metadata.OriginalRotation;
                    DefaultValue.ReverseGamma = raw.IsGammaCorrected;
                    DefaultValue.RMul = raw?.metadata.WbCoeffs?.Red ?? 1;
                    DefaultValue.GMul = raw?.metadata.WbCoeffs?.Green ?? 1;
                    DefaultValue.BMul = raw?.metadata.WbCoeffs?.Blue ?? 1;
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        var exif = raw.ParseExif();
                        //Group the data
                        var groups = from x in exif group x by x.Group into grp orderby grp.Key select grp;
                        //Set the grouped data to CollectionViewSource
                        ExifSource.Source = groups;
                        ResetControls();
                        UpdatePreview(true);
                        ControlVisibilty.Value = true;
                        SetImageSizeText();
                    });
                    if (raw.errors.Count > 0)
                    {
                        var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
                        TextDisplay.DisplayWarning(loader.GetString("ErrorOnLoadWarning"));
#if !DEBUG
                            //send an event with file extension and camera model and make if any                   
                            logger.Log("ErrorOnOpen " + file?.FileType.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model + "" + raw.errors.Count);
#endif
                    }
                    thumbnail = null;
                }
                catch (Exception)
                {
#if !DEBUG
                    //send an event with file extension and camera model and make if any                   
                    logger.Log("FailOpening " + file?.FileType.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model);
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
            if (raw?.raw != null)
            {
                //Blur(true);
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
                        var result = ApplyImageEffect8bitsNoHistoAsync(raw.raw, EditionValue);
                        FormatHelper.SaveAsync(file, result);
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

        private void UpdatePreview(bool move)
        {
            if (raw?.preview != null)
            {
                Task.Run(() =>
                {
                    var result = ApplyImageEffect8bits(raw.preview, EditionValue);
                    DisplayImage(result.Item2, move);
                    Histo.FillAsync(result.Item1);
                });
            }
        }

        private SoftwareBitmap CreateBitmap(BitmapPixelFormat format, int rotation, Point2D dim)
        {
            SoftwareBitmap bitmap = null;
            //Needs to run in UI thread
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (rotation == 1 || rotation == 3)
                {
                    bitmap = new SoftwareBitmap(format, (int)dim.Height, (int)dim.Width);
                }
                else
                {
                    bitmap = new SoftwareBitmap(format, (int)dim.Width, (int)dim.Height);
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

        //Apply the change over the image preview       
        private SoftwareBitmap ApplyImageEffect16bitsAsync(ImageComponent<ushort> image, ImageEffect edition)
        {
            SoftwareBitmap bitmap = CreateBitmap(BitmapPixelFormat.Rgba16, edition.Rotation, image.dim);
            edition.ApplyTo16Bits(image, bitmap, false);
            return bitmap;
        }

        private void EditingControlChanged()
        {
            //find the changed value
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
            if (raw != null)
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
            if (raw != null)
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
                var result = ApplyImageEffect8bitsNoHistoAsync(raw.raw, EditionValue);
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
            if (raw?.raw != null)
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
                    h = raw.preview.UncroppedDim.Width;
                    w = raw.preview.UncroppedDim.Height;
                }
                else
                {
                    h = raw.preview.UncroppedDim.Height;
                    w = raw.preview.UncroppedDim.Width;
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
                ImageComponent<ushort> img = new ImageComponent<ushort>(raw.preview) { offset = new Point2D(), dim = new Point2D(raw.preview.UncroppedDim) };
                raw.preview.dim = new Point2D(raw.preview.UncroppedDim);
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
            if (raw?.raw != null && raw?.preview != null)
            {
                raw.raw.offset = new Point2D((uint)(raw.raw.UncroppedDim.Width * left), (uint)(raw.raw.UncroppedDim.Height * top));
                raw.raw.dim = new Point2D((uint)(raw.raw.UncroppedDim.Width * right), (uint)(raw.raw.UncroppedDim.Height * bottom));
                raw.preview.offset = new Point2D((uint)(raw.preview.UncroppedDim.Width * left), (uint)(raw.preview.UncroppedDim.Height * top));
                raw.preview.dim = new Point2D((uint)(raw.preview.UncroppedDim.Width * right), (uint)(raw.preview.UncroppedDim.Height * bottom));
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
            ImageHeight.Text = raw.raw.dim.Height + "px";
            ImageWidth.Text = raw.raw.dim.Width + "px";
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

        private void Button_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            Load.Show();
            raw.CreatePreview(SettingStorage.PreviewFactor, ImageDisplay.ViewportHeight, ImageDisplay.ViewportWidth);
            UpdatePreview(true);
            Load.Hide();
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
                    return ApplyImageEffect8bitsNoHistoAsync(raw.preview, edit);
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
                uint width = (uint)((position.X / ImageBox.ActualWidth) * raw.raw.dim.Width) + raw.raw.offset.Width;
                uint height = (uint)((position.Y / ImageBox.ActualHeight) * raw.raw.dim.Height) + raw.raw.offset.Height;
                long pixelPos = height * raw.raw.UncroppedDim.Width + width;
                //Calculate the multiplier
                double gMul = raw.raw.green[pixelPos] + 1;
                double rMul = gMul / (raw.raw.red[pixelPos] + 1);
                double bMul = gMul / (raw.raw.blue[pixelPos] + 1);

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
            var autoval = AutoExposure.Get(raw.preview, Histo.PointsL);
            //add history object
            History.Add(new HistoryObject(EffectType.AutoExposure, EditionValue.GetCopy()));

            //apply it
            EditionValue.Copy(autoval);
            UpdatePreview(false);
        }
    }
}