using System;
using System.Collections.Generic;
using System.IO;
using RawParser.Format.IFD;

namespace RawParser.Parser
{
    class DNGParser : TiffParser
    {

        public override void Parse(Stream file)
        {
            readTiffBase(file);           
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            throw new NotImplementedException();
        }

        public override byte[] parsePreview()
        {
            throw new NotImplementedException();
        }

        public override ushort[] parseRAWImage()
        {
            throw new NotImplementedException();
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
            //Clipping            //Map from camera color space to CIEXYZ then RGB
        }

        public override byte[] parseThumbnail()
        {
            IFD ifd = new IFD(fileStream, header.TIFFoffset, false, false);
            return null;
        }
    }
}
