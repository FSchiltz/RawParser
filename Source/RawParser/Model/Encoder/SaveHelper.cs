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
                    ".bmp",
                    ".gif",
                    ".ico",
                    ".jxr",

                    //raw
                    ".nef",
                    ".dng",
                    ".cr2",
                    ".pef",
                    ".arw",
                    ".raw",
                    ".orf",
                    ".raf"
                };
            }
        }

        public static Dictionary<string, List<string>> SaveSupportedFormat
        {
            get
            {
                var temp = new Dictionary<string, List<string>>
                {
                    { "Jpeg image", new List<string>() { ".jpg" } },
                    { "PNG image", new List<string>() { ".png" } },
                    { "Tiff image", new List<string>() { ".tiff" } },
                    { "BMP image", new List<string>() { ".bmp" } },
                    { "JpegXR image", new List<string>() { ".jxr" } }
                };
                return temp;
            }
        }

        public static async void SaveAsync(StorageFile file, SoftwareBitmap bitmap)
        {
            CachedFileManager.DeferUpdates(file);
            using (var filestream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                BitmapEncoder encoder = null;
                // write to file
                if (file.FileType == ".jxr")
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegXREncoderId, filestream);
                }
                else if (file.FileType == ".jpg" || file.FileType == ".jpeg")
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, filestream);
                }
                else if (file.FileType == ".png")
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, filestream);
                }
                else if (file.FileType == ".bmp")
                {
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, filestream);
                }
                else if (file.FileType == ".tiff" || file.FileType == ".tif")
                {
                    var propertySet = new BitmapPropertySet();
                    var compressionValue = new BitmapTypedValue(
                        TiffCompressionMode.None, // no compression
                        PropertyType.UInt8
                        );
                    propertySet.Add("TiffCompressionMethod", compressionValue);
                    encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, filestream, propertySet);
                }
                else throw new FormatException("Format not supported: " + file.FileType);
                //Needs to run in the UI thread because fuck performance
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Do some UI-code that must be run on the UI thread.
                    encoder.SetSoftwareBitmap(bitmap);
                });
                await encoder.FlushAsync();
                encoder = null;
                bitmap.Dispose();
                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status != FileUpdateStatus.Complete && status != FileUpdateStatus.CompleteAndRenamed)
                    throw new IOException("File could not be saved");
            }
        }
    }
}


