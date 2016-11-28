/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post
    Copyright (C) 2014 Pedro Côrte-Real

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA

    http://www.klauspost.com
*/
#ifndef DCR_DECODER_H
#define DCR_DECODER_H

#include "RawDecoder.h"
#include "TiffIFDBE.h"

namespace RawSpeed {

class DcrDecoder :
  public RawDecoder
{
public:
  DcrDecoder(TiffIFD *rootIFD, FileMap* file);
  virtual ~DcrDecoder(void);
  virtual RawImage decodeRawInternal();
  virtual void checkSupportInternal(CameraMetaData *meta);
  virtual void decodeMetaDataInternal(CameraMetaData *meta);
protected:
  TiffIFD *mRootIFD;
  void decodeKodak65000(ByteStream &input, UInt32 w, UInt32 h);
  void decodeKodak65000Segment(ByteStream &input, UInt16 *out, UInt32 bsize);
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "DcrDecoder.h"

/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post
    Copyright (C) 2014 Pedro Côrte-Real

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA

    http://www.klauspost.com
*/

namespace RawSpeed {

DcrDecoder::DcrDecoder(TiffIFD *rootIFD, FileMap* file)  :
    RawDecoder(file), mRootIFD(rootIFD) {
  decoderVersion = 0;
}

DcrDecoder::~DcrDecoder(void) {
}

RawImage DcrDecoder::decodeRawInternal() {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(CFAPATTERN);

  if (data.size() < 1)
    ThrowRDE("DCR Decoder: No image data found");

  TiffIFD* raw = data[0];
  UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
  UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
  UInt32 off = raw.getEntry(STRIPOFFSETS).getInt();
  UInt32 c2 = raw.getEntry(STRIPBYTECOUNTS).getInt();

  if (off > mFile.getSize())
    ThrowRDE("DCR Decoder: Offset is out of bounds");

  if (c2 > mFile.getSize() - off) {
    mRaw.setError("Warning: byte count larger than file size, file probably truncated.");
  }

  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();
  ByteStream input(mFile, off);

  int compression = raw.getEntry(COMPRESSION).getInt();
  if (65000 == compression) {
    TiffEntry *ifdoffset = mRootIFD.getEntryRecursive(KODAK_IFD);
    if (!ifdoffset)
      ThrowRDE("DCR Decoder: Couldn't find the Kodak IFD offset");
    TiffIFD *kodakifd;
    if (mRootIFD.endian == getHostEndianness())
      kodakifd = new TiffIFD(mFile, ifdoffset.getInt());
    else
      kodakifd = new TiffIFDBE(mFile, ifdoffset.getInt());
    TiffEntry *linearization = kodakifd.getEntryRecursive(KODAK_LINEARIZATION);
    if (!linearization || linearization.count != 1024 || linearization.type != TIFF_SHORT) {
      delete kodakifd;
      ThrowRDE("DCR Decoder: Couldn't find the linearization table");
    }

    UInt16 *linearization_table = new UInt16[1024];
    linearization.getShortArray(linearization_table, 1024);

    if (!uncorrectedRawValues)
      mRaw.setTable(linearization_table, 1024, true);

    // FIXME: dcraw does all sorts of crazy things besides this to fetch
    //        WB from what appear to be presets and calculate it in weird ways
    //        The only file I have only uses this method, if anybody careas look
    //        in dcraw.c parse_kodak_ifd() for all that weirdness
    TiffEntry *blob = kodakifd.getEntryRecursive((TiffTag) 0x03fd);
    if (blob && blob.count == 72) {
      mRaw.metadata.wbCoeffs[0] = (float) 2048.0f / blob.getShort(20);
      mRaw.metadata.wbCoeffs[1] = (float) 2048.0f / blob.getShort(21);
      mRaw.metadata.wbCoeffs[2] = (float) 2048.0f / blob.getShort(22);
    }

    try {
      decodeKodak65000(input, width, height);
    } catch (IOException) {
      mRaw.setError("IO error occurred while reading image. Returning partial result.");
    }

    // Set the table, if it should be needed later.
    if (uncorrectedRawValues) {
      mRaw.setTable(linearization_table, 1024, false);
    } else {
      mRaw.setTable(null);
    }

    delete kodakifd;
  } else
    ThrowRDE("DCR Decoder: Unsupported compression %d", compression);

  return mRaw;
}

void DcrDecoder::decodeKodak65000(ByteStream &input, UInt32 w, UInt32 h) {
  UInt16 buf[256];
  UInt32 pred[2];
  byte[] data = mRaw.getData();
  UInt32 pitch = mRaw.pitch;

  UInt32 random = 0;
  for (UInt32 y = 0; y < h; y++) {
    UInt16* dest = (UInt16*) & data[y*pitch];
    for (UInt32 x = 0 ; x < w; x += 256) {
      pred[0] = pred[1] = 0;
      UInt32 len = Math.Math.Min(((256, w-x);
      decodeKodak65000Segment(input, buf, len);
      for (UInt32 i = 0; i < len; i++) {
        UInt16 value = pred[i & 1] += buf[i];
        if (value > 1023)
          ThrowRDE("DCR Decoder: Value out of bounds %d", value);
        if(uncorrectedRawValues)
          dest[x+i] = value;
        else
          mRaw.setWithLookUp(value, (byte8*)&dest[x+i], &random);
      }
    }
  }
}

void DcrDecoder::decodeKodak65000Segment(ByteStream &input, UInt16 *out, UInt32 bsize) {
  byte blen[768];
  UInt64 bitbuf=0;
  UInt32 bits=0;
  
  bsize = (bsize + 3) & -4;
  for (UInt32 i=0; i < bsize; i+=2) {
    blen[i] = input.peekByte() & 15;
    blen[i+1] = input.getByte() >> 4;
  }
  if ((bsize & 7) == 4) {
    bitbuf  = ((int) input.getByte()) << 8;
    bitbuf += ((int) input.getByte());
    bits = 16;
  }
  for (UInt32 i=0; i < bsize; i++) {
    UInt32 len = blen[i];
    if (bits < len) {
      for (UInt32 j=0; j < 32; j+=8) {
        bitbuf += (long long) ((int) input.getByte()) << (bits+(j^8));
      }
      bits += 32;
    }
    UInt32 diff = (UInt32)bitbuf & (0xffff >> (16-len));
    bitbuf >>= len;
    bits -= len;
    if (len && (diff & (1 << (len-1))) == 0)
      diff -= (1 << len) - 1;
    out[i] = diff;
  }
}

void DcrDecoder::checkSupportInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
  if (data.empty())
    ThrowRDE("DCR Support check: Model name not found");
  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  this.checkCameraSupported(meta, make, model, "");
}

void DcrDecoder::decodeMetaDataInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);

  if (data.empty())
    ThrowRDE("DCR Decoder: Model name found");
  if (!data[0].hasEntry(MAKE))
    ThrowRDE("DCR Decoder: Make name not found");

  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  setMetaData(meta, make, model, "", 0);
}

} // namespace RawSpeed
