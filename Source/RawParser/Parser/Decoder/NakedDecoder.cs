using System;
using System.IO;

namespace RawNet
{

    class NakedDecoder : RawDecoder
    {
        Camera cam;

        public NakedDecoder(ref Stream file, Camera c, CameraMetaData meta) : base(meta)
        {
            cam = c;
            this.reader = new TIFFBinaryReader(file);
        }


        protected override void decodeRawInternal()
        {
            UInt32 width = 0, height = 0, filesize = 0, bits = 0, offset = 0;
            if (cam.hints.TryGetValue("full_width", out string tmp))
            {
                width = UInt32.Parse(tmp);
            }
            else
                throw new RawDecoderException("Naked: couldn't ContainsKey width");

            if (cam.hints.TryGetValue("full_height", out tmp))
            {
                height = UInt32.Parse(tmp);
            }
            else
                throw new RawDecoderException("Naked: couldn't ContainsKey height");

            if (cam.hints.TryGetValue("filesize", out tmp))
            {
                filesize = UInt32.Parse(tmp);
            }
            else
                throw new RawDecoderException("Naked: couldn't ContainsKey filesize");

            if (cam.hints.TryGetValue("offset", out tmp))
            {
                offset = UInt32.Parse(tmp);
            }

            if (cam.hints.TryGetValue("bits", out tmp))
            {
                bits = UInt32.Parse(tmp);
            }
            else
                bits = (filesize - offset) * 8 / width / height;

            BitOrder bo = BitOrder.Jpeg16;  // Default
            if (cam.hints.TryGetValue("order", out tmp))
            {
                if (tmp == "plain")
                {
                    bo = BitOrder.Plain;
                }
                else if (tmp == "jpeg")
                {
                    bo = BitOrder.Jpeg;
                }
                else if (tmp == "jpeg16")
                {
                    bo = BitOrder.Jpeg16;
                }
                else if (tmp == "jpeg32")
                {
                    bo = BitOrder.Jpeg32;
                }
            }

            rawImage.dim = new Point2D((int)width, (int)height);
            rawImage.Init();
            reader = new TIFFBinaryReader(reader.BaseStream, offset, (uint)reader.BaseStream.Length);
            Point2D pos = new Point2D(0, 0);
            readUncompressedRaw(ref reader, rawImage.dim, pos, (int)(width * bits / 8), (int)bits, bo);
        }

        protected override void checkSupportInternal()
        {
            this.checkCameraSupported(metaData, cam.make, cam.model, cam.mode);
        }

        protected override void decodeMetaDataInternal()
        {
            setMetaData(metaData, cam.make, cam.model, cam.mode);
        }

    }
}
