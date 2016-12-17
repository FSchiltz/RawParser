using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.UI.Core;

namespace RawEditor
{
    public static class FormatHelper
    {
        public static List<string> ReadSupportedFormat
        {
            get
            {
                return new List<string>() {
                    ".tiff",
                    ".tif",
                    ".jpg",
                    ".jpeg",
                    ".png",

                    //raw
                    ".nef",
                    ".dng",
                    ".cr2",
                    ".pef",
                    ".arw",
                    ".raw",
                    ".orf"
                };
            }
        }

        public static List<string> SaveSupportedFormat
        {
            get
            {
                return new List<string>() {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".tiff",
                    ".tif",
                    ".bmp"
                };
            }
        }

        public static async void SaveAsync(StorageFile file, SoftwareBitmap bitmap)
        {
            CachedFileManager.DeferUpdates(file);
            // write to file
            if (file.FileType == ".jpg" || file.FileType == ".jpeg")
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
            else if (file.FileType == ".tiff" || file.FileType == ".tif")
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
            else throw new FormatException("Format not supported: " + file.FileType);
            // Let Windows know that we're finished changing the file so
            // the other app can update the remote version of the file.
            // Completing updates may require Windows to ask for user input.
            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status != FileUpdateStatus.Complete && status != FileUpdateStatus.CompleteAndRenamed)
                throw new IOException("File could not be saved");
        }
    }
}

