using PhotoNet.Common;
using System.IO;

namespace RawNet
{
    public abstract partial class RawDecoder
    {

        /* The decoded image - undefined if image has not or could not be decoded. */
        public RawImage rawImage;

        /* Apply stage 1 DNG opcodes. */
        /* This usually maps out bad pixels, etc */
        protected bool ApplyStage1DngOpcodes { get; set; }

        /* Should Fuji images be rotated? */
        protected bool FujiRotate { get; set; }

        public bool ScaleValue { get; set; } = false;

        /* The Raw input file to be decoded */
        protected ImageBinaryReader reader;
        protected Stream stream;

        /* Construct decoder instance - FileMap is a filemap of the file to be decoded */
        /* The FileMap is not owned by this class, will not be deleted, and must remain */
        /* valid while this object exists */
        protected RawDecoder(Stream stream)
        {
            this.stream = stream;
            rawImage = new RawImage();
            ApplyStage1DngOpcodes = true;
            FujiRotate = true;
        }

        /*
         * return a byte[] containing an JPEG image or null if the file doesn't have a thumbnail
         */
        public virtual Thumbnail DecodeThumb() { return null; }

        /* Attempt to decode the image */
        /* A RawDecoderException will be thrown if the image cannot be decoded, */
        /* and there will not be any data in the mRaw image. */
        /* This function must be overridden by actual decoders. */
        public abstract void DecodeRaw();

        public abstract void DecodeMetadata();

        /* This is faster - at least when compiled on visual studio 32 bits */
        protected long Other_abs(long x)
        {
            long mask = x >> 31;
            return (x + mask) ^ mask;
        }
    }
}
