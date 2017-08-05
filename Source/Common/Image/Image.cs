using System.Collections.Generic;
using System;

namespace PhotoNet.Common
{
    public class Image<T>
    {
        public ImageComponent<ushort> preview = new ImageComponent<ushort>();
        public ImageComponent<byte> thumb;
        public ImageComponent<T> fullSize = new ImageComponent<T>();

        public ImageMetadata metadata = new ImageMetadata();
 
        public List<String> errors = new List<string>();
        
        public bool isCFA = true;
        public ColorFilterArray UncroppedColorFilter;
        public ColorFilterArray colorFilter = new ColorFilterArray();

        public Image(uint width, uint height) : this()
        {
            fullSize.dim = new Point2D(width, height);
        }

        public Image()
        {
            //Set for 16bit image non demos           
            fullSize.cpp = 1;
            fullSize.ColorDepth = 0;
        }

        public void Crop(Rectangle2D crop)
        {
            if (!crop.Dimension.IsThisInside(fullSize.dim - crop.Position))
            {
                return;
            }
            if (crop.Position.width < 0 || crop.Position.height < 0 || !crop.HasPositiveArea())
            {
                return;
            }

            fullSize.offset += crop.Position;
            fullSize.dim = crop.Dimension;
        }

        public void ResetCrop()
        {
            fullSize.offset = new Point2D(0, 0);
            fullSize.dim = new Point2D(fullSize.UncroppedDim);
            preview.offset = new Point2D(0, 0);
            preview.dim = new Point2D(preview.UncroppedDim);
        }

        public List<ExifValue> ParseExif()
        {
            List<ExifValue> exif = new List<ExifValue>
            {
                new ExifValue("File", metadata.FileNameComplete, ExifGroup.Parser),
                new ExifValue("Parsing time", metadata.ParsingTimeAsString, ExifGroup.Parser),
                new ExifValue("Size", "" + ((fullSize.dim.width * fullSize.dim.height) / 1000000.0).ToString("F") + " MPixels", ExifGroup.Camera),
                new ExifValue("Dimension", "" + fullSize.dim.width + " x " + fullSize.dim.height, ExifGroup.Camera),
                new ExifValue("Original size", "" + ((metadata.RawDim.width * metadata.RawDim.height) / 1000000.0).ToString("F") + " MPixels", ExifGroup.Camera),
                new ExifValue("Original dimension", "" + metadata.RawDim.width + " x " + metadata.RawDim.height, ExifGroup.Camera),
                new ExifValue("Color depth", "" + fullSize.ColorDepth + " bits", ExifGroup.Image),
                new ExifValue("Color space", "" + metadata.ColorSpace.ToString(), ExifGroup.Shot)
            };
            //Camera
            if (!string.IsNullOrEmpty(metadata.Make))
                exif.Add(new ExifValue("Maker", metadata.Make, ExifGroup.Camera));
            if (!string.IsNullOrEmpty(metadata.Model))
                exif.Add(new ExifValue("Model", metadata.Model, ExifGroup.Camera));
                        
            //Image
            if (!string.IsNullOrEmpty(metadata.Mode))
                exif.Add(new ExifValue("Image mode", metadata.Mode, ExifGroup.Image));

            //Shot settings
            if (metadata.IsoSpeed > 0)
                exif.Add(new ExifValue("ISO", "" + metadata.IsoSpeed, ExifGroup.Shot));
            if (metadata.Exposure > 0)
                exif.Add(new ExifValue("Exposure time", "" + metadata.ExposureAsString, ExifGroup.Shot));
            //Lens
            if (!string.IsNullOrEmpty(metadata.Lens))
                exif.Add(new ExifValue("Lense", metadata.Lens, ExifGroup.Lens));
            if (metadata.Focal > 0)
                exif.Add(new ExifValue("Focal", "" + (int)metadata.Focal + " mm", ExifGroup.Lens));
            if (metadata.Aperture > 0)
                exif.Add(new ExifValue("Aperture", "" + metadata.Aperture.ToString("F"), ExifGroup.Lens));

            //Various
            if (!string.IsNullOrEmpty(metadata.TimeTake))
                exif.Add(new ExifValue("Time of capture", "" + metadata.TimeTake, ExifGroup.Various));
            if (!string.IsNullOrEmpty(metadata.TimeModify))
                exif.Add(new ExifValue("Time modified", "" + metadata.TimeModify, ExifGroup.Various));
            if (!string.IsNullOrEmpty(metadata.Comment))
                exif.Add(new ExifValue("Comment", "" + metadata.Comment, ExifGroup.Various));

            //GPS
            if (metadata.Gps != null)
            {
                exif.Add(new ExifValue("Longitude", metadata.Gps.LongitudeAsString, ExifGroup.GPS));
                exif.Add(new ExifValue("lattitude", metadata.Gps.LattitudeAsString, ExifGroup.GPS));
                exif.Add(new ExifValue("altitude", metadata.Gps.AltitudeAsString, ExifGroup.GPS));
            }

            return exif;
        }
    }
}