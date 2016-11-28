using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{
    class DNGDecoder : TiffDecoder
    {
        public DNGDecoder(ref TIFFBinaryReader file) : base(ref file)
        {
        }

        public void Parse(Stream file)
        {
            base.Parse(file);
            int i = 0;
        }

        public ushort[] parseRAWImage()
        {
            //this return a preview from the dng file, to remplace
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

        public byte[] parseThumbnail()
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
                    fileStream.Position = (uint)(thumbnailOffset.data[0]);//check offset
                    return fileStream.ReadBytes(Convert.ToInt32(thumbnailSize.data[0]));
                }
            }*/
            return null;
        }
    }
}
