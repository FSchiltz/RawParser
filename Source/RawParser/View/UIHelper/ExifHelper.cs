using RawNet;
using System.Collections.Generic;

namespace RawEditor
{
    static class ExifHelper
    {
        public static Dictionary<string, string> ParseExif(RawImage raw)
        {
            Dictionary<string, string> exif = new Dictionary<string, string>();
            exif.Add("File", raw.metadata.FileNameComplete);
            exif.Add("Parsing time", raw.metadata.ParsingTimeAsString);
            if (!string.IsNullOrEmpty(raw.metadata.Make))
                exif.Add("Maker", raw.metadata.Make);
            if (!string.IsNullOrEmpty(raw.metadata.Model))
                exif.Add("Model", raw.metadata.Model);
            if (!string.IsNullOrEmpty(raw.metadata.Mode))
                exif.Add("Image mode", raw.metadata.Mode);

            exif.Add("Size", "" + ((raw.raw.dim.width * raw.raw.dim.height) / 1000000.0).ToString("F") + " MPixels");
            exif.Add("Dimension", "" + raw.raw.dim.width + " x " + raw.raw.dim.height);

            exif.Add("Sensor size", "" + ((raw.metadata.RawDim.width * raw.metadata.RawDim.height) / 1000000.0).ToString("F") + " MPixels");
            exif.Add("Sensor dimension", "" + raw.metadata.RawDim.width + " x " + raw.metadata.RawDim.height);

            if (raw.metadata.IsoSpeed > 0)
                exif.Add("ISO", "" + raw.metadata.IsoSpeed);
            if (raw.metadata.Aperture > 0)
                exif.Add("Aperture", "" + raw.metadata.Aperture.ToString("F"));
            if (raw.metadata.Exposure > 0)
                exif.Add("Exposure time", "" + raw.metadata.ExposureAsString);

            if (!string.IsNullOrEmpty(raw.metadata.TimeTake))
                exif.Add("Time of capture", "" + raw.metadata.TimeTake);
            if (!string.IsNullOrEmpty(raw.metadata.TimeModify))
                exif.Add("Time modified", "" + raw.metadata.TimeModify);

            if (raw.metadata.Gps != null)
            {
                exif.Add("Longitude", raw.metadata.Gps.LongitudeAsString);
                exif.Add("lattitude", raw.metadata.Gps.LattitudeAsString);
                exif.Add("altitude", raw.metadata.Gps.AltitudeAsString);
            }

            //more metadata
            exif.Add("Black level", "" + raw.blackLevel);
            exif.Add("White level", "" + raw.whitePoint);
            exif.Add("Color depth", "" + raw.ColorDepth + " bits");

            return exif;
        }
    }
}
