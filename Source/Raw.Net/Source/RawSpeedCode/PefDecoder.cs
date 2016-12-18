#ifndef PEF_DECODER_H
#define PEF_DECODER_H

#include "RawDecoder.h"
#include "TiffIFD.h"
#include "PentaxDecompressor.h"
/* 
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post

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

class PefDecoder :
  public RawDecoder
{
public:
  PefDecoder(TiffIFD *rootIFD, FileMap* file);
  virtual ~PefDecoder(void);
  virtual RawImage decodeRawInternal();
  virtual void decodeMetaDataInternal(CameraMetaData *meta);
  virtual void checkSupportInternal(CameraMetaData *meta);
  virtual TiffIFD* getRootIFD() {return mRootIFD;}
  TiffIFD *mRootIFD;
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "PefDecoder.h"
/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post
    Copyright (C) 2015 Pedro Côrte-Real

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

PefDecoder::PefDecoder(TiffIFD *rootIFD, FileMap* file) :
    RawDecoder(file), mRootIFD(rootIFD) {
      decoderVersion = 3;
}

PefDecoder::~PefDecoder(void) {
  if (mRootIFD)
    delete mRootIFD;
  mRootIFD = null;
}

RawImage PefDecoder::decodeRawInternal() {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(STRIPOFFSETS);
  if (data.empty())
    ThrowRDE("PEF Decoder: No image data found");

  TiffIFD* raw = data[0];

  int compression = raw.getEntry(COMPRESSION).getInt();

  if (1 == compression || compression == 32773) {
    decodeUncompressed(raw, BitOrder_Jpeg);
    return mRaw;
  }

  if (65535 != compression)
    ThrowRDE("PEF Decoder: Unsupported compression");

  TiffEntry *offsets = raw.getEntry(STRIPOFFSETS);
  TiffEntry *counts = raw.getEntry(STRIPBYTECOUNTS);

  if (offsets.count != 1) {
    ThrowRDE("PEF Decoder: Multiple Strips found: %u", offsets.count);
  }
  if (counts.count != offsets.count) {
    ThrowRDE("PEF Decoder: Byte count number does not match strip size: count:%u, strips:%u ", counts.count, offsets.count);
  }
  if (!mFile.isValid(offsets.getInt(), counts.getInt()))
    ThrowRDE("PEF Decoder: Truncated file.");

  UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
  UInt32 height = raw.getEntry(IMAGELENGTH).getInt();

  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();
  try {
    PentaxDecompressor l(mFile, mRaw);
    l.decodePentax(mRootIFD, offsets.getInt(), counts.getInt());
  } catch (IOException &e) {
    mRaw.setError(e.what());
    // Let's ignore it, it may have delivered somewhat useful data.
  }

  return mRaw;
}

void PefDecoder::checkSupportInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
  if (data.empty())
    ThrowRDE("PEF Support check: Model name found");
  if (!data[0].hasEntry(MAKE))
    ThrowRDE("PEF Support: Make name not found");

  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  this.checkCameraSupported(meta, make, model, "");
}

void PefDecoder::decodeMetaDataInternal(CameraMetaData *meta) {
  int iso = 0;
  mRaw.cfa.setCFA(iPoint2D(2,2), CFA_RED, CFA_GREEN, CFA_GREEN2, CFA_BLUE);
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);

  if (data.empty())
    ThrowRDE("PEF Meta Decoder: Model name found");

  TiffIFD* raw = data[0];

  string make = raw.getEntry(MAKE).getString();
  string model = raw.getEntry(MODEL).getString();

  if (mRootIFD.hasEntryRecursive(ISOSPEEDRATINGS))
    iso = mRootIFD.getEntryRecursive(ISOSPEEDRATINGS).getInt();

  setMetaData(meta, make, model, "", iso);

  // Read black level
  if (mRootIFD.hasEntryRecursive((TiffTag)0x200)) {
    TiffEntry *black = mRootIFD.getEntryRecursive((TiffTag)0x200);
    if (black.count == 4) {
      for (int i = 0; i < 4; i++)
        mRaw.blackLevelSeparate[i] = black.getInt(i);
    }
  }

  // Set the whitebalance
  if (mRootIFD.hasEntryRecursive((TiffTag) 0x0201)) {
    TiffEntry *wb = mRootIFD.getEntryRecursive((TiffTag) 0x0201);
    if (wb.count == 4) {
      mRaw.metadata.wbCoeffs[0] = wb.getInt(0);
      mRaw.metadata.wbCoeffs[1] = wb.getInt(1);
      mRaw.metadata.wbCoeffs[2] = wb.getInt(3);
    }
  }
}

} // namespace RawSpeed
