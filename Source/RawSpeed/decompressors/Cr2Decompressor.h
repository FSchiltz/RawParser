/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2017 Axel Waggershauser

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
*/

#pragma once

#include "decompressors/AbstractLJpegDecompressor.h" // for AbstractLJpegDe...
#include "io/Buffer.h"                               // for Buffer, Buffer:...
#include "io/ByteStream.h"                           // for ByteStream
#include <vector>                                    // for vector

namespace rawspeed {

class RawImage;

// Decompresses Lossless JPEGs, with 2-4 components and optional X/Y subsampling

class Cr2Decompressor final : public AbstractLJpegDecompressor
{
  // CR2 slices
  std::vector<int> slicesWidths;

  void decodeScan() override;
  template<int N_COMP, int X_S_F, int Y_S_F> void decodeN_X_Y();

public:
  Cr2Decompressor(const ByteStream& bs, const RawImage& img)
      : AbstractLJpegDecompressor(bs, img) {}

  void decode(std::vector<int> slicesWidths);
};

} // namespace rawspeed
