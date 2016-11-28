#ifndef SRW_DECODER_H
#define SRW_DECODER_H

#include "RawDecoder.h"
#include "LJpegPlain.h"
#include "TiffIFD.h"
#include "BitPumpPlain.h"

/* 
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2010 Klaus Post

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

class SrwDecoder :
  public RawDecoder
{
public:
  SrwDecoder(TiffIFD *rootIFD, FileMap* file);
  virtual ~SrwDecoder(void);
  virtual RawImage decodeRawInternal();
  virtual void decodeMetaDataInternal(CameraMetaData *meta);
  virtual void checkSupportInternal(CameraMetaData *meta);
  virtual TiffIFD* getRootIFD() {return mRootIFD;}
private:
  typedef struct {
    byte encLen;
    byte diffLen;
  } encTableItem;

  void decodeCompressed(TiffIFD* raw);
  void decodeCompressed2(TiffIFD* raw, int bits);
  int32 samsungDiff (BitPumpMSB &pump, encTableItem *tbl);
  void decodeCompressed3(TiffIFD* raw, int bits);
  string getMode();
  TiffIFD *mRootIFD;
  ByteStream *b;
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "SrwDecoder.h"
#include "ByteStreamSwap.h"

#if defined(__unix__) || defined(__APPLE__) 
#include <stdlib.h>
#endif
/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2010 Klaus Post
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

SrwDecoder::SrwDecoder(TiffIFD *rootIFD, FileMap* file):
RawDecoder(file), mRootIFD(rootIFD) {
  decoderVersion = 3;
  b = null;
}

SrwDecoder::~SrwDecoder(void) {
  if (mRootIFD)
    delete mRootIFD;
  mRootIFD = null;
  if (null != b)
    delete b;
  b = null;
}

RawImage SrwDecoder::decodeRawInternal() {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(STRIPOFFSETS);

  if (data.empty())
    ThrowRDE("Srw Decoder: No image data found");

  TiffIFD* raw = data[0];

  int compression = raw.getEntry(COMPRESSION).getInt();
  int bits = raw.getEntry(BITSPERSAMPLE).getInt();

  if (32769 != compression && 32770 != compression && 32772 != compression && 32773 != compression)
    ThrowRDE("Srw Decoder: Unsupported compression");

  if (32769 == compression)
  {
    bool bit_order = false;  // Default guess
    map<string,string>::iterator msb_hint = hints.find("msb_override");
    if (msb_hint != hints.end())
      bit_order = (0 == (msb_hint.second).compare("true"));
    this.decodeUncompressed(raw, bit_order ? BitOrder_Jpeg : BitOrder_Plain);
    return mRaw;
  }

  if (32770 == compression)
  {
    if (!raw.hasEntry ((TiffTag)40976)) {
      bool bit_order = (bits == 12);  // Default guess
      map<string,string>::iterator msb_hint = hints.find("msb_override");
      if (msb_hint != hints.end())
        bit_order = (0 == (msb_hint.second).compare("true"));
      this.decodeUncompressed(raw, bit_order ? BitOrder_Jpeg : BitOrder_Plain);
      return mRaw;
    } else {
      UInt32 nslices = raw.getEntry(STRIPOFFSETS).count;
      if (nslices != 1)
        ThrowRDE("Srw Decoder: Only one slice supported, found %u", nslices);
      try {
        decodeCompressed(raw);
      } catch (RawDecoderException& e) {
        mRaw.setError(e.what());
      }
      return mRaw;
    }
  }
  if (32772 == compression)
  {
    UInt32 nslices = raw.getEntry(STRIPOFFSETS).count;
    if (nslices != 1)
      ThrowRDE("Srw Decoder: Only one slice supported, found %u", nslices);
    try {
      decodeCompressed2(raw, bits);
    } catch (RawDecoderException& e) {
      mRaw.setError(e.what());
    }
    return mRaw;
  }
  if (32773 == compression)
  {
    decodeCompressed3(raw, bits);
    return mRaw;
  }
  ThrowRDE("Srw Decoder: Unsupported compression");
  return mRaw;
}
// Decoder for compressed srw files (NX300 and later)
void SrwDecoder::decodeCompressed( TiffIFD* raw )
{
  UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
  UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();
  UInt32 offset = raw.getEntry(STRIPOFFSETS).getInt();
  UInt32 compressed_offset = raw.getEntry((TiffTag)40976).getInt();

  if (null != b)
    delete b;
  if (getHostEndianness() == little)
    b = new ByteStream(mFile, 0);
  else
    b = new ByteStreamSwap(mFile, 0);
  b.setAbsoluteOffset(compressed_offset);

  for (UInt32 y = 0; y < height; y++) {
    UInt32 line_offset = offset + b.getInt();
    if (line_offset >= mFile.getSize())
      ThrowRDE("Srw decoder: Offset outside image file, file probably truncated.");
    int len[4];
    for (int i = 0; i < 4; i++)
      len[i] = y < 2 ? 7 : 4;
    BitPumpMSB32 bits(mFile, line_offset);
    int op[4];
    UInt16* img = (UInt16*)mRaw.getData(0, y);
    UInt16* img_up = (UInt16*)mRaw.getData(0, Math.Max(0, (int)y - 1));
    UInt16* img_up2 = (UInt16*)mRaw.getData(0, Math.Max(0, (int)y - 2));
    // Image is arranged in groups of 16 pixels horizontally
    for (UInt32 x = 0; x < width; x += 16) {
      bits.fill();
      bool dir = !!bits.getBitNoFill();
      for (int i = 0; i < 4; i++)
        op[i] = bits.getBitsNoFill(2);
      for (int i = 0; i < 4; i++) {
        switch (op[i]) {
          case 3: len[i] = bits.getBits(4);
            break;
          case 2: len[i]--;
            break;
          case 1: len[i]++;
        }
        if (len[i] < 0)
          ThrowRDE("Srw Decompressor: Bit length less than 0.");
        if (len[i] > 16)
          ThrowRDE("Srw Decompressor: Bit Length more than 16.");
      }
      if (dir) {
        // Upward prediction
        // First we decode even pixels
        for (int c = 0; c < 16; c += 2) {
          int b = len[(c >> 3)];
          int32 adj = ((int32) bits.getBits(b) << (32-b) >> (32-b));
          img[c] = adj + img_up[c];
        }
        // Now we decode odd pixels
        // Why on earth upward prediction only looks up 1 line above
        // is beyond me, it will hurt compression a deal.
        for (int c = 1; c < 16; c += 2) {
          int b = len[2 | (c >> 3)];
          int32 adj = ((int32) bits.getBits(b) << (32-b) >> (32-b));
          img[c] = adj + img_up2[c];
        }
      } else {
        // Left to right prediction
        // First we decode even pixels
        int pred_left = x ? img[-2] : 128;
        for (int c = 0; c < 16; c += 2) {
          int b = len[(c >> 3)];
          int32 adj = ((int32) bits.getBits(b) << (32-b) >> (32-b));
          img[c] = adj + pred_left;
        }
        // Now we decode odd pixels
        pred_left = x ? img[-1] : 128;
        for (int c = 1; c < 16; c += 2) {
          int b = len[2 | (c >> 3)];
          int32 adj = ((int32) bits.getBits(b) << (32-b) >> (32-b));
          img[c] = adj + pred_left;
        }
      }
      bits.checkPos();
      img += 16;
      img_up += 16;
      img_up2 += 16;
    }
  }

  // Swap red and blue pixels to get the final CFA pattern
  for (UInt32 y = 0; y < height-1; y+=2) {
    UInt16* topline = (UInt16*)mRaw.getData(0, y);
    UInt16* bottomline = (UInt16*)mRaw.getData(0, y+1);
    for (UInt32 x = 0; x < width-1; x += 2) {
      UInt16 temp = topline[1];
      topline[1] = bottomline[0];
      bottomline[0] = temp;
      topline += 2;
      bottomline += 2;
    }
  }
}

// Decoder for compressed srw files (NX3000 and later)
void SrwDecoder::decodeCompressed2( TiffIFD* raw, int bits)
{
  UInt32 width = raw.getEntry(IMAGEWIDTH).getInt();
  UInt32 height = raw.getEntry(IMAGELENGTH).getInt();
  UInt32 offset = raw.getEntry(STRIPOFFSETS).getInt();

  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();

  // This format has a variable length encoding of how many bits are needed
  // to encode the difference between pixels, we use a table to process it
  // that has two values, the first the number of bits that were used to 
  // encode, the second the number of bits that come after with the difference
  // The table has 14 entries because the difference can have between 0 (no 
  // difference) and 13 bits (differences between 12 bits numbers can need 13)
  byte tab[14][2] = {{3,4}, {3,7}, {2,6}, {2,5}, {4,3}, {6,0}, {7,9},
                               {8,10}, {9,11}, {10,12}, {10,13}, {5,1}, {4,8}, {4,2}};
  encTableItem tbl[1024];
  UInt16 vpred[2][2] = {{0,0},{0,0}}, hpred[2];

  // We generate a 1024 entry table (to be addressed by reading 10 bits) by 
  // consecutively filling in 2^(10-N) positions where N is the variable number of
  // bits of the encoding. So for example 4 is encoded with 3 bits so the first
  // 2^(10-3)=128 positions are set with 3,4 so that any time we read 000 we 
  // know the next 4 bits are the difference. We read 10 bits because that is
  // the maximum number of bits used in the variable encoding (for the 12 and 
  // 13 cases)
  UInt32 n = 0;
  for (UInt32 i=0; i < 14; i++) {
    for(int32 c = 0; c < (1024 >> tab[i][0]); c++) {
      tbl[n  ].encLen = tab[i][0];
      tbl[n++].diffLen = tab[i][1];
    }
  }

  BitPumpMSB pump(mFile, offset);
  for (UInt32 y = 0; y < height; y++) {
    UInt16* img = (UInt16*)mRaw.getData(0, y);
    for (UInt32 x = 0; x < width; x++) {
      int32 diff = samsungDiff(pump, tbl);
      if (x < 2)
        hpred[x] = vpred[y & 1][x] += diff;
      else
        hpred[x & 1] += diff;
      img[x] = hpred[x & 1];
      if (img[x] >> bits)
        ThrowRDE("SRW: Error: decoded value out of bounds at %d:%d", x, y);
    }
  }
}

int32 SrwDecoder::samsungDiff (BitPumpMSB &pump, encTableItem *tbl)
{
  // We read 10 bits to index into our table
  UInt32 c = pump.peekBits(10);
  // Skip the bits that were used to encode this case
  pump.getBitsSafe(tbl[c].encLen);
  // Read the number of bits the table tells me
  int32 len = tbl[c].diffLen;
  int32 diff = pump.getBitsSafe(len);

  // If the first bit is 0 we need to turn this into a negative number
  if (len && (diff & (1 << (len-1))) == 0)
    diff -= (1 << len) - 1;
  return diff;
}

// Decoder for third generation compressed SRW files (NX1)
// Seriously Samsung just use lossless jpeg already, it compresses better too :)

// Thanks to Michael Reichmann (LuMath.Math.Min((ous Landscape) for putting me in contact
// and Loring von Palleske (Samsung) for pointing to the open-source code of
// Samsung's DNG converter at http://opensource.samsung.com/
void SrwDecoder::decodeCompressed3( TiffIFD* raw, int bits)
{
  UInt32 offset = raw.getEntry(STRIPOFFSETS).getInt();
  BitPumpMSB32 startpump(mFile, offset);

  // Process the initial metadata bits, we only really use initVal, width and
  // height (the last two match the TIFF values anyway)
  startpump.getBitsSafe(16); // NLCVersion
  startpump.getBitsSafe(4);  // ImgFormat
  UInt32 bitDepth = startpump.getBitsSafe(4)+1;
  startpump.getBitsSafe(4);  // NumBlkInRCUnit
  startpump.getBitsSafe(4);  // CompressionRatio
  UInt32 width    = startpump.getBitsSafe(16);
  UInt32 height    = startpump.getBitsSafe(16);
  startpump.getBitsSafe(16); // TileWidth
  startpump.getBitsSafe(4);  // reserved

  // The format includes an optimization code that sets 3 flags to change the
  // decoding parameters
  UInt32 optflags = startpump.getBitsSafe(4);
  #define OPT_SKIP 1 // Skip checking if we need differences from previous line
  #define OPT_MV   2 // Simplify motion vector definition
  #define OPT_QP   4 // Don't scale the diff values

  startpump.getBitsSafe(8);  // OverlapWidth
  startpump.getBitsSafe(8);  // reserved
  startpump.getBitsSafe(8);  // Inc
  startpump.getBitsSafe(2);  // reserved
  UInt32 initVal = startpump.getBitsSafe(14);

  mRaw.dim = iPoint2D(width, height);
  mRaw.createData();

  // The format is relatively straightforward. Each line gets encoded as a set 
  // of differences from pixels from another line. Pixels are grouped in blocks 
  // of 16 (8 green, 8 red or blue). Each block is encoded in three sections.
  // First 1 or 4 bits to specify which reference pixels to use, then a section
  // that specifies for each pixel the number of bits in the difference, then
  // the actual difference bits
  UInt32 motion;
  UInt32 diffBitsMode[3][2] = {{0}};
  UInt32 line_offset = startpump.getOffset();
  for (UInt32 row=0; row < height; row++) {
    // Align pump to 16byte boundary
    if ((line_offset & 0xf) != 0)
      line_offset += 16 - (line_offset & 0xf);
    BitPumpMSB32 pump(mFile, offset+line_offset);

    UInt16* img = (UInt16*)mRaw.getData(0, row);
    UInt16* img_up = (UInt16*)mRaw.getData(0, Math.Max(0, (int)row - 1));
    UInt16* img_up2 = (UInt16*)mRaw.getData(0, Math.Max(0, (int)row - 2));
    // Initialize the motion and diff modes at the start of the line
    motion = 7;
    // By default we are not scaling values at all
    int32 scale = 0;
    for (UInt32 i=0; i<3; i++)
      diffBitsMode[i][0] = diffBitsMode[i][1] = (row==0 || row==1) ? 7 : 4;

    for (UInt32 col=0; col < width; col += 16) {
      if (!(optflags & OPT_QP) && !(col & 63)) {
        int32 scalevals[] = {0,-2,2};
        UInt32 i = pump.getBitsSafe(2);
        scale = i < 3 ? scale+scalevals[i] : pump.getBitsSafe(12);
      }

      // First we figure out which reference pixels mode we're in
      if (optflags & OPT_MV)
        motion = pump.getBitsSafe(1) ? 3 : 7;
      else if (!pump.getBitsSafe(1))
        motion = pump.getBitsSafe(3);
      if ((row==0 || row==1) && (motion != 7))
        ThrowRDE("SRW Decoder: At start of image and motion isn't 7. File corrupted?");
      if (motion == 7) {
        // The base case, just set all pixels to the previous ones on the same line
        // If we're at the left edge we just start at the initial value
        for (UInt32 i=0; i<16; i++) {
          img[i] = (col == 0) ? initVal : *(img+i-2);
        }
      } else {
        // The complex case, we now need to actually lookup one or two lines above
        if (row < 2)
          ThrowRDE("SRW: Got a previous line lookup on first two lines. File corrupted?");
        int32 motionOffset[7] =    {-4,-2,-2,0,0,2,4};
        int32 motionDoAverage[7] = { 0, 0, 1,0,1,0,0};

        int32 slideOffset = motionOffset[motion];
        int32 doAverage = motionDoAverage[motion];

        for (UInt32 i=0; i<16; i++) {
          UInt16* refpixel;
          if ((row+i) & 0x1) // Red or blue pixels use same color two lines up
            refpixel = img_up2 + i + slideOffset;
          else // Green pixel N uses Green pixel N from row above (top left or top right)
            refpixel = img_up + i + slideOffset + ((i%2) ? -1 : 1);

          // In some cases we use as reference interpolation of this pixel and the next
          if (doAverage)
            img[i] = (*refpixel + *(refpixel+2) + 1) >> 1;
          else
            img[i] = *refpixel;
        }
      }

      // Figure out how many difference bits we have to read for each pixel
      UInt32 diffBits[4] = {0};
      if (optflags & OPT_SKIP || !pump.getBitsSafe(1)) {
        UInt32 flags[4];
        for (UInt32 i=0; i<4; i++)
          flags[i] = pump.getBitsSafe(2);
        for (UInt32 i=0; i<4; i++) {
          // The color is 0-Green 1-Blue 2-Red
          UInt32 colornum = (row % 2 != 0) ? i>>1 : ((i>>1)+2) % 3;
          switch(flags[i]) {
            case 0: diffBits[i] = diffBitsMode[colornum][0]; break;
            case 1: diffBits[i] = diffBitsMode[colornum][0]+1; break;
            case 2: diffBits[i] = diffBitsMode[colornum][0]-1; break;
            case 3: diffBits[i] = pump.getBitsSafe(4); break;
          }
          diffBitsMode[colornum][0] = diffBitsMode[colornum][1];
          diffBitsMode[colornum][1] = diffBits[i];
          if(diffBits[i] > bitDepth+1)
            ThrowRDE("SRW Decoder: Too many difference bits. File corrupted?");
        }
      }

      // Actually read the differences and write them to the pixels
      for (UInt32 i=0; i<16; i++) {
        UInt32 len = diffBits[i>>2];
        int32 diff = pump.getBitsSafe(len);

        // If the first bit is 1 we need to turn this into a negative number
        if (diff >> (len-1))
          diff -= (1 << len);

        UInt16 *value = null;
        // Apply the diff to pixels 0 2 4 6 8 10 12 14 1 3 5 7 9 11 13 15
        if (row % 2)
          value = &img[((i&0x7)<<1)+1-(i>>3)];
        else
          value = &img[((i&0x7)<<1)+(i>>3)];

        diff = diff * (scale*2+1) + scale;
        *value = clampbits(*value + diff, bits);
      }

      img += 16;
      img_up += 16;
      img_up2 += 16;
    }
    line_offset += pump.getOffset();
  }
}

string SrwDecoder::getMode() {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(CFAPATTERN);
  ostringstream mode;
  if (!data.empty() && data[0].hasEntryRecursive(BITSPERSAMPLE)) {
    mode << data[0].getEntryRecursive(BITSPERSAMPLE).getInt() << "bit";
    return mode.str();
  }
  return "";
}

void SrwDecoder::checkSupportInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
  if (data.empty())
    ThrowRDE("Srw Support check: Model name found");
  if (!data[0].hasEntry(MAKE))
    ThrowRDE("SRW Support: Make name not found");
  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();
  string mode = getMode();
  if (meta.hasCamera(make, model, mode))
    this.checkCameraSupported(meta, make, model, getMode());
  else
    this.checkCameraSupported(meta, make, model, "");
}

void SrwDecoder::decodeMetaDataInternal(CameraMetaData *meta) {
  vector<TiffIFD*> data = mRootIFD.getIFDsWithTag(MODEL);
  if (data.empty())
    ThrowRDE("SRW Meta Decoder: Model name found");

  string make = data[0].getEntry(MAKE).getString();
  string model = data[0].getEntry(MODEL).getString();

  int iso = 0;
  if (mRootIFD.hasEntryRecursive(ISOSPEEDRATINGS))
    iso = mRootIFD.getEntryRecursive(ISOSPEEDRATINGS).getInt();

  string mode = getMode();
  if (meta.hasCamera(make, model, mode))
    setMetaData(meta, make, model, mode, iso);
  else
    setMetaData(meta, make, model, "", iso);

  // Set the whitebalance
  if (mRootIFD.hasEntryRecursive(SAMSUNG_WB_RGGBLEVELSUNCORRECTED) &&
      mRootIFD.hasEntryRecursive(SAMSUNG_WB_RGGBLEVELSBLACK)) {
    TiffEntry *wb_levels = mRootIFD.getEntryRecursive(SAMSUNG_WB_RGGBLEVELSUNCORRECTED);
    TiffEntry *wb_black = mRootIFD.getEntryRecursive(SAMSUNG_WB_RGGBLEVELSBLACK);
    if (wb_levels.count == 4 && wb_black.count == 4) {
      wb_levels.offsetFromParent();
      wb_black.offsetFromParent();
      mRaw.metadata.wbCoeffs[0] = wb_levels.getFloat(0) - wb_black.getFloat(0);
      mRaw.metadata.wbCoeffs[1] = wb_levels.getFloat(1) - wb_black.getFloat(1);
      mRaw.metadata.wbCoeffs[2] = wb_levels.getFloat(3) - wb_black.getFloat(3);
    }
  }
}


} // namespace RawSpeed
