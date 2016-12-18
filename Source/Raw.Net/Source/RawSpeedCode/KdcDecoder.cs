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
#ifndef KDC_DECODER_H
#define KDC_DECODER_H

#include "RawDecoder.h"
#include "TiffIFDBE.h"

namespace RawSpeed {

class KdcDecoder :
  public RawDecoder
{
public:
  KdcDecoder(TiffIFD *rootIFD, FileMap* file);
  virtual ~KdcDecoder(void);
  virtual RawImage decodeRawInternal();
  virtual void checkSupportInternal(CameraMetaData *meta);
  virtual void decodeMetaDataInternal(CameraMetaData *meta);
protected:
  TiffIFD *mRootIFD;
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "KdcDecoder.h"

/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post
    Copyright (C) 2014-2015 Pedro Côrte-Real

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

KdcDecoder::KdcDecoder(TiffIFD *rootIFD, FileMap* file)  :
    RawDecoder(file), mRootIFD(rootIFD) {
  decoderVersion = 0;
}

KdcDecoder::~KdcDecoder(void) {
}

RawImage KdcDecoder::decodeRawInternal() {
  if (!mRootIFD.hasEntryRecursive(COMPRESSION))
    ThrowRDE("KDC Decoder: Couldn't find compression setting");

  int compression = mRootIFD.getEntryRecursive(COMPRESSION).getInt();
  if (7 != compression)
    ThrowRDE("KDC Decoder: Unsupported compression %d", compression);

  UInt32 width = 0;
  UInt32 height = 0;
  TiffEntry *ew = mRootIFD.getEntryRecursive(KODAK_KDC_WIDTH);
  TiffEntry *eh = mRootIFD.getEntryRecursive(KODAK_KDC_HEIGHT);
  if (ew && eh) {
    width = ew.getInt()+80;
    height = eh.getInt()+70;
  } else
    ThrowRDE("KDC Decoder: Unable to retrieve image size");

  TiffEntry *offset = mRootIFD.getEntryRecursive(KODAK_KDC_OFFSET);
  if (!offset || offset.count < 13)
    ThrowRDE("KDC Decoder: Couldn't find the KDC offset");
  UInt32 off = offset.getInt(4) + offset.getInt(12);

  // Offset hardcoding gotten from dcraw
  if (hints.find("easyshare_offset_hack") != hints.end())
    off = off < 0x15000 ? 0x15000 : 0x17000;

  if (off > mFile.getSize())
    ThrowRDE("KDC Decoder: offset is out of bounds");

  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();
  ByteStream input(mFile, off);

  Decode12BitRawBE(input, width, height);

  return mRaw;
}

void KdcDecoder::checkSupportInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
  if (data.empty())
    ThrowRDE("KDC Support check: Model name not found");
  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  this.checkCameraSupported(meta, make, model, "");
}

void KdcDecoder::decodeMetaDataInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);

  if (data.empty())
    ThrowRDE("KDC Decoder: Model name found");
  if (!data[0].hasEntry(MAKE))
    ThrowRDE("KDC Decoder: Make name not found");

  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  setMetaData(meta, make, model, "", 0);

  // Try the kodak hidden IFD for WB
  if (mRootIFD.hasEntryRecursive(KODAK_IFD2)) {
    TiffEntry *ifdoffset = mRootIFD.getEntryRecursive(KODAK_IFD2);
    TiffIFD *kodakifd = null;
    try {
      if (mRootIFD.endian == getHostEndianness())
        kodakifd = new TiffIFD(mFile, ifdoffset.getInt());
      else
        kodakifd = new TiffIFDBE(mFile, ifdoffset.getInt());

     if (kodakifd && kodakifd.hasEntryRecursive(KODAK_KDC_WB)) {
        TiffEntry *wb = kodakifd.getEntryRecursive(KODAK_KDC_WB);
        if (wb.count == 3) {
          mRaw.metadata.wbCoeffs[0] = wb.getFloat(0);
          mRaw.metadata.wbCoeffs[1] = wb.getFloat(1);
          mRaw.metadata.wbCoeffs[2] = wb.getFloat(2);
        }
      }
    } catch(TiffParserException e) {
      mRaw.setError(e.what());
    }
    if (kodakifd)
      delete kodakifd;
  }

  // Use the normal WB if available
  if (mRootIFD.hasEntryRecursive(KODAKWB)) {
    TiffEntry *wb = mRootIFD.getEntryRecursive(KODAKWB);
    if (wb.count == 734 || wb.count == 1502) {
      byte *tmp = wb.getData();
      mRaw.metadata.wbCoeffs[0] = (float)((((UInt16) tmp[148])<<8)|tmp[149])/256.0f;
      mRaw.metadata.wbCoeffs[1] = 1.0f;
      mRaw.metadata.wbCoeffs[2] = (float)((((UInt16) tmp[150])<<8)|tmp[151])/256.0f;
    }
  }
}

} // namespace RawSpeed
