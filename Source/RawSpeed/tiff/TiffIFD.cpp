/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post
    Copyright (C) 2015 Pedro Côrte-Real
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

#include "tiff/TiffIFD.h"
#include "common/Common.h"            // for getHostEndianness, uint32, make_...
#include "common/NORangesSet.h"       // for NORangesSet
#include "common/RawspeedException.h" // for RawspeedException
#include "io/IOException.h"           // for IOException
#include "tiff/TiffEntry.h"           // for TiffEntry
#include "tiff/TiffTag.h"             // for TiffTag, ::DNGPRIVATEDATA, ::EXI...
#include <algorithm>                  // for move
#include <cstdint>                    // for UINT32_MAX
#include <map>                        // for map, _Rb_tree_const_iterator, al...
#include <memory>                     // for default_delete, unique_ptr
#include <string>                     // for operator==, string, basic_string
#include <utility>                    // for pair
#include <vector>                     // for vector

using std::string;
using std::vector;

namespace rawspeed {

void TiffIFD::parseIFDEntry(NORangesSet<Buffer>* ifds, ByteStream* bs) {
  TiffEntryOwner t;

  auto origPos = bs->getPosition();

  try {
    t = std::make_unique<TiffEntry>(this, bs);
  } catch (IOException&) { // Ignore unparsable entry
    // fix probably broken position due to interruption by exception
    // i.e. setting it to the next entry.
    bs->setPosition(origPos + 12);
    return;
  }

  try {
    switch (t->tag) {
    case DNGPRIVATEDATA:
      add(parseDngPrivateData(ifds, t.get()));
      break;

    case MAKERNOTE:
    case MAKERNOTE_ALT:
      add(parseMakerNote(ifds, t.get()));
      break;

    case FUJI_RAW_IFD:
    case SUBIFDS:
    case EXIFIFDPOINTER:
      for (uint32 j = 0; j < t->count; j++)
        add(std::make_unique<TiffIFD>(this, ifds, *bs, t->getU32(j)));
      break;

    default:
      add(move(t));
    }
  } catch (RawspeedException&) { // Unparsable private data are added as entries
    add(move(t));
  }
}

TiffIFD::TiffIFD(TiffIFD* parent_) : parent(parent_) {}

TiffIFD::TiffIFD(TiffIFD* parent_, NORangesSet<Buffer>* ifds,
                 const DataBuffer& data, uint32 offset)
    : TiffIFD(parent_) {

  // see TiffParser::parse: UINT32_MAX is used to mark the "virtual" top level
  // TiffRootIFD in a tiff file
  if (offset == UINT32_MAX)
    return;

  checkOverflow();

  ByteStream bs(data);
  bs.setPosition(offset);

  // Directory entries in this IFD
  auto numEntries = bs.getU16();

  // 2 bytes for entry count
  // each entry is 12 bytes
  // 4-byte offset to the next IFD at the end
  const auto IFDFullSize = 2 + 4 + 12 * numEntries;
  const Buffer IFDBuf(data.getSubView(offset, IFDFullSize));
  if (ifds && !ifds->emplace(IFDBuf).second)
    ThrowTPE("Two IFD's overlap. Raw corrupt!");

  for (uint32 i = 0; i < numEntries; i++)
    parseIFDEntry(ifds, &bs);

  nextIFD = bs.getU32();
}

TiffRootIFDOwner TiffIFD::parseDngPrivateData(NORangesSet<Buffer>* ifds,
                                              TiffEntry* t) {
  /*
  1. Six bytes containing the zero-terminated string "Adobe". (The DNG specification calls for the DNGPrivateData tag to start with an ASCII string identifying the creator/format).
  2. 4 bytes: an ASCII string ("MakN" for a Makernote),  indicating what sort of data is being stored here. Note that this is not zero-terminated.
  3. A four-byte count (number of data bytes following); this is the length of the original MakerNote data. (This is always in "most significant byte first" format).
  4. 2 bytes: the byte-order indicator from the original file (the usual 'MM'/4D4D or 'II'/4949).
  5. 4 bytes: the original file offset for the MakerNote tag data (stored according to the byte order given above).
  6. The contents of the MakerNote tag. This is a simple byte-for-byte copy, with no modification.
  */
  ByteStream& bs = t->getData();
  if (!bs.skipPrefix("Adobe", 6))
    ThrowTPE("Not Adobe Private data");

  if (!bs.skipPrefix("MakN", 4))
    ThrowTPE("Not Makernote");

  bs.setByteOrder(Endianness::big);
  uint32 makerNoteSize = bs.getU32();
  if (makerNoteSize != bs.getRemainSize())
    ThrowTPE("Error reading TIFF structure (invalid size). File Corrupt");

  bs.setByteOrder(getTiffByteOrder(bs, 0, "DNG makernote"));
  bs.skipBytes(2);

  uint32 makerNoteOffset = bs.getU32();
  makerNoteSize -= 6; // update size of orinial maker note, we skipped 2+4 bytes

  // Update the underlying buffer of t, such that the maker note data starts at its original offset
  bs.rebase(makerNoteOffset, makerNoteSize);

  return parseMakerNote(ifds, t);
}

/* This will attempt to parse makernotes and return it as an IFD */
TiffRootIFDOwner TiffIFD::parseMakerNote(NORangesSet<Buffer>* ifds,
                                         TiffEntry* t) {
  // go up the IFD tree and try to find the MAKE entry on each level.
  // we can not go all the way to the top first because this partial tree
  // is not yet added to the TiffRootIFD.
  TiffIFD* p = this;
  TiffEntry* makeEntry;
  do {
    makeEntry = p->getEntryRecursive(MAKE);
    p = p->parent;
  } while (!makeEntry && p);
  string make = makeEntry != nullptr ? trimSpaces(makeEntry->getString()) : "";

  ByteStream bs = t->getData();

  // helper function for easy setup of ByteStream buffer for the different maker note types
  // 'rebase' means position 0 of new stream equals current position
  // 'newPosition' is the position where the IFD starts
  // 'byteOrderOffset' is the position wher the 2 magic bytes (II/MM) may be found
  // 'context' is a string providing error information in case the byte order parsing should fail
  auto setup = [&bs](bool rebase, uint32 newPosition,
                     uint32 byteOrderOffset = 0,
                     const char *context = nullptr) {
    if (rebase)
      bs = bs.getSubStream(bs.getPosition(), bs.getRemainSize());
    if (context)
      bs.setByteOrder(getTiffByteOrder(bs, byteOrderOffset, context));
    bs.skipBytes(newPosition);
  };

  if (bs.hasPrefix("AOC\0", 4)) {
    setup(false, 6, 4, "Pentax makernote");
  } else if (bs.hasPrefix("PENTAX", 6)) {
    setup(true, 10, 8, "Pentax makernote");
  } else if (bs.hasPrefix("FUJIFILM\x0c\x00\x00\x00", 12)) {
    bs.setByteOrder(Endianness::little);
    setup(true, 12);
  } else if (bs.hasPrefix("Nikon\x00\x02", 7)) {
    // this is Nikon type 3 maker note format
    // TODO: implement Nikon type 1 maker note format
    // see http://www.ozhiker.com/electronics/pjmt/jpeg_info/nikon_mn.html
    bs.skipBytes(10);
    setup(true, 8, 0, "Nikon makernote");
  } else if (bs.hasPrefix("OLYMPUS", 7)) { // new Olympus
    setup(true, 12);
  } else if (bs.hasPrefix("OLYMP", 5)) {   // old Olympus
    setup(true, 8);
  } else if (bs.hasPrefix("EPSON", 5)) {
    setup(false, 8);
  } else if (bs.hasPatternAt("Exif", 4, 6)) {
    // TODO: for none of the rawsamples.ch files from Panasonic is this true, instead their MakerNote start with "Panasonic"
    // Panasonic has the word Exif at byte 6, a complete Tiff header starts at byte 12
    // This TIFF is 0 offset based
    setup(false, 20, 12, "Panosonic makernote");
  } else if (make == "SAMSUNG") {
    // Samsung has no identification in its MakerNote but starts with the IFD right away
    setup(true, 0);
  } else {
    // cerr << "default MakerNote from " << make << endl; // Canon, Nikon (type 2), Sony, Minolta, Ricoh, Leica, Hasselblad, etc.

    // At least one MAKE has not been handled explicitly and starts its MakerNote with an endian prefix: Kodak
    if (bs.skipPrefix("II", 2)) {
      bs.setByteOrder(Endianness::little);
    } else if (bs.skipPrefix("MM", 2)) {
      bs.setByteOrder(Endianness::big);
    }
  }

  // Attempt to parse the rest as an IFD
  return std::make_unique<TiffRootIFD>(this, ifds, bs, bs.getPosition());
}

std::vector<const TiffIFD*> TiffIFD::getIFDsWithTag(TiffTag tag) const {
  vector<const TiffIFD*> matchingIFDs;
  if (entries.find(tag) != entries.end()) {
    matchingIFDs.push_back(this);
  }
  for (auto& i : subIFDs) {
    vector<const TiffIFD*> t = i->getIFDsWithTag(tag);
    matchingIFDs.insert(matchingIFDs.end(), t.begin(), t.end());
  }
  return matchingIFDs;
}

const TiffIFD* TiffIFD::getIFDWithTag(TiffTag tag, uint32 index) const
{
  auto ifds = getIFDsWithTag(tag);
  if (index >= ifds.size())
    ThrowTPE("failed to find %u ifs with tag 0x%04x", index + 1, tag);
  return ifds[index];
}

TiffEntry* __attribute__((pure)) TiffIFD::getEntryRecursive(TiffTag tag) const {
  auto i = entries.find(tag);
  if (i != entries.end()) {
    return i->second.get();
  }
  for (auto &j : subIFDs) {
    TiffEntry *entry = j->getEntryRecursive(tag);
    if (entry)
      return entry;
  }
  return nullptr;
}

void TiffIFD::checkOverflow() {
  TiffIFD* p = this;
  int i = 0;
  while ((p = p->parent) != nullptr) {
    i++;
    if (i > 5)
      ThrowTPE("TiffIFD cascading overflow.");
  }
}

void TiffIFD::add(TiffIFDOwner subIFD) {
  checkOverflow();
  if (subIFDs.size() > 100)
    ThrowTPE("TIFF file has too many SubIFDs, probably broken");
  subIFD->parent = this;
  subIFDs.push_back(move(subIFD));
}

void TiffIFD::add(TiffEntryOwner entry) {
  entry->parent = this;
  entries[entry->tag] = move(entry);
}

TiffEntry* TiffIFD::getEntry(TiffTag tag) const {
  auto i = entries.find(tag);
  if (i == entries.end())
    ThrowTPE("Entry 0x%x not found.", tag);
  return i->second.get();
}

TiffID TiffRootIFD::getID() const
{
  TiffID id;
  auto makeE = getEntryRecursive(MAKE);
  auto modelE = getEntryRecursive(MODEL);

  if (!makeE)
    ThrowTPE("Failed to find MAKE entry.");
  if (!modelE)
    ThrowTPE("Failed to find MODEL entry.");

  id.make = trimSpaces(makeE->getString());
  id.model = trimSpaces(modelE->getString());

  return id;
}

} // namespace rawspeed
