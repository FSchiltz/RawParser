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
            if (raw.metadata.Make != null && raw.metadata.Make.Trim() != "")
                exif.Add("Maker", raw.metadata.Make);
            if (raw.metadata.Model != null && raw.metadata.Model.Trim() != "")
                exif.Add("Model", raw.metadata.Model);
            if (raw.metadata.Mode != null && raw.metadata.Mode.Trim() != "")
                exif.Add("Image mode", raw.metadata.Mode);

            exif.Add("Size", "" + ((raw.dim.width * raw.dim.height) / 1000000.0).ToString("F") + " MPixels");
            exif.Add("Width", "" + raw.dim.width);
            exif.Add("Height", "" + raw.dim.height);
            exif.Add("Uncropped height", "" + raw.metadata.RawDim.width);
            exif.Add("Uncropped width", "" + raw.metadata.RawDim.height);

            if (raw.metadata.IsoSpeed > 0)
                exif.Add("ISO", "" + raw.metadata.IsoSpeed);
            if (raw.metadata.Aperture > 0)
                exif.Add("Aperture", "" + raw.metadata.Aperture.ToString("F"));
            if (raw.metadata.Exposure > 0)
                exif.Add("Exposure time", "" + raw.metadata.ExposureAsString());

            if (raw.metadata.TimeTake != null)
                exif.Add("Time of capture", "" + raw.metadata.TimeTake);
            if (raw.metadata.TimeModify != null)
                exif.Add("Time modified", "" + raw.metadata.TimeModify);

            if (raw.metadata.Gps != null)
            {
                exif.Add("Longitude", raw.metadata.Gps.LongitudeToString());
                exif.Add("lattitude", raw.metadata.Gps.LattitudeToString());
                exif.Add("altitude", raw.metadata.Gps.AltitudeToString());
            }

            //more metadata
            exif.Add("Black level", "" + raw.blackLevel);
            exif.Add("White level", "" + raw.whitePoint);
            exif.Add("Color depth", "" + raw.ColorDepth +" bits");

            return exif;
        }
    }
}
