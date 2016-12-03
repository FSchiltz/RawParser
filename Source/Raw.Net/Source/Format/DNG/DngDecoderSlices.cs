using System;
using System.Collections.Generic;
using System.IO;

namespace RawNet
{

    public class DngSliceElement
    {
        public DngSliceElement(UInt32 off, UInt32 count, UInt32 offsetX, UInt32 offsetY)
        {
            byteOffset = (off);
            byteCount = (count);
            offX = (offsetX);
            offY = (offsetY);
            mUseBigtable = (false);
        }

        public UInt32 byteOffset;
        public UInt32 byteCount;
        public UInt32 offX;
        public UInt32 offY;
        public bool mUseBigtable;
    };

    class DngDecoderSlices
    {
        public Queue<DngSliceElement> slices = new Queue<DngSliceElement>();
        TIFFBinaryReader mFile;
        RawImage mRaw;
        public bool mFixLjpeg;
        int compression;

        /*
        void DecodeThread()
        {
            DngDecoderThread me = _this;
            DngDecoderSlices parent = me.parent;
            try
            {
                Task
                parent.decodeSlice(me);
            }
            catch (Exception)
            {
                parent.mRaw.errors.Add("DNGDEcodeThread: Caught exception.");
            }
            return;
        }*/
        

        public DngDecoderSlices(TIFFBinaryReader file, RawImage img, int _compression)
        {
            mFile = (file);
            mRaw = (img);
            mFixLjpeg = false;
            compression = _compression;
        }

        
        public void addSlice(DngSliceElement slice)
        {
            slices.Enqueue(slice);
        }

        public void decodeSlice()
        {
            if (compression == 7)
            {
                while (slices.Count != 0)
                {
                    LJpegPlain l = new LJpegPlain(mFile, mRaw)
                    {
                        mDNGCompatible = mFixLjpeg
                    };
                    DngSliceElement e = slices.Dequeue();
                    l.mUseBigtable = e.mUseBigtable;                    
                    try
                    {
                        l.startDecoder(e.byteOffset, e.byteCount, e.offX, e.offY);
                    }
                    catch (RawDecoderException err)
                    {
                        mRaw.errors.Add(err.Message);
                    }
                    catch (IOException err)
                    {
                        mRaw.errors.Add(err.Message);
                    }
                }
                /* Lossy DNG */
            }
            /*else if (compression == 0x884c)
            {
// Each slice is a JPEG image 
struct jpeg_decompress_struct dinfo;
struct jpeg_error_mgr jerr;
while (!t.slices.empty()) {
  DngSliceElement e = t.slices.front();
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
int copy_w = Math.Math.Min(((mRaw.dim.x - e.offX, dinfo.output_width);
int copy_h = Math.Math.Min(((mRaw.dim.y - e.offY, dinfo.output_height);
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
            else
                mRaw.errors.Add("DngDecoderSlices: Unknown compression");
        }

        int size()
        {
            return slices.Count;
        }
    }
} // namespace RawSpeed
