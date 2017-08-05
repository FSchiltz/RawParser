using PhotoNet.Common;
using RawNet.Decoder.Decompressor;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RawNet.Dng
{

    internal class DngSliceElement
    {
        public DngSliceElement(uint off, uint count, uint offsetX, uint offsetY)
        {
            byteOffset = off;
            byteCount = count;
            offX = offsetX;
            offY = offsetY;
            mUseBigtable = false;
        }

        public uint byteOffset;
        public uint byteCount;
        public uint offX;
        public uint offY;
        public bool mUseBigtable;
        public byte[] data;
    };

    internal class DngDecoderSlices
    {
        public List<DngSliceElement> slices = new List<DngSliceElement>();
        ImageBinaryReader file;
        RawImage raw;
        public bool FixLjpeg { get; set; }

        public DngDecoderSlices(ImageBinaryReader file, RawImage img)
        {
            this.file = file;
            raw = img;
            FixLjpeg = false;
        }

        public void DecodeSlice()
        {
            //first read data for each slice
            for (int i = 0; i < slices.Count; i++)
            {
                file.BaseStream.Position = slices[i].byteOffset;
                slices[i].data = file.ReadBytes((int)slices[i].byteCount);
            }
            Parallel.For(0, slices.Count, (i) =>
            {
                DngSliceElement e = slices[i];
                LJPEGPlain l = new LJPEGPlain(e.data, raw, e.mUseBigtable, FixLjpeg)
                {
                    offX = e.offX,
                    offY = e.offY
                };
                l.StartDecoder(0, e.byteCount);
                l.input.Dispose();
            });
            /* Lossy DNG 

// Each slice is a JPEG image 
struct jpeg_decompress_struct dinfo;
struct jpeg_error_mgr jerr;
while (!t.slices.empty()) {
DngSliceEleqment e = t.slices.front();
t.slices.pop();
byte* complete_buffer = null;
JSAMPARRAY buffer = (JSAMPARRAY)malloc(sizeof(JSAMPROW));

try {
jpeg_create_decompress(&dinfo);
dinfo.err = jpeg_std_error(&jerr);
jerr.error_exit = my_error_throw;
JPEG_MEMSRC(&dinfo, (unsigned char*)mFile.getData(e.byteOffset, e.byteCount), e.byteCount);

if (JPEG_HEADER_OK != jpeg_read_header(&dinfo, true))
  ThrowRDE("DngDecoderSlices: Unable to read JPEG header");

jpeg_start_decompress(&dinfo);
if (dinfo.output_components != (int) mRaw.getCpp())
  ThrowRDE("DngDecoderSlices: Component count doesn't match");
int row_stride = dinfo.output_width * dinfo.output_components;
int pic_size = dinfo.output_height * row_stride;
complete_buffer = (byte8*) _aligned_malloc(pic_size, 16);
while (dinfo.output_scanline<dinfo.output_height) {
  buffer[0] = (JSAMPROW) (&complete_buffer[dinfo.output_scanline * row_stride]);
  if (0 == jpeg_read_scanlines(&dinfo, buffer, 1))
    ThrowRDE("DngDecoderSlices: JPEG Error while decompressing image.");
}
jpeg_finish_decompress(&dinfo);

// Now the image is decoded, and we copy the image data
int copy_w = Math.Math.Min(((mraw.raw.dim.x - e.offX, dinfo.output_width);
int copy_h = Math.Math.Min(((mraw.raw.dim.y - e.offY, dinfo.output_height);
for (int y = 0; y<copy_h; y++) {
  byte[] src = &complete_buffer[row_stride * y];
UInt16* dst = (UInt16*)mRaw.getData(e.offX, y + e.offY);
  for (int x = 0; x<copy_w; x++) {
    for (int c=0; c<dinfo.output_components; c++)
      * dst++ = (* src++);
  }
}
} catch (RawDecoderException &err) {
mRaw.setError(err.what());
} catch (IOException &err) {
mRaw.setError(err.what());
}
free(buffer);
if (complete_buffer)
_aligned_free(complete_buffer);
jpeg_destroy_decompress(&dinfo);
}
    }*/
        }
    }
}