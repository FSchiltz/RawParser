/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post
    Copyright (C) 2014 Pedro Côrte-Real
    Copyright (C) 2017 Roman Lebedev

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

#include "parsers/CiffParser.h"
#include "common/Common.h"               // for make_unique, trimSpaces
#include "common/NORangesSet.h"          // for NORangesSet
#include "decoders/CrwDecoder.h"         // for CrwDecoder
#include "decoders/RawDecoder.h"         // for RawDecoder
#include "io/ByteStream.h"               // for ByteStream
#include "io/Endianness.h"               // for getHostEndianness, Endianne...
#include "parsers/CiffParserException.h" // for CiffParserException (ptr only)
#include "tiff/CiffEntry.h"              // for CiffEntry
#include "tiff/CiffIFD.h"                // for CiffIFD
#include "tiff/CiffTag.h"                // for CiffTag::CIFF_MAKEMODEL
#include <memory>                        // for unique_ptr, default_delete
#include <string>                        // for operator==, basic_string
#include <utility>                       // for move, pair

using std::string;

namespace rawspeed {

CiffParser::CiffParser(const Buffer* inputData) : RawParser(inputData) {}

void CiffParser::parseData() {
  ByteStream bs(*mInput, 0);
  bs.setByteOrder(Endianness::little);

  ushort16 magic = bs.getU16();
  if (magic != 0x4949)
    ThrowCPE("Not a CIFF file (ID)");

  NORangesSet<Buffer> ifds;

  // Offset to the beginning of the CIFF
  ByteStream subStream(bs.getSubStream(bs.getByte()));
  mRootIFD = std::make_unique<CiffIFD>(nullptr, &ifds, &subStream);
}

std::unique_ptr<RawDecoder> CiffParser::getDecoder(const CameraMetaData* meta) {
  if (!mRootIFD)
    parseData();

  const auto potentials(mRootIFD->getIFDsWithTag(CIFF_MAKEMODEL));

  for (const auto& potential : potentials) {
    const auto mm = potential->getEntry(CIFF_MAKEMODEL);
    const string make = trimSpaces(mm->getString());

    if (make == "Canon")
      return std::make_unique<CrwDecoder>(move(mRootIFD), mInput);
  }

  ThrowCPE("No decoder found. Sorry.");
}

} // namespace rawspeed
