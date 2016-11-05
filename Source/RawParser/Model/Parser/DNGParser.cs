using System;
using System.Collections.Generic;
using System.IO;
using RawParser.Format.IFD;
using RawParser.Base;

namespace RawParser.Parser
{
    class DNGParser : TiffParser
    {

        public override void Parse(Stream file)
        {
            base.Parse(file);
            int i = 0;
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            //From Nef parser, to replace
            Dictionary<ushort, Tag> temp = new Dictionary<ushort, Tag>();
            Dictionary<ushort, string> standardExifName = new DictionnaryFromFileString(@"Assets\\Dic\StandardExif.dic");
            foreach (ushort exifTag in standardExifName.Keys)
            {
                Tag tempTag = null;

                if (ifd != null && tempTag == null) ifd.tags.TryGetValue(exifTag, out tempTag);

                if (tempTag != null)
                {
                    string t = "";
                    standardExifName.TryGetValue(exifTag, out t);
                    tempTag.displayName = t;

                    temp.Add(exifTag, tempTag);
                }
            }
            return temp;
        }

        public override byte[] parsePreview()
        {
            throw new NotImplementedException();
        }

        public override ushort[] parseRAWImage()
        {
            return base.parseRAWImage();
            //Find the IFD with tag NewSubFileType=0 (it's the raw)
            //Find the BitsPerSample tag (used to convert from the raw bit/pixel to 16bit/pixel of the raw processor)
            //Find the SampleFormat tag for integer or float image
            //Find the compression tag (Compression)
            //Find the PhotometricInterpretation tag (if RGB or YCbCr)
            //FInd the orientation for future use

            //Get the linearisation table for decoding

            //Support for croppping active area not in first one
            //Linearization
            //Black Subtraction
            //Rescaling
            //Clipping
            //Map from camera color space to CIEXYZ then RGB

        }

        public override byte[] parseThumbnail()
        {
            //Thumb is in the ifd
            //call parse image from the tiff parserover the first ifd
            /*
            //Get the full size preview          
            Tag thumbnailOffset, thumbnailSize, newSubFileType;
            //Value from tiff (First oneis preview if  NewSubFileType == 1
            if (!ifd.tags.TryGetValue(0x0FE, out newSubFileType)) throw new FormatException("File not correct");
            if (Convert.ToInt32(newSubFileType.data[0]) == 1)
            {
                if (ifd != null && ifd.tags.TryGetValue(0x0111, out thumbnailOffset))
                {

                    if (!ifd.tags.TryGetValue(0x0117, out thumbnailSize)) throw new FormatException("File not correct");
                    fileStream.BaseStream.Position = (uint)(thumbnailOffset.data[0]);//check offset
                    return fileStream.ReadBytes(Convert.ToInt32(thumbnailSize.data[0]));
                }
            }*/
            return null;
        }
    }
}
