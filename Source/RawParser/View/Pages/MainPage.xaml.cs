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
/*
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using System.Numerics;*/
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
        public RawImage raw;
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

        /*
        private float blurAmount = 5;
        private SpriteVisual _pivotGridSprite;
        private Compositor _compositor;
        private CompositionEffectBrush brush;
        private GaussianBlurEffect graphicsEffect = new GaussianBlurEffect()
        {
            Name = "Blur",
            BlurAmount = 0f,
            Source = new CompositionEffectSourceParameter("ImageSource"),
            Optimization = EffectOptimization.Balanced,
            BorderMode = EffectBorderMode.Soft
        };
        private TimeSpan animationDuration = TimeSpan.FromMilliseconds(300);*/

#if !DEBUG
        private StoreServicesCustomEventLogger logger = StoreServicesCustomEventLogger.GetDefault();
#endif

        public MainPage()
        {
            History.effect = EditionValue;
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
            History.HistoryChanged += new PropertyChangedEventHandler((e, d) => UpdatePreview(false));
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
                raw.raw.dim = new Point2D(raw.raw.uncroppedDim.width, raw.raw.uncroppedDim.height);
                raw.preview.offset = new Point2D(0, 0);
                raw.preview.dim = new Point2D(raw.preview.uncroppedDim.width, raw.preview.uncroppedDim.height);
                EditionValue.Rotation = raw.metadata.OriginalRotation;
                EditionValue.ReverseGamma = raw.IsGammaCorrected;
            }
            SetWBAsync();
            ResetButtonVisibility.Value = false;
            HideCropUI();
        }

        private void ResetUpdateControls()
        {
            ResetControls();

            if (raw != null)
            {
                var old = new ImageEffect();
                old.Copy(EditionValue);
                History.Add(new HistoryObject() { oldValue = old, target = EffectObject.Reset });
                UpdatePreview(false);
            }
        }

        private void SetWBUpdate()
        {
            //add an history object
            History.Add(new HistoryObject()
            {
                target = EffectObject.WB,
                oldValue = new double[] { EditionValue.RMul, EditionValue.GMul, EditionValue.BMul },
                value = new double[] { raw?.metadata.WbCoeffs[0] ?? 1, raw?.metadata.WbCoeffs[1] ?? 1, raw?.metadata.WbCoeffs[2] ?? 1 }
            });
            SetWBAsync();
            UpdatePreview(false);
        }

        private async void SetWBAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                EditionValue.RMul = raw?.metadata.WbCoeffs[0] ?? 1;
                EditionValue.GMul = raw?.metadata.WbCoeffs[1] ?? 1;
                EditionValue.BMul = raw?.metadata.WbCoeffs[2] ?? 1;
            });
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
                    }
                    //if (decoder.ScaleValue) raw.ScaleValues();
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
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                            algo = DemosaicAlgorithm.None;
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
                    });
                    if (raw.errors.Count > 0)
                    {
                        TextDisplay.DisplayWarning("This file is not fully supported, it may appear incorrectly");
#if !DEBUG
                            //send an event with file extension and camera model and make if any                   
                            logger.Log("ErrorOnOpen " + file?.FileType.ToLower() + " " + raw?.metadata?.Make + " " + raw?.metadata?.Model + "" + raw.errors.Count);
#endif
                    }
                    thumbnail = null;
                }
                catch (Exception e)
                {
#if DEBUG
                    Debug.WriteLine(e.StackTrace);
#else
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
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ApplyUserModifAsync(raw.raw);
                        FormatHelper.SaveAsync(file, result.Item2);
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
                Task.Run(async () =>
                {
                    var result = await ApplyUserModifAsync(raw.preview);
                    DisplayImage(result.Item2, move);
                    Histo.FillAsync(result.Item1, raw.preview.dim.height, raw.preview.dim.width);
                });
            }
        }

        //Apply the change over the image preview       
        async private Task<Tuple<HistoRaw, SoftwareBitmap>> ApplyUserModifAsync(RawNet.ImageComponent image)
        {
            SoftwareBitmap bitmap = null;
            //Needs to run in UI thread
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (EditionValue.Rotation == 1 || EditionValue.Rotation == 3)
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)image.dim.height, (int)image.dim.width);
                }
                else
                {
                    bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, (int)image.dim.width, (int)image.dim.height);
                }
            });
            var tmp = EditionValue?.Apply(image, bitmap) ?? DefaultValue.Apply(image, bitmap);
            return Tuple.Create(tmp, bitmap);
        }

        private void EditingControlChanged()
        {
            //generate the new object
            //ImageEffect newEffect = GetEffects();
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
                EditionValue.Rotation++;
                var t = new HistoryObject() { oldValue = EditionValue.Rotation, target = EffectObject.Rotate };
                t.value = EditionValue.Rotation;
                History.Add(t);
                EditingControlChanged();
            }
        }

        private void RotateLeftButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (raw != null)
            {
                EditionValue.Rotation--;
                var t = new HistoryObject() { oldValue = EditionValue.Rotation, target = EffectObject.Rotate };
                t.value = EditionValue.Rotation;
                History.Add(t);
                EditingControlChanged();
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
                var result = await ApplyUserModifAsync(raw.raw);
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
                    factor = ImageDisplay.ActualWidth / (w);
                }
                else
                {
                    factor = ImageDisplay.ActualHeight / (h + 160);
                }
                CropUI.SetSize((int)(w * factor * 0.7), (int)(h * factor * 0.7), EditionValue.Rotation);
                //create a preview of the image
                ImageComponent img = new ImageComponent(raw.preview)
                {
                    dim = raw.preview.uncroppedDim
                };
                var result = await ApplyUserModifAsync(img);
                //display the preview
                CropUI.SetThumbAsync(result.Item2);
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
                raw.raw.offset = new Point2D((uint)(raw.raw.uncroppedDim.width * left), (uint)(raw.raw.uncroppedDim.height * top));
                raw.raw.dim = new Point2D((uint)(raw.raw.uncroppedDim.width * right), (uint)(raw.raw.uncroppedDim.height * bottom));
                raw.preview.offset = new Point2D((uint)(raw.preview.uncroppedDim.width * left), (uint)(raw.preview.uncroppedDim.height * top));
                raw.preview.dim = new Point2D((uint)(raw.preview.uncroppedDim.width * right), (uint)(raw.preview.uncroppedDim.height * bottom));
                UpdatePreview(true);
            }
            var t = new HistoryObject() { oldValue = 0, target = EffectObject.Crop };
            History.Add(t);
            ResetButtonVisibility.Value = true;
        }

        #region blur
        /*
        private void Blur(bool visibility)
        {
            if (visibility)
            {
                PivotGrid.IsEnabled = false;
                try
                {
                    // Get the current compositor
                    _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
                    // Create the destinatio sprite, sized to cover the entire list
                    _pivotGridSprite = _compositor.CreateSpriteVisual();
                    _pivotGridSprite.Size = new Vector2((float)PivotGrid.ActualWidth, (float)PivotGrid.ActualHeight);
                    ElementCompositionPreview.SetElementChildVisual(PivotGrid, _pivotGridSprite);
                    // Create the effect factory and instantiate a brush
                    CompositionEffectFactory _effectFactory = _compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
                    brush = _effectFactory.CreateBrush();
                    // Set the destination brush as the source of the image content
                    brush.SetSourceParameter("ImageSource", _compositor.CreateBackdropBrush());
                    // Update the destination layer with the fully configured brush
                    _pivotGridSprite.Brush = brush;

                    ScalarKeyFrameAnimation blurAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    blurAnimation.InsertKeyFrame(0.0f, 0.0f);
                    blurAnimation.InsertKeyFrame(1.0f, blurAmount);
                    blurAnimation.Duration = animationDuration;
                    blurAnimation.IterationBehavior = AnimationIterationBehavior.Count;
                    blurAnimation.IterationCount = 1;
                    brush.StartAnimation("Blur.BlurAmount", blurAnimation);
                }
                catch (Exception e)
                {
                    //no blur if exception (bug on some phone)
                }
            }
            else
            {

                PivotGrid.IsEnabled = true;
                try
                {
                    // Update the destination layer with the fully configured brush
                    _pivotGridSprite.Brush = brush;
                    ScalarKeyFrameAnimation blurAnimation = _compositor.CreateScalarKeyFrameAnimation();
                    blurAnimation.InsertKeyFrame(0.0f, blurAmount);
                    blurAnimation.InsertKeyFrame(1.0f, 0.0f);
                    blurAnimation.Duration = animationDuration;
                    blurAnimation.IterationBehavior = AnimationIterationBehavior.Count;
                    blurAnimation.IterationCount = 1;
                    brush.StartAnimation("Blur.BlurAmount", blurAnimation);
                }
                catch (Exception e)
                {
                }
            }
        }
       */
        #endregion

        private void HideCropUI()
        {
            if (CropGrid.Visibility == Visibility.Visible)
            {
                //Blur(false);
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
    }
}