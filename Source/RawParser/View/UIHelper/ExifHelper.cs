using RawNet;
using System.Collections.Generic;

namespace RawEditor
{
    static class ExifHelper
    {
        public static Dictionary<string, string> ParseExif(ref RawImage raw)
        {
            Dictionary<string, string> exif = new Dictionary<string, string>();
            exif.Add("File", raw.metadata.FileNameComplete);
            exif.Add("Parsing time", raw.metadata.ParsingTimeAsString());
            if (raw.metadata.make != null && raw.metadata.make.Trim() != "")
                exif.Add("Maker", raw.metadata.make);
            if (raw.metadata.model != null && raw.metadata.model.Trim() != "")
                exif.Add("Model", raw.metadata.model);
            if (raw.metadata.mode != null && raw.metadata.mode.Trim() != "")
                exif.Add("Image mode", raw.metadata.mode);

            exif.Add("Size", "" + ((raw.dim.width * raw.dim.height) / 1000000.0).ToString("F") + " MPixels");
            exif.Add("Width", "" + raw.dim.width);
            exif.Add("Height", "" + raw.dim.height);
            exif.Add("Uncropped height", "" + raw.uncroppedDim.width);
            exif.Add("Uncropped width", "" + raw.uncroppedDim.height);

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

            //more metadata
            exif.Add("Black level", "" + raw.blackLevel);
            exif.Add("White level", "" + raw.whitePoint);
            exif.Add("Color depth", "" + raw.ColorDepth +" bits");

            return exif;
        }
    }
}
