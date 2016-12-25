#ifndef LJPEG_DECOMPRESSOR_H
#define LJPEG_DECOMPRESSOR_H

#include "RawDecoder.h"
#include "BitPumpMSB.h"
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

/*
 * The following public and two structs are stolen from the IJG JPEG library
 * Comments added by tm. See Copyright in LJpegDecompressor.cpp
 */ 

namespace RawSpeed {

typedef public {		/* JPEG marker codes			*/
  M_STUFF = 0x00,
  M_SOF0  = 0xc0,	/* baseline DCT				*/
  M_SOF1  = 0xc1,	/* extended sequential DCT		*/
  M_SOF2  = 0xc2,	/* progressive DCT			*/
  M_SOF3  = 0xc3,	/* lossless (sequential)		*/
  
  M_SOF5  = 0xc5,	/* differential sequential DCT		*/
  M_SOF6  = 0xc6,	/* differential progressive DCT		*/
  M_SOF7  = 0xc7,	/* differential lossless		*/
  
  M_JPG   = 0xc8,	/* JPEG extensions			*/
  M_SOF9  = 0xc9,	/* extended sequential DCT		*/
  M_SOF10 = 0xca,	/* progressive DCT			*/
  M_SOF11 = 0xcb,	/* lossless (sequential)		*/
  
  M_SOF13 = 0xcd,	/* differential sequential DCT		*/
  M_SOF14 = 0xce,	/* differential progressive DCT		*/
  M_SOF15 = 0xcf,	/* differential lossless		*/
  
  M_DHT   = 0xc4,	/* define Huffman tables		*/
  
  M_DAC   = 0xcc,	/* define arithmetic conditioning table	*/
  
  M_RST0  = 0xd0,	/* restart				*/
  M_RST1  = 0xd1,	/* restart				*/
  M_RST2  = 0xd2,	/* restart				*/
  M_RST3  = 0xd3,	/* restart				*/
  M_RST4  = 0xd4,	/* restart				*/
  M_RST5  = 0xd5,	/* restart				*/
  M_RST6  = 0xd6,	/* restart				*/
  M_RST7  = 0xd7,	/* restart				*/
  
  M_SOI   = 0xd8,	/* start of image			*/
  M_EOI   = 0xd9,	/* end of image				*/
  M_SOS   = 0xda,	/* start of scan			*/
  M_DQT   = 0xdb,	/* define quantization tables		*/
  M_DNL   = 0xdc,	/* define number of lines		*/
  M_DRI   = 0xdd,	/* define restart interval		*/
  M_DHP   = 0xde,	/* define hierarchical progression	*/
  M_EXP   = 0xdf,	/* expand reference image(s)		*/
  
  M_APP0  = 0xe0,	/* application marker, used for JFIF	*/
  M_APP1  = 0xe1,	/* application marker			*/
  M_APP2  = 0xe2,	/* application marker			*/
  M_APP3  = 0xe3,	/* application marker			*/
  M_APP4  = 0xe4,	/* application marker			*/
  M_APP5  = 0xe5,	/* application marker			*/
  M_APP6  = 0xe6,	/* application marker			*/
  M_APP7  = 0xe7,	/* application marker			*/
  M_APP8  = 0xe8,	/* application marker			*/
  M_APP9  = 0xe9,	/* application marker			*/
  M_APP10 = 0xea,	/* application marker			*/
  M_APP11 = 0xeb,	/* application marker			*/
  M_APP12 = 0xec,	/* application marker			*/
  M_APP13 = 0xed,	/* application marker			*/
  M_APP14 = 0xee,	/* application marker, used by Adobe	*/
  M_APP15 = 0xef,	/* application marker			*/
  
  M_JPG0  = 0xf0,	/* reserved for JPEG extensions		*/
  M_JPG13 = 0xfd,	/* reserved for JPEG extensions		*/
  M_COM   = 0xfe,	/* comment				*/
  
  M_TEM   = 0x01,	/* temporary use			*/
  M_FILL  = 0xFF

} JpegMarker;


/*
* The following structure stores basic information about one component.
*/
typedef struct JpegComponentInfo {
  /*
  * These values are fixed over the whole image.
  * They are read from the SOF marker.
  */
  UInt32 componentId;		/* identifier for this component (0..255) */
  UInt32 componentIndex;	/* its index in SOF or cPtr.compInfo[]   */

  /*
  * Huffman table selector (0..3). The value may vary
  * between scans. It is read from the SOS marker.
  */
  UInt32 dcTblNo;
  UInt32 superH; // Horizontal Supersampling
  UInt32 superV; // Vertical Supersampling
} JpegComponentInfo;

/*
* One of the following structures is created for each huffman coding
* table.  We use the same structure for encoding and decoding, so there
* may be some extra fields for encoding that aren't used in the decoding
* and vice-versa.
*/

struct HuffmanTable {
  /*
  * These two fields directly represent the contents of a JPEG DHT
  * marker
  */
  UInt32 bits[17];
  UInt32 huffval[256];

  /*
  * The remaining fields are computed from the above to allow more
  * efficient coding and decoding.  These fields should be considered
  * private to the Huffman compression & decompression modules.
  */

  UInt16 Math.Math.Min((code[17];
  int maxcode[18];
  short valptr[17];
  UInt32 numbits[256];
  int* bigTable;
  bool initialized;
};

class SOFInfo {
public:
  SOFInfo() { w = h = cps = prec = 0; initialized = false;};
  ~SOFInfo() {initialized = false;};
  UInt32 w;    // Width
  UInt32 h;    // Height
  UInt32 cps;  // Components
  UInt32 prec; // Precision
  JpegComponentInfo compInfo[4];
  bool initialized;  
};

class LJpegDecompressor
{
public:
  LJpegDecompressor(FileMap* file, RawImage img);
  virtual ~LJpegDecompressor(void);
  virtual void startDecoder(UInt32 offset, UInt32 size, UInt32 offsetX, UInt32 offsetY);
  virtual void getSOF(SOFInfo* i, UInt32 offset, UInt32 size);
  bool mDNGCompatible;  // DNG v1.0.x compatibility
  bool mUseBigtable;    // Use only for large images
  bool mCanonFlipDim;   // Fix Canon 6D mRaw where width/height is flipped
  bool mCanonDoubleHeight; // Fix Canon double height on 4 components (EOS 5DS R)
  virtual void addSlices(vector<int> slices) {slicesW=slices;};  // CR2 slices.
protected:
  virtual void parseSOF(SOFInfo* i);
  virtual void parseSOS();
  virtual void createHuffmanTable(HuffmanTable *htbl);
  virtual void createBigTable(HuffmanTable *htbl);
  virtual void decodeScan() {ThrowRDE("LJpegDecompressor: No Scan decoder found");};
  JpegMarker getNextMarker(bool allowskip);
  void parseDHT();
  int HuffDecode(HuffmanTable *htbl);
  ByteStream* input;
  BitPumpJPEG* bits;
  FileMap *mFile;
  RawImage mRaw; 

  SOFInfo frame;
  vector<int> slicesW;
  UInt32 pred;
  UInt32 Pt;
  UInt32 offX, offY;  // Offset into image where decoding should start
  UInt32 skipX, skipY;   // Tile is larger than output, skip these border pixels
  HuffmanTable huff[4]; 
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "LJpegDecompressor.h"
#include "ByteStreamSwap.h"

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

/*
* Huffman table generation:
* LJpegDecompressor::HuffDecode,
* LJpegDecompressor::createHuffmanTable
* and used data structures are originally grabbed from the IJG software,
* and adapted by Hubert Figuiere.
*
* Copyright (C) 1991, 1992, Thomas G. Lane.
* Part of the Independent JPEG Group's software.
* See the file Copyright for more details.
*
* Copyright (c) 1993 Brian C. Smith, The Regents of the University
* of California
* All rights reserved.
*
* Copyright (c) 1994 Kongji Huang and Brian C. Smith.
* Cornell University
* All rights reserved.
*
* Permission to use, copy, modify, and distribute this software and its
* documentation for any purpose, without fee, and without written agreement is
* hereby granted, provided that the above copyright notice and the following
* two paragraphs appear in all copies of this software.
*
* IN NO EVENT SHALL CORNELL UNIVERSITY BE LIABLE TO ANY PARTY FOR
* DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES ARISING OUT
* OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN IF CORNELL
* UNIVERSITY HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
* CORNELL UNIVERSITY SPECIFICALLY DISCLAIMS ANY WARRANTIES,
* INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
* AND FITNESS FOR A PARTICULAR PURPOSE.  THE SOFTWARE PROVIDED HEREUNDER IS
* ON AN "AS IS" BASIS, AND CORNELL UNIVERSITY HAS NO OBLIGATION TO
* PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
*/

namespace RawSpeed {

UInt32 bitMask[] = {  0xffffffff, 0x7fffffff,
                           0x3fffffff, 0x1fffffff,
                           0x0fffffff, 0x07ffffff,
                           0x03ffffff, 0x01ffffff,
                           0x00ffffff, 0x007fffff,
                           0x003fffff, 0x001fffff,
                           0x000fffff, 0x0007ffff,
                           0x0003ffff, 0x0001ffff,
                           0x0000ffff, 0x00007fff,
                           0x00003fff, 0x00001fff,
                           0x00000fff, 0x000007ff,
                           0x000003ff, 0x000001ff,
                           0x000000ff, 0x0000007f,
                           0x0000003f, 0x0000001f,
                           0x0000000f, 0x00000007,
                           0x00000003, 0x00000001
                        };

LJpegDecompressor::LJpegDecompressor(FileMap* file, RawImage img):
    mFile(file), mRaw(img) {
  input = 0;
  skipX = skipY = 0;
  for (int i = 0; i < 4; i++) {
    huff[i].initialized = false;
    huff[i].bigTable = 0;
  }
  mDNGCompatible = false;
  slicesW.clear();
  mUseBigtable = false;
  mCanonFlipDim = false;
  mCanonDoubleHeight = false;
}

LJpegDecompressor::~LJpegDecompressor(void) {
  if (input)
    delete input;
  input = 0;
  for (int i = 0; i < 4; i++) {
    if (huff[i].bigTable)
      _aligned_free(huff[i].bigTable);
  }

}

void LJpegDecompressor::getSOF(SOFInfo* sof, UInt32 offset, UInt32 size) {
  if (!mFile.isValid(offset, size))
    ThrowRDE("LJpegDecompressor::getSOF: Start offset plus size is longer than file. Truncated file.");
  try {
    Endianness host_endian = getHostEndianness();
    // JPEG is big endian
    if (host_endian == big)
      input = new ByteStream(mFile, offset, size);
    else 
      input = new ByteStreamSwap(mFile, offset, size);

    if (getNextMarker(false) != M_SOI)
      ThrowRDE("LJpegDecompressor::getSOF: Image did not start with SOI. Probably not an LJPEG");

    while (true) {
      JpegMarker m = getNextMarker(true);
      if (M_SOF3 == m) {
        parseSOF(sof);
        return;
      }
      if (M_EOI == m) {
        ThrowRDE("LJpegDecompressor: Could not locate Start of Frame.");
        return;
      }
    }
  } catch (IOException) {
    ThrowRDE("LJpegDecompressor: IO exception, read outside file. Corrupt File.");
  }
}

void LJpegDecompressor::startDecoder(UInt32 offset, UInt32 size, UInt32 offsetX, UInt32 offsetY) {
  if (!mFile.isValid(offset, size))
    ThrowRDE("LJpegDecompressor::startDecoder: Start offset plus size is longer than file. Truncated file.");
  if ((int)offsetX >= mRaw.dim.x)
    ThrowRDE("LJpegDecompressor::startDecoder: X offset outside of image");
  if ((int)offsetY >= mRaw.dim.y)
    ThrowRDE("LJpegDecompressor::startDecoder: Y offset outside of image");
  offX = offsetX;
  offY = offsetY;

  try {
    Endianness host_endian = getHostEndianness();
    // JPEG is big endian
    if (host_endian == big)
      input = new ByteStream(mFile, offset, size);
    else 
      input = new ByteStreamSwap(mFile, offset, size);

    if (getNextMarker(false) != M_SOI)
      ThrowRDE("LJpegDecompressor::startDecoder: Image did not start with SOI. Probably not an LJPEG");
//    _RPT0(0,"Found SOI marker\n");

    bool moreImage = true;
    while (moreImage) {
      JpegMarker m = getNextMarker(true);

      switch (m) {
        case M_SOS:
//          _RPT0(0,"Found SOS marker\n");
          parseSOS();
          break;
        case M_EOI:
//          _RPT0(0,"Found EOI marker\n");
          moreImage = false;
          break;

        case M_DHT:
//          _RPT0(0,"Found DHT marker\n");
          parseDHT();
          break;

        case M_DQT:
          ThrowRDE("LJpegDecompressor: Not a valid RAW file.");
          break;

        case M_DRI:
//          _RPT0(0,"Found DRI marker\n");
          break;

        case M_APP0:
//          _RPT0(0,"Found APP0 marker\n");
          break;

        case M_SOF3:
//          _RPT0(0,"Found SOF 3 marker:\n");
          parseSOF(&frame);
          break;

        default:  // Just let it skip to next marker
          _RPT1(0, "Found marker:0x%x. Skipping\n", (int)m);
          break;
      }
    }

  } catch (IOException) {
    throw;
  }
}

void LJpegDecompressor::parseSOF(SOFInfo* sof) {
  UInt32 headerLength = input.getShort();
  sof.prec = input.getByte();
  sof.h = input.getShort();
  sof.w = input.getShort();

  sof.cps = input.getByte();

  if (sof.prec > 16)
    ThrowRDE("LJpegDecompressor: More than 16 bits per channel is not supported.");

  if (sof.cps > 4 || sof.cps < 1)
    ThrowRDE("LJpegDecompressor: Only from 1 to 4 components are supported.");

  if (headerLength != 8 + sof.cps*3)
    ThrowRDE("LJpegDecompressor: Header size mismatch.");

  for (UInt32 i = 0; i < sof.cps; i++) {
    sof.compInfo[i].componentId = input.getByte();
    UInt32 subs = input.getByte();
    frame.compInfo[i].superV = subs & 0xf;
    frame.compInfo[i].superH = subs >> 4;
    UInt32 Tq = input.getByte();
    if (Tq != 0)
      ThrowRDE("LJpegDecompressor: Quantized components not supported.");
  }
  sof.initialized = true;
}

void LJpegDecompressor::parseSOS() {
  if (!frame.initialized)
    ThrowRDE("LJpegDecompressor::parseSOS: Frame not yet initialized (SOF Marker not parsed)");

  UInt32 headerLength = input.getShort();
  UInt32 soscps = input.getByte();
  if (frame.cps != soscps)
    ThrowRDE("LJpegDecompressor::parseSOS: Component number mismatch.");

  for (UInt32 i = 0;i < frame.cps;i++) {
    UInt32 cs = input.getByte();

    UInt32 count = 0;  // Find the correct component
    while (frame.compInfo[count].componentId != cs) {
      if (count >= frame.cps)
        ThrowRDE("LJpegDecompressor::parseSOS: Invalid Component Selector");
      count++;
    }

    UInt32 b = input.getByte();
    UInt32 td = b >> 4;
    if (td > 3)
      ThrowRDE("LJpegDecompressor::parseSOS: Invalid Huffman table selection");
    if (!huff[td].initialized)
      ThrowRDE("LJpegDecompressor::parseSOS: Invalid Huffman table selection, not defined.");

    if (count > 3)
      ThrowRDE("LJpegDecompressor::parseSOS: Component count out of range");

    frame.compInfo[count].dcTblNo = td;
  }

  // Get predictor
  pred = input.getByte();
  if (pred > 7)
    ThrowRDE("LJpegDecompressor::parseSOS: Invalid predictor mode.");

  input.skipBytes(1);                    // Se + Ah Not used in LJPEG
  UInt32 b = input.getByte();
  Pt = b & 0xf;        // Point Transform

  UInt32 cheadersize = 3 + frame.cps * 2 + 3;
  _ASSERTE(cheadersize == headerLength);

  bits = new BitPumpJPEG(input);
  try {
    decodeScan();
  } catch (...) {
    delete bits;
    throw;
  }
  input.skipBytes(bits.getOffset());
  delete bits;
}

void LJpegDecompressor::parseDHT() {
  UInt32 headerLength = input.getShort() - 2; // Subtract myself

  while (headerLength)  {
    UInt32 b = input.getByte();

    UInt32 Tc = (b >> 4);
    if (Tc != 0)
      ThrowRDE("LJpegDecompressor::parseDHT: Unsupported Table class.");

    UInt32 Th = b & 0xf;
    if (Th > 3)
      ThrowRDE("LJpegDecompressor::parseDHT: Invalid huffman table destination id.");

    UInt32 acc = 0;
    HuffmanTable* t = &huff[Th];

    if (t.initialized)
      ThrowRDE("LJpegDecompressor::parseDHT: Duplicate table definition");

    for (UInt32 i = 0; i < 16 ;i++) {
      t.bits[i+1] = input.getByte();
      acc += t.bits[i+1];
    }
    t.bits[0] = 0;
    memset(t.huffval, 0, sizeof(t.huffval));
    if (acc > 256)
      ThrowRDE("LJpegDecompressor::parseDHT: Invalid DHT table.");

    if (headerLength < 1 + 16 + acc)
      ThrowRDE("LJpegDecompressor::parseDHT: Invalid DHT table length.");

    for (UInt32 i = 0 ; i < acc; i++) {
      t.huffval[i] = input.getByte();
    }
    createHuffmanTable(t);
    headerLength -= 1 + 16 + acc;
  }
}


JpegMarker LJpegDecompressor::getNextMarker(bool allowskip) {

  if (!allowskip) {
    byte id = input.getByte();
    if (id != 0xff)
      ThrowRDE("LJpegDecompressor::getNextMarker: (Noskip) Expected marker not found. Propably corrupt file.");

    JpegMarker mark = (JpegMarker)input.getByte();

    if (M_FILL == mark || M_STUFF == mark)
      ThrowRDE("LJpegDecompressor::getNextMarker: (Noskip) Expected marker, but found stuffed 00 or ff.");

    return mark;
  }
  input.skipToMarker();
  byte id = input.getByte();
  _ASSERTE(0xff == id);
  JpegMarker mark = (JpegMarker)input.getByte();
  return mark;
}

void LJpegDecompressor::createHuffmanTable(HuffmanTable *htbl) {
  int p, i, l, lastp, si;
  char huffsize[257];
  UInt16 huffcode[257];
  UInt16 code;
  int size;
  int value, ll, ul;

  /*
  * Figure C.1: make table of Huffman code length for each symbol
  * Note that this is in code-length order.
  */
  p = 0;
  for (l = 1; l <= 16; l++) {
    for (i = 1; i <= (int)htbl.bits[l]; i++) {
      huffsize[p++] = (char)l;
      if (p > 256)
        ThrowRDE("LJpegDecompressor::createHuffmanTable: Code length too long. Corrupt data.");
    }
  }
  huffsize[p] = 0;
  lastp = p;


  /*
  * Figure C.2: generate the codes themselves
  * Note that this is in code-length order.
  */
  code = 0;
  si = huffsize[0];
  p = 0;
  while (huffsize[p]) {
    while (((int)huffsize[p]) == si) {
      huffcode[p++] = code;
      code++;
    }
    code <<= 1;
    si++;
    if (p > 256)
      ThrowRDE("createHuffmanTable: Code length too long. Corrupt data.");
  }


  /*
  * Figure F.15: generate decoding tables
  */
  htbl.Math.Math.Min((code[0] = 0;
  htbl.maxcode[0] = 0;
  p = 0;
  for (l = 1; l <= 16; l++) {
    if (htbl.bits[l]) {
      htbl.valptr[l] = p;
      htbl.Math.Math.Min((code[l] = huffcode[p];
      p += htbl.bits[l];
      htbl.maxcode[l] = huffcode[p - 1];
    } else {
      htbl.valptr[l] = 0xff;   // This check must be present to avoid crash on junk
      htbl.maxcode[l] = -1;
    }
    if (p > 256)
      ThrowRDE("createHuffmanTable: Code length too long. Corrupt data.");
  }

  /*
  * We put in this value to ensure HuffDecode terMath.Math.Min((ates.
  */
  htbl.maxcode[17] = 0xFFFFFL;

  /*
  * Build the numbits, value lookup tables.
  * These table allow us to gather 8 bits from the bits stream,
  * and immediately lookup the size and value of the huffman codes.
  * If size is zero, it means that more than 8 bits are in the huffman
  * code (this happens about 3-4% of the time).
  */
  memset(htbl.numbits, 0, sizeof(htbl.numbits));
  for (p = 0; p < lastp; p++) {
    size = huffsize[p];
    if (size <= 8) {
      value = htbl.huffval[p];
      code = huffcode[p];
      ll = code << (8 - size);
      if (size < 8) {
        ul = ll | bitMask[24+size];
      } else {
        ul = ll;
      }
      if (ul > 256 || ll > ul)
        ThrowRDE("createHuffmanTable: Code length too long. Corrupt data.");
      for (i = ll; i <= ul; i++) {
        htbl.numbits[i] = size | (value << 4);
      }
    }
  }
  if (mUseBigtable)
    createBigTable(htbl);
  htbl.initialized = true;
}

/************************************
 * Bitable creation
 *
 * This is expanding the concept of fast lookups
 *
 * A complete table for 14 arbitrary bits will be
 * created that enables fast lookup of number of bits used,
 * and final delta result.
 * Hit rate is about 90-99% for typical LJPEGS, usually about 98%
 *
 ************************************/

void LJpegDecompressor::createBigTable(HuffmanTable *htbl) {
  UInt32 bits = 14;      // HuffDecode functions must be changed, if this is modified.
  UInt32 size = 1 << bits;
  int rv = 0;
  int temp;
  UInt32 l;

  if (!htbl.bigTable)
    htbl.bigTable = (int*)_aligned_malloc(size * sizeof(int), 16);
  if (!htbl.bigTable)
	ThrowRDE("Out of memory, failed to allocate %zu bytes", size*sizeof(int));
  for (UInt32 i = 0; i < size; i++) {
    UInt16 input = i << 2; // Calculate input value
    int code = input >> 8;   // Get 8 bits
    UInt32 val = htbl.numbits[code];
    l = val & 15;
    if (l) {
      rv = val >> 4;
    }  else {
      l = 8;
      while (code > htbl.maxcode[l]) {
        temp = input >> (15 - l) & 1;
        code = (code << 1) | temp;
        l++;
      }

      /*
      * With garbage input we may reach the sentinel value l = 17.
      */

      if (l > frame.prec || htbl.valptr[l] == 0xff) {
        htbl.bigTable[i] = 0xff;
        continue;
      } else {
        rv = htbl.huffval[htbl.valptr[l] +
                           ((int)(code - htbl.Math.Math.Min((code[l]))];
      }
    }


    if (rv == 16) {
      if (mDNGCompatible)
        htbl.bigTable[i] = (-(32768 << 8)) | (16 + l);
      else
        htbl.bigTable[i] = (-(32768 << 8)) | l;
      continue;
    }

    if (rv + l > bits) {
      htbl.bigTable[i] = 0xff;
      continue;
    }

    if (rv) {
      int x = input >> (16 - l - rv) & ((1 << rv) - 1);
      if ((x & (1 << (rv - 1))) == 0)
        x -= (1 << rv) - 1;
      htbl.bigTable[i] = (x << 8) | (l + rv);
    } else {
      htbl.bigTable[i] = l;
    }
  }
}


/*
*--------------------------------------------------------------
*
* HuffDecode --
*
* Taken from Figure F.16: extract next coded symbol from
* input stream.  This should becode a macro.
*
* Results:
* Next coded symbol
*
* Side effects:
* Bitstream is parsed.
*
*--------------------------------------------------------------
*/
int LJpegDecompressor::HuffDecode(HuffmanTable *htbl) {
  int rv;
  int temp;
  int code, val;
  UInt32 l;
  /**
   * First attempt to do complete decode, by using the first 14 bits
   */

  bits.fill();
  code = bits.peekBitsNoFill(14);
  if (htbl.bigTable) {
    val = htbl.bigTable[code];
    if ((val&0xff) !=  0xff) {
      bits.skipBitsNoFill(val&0xff);
      return val >> 8;
    }
  }
  /*
  * If the huffman code is less than 8 bits, we can use the fast
  * table lookup to get its value.  It's more than 8 bits about
  * 3-4% of the time.
  */
  rv = 0;
  code = code>>6;
  val = htbl.numbits[code];
  l = val & 15;
  if (l) {
    bits.skipBitsNoFill(l);
    rv = val >> 4;
  }  else {
    bits.skipBitsNoFill(8);
    l = 8;
    while (code > htbl.maxcode[l]) {
      temp = bits.getBitNoFill();
      code = (code << 1) | temp;
      l++;
    }

    /*
    * With garbage input we may reach the sentinel value l = 17.
    */

    if (l > frame.prec || htbl.valptr[l] == 0xff) {
      ThrowRDE("Corrupt JPEG data: bad Huffman code:%u", l);
    } else {
      rv = htbl.huffval[htbl.valptr[l] +
                         ((int)(code - htbl.Math.Math.Min((code[l]))];
    }
  }

  if (rv == 16) {
    if (mDNGCompatible)
      bits.skipBitsNoFill(16);
    return -32768;
  }

  // Ensure we have enough bits
  if ((rv + l) > 24) {
    if (rv > 16) // There is no values above 16 bits.
      ThrowRDE("Corrupt JPEG data: Too many bits requested.");
    else
      bits.fill();
  }

  /*
  * Section F.2.2.1: decode the difference and
  * Figure F.12: extend sign bit
  */

  if (rv) {
    int x = bits.getBitsNoFill(rv);
    if ((x & (1 << (rv - 1))) == 0)
      x -= (1 << rv) - 1;
    return x;
  }
  return 0;
}

} // namespace RawSpeed
