#ifndef DNG_OPCODES_H
#define DNG_OPCODES_H
#include <vector>
#include "TiffIFD.h"
#include "RawImage.h"
/* 
RawSpeed - RAW file decoder.

Copyright (C) 2012 Klaus Post

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

class DngOpcode
{
public:
  DngOpcode(void) {host = getHostEndianness();};
  virtual ~DngOpcode(void) {};

  /* Will be called exactly once, when input changes */
  /* Can be used for preparing pre-calculated values, etc */
  virtual RawImage& createOutput(RawImage &in) {return in;}
  /* Will be called for actual processing */
  /* If multiThreaded is true, it will be called several times, */
  /* otherwise only once */
  /* Properties of out will not have changed from createOutput */
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY) = 0;
  iRectangle2D mAoi;
  int mFlags;
  public Flags
  {
    MultiThreaded = 1,
    PureLookup = 2
  };

  
protected:
  Endianness host;
  int32 getLong(byte *ptr) {
    if (host == big)
      return *(int32*)ptr;
    return (int32)ptr[0] << 24 | (int32)ptr[1] << 16 | (int32)ptr[2] << 8 | (int32)ptr[3];
  }
  UInt32 getULong(byte *ptr) {
    if (host == big)
      return *(UInt32*)ptr;
    return (UInt32)ptr[0] << 24 | (UInt32)ptr[1] << 16 | (UInt32)ptr[2] << 8 | (UInt32)ptr[3];
  }
  double getDouble(byte *ptr) {
    if (host == big)
      return *(double*)ptr;
    double ret;
    byte *tmp = (byte8*)&ret;
    for (int i = 0; i < 8; i++)
     tmp[i] = ptr[7-i];
    return ret;
  }
  float getFloat(byte *ptr) {
    if (host == big)
      return *(float*)ptr;
    float ret;
    byte *tmp = (byte8*)&ret;
    for (int i = 0; i < 4; i++)
      tmp[i] = ptr[3-i];
    return ret;
  }
  UInt16 getUshort(byte *ptr) {
    if (host == big)
      return *(UInt16*)ptr;
    return (UInt16)ptr[0] << 8 | (UInt16)ptr[1];
  }

};


class DngOpcodes
{
public:
  DngOpcodes(TiffEntry *entry);
  virtual ~DngOpcodes(void);
  RawImage& applyOpCodes(RawImage &img);
private:
  vector<DngOpcode*> mOpcodes;
  Endianness host;
  UInt32 getULong(byte *ptr) {
    if (host == big)
      return *(UInt32*)ptr;
    return (UInt32)ptr[0] << 24 | (UInt32)ptr[1] << 16 | (UInt32)ptr[2] << 8 | (UInt32)ptr[3];
  }
};

class OpcodeFixBadPixelsConstant: public DngOpcode
{
public:
  OpcodeFixBadPixelsConstant(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeFixBadPixelsConstant(void) {};
  virtual RawImage& createOutput( RawImage &in );
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  int mValue;
};


class OpcodeFixBadPixelsList: public DngOpcode
{
public:
  OpcodeFixBadPixelsList(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeFixBadPixelsList(void) {};
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  vector<UInt32> bad_pos;
};


class OpcodeTrimBounds: public DngOpcode
{
public:
  OpcodeTrimBounds(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeTrimBounds(void) {};
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mTop, mLeft, mBottom, mRight;
};


class OpcodeMapTable: public DngOpcode
{
public:
  OpcodeMapTable(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeMapTable(void) {};
  virtual RawImage& createOutput(RawImage &in);
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch;
  UInt16 mLookup[65536];
};

class OpcodeMapPolynomial: public DngOpcode
{
public:
  OpcodeMapPolynomial(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeMapPolynomial(void) {};
  virtual RawImage& createOutput(RawImage &in);
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mDegree;
  double mCoefficient[9];
  UInt16 mLookup[65536];
};

class OpcodeDeltaPerRow: public DngOpcode
{
public:
  OpcodeDeltaPerRow(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeDeltaPerRow(void) {};
  virtual RawImage& createOutput(RawImage &in);
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
  float* mDelta;
};

class OpcodeDeltaPerCol: public DngOpcode
{
public:
  OpcodeDeltaPerCol(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeDeltaPerCol(void);
  virtual RawImage& createOutput(RawImage &in);
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
  float* mDelta;
  int* mDeltaX;
};

class OpcodeScalePerRow: public DngOpcode
{
public:
  OpcodeScalePerRow(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeScalePerRow(void) {};
  virtual RawImage& createOutput(RawImage &in);
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
  float* mDelta;
};

class OpcodeScalePerCol: public DngOpcode
{
public:
  OpcodeScalePerCol(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used);
  virtual ~OpcodeScalePerCol(void);
  virtual RawImage& createOutput(RawImage &in);
  virtual void apply(RawImage &in, RawImage &out, UInt32 startY, UInt32 endY);
private:
  UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
  float* mDelta;
  int* mDeltaX;
};

} // namespace RawSpeed 

#endif // DNG_OPCODES_H
#include "StdAfx.h"
#include "DngOpcodes.h"
/* 
RawSpeed - RAW file decoder.

Copyright (C) 2012 Klaus Post

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

DngOpcodes::DngOpcodes(TiffEntry *entry)
{
  host = getHostEndianness();
  byte[] data = entry.getData();
  UInt32 entry_size = entry.count;

  if (entry_size < 20)
    ThrowRDE("DngOpcodes: Not enough bytes to read a single opcode");

  UInt32 opcode_count = getULong(&data[0]);
  int bytes_used = 4;
  for (UInt32 i = 0; i < opcode_count; i++) {
    if ((int)entry_size - bytes_used < 16)
      ThrowRDE("DngOpcodes: Not enough bytes to read a new opcode");

    UInt32 code = getULong(&data[bytes_used]);
    //UInt32 version = getULong(&data[bytes_used+4]);
    UInt32 flags = getULong(&data[bytes_used+8]);
    UInt32 expected_size = getULong(&data[bytes_used+12]);
    bytes_used += 16;
    UInt32 opcode_used = 0;
    switch (code)
    {
      case 4:
        mOpcodes.push_back(new OpcodeFixBadPixelsConstant(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 5:
        mOpcodes.push_back(new OpcodeFixBadPixelsList(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 6:
        mOpcodes.push_back(new OpcodeTrimBounds(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 7:
        mOpcodes.push_back(new OpcodeMapTable(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 8:
        mOpcodes.push_back(new OpcodeMapPolynomial(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 10:
        mOpcodes.push_back(new OpcodeDeltaPerRow(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 11:
        mOpcodes.push_back(new OpcodeDeltaPerCol(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 12:
        mOpcodes.push_back(new OpcodeScalePerRow(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      case 13:
        mOpcodes.push_back(new OpcodeScalePerCol(&data[bytes_used], entry_size - bytes_used, &opcode_used));
        break;
      default:
        // Throw Error if not marked as optional
        if (!(flags & 1))
          ThrowRDE("DngOpcodes: Unsupported Opcode: %d", code);
    }
    if (opcode_used != expected_size)
      ThrowRDE("DngOpcodes: Inconsistent length of opcode");
    bytes_used += opcode_used;
  }
}

DngOpcodes::~DngOpcodes(void)
{
  size_t codes = mOpcodes.size();
  for (UInt32 i = 0; i < codes; i++)
    delete mOpcodes[i];
  mOpcodes.clear();
}

/* TODO: Apply in separate threads */
RawImage& DngOpcodes::applyOpCodes( RawImage &img )
{
  size_t codes = mOpcodes.size();
  for (UInt32 i = 0; i < codes; i++)
  {
    DngOpcode* code = mOpcodes[i];
    RawImage img_out = code.createOutput(img);
    iRectangle2D fullImage(0,0,img.dim.x, img.dim.y);

    if (!code.mAoi.isThisInside(fullImage))
      ThrowRDE("DngOpcodes: Area of interest not inside image!");
    if (code.mAoi.hasPositiveArea()) {
      code.apply(img, img_out, code.mAoi.getTop(), code.mAoi.getBottom());
      img = img_out;
    }
  }
  return img;
}

/***************** OpcodeFixBadPixelsConstant   ****************/

OpcodeFixBadPixelsConstant::OpcodeFixBadPixelsConstant(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 8)
    ThrowRDE("OpcodeFixBadPixelsConstant: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mValue = getLong(&parameters[0]);
  // Bayer Phase not used
  *bytes_used = 8;
  mFlags = MultiThreaded;
}

RawImage& OpcodeFixBadPixelsConstant::createOutput( RawImage &in )
{
  // These limitations are present within the DNG SDK as well.
  if (in.getDataType() != TYPE_USHORT16)
    ThrowRDE("OpcodeFixBadPixelsConstant: Only 16 bit images supported");

  if (in.getCpp() > 1)
    ThrowRDE("OpcodeFixBadPixelsConstant: This operation is only supported with 1 component");

  return in;
}

void OpcodeFixBadPixelsConstant::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  iPoint2D crop = in.getCropOffset();
  UInt32 offset = crop.x | (crop.y << 16);
  vector<UInt32> bad_pos;
  for (UInt32 y = startY; y < endY; y++) {
    UInt16* src = (UInt16*)out.getData(0, y);
    for (UInt32 x = 0; x < (UInt32)in.dim.x; x++) {
      if (src[x]== mValue) {
        bad_pos.push_back(offset + ((UInt32)x | (UInt32)y<<16));
      }
    }
  }
  if (!bad_pos.empty()) {
    pthread_mutex_lock(&out.mBadPixelMutex);
    out.mBadPixelPositions.insert(out.mBadPixelPositions.end(), bad_pos.begin(), bad_pos.end());
    pthread_mutex_unlock(&out.mBadPixelMutex);
  }

}

/***************** OpcodeFixBadPixelsList   ****************/

OpcodeFixBadPixelsList::OpcodeFixBadPixelsList( byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 12)
    ThrowRDE("OpcodeFixBadPixelsList: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  // Skip phase - we don't care
  UInt64 BadPointCount = getULong(&parameters[4]);
  UInt64 BadRectCount = getULong(&parameters[8]);
  bytes_used[0] = 12;

  if (12 + BadPointCount * 8 + BadRectCount * 16 > (UInt64) param_max_bytes)
    ThrowRDE("OpcodeFixBadPixelsList: Ran out parameter space, only %u bytes left.", param_max_bytes);

  // Read points
  for (UInt64 i = 0; i < BadPointCount; i++) {
    UInt32 BadPointRow = (UInt32)getLong(&parameters[bytes_used[0]]);
    UInt32 BadPointCol = (UInt32)getLong(&parameters[bytes_used[0]+4]);
    bytes_used[0] += 8;
    bad_pos.push_back(BadPointRow | (BadPointCol << 16));
  }

  // Read rects
  for (UInt64 i = 0; i < BadRectCount; i++) {
    UInt32 BadRectTop = (UInt32)getLong(&parameters[bytes_used[0]]);
    UInt32 BadRectLeft = (UInt32)getLong(&parameters[bytes_used[0]+4]);
    UInt32 BadRectBottom = (UInt32)getLong(&parameters[bytes_used[0]]);
    UInt32 BadRectRight = (UInt32)getLong(&parameters[bytes_used[0]+4]);
    bytes_used[0] += 16;
    if (BadRectTop < BadRectBottom && BadRectLeft < BadRectRight) {
      for (UInt32 y = BadRectLeft; y <= BadRectRight; y++) {
        for (UInt32 x = BadRectTop; x <= BadRectBottom; x++) {
          bad_pos.push_back(x | (y << 16));
        }
      }
    }
  }
}

void OpcodeFixBadPixelsList::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  iPoint2D crop = in.getCropOffset();
  UInt32 offset = crop.x | (crop.y << 16);
  for (vector<UInt32>::iterator i=bad_pos.begin(); i != bad_pos.end(); ++i) {
    UInt32 pos = offset + (*i);
    out.mBadPixelPositions.push_back(pos);
  }
}

 /***************** OpcodeTrimBounds   ****************/

OpcodeTrimBounds::OpcodeTrimBounds(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 16)
    ThrowRDE("OpcodeTrimBounds: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mTop = getLong(&parameters[0]);
  mLeft = getLong(&parameters[4]);
  mBottom = getLong(&parameters[8]);
  mRight = getLong(&parameters[12]);
  *bytes_used = 16;
}

void OpcodeTrimBounds::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  iRectangle2D crop(mLeft, mTop, mRight-mLeft, mBottom-mTop);
  out.subFrame(crop);
}

/***************** OpcodeMapTable   ****************/

OpcodeMapTable::OpcodeMapTable(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 36)
    ThrowRDE("OpcodeMapTable: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
  mPlanes = getLong(&parameters[20]);
  mRowPitch = getLong(&parameters[24]);
  mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    ThrowRDE("OpcodeMapPolynomial: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    ThrowRDE("OpcodeMapPolynomial: Invalid Pitch");

  int tablesize = getLong(&parameters[32]);
  *bytes_used = 36;

  if (tablesize <= 0)
    ThrowRDE("OpcodeMapTable: Table size must be positive");
  if (tablesize > 65536)
    ThrowRDE("OpcodeMapTable: A map with more than 65536 entries not allowed");

  if (param_max_bytes < 36 + ((UInt64)tablesize*2))
    ThrowRDE("OpcodeMapPolynomial: Not enough data to read parameters, only %u bytes left.", param_max_bytes);

  for (int i = 0; i <= 65535; i++)
  {
    int location = Math.Math.Min(((tablesize-1, i);
    mLookup[i] = getUshort(&parameters[36+2*location]);
  }

  *bytes_used += tablesize*2;
  mFlags = MultiThreaded | PureLookup;
}


RawImage& OpcodeMapTable::createOutput( RawImage &in )
{
  if (in.getDataType() != TYPE_USHORT16)
    ThrowRDE("OpcodeMapTable: Only 16 bit images supported");

  if (mFirstPlane > in.getCpp())
    ThrowRDE("OpcodeMapTable: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    ThrowRDE("OpcodeMapTable: Not that many planes in actual image");

  return in;
}

void OpcodeMapTable::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  int cpp = out.getCpp();
  for (UInt64 y = startY; y < endY; y += mRowPitch) {
    UInt16 *src = (UInt16*)out.getData(mAoi.getLeft(), y);
    // Add offset, so this is always first plane
    src+=mFirstPlane;
    for (UInt64 x = 0; x < (UInt64) mAoi.getWidth(); x += mColPitch) {
      for (UInt64 p = 0; p < mPlanes; p++)
      {
        src[x*cpp+p] = mLookup[src[x*cpp+p]];
      }
    }
  }
}



 /***************** OpcodeMapPolynomial   ****************/

OpcodeMapPolynomial::OpcodeMapPolynomial(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 36)
    ThrowRDE("OpcodeMapPolynomial: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
  mPlanes = getLong(&parameters[20]);
  mRowPitch = getLong(&parameters[24]);
  mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    ThrowRDE("OpcodeMapPolynomial: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    ThrowRDE("OpcodeMapPolynomial: Invalid Pitch");

  mDegree = getLong(&parameters[32]);
  *bytes_used = 36;
  if (mDegree > 8)
    ThrowRDE("OpcodeMapPolynomial: A polynomial with more than 8 degrees not allowed");
  if (param_max_bytes < 36 + (mDegree*8))
    ThrowRDE("OpcodeMapPolynomial: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  for (UInt64 i = 0; i <= mDegree; i++)
    mCoefficient[i] = getDouble(&parameters[36+8*i]);
  *bytes_used += 8*mDegree+8;
  mFlags = MultiThreaded | PureLookup;
}


RawImage& OpcodeMapPolynomial::createOutput( RawImage &in )
{
  if (in.getDataType() != TYPE_USHORT16)
    ThrowRDE("OpcodeMapPolynomial: Only 16 bit images supported");

  if (mFirstPlane > in.getCpp())
    ThrowRDE("OpcodeMapPolynomial: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    ThrowRDE("OpcodeMapPolynomial: Not that many planes in actual image");

  // Create lookup
  for (int i = 0; i < 65536; i++)
  {
    double in_val = (double)i/65536.0;
    double val = mCoefficient[0];
    for (UInt64 j = 1; j <= mDegree; j++)
      val += mCoefficient[j] * pow(in_val, (double)(j));
    mLookup[i] = clampbits((int)(val*65535.5), 16);
  }
  return in;
}

void OpcodeMapPolynomial::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  int cpp = out.getCpp();
  for (UInt64 y = startY; y < endY; y += mRowPitch) {
    UInt16 *src = (UInt16*)out.getData(mAoi.getLeft(), y);
    // Add offset, so this is always first plane
    src+=mFirstPlane;
    for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
      for (UInt64 p = 0; p < mPlanes; p++)
      {
        src[x*cpp+p] = mLookup[src[x*cpp+p]];
      }
    }
  }
}

/***************** OpcodeDeltaPerRow   ****************/

OpcodeDeltaPerRow::OpcodeDeltaPerRow(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 36)
    ThrowRDE("OpcodeDeltaPerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
  mPlanes = getLong(&parameters[20]);
  mRowPitch = getLong(&parameters[24]);
  mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    ThrowRDE("OpcodeDeltaPerRow: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    ThrowRDE("OpcodeDeltaPerRow: Invalid Pitch");

  mCount = getLong(&parameters[32]);
  *bytes_used = 36;
  if (param_max_bytes < 36 + (mCount*4))
    ThrowRDE("OpcodeDeltaPerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64)mAoi.getHeight() != mCount)
    ThrowRDE("OpcodeDeltaPerRow: Element count (%llu) does not match height of area (%d.", mCount, mAoi.getHeight());

  for (UInt64 i = 0; i < mCount; i++)
    mDelta[i] = getFloat(&parameters[36+4*i]);
  *bytes_used += 4*mCount;
  mFlags = MultiThreaded;
}


RawImage& OpcodeDeltaPerRow::createOutput( RawImage &in )
{
  if (mFirstPlane > in.getCpp())
    ThrowRDE("OpcodeDeltaPerRow: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    ThrowRDE("OpcodeDeltaPerRow: Not that many planes in actual image");

  return in;
}

void OpcodeDeltaPerRow::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  if (in.getDataType() == TYPE_USHORT16) {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      UInt16 *src = (UInt16*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      int delta = (int)(65535.0f * mDelta[y]);
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = clampbits(16,delta + src[x*cpp+p]);
        }
      }
    }
  } else {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      float *src = (float*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      float delta = mDelta[y];
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = delta + src[x*cpp+p];
        }
      }
    }
  }
}

/***************** OpcodeDeltaPerCol   ****************/

OpcodeDeltaPerCol::OpcodeDeltaPerCol(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 36)
    ThrowRDE("OpcodeDeltaPerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
  mPlanes = getLong(&parameters[20]);
  mRowPitch = getLong(&parameters[24]);
  mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    ThrowRDE("OpcodeDeltaPerCol: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    ThrowRDE("OpcodeDeltaPerCol: Invalid Pitch");

  mCount = getLong(&parameters[32]);
  *bytes_used = 36;
  if (param_max_bytes < 36 + (mCount*4))
    ThrowRDE("OpcodeDeltaPerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64)mAoi.getWidth() != mCount)
    ThrowRDE("OpcodeDeltaPerRow: Element count (%llu) does not match width of area (%d).", mCount, mAoi.getWidth());

  for (UInt64 i = 0; i < mCount; i++)
    mDelta[i] = getFloat(&parameters[36+4*i]);
  *bytes_used += 4*mCount;
  mFlags = MultiThreaded;
  mDeltaX = null;
}

OpcodeDeltaPerCol::~OpcodeDeltaPerCol( void )
{
  if (mDeltaX)
    delete[] mDeltaX;
  mDeltaX = null;
}


RawImage& OpcodeDeltaPerCol::createOutput( RawImage &in )
{
  if (mFirstPlane > in.getCpp())
    ThrowRDE("OpcodeDeltaPerCol: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    ThrowRDE("OpcodeDeltaPerCol: Not that many planes in actual image");

  if (in.getDataType() == TYPE_USHORT16) {
    if (mDeltaX)
      delete[] mDeltaX;
    int w = mAoi.getWidth();
    mDeltaX = new int[w];
    for (int i = 0; i < w; i++)
      mDeltaX[i] = (int)(65535.0f * mDelta[i] + 0.5f);
  }
  return in;
}

void OpcodeDeltaPerCol::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  if (in.getDataType() == TYPE_USHORT16) {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      UInt16 *src = (UInt16*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = clampbits(16, mDeltaX[x] + src[x*cpp+p]);
        }
      }
    }
  } else {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      float *src = (float*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = mDelta[x] + src[x*cpp+p];
        }
      }
    }
  }
}

/***************** OpcodeScalePerRow   ****************/

OpcodeScalePerRow::OpcodeScalePerRow(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 36)
    ThrowRDE("OpcodeScalePerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
  mPlanes = getLong(&parameters[20]);
  mRowPitch = getLong(&parameters[24]);
  mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    ThrowRDE("OpcodeScalePerRow: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    ThrowRDE("OpcodeScalePerRow: Invalid Pitch");

  mCount = getLong(&parameters[32]);
  *bytes_used = 36;
  if (param_max_bytes < 36 + (mCount*4))
    ThrowRDE("OpcodeScalePerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64)mAoi.getHeight() != mCount)
    ThrowRDE("OpcodeScalePerRow: Element count (%llu) does not match height of area (%d).", mCount, mAoi.getHeight());

  for (UInt64 i = 0; i < mCount; i++)
    mDelta[i] = getFloat(&parameters[36+4*i]);
  *bytes_used += 4*mCount;
  mFlags = MultiThreaded;
}


RawImage& OpcodeScalePerRow::createOutput( RawImage &in )
{
  if (mFirstPlane > in.getCpp())
    ThrowRDE("OpcodeScalePerRow: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    ThrowRDE("OpcodeScalePerRow: Not that many planes in actual image");

  return in;
}

void OpcodeScalePerRow::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  if (in.getDataType() == TYPE_USHORT16) {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      UInt16 *src = (UInt16*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      int delta = (int)(1024.0f * mDelta[y]);
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = clampbits(16,(delta * src[x*cpp+p] + 512) >> 10);
        }
      }
    }
  } else {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      float *src = (float*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      float delta = mDelta[y];
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = delta * src[x*cpp+p];
        }
      }
    }
  }
}

/***************** OpcodeScalePerCol   ****************/

OpcodeScalePerCol::OpcodeScalePerCol(byte[] parameters, UInt32 param_max_bytes, UInt32 *bytes_used )
{
  if (param_max_bytes < 36)
    ThrowRDE("OpcodeScalePerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
  mPlanes = getLong(&parameters[20]);
  mRowPitch = getLong(&parameters[24]);
  mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    ThrowRDE("OpcodeScalePerCol: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    ThrowRDE("OpcodeScalePerCol: Invalid Pitch");

  mCount = getLong(&parameters[32]);
  *bytes_used = 36;
  if (param_max_bytes < 36 + (mCount*4))
    ThrowRDE("OpcodeScalePerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64)mAoi.getWidth() != mCount)
    ThrowRDE("OpcodeScalePerCol: Element count (%llu) does not match width of area (%d).", mCount, mAoi.getWidth());

  for (UInt64 i = 0; i < mCount; i++)
    mDelta[i] = getFloat(&parameters[36+4*i]);
  *bytes_used += 4*mCount;
  mFlags = MultiThreaded;
  mDeltaX = null;
}

OpcodeScalePerCol::~OpcodeScalePerCol( void )
{
  if (mDeltaX)
    delete[] mDeltaX;
  mDeltaX = null;
}


RawImage& OpcodeScalePerCol::createOutput( RawImage &in )
{
  if (mFirstPlane > in.getCpp())
    ThrowRDE("OpcodeScalePerCol: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    ThrowRDE("OpcodeScalePerCol: Not that many planes in actual image");

  if (in.getDataType() == TYPE_USHORT16) {
    if (mDeltaX)
      delete[] mDeltaX;
    int w = mAoi.getWidth();
    mDeltaX = new int[w];
    for (int i = 0; i < w; i++)
      mDeltaX[i] = (int)(1024.0f * mDelta[i]);
  }
  return in;
}

void OpcodeScalePerCol::apply( RawImage &in, RawImage &out, UInt32 startY, UInt32 endY )
{
  if (in.getDataType() == TYPE_USHORT16) {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      UInt16 *src = (UInt16*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = clampbits(16, (mDeltaX[x] * src[x*cpp+p] + 512) >> 10);
        }
      }
    }
  } else {
    int cpp = out.getCpp();
    for (UInt64 y = startY; y < endY; y += mRowPitch) {
      float *src = (float*)out.getData(mAoi.getLeft(), y);
      // Add offset, so this is always first plane
      src+=mFirstPlane;
      for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch) {
        for (UInt64 p = 0; p < mPlanes; p++)
        {
          src[x*cpp+p] = mDelta[x] * src[x*cpp+p];
        }
      }
    }
  }
}


} // namespace RawSpeed 
