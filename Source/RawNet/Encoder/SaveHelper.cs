using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Provider;
using Windows.UI.Core;

namespace RawNet
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
                    ".rw2",
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
                Guid type;
                var propertySet = new BitmapPropertySet();
                BitmapEncoder encoder = null;
                // write to file
                switch (file.FileType.ToLower())
                {
                    case ".jxr":
                        type = BitmapEncoder.JpegXREncoderId;
                        break;
                    case ".jpg":
                    case ".jpeg":
                        type = BitmapEncoder.JpegEncoderId;
                        break;
                    case ".png":
                        type = BitmapEncoder.PngEncoderId;
                        break;
                    case ".bmp":
                        type = BitmapEncoder.BmpEncoderId;
                        break;
                    case ".tiff":
                    case ".tif":
                        var compressionValue = new BitmapTypedValue(
                            TiffCompressionMode.None, // no compression
                            PropertyType.UInt8
                            );
                        propertySet.Add("TiffCompressionMethod", compressionValue);
                        type = BitmapEncoder.TiffEncoderId;
                        break;
                    default:
                        throw new FormatException("Format not supported: " + file.FileType);
                }

                encoder = await BitmapEncoder.CreateAsync(type, filestream, propertySet);
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


