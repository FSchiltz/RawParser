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
#ifndef MEF_DECODER_H
#define MEF_DECODER_H

#include "RawDecoder.h"

namespace RawSpeed {

class MefDecoder :
  public RawDecoder
{
public:
  MefDecoder(TiffIFD *rootIFD, FileMap* file);
  virtual ~MefDecoder(void);
  virtual RawImage decodeRawInternal();
  virtual void checkSupportInternal(CameraMetaData *meta);
  virtual void decodeMetaDataInternal(CameraMetaData *meta);
protected:
  TiffIFD *mRootIFD;
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "MefDecoder.h"
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

MefDecoder::MefDecoder(TiffIFD *rootIFD, FileMap* file)  :
    RawDecoder(file), mRootIFD(rootIFD) {
  decoderVersion = 0;
}

MefDecoder::~MefDecoder(void) {
}

RawImage MefDecoder::decodeRawInternal() {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(STRIPOFFSETS);

  if (data.size() < 2)
    ThrowRDE("MEF Decoder: No image data found");
    
  TiffIFD* raw = data[1];
  UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
  UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
  UInt32 off = raw.getEntry(STRIPOFFSETS).getInt();
  UInt32 c2 = raw.getEntry(STRIPBYTECOUNTS).getInt();

  if (c2 > mFile.getSize() - off) {
    mRaw.setError("Warning: byte count larger than file size, file probably truncated.");
  }

  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();
  ByteStream input(mFile, off);

  Decode12BitRawBE(input, width, height);
  return mRaw;
}

void MefDecoder::checkSupportInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
  if (data.empty())
    ThrowRDE("MEF Support check: Model name not found");
  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  this.checkCameraSupported(meta, make, model, "");
}

void MefDecoder::decodeMetaDataInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);

  if (data.empty())
    ThrowRDE("MEF Decoder: Model name found");
  if (!data[0].hasEntry(MAKE))
    ThrowRDE("MEF Decoder: Make name not found");

  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  setMetaData(meta, make, model, "", 0);
}

} // namespace RawSpeed
