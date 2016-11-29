﻿using System;
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
using System.Runtime.InteropServices;
using RawEditor.View.UIHelper;
using RawEditor.Effect;
using RawEditor.Model.Settings;
using RawEditor.Model.Encoder;
using Windows.UI.ViewManagement;
using Windows.Foundation;
using RawNet;

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
        public byte[] thumbnail;

        public MainPage()
        {
            InitializeComponent();
            if (null == metadata)
            {
                try
                {
                    StorageFolder installationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    var stringfile = @"\Assets\Data\cameras.xml";
                    metadata = new CameraMetaData(installationFolder.Path + stringfile);
                }
                catch (CameraMetadataException e)
                {
                    ExceptionDisplay.display(e.Message);
                }
            }
            SettingStorage.init();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            imageSelected = false;
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size(200, 100));
            if (VisualStateGroupeMainUI.CurrentState == narrowState)
            {
                ChangeUIForMobile(wideState, narrowState);
            }
            else if (VisualStateGroupeMainUI.CurrentState == mediumState)
            {
                ChangeUIForMobile(wideState, mediumState);
            }
        }

        private async void appBarImageChooseClick(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            filePicker.FileTypeFilter.Add(".nef");
            filePicker.FileTypeFilter.Add(".tiff");
            filePicker.FileTypeFilter.Add(".tif");
            filePicker.FileTypeFilter.Add(".dng");
            filePicker.FileTypeFilter.Add(".cr2");
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
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                 {
                     colorTempSlider.IsEnabled = v;
                     colorTintSlider.IsEnabled = v;
                     exposureSlider.IsEnabled = v;
                     ShadowSlider.IsEnabled = v;
                     HighLightSlider.IsEnabled = v;
                     //gammaSlider.IsEnabled = v;
                     contrastSlider.IsEnabled = v;
                     brightnessSlider.IsEnabled = v;
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

        private async void OpenFile(StorageFile file)
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

                    //read the thumbnail
                    thumbnail = decoder.decodeThumb();
                    if (thumbnail != null)
                    {
                        displayImage(JpegHelper.getJpegInArrayAsync(thumbnail));
                    }

                    raw = decoder.decodeRaw();
                    decoder.decodeMetaData(metadata);
                    raw.fileName = file.DisplayName;
                    //read the exifs
                    //if (raw.exif != null) displayExif();
                    //demos   
                    if (raw.UncroppedCfa != null) raw.cfa = raw.UncroppedCfa; //TODO remove and use crop
                    if (raw.cfa != null && raw.cpp == 1)
                    {
                        Demosaic.demos(ref raw, demosAlgorithm.NearNeighbour);
                    }
                    //create a small image from raw to display
                    bool autoFactor = SettingStorage.autoPreviewFactor;
                    int previewFactor = 0;
                    if (autoFactor)
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
                        previewFactor = SettingStorage.previewFactor;
                    }
                    raw.previewDim = new iPoint2D(raw.dim.x / previewFactor, raw.dim.y / previewFactor);
                    raw.previewData = new ushort[raw.previewDim.y * raw.previewDim.x * 3];
                    for (int i = 0; i < raw.previewDim.y; i++)
                    {
                        for (int j = 0; j < raw.previewDim.x; j++)
                        {
                            raw.previewData[((i * raw.previewDim.x) + j) * 3] = raw.rawData[((i * previewFactor * raw.previewDim.x) + j) * 3 * previewFactor];
                            raw.previewData[(((i * raw.previewDim.x) + j) * 3) + 1] = raw.rawData[(((i * previewFactor * raw.previewDim.x) + j) * 3 * previewFactor) + 1];
                            raw.previewData[(((i * raw.previewDim.x) + j) * 3) + 2] = raw.rawData[(((i * previewFactor * raw.previewDim.x) + j) * 3 * previewFactor) + 2];
                        }
                    }
                    updatePreview();

                    //activate the editing control
                    enableEditingControl(true);
                    //dispose
                    file = null;
                    parser = null;
                }
                catch (FormatException e)
                {
                    ExceptionDisplay.display(e.Message);
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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

                    ExceptionDisplay.display("No file selected");
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
                imageDisplayScroll.MinZoomFactor = 0.1f;
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
                    ushort[] copyOfimage = new ushort[raw.rawData.Length];
                    for (int i = 0; i < raw.rawData.Length; i++) copyOfimage[i] = raw.rawData[i];
                    applyUserModif(ref copyOfimage, raw.dim.y, raw.dim.x, raw.colorDepth);

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
                            bitmap = JpegHelper.getImageAs8bitsBitmap(ref copyOfimage, raw.dim.y, raw.dim.x, raw.colorDepth, null, ref t, false, false);
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
                            bitmap = JpegHelper.getImageAs8bitsBitmap(ref copyOfimage, raw.dim.y, raw.dim.x, raw.colorDepth, null, ref t, false, false);
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
                            bitmap = JpegHelper.getImageAs8bitsBitmap(ref copyOfimage, raw.dim.y, raw.dim.x, raw.colorDepth, null, ref t, false, false);
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

        private void updatePreview()
        {
            //display the histogram                    
            Task histoTask = Task.Run(async () =>
            {
                int[] value = new int[256];
                ushort[] copyofpreview = new ushort[raw.previewData.Length];
                for (int i = 0; i < copyofpreview.Length; i++)
                {
                    copyofpreview[i] = raw.previewData[i];
                }

                applyUserModif(ref copyofpreview, raw.previewDim.y, raw.previewDim.x, raw.colorDepth);
                SoftwareBitmap bitmap = null;
                //Needs to run in UI thread
                await CoreApplication.MainView.CoreWindow.Dispatcher
             .RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 histoLoadingBar.Visibility = Visibility.Visible;
                 //Writeablebitmap use BGRA
                 bitmap = JpegHelper.getImageAs8bitsBitmap(ref copyofpreview, raw.previewDim.y, raw.previewDim.x, raw.colorDepth, null, ref value, true, true);
             });
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
        private void applyUserModif(ref ushort[] image, int imageHeight, int imageWidth, ushort colorDepth)
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
                    effect.gamma = gammaSlider.Value;
                    effect.contrast = contrastSlider.Value / 10;
                    effect.brightness = (1 << colorDepth) * (brightnessSlider.Value / 100);
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
            effect.applyModification(ref image, imageHeight, imageWidth, colorDepth);
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

        private void VisualStateGroup_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            ChangeUIForMobile(e.OldState, e.NewState);
        }

        private void ChangeUIForMobile(VisualState oldState, VisualState newState)
        {
            if (newState == narrowState)
            {
                PivotGrid.Children.Remove(ControlPivot);
                Grid.SetRow(ControlPivot, 1);
                MainGrid.Children.Add(ControlPivot);
                // MainGridRow1.Height = new GridLength(2,GridUnitType.Star);
                // MainGridRow2.Height = new GridLength(3, GridUnitType.Star);
            }
            else if (oldState == narrowState)
            {
                MainGrid.Children.Remove(ControlPivot);
                Grid.SetRow(ControlPivot, 0);
                PivotGrid.Children.Add(ControlPivot);
                // MainGridRow1.Height = new GridLength(1, GridUnitType.Star);
                MainGridRow2.Height = new GridLength(0, GridUnitType.Star);
            }
        }
    }
}
