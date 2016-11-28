#ifndef CIFF_IFD_H
#define CIFF_IFD_H

#include "FileMap.h"
#include "CiffEntry.h"
#include "CiffParserException.h"

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

#define CIFF_DEPTH(_depth) if((depth=_depth+1) > 10) ThrowCPE("CIFF: sub-micron matryoshka dolls are ignored");

class CiffIFD
{
public:
  CiffIFD(FileMap* f, UInt32 start, UInt32 end, UInt32 depth=0);
  virtual ~CiffIFD(void);
  vector<CiffIFD*> mSubIFD;
  map<CiffTag, CiffEntry*> mEntry;
  vector<CiffIFD*> getIFDsWithTag(CiffTag tag);
  CiffEntry* getEntry(CiffTag tag);
  bool hasEntry(CiffTag tag);
  bool hasEntryRecursive(CiffTag tag);
  CiffEntry* getEntryRecursive(CiffTag tag);
  CiffEntry* getEntryRecursiveWhere(CiffTag tag, UInt32 isValue);
  CiffEntry* getEntryRecursiveWhere(CiffTag tag, string isValue);
  vector<CiffIFD*> getIFDsWithTagWhere(CiffTag tag, string isValue);
  vector<CiffIFD*> getIFDsWithTagWhere(CiffTag tag, UInt32 isValue);
  FileMap* getFileMap() {return mFile;};
protected:
  FileMap *mFile;
  UInt32 depth;
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "CiffIFD.h"
#include "CiffParser.h"
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

	CiffIFD::CiffIFD(FileMap* f, UInt32 start, UInt32 end, UInt32 _depth) {
		CIFF_DEPTH(_depth);
		mFile = f;

		UInt32 valuedata_size = get4LE(f.getData(end - 4, 4), 0);
		UInt16 dircount = get2LE(f.getData(start + valuedata_size, 2), 0);

		//  fprintf(stderr, "Found %d entries between %d and %d after %d data bytes\n", 
		//                  dircount, start, end, valuedata_size);

		for (UInt32 i = 0; i < dircount; i++) {
			int entry_offset = start + valuedata_size + 2 + i * 10;

			// If the space for the entry is no longer valid stop reading any more as
			// the file is broken or truncated
			if (!mFile.isValid(entry_offset, 10))
				break;

			CiffEntry *t = null;
			try {
				t = new CiffEntry(f, start, entry_offset);
			}
			catch (IOException) { // Ignore unparsable entry
				continue;
			}

			if (t.type == CIFF_SUB1 || t.type == CIFF_SUB2) {
				try {
					mSubIFD.push_back(new CiffIFD(f, t.data_offset, t.data_offset + t.bytesize, depth));
					delete(t);
				}
				catch (CiffParserException) { // Unparsable subifds are added as entries
					mEntry[t.tag] = t;
				}
				catch (IOException) { // Unparsable private data are added as entries
					mEntry[t.tag] = t;
				}
			}
			else {
				mEntry[t.tag] = t;
			}
		}
	}


	CiffIFD::~CiffIFD(void) {
		for (map<CiffTag, CiffEntry*>::iterator i = mEntry.begin(); i != mEntry.end(); ++i) {
			delete((*i).second);
		}
		mEntry.clear();
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			delete(*i);
		}
		mSubIFD.clear();
	}

	bool CiffIFD::hasEntryRecursive(CiffTag tag) {
		if (mEntry.find(tag) != mEntry.end())
			return true;
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			if ((*i).hasEntryRecursive(tag))
				return true;
		}
		return false;
	}

	vector<CiffIFD*> CiffIFD::getIFDsWithTag(CiffTag tag) {
		vector<CiffIFD*> matchingIFDs;
		if (mEntry.find(tag) != mEntry.end()) {
			matchingIFDs.push_back(this);
		}
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			vector<CiffIFD*> t = (*i).getIFDsWithTag(tag);
			for (UInt32 j = 0; j < t.size(); j++) {
				matchingIFDs.push_back(t[j]);
			}
		}
		return matchingIFDs;
	}

	vector<CiffIFD*> CiffIFD::getIFDsWithTagWhere(CiffTag tag, UInt32 isValue) {
		vector<CiffIFD*> matchingIFDs;
		if (mEntry.find(tag) != mEntry.end()) {
			CiffEntry* entry = mEntry[tag];
			if (entry.isInt() && entry.getInt() == isValue)
				matchingIFDs.push_back(this);
		}
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			vector<CiffIFD*> t = (*i).getIFDsWithTag(tag);
			for (UInt32 j = 0; j < t.size(); j++) {
				matchingIFDs.push_back(t[j]);
			}
		}
		return matchingIFDs;
	}

	vector<CiffIFD*> CiffIFD::getIFDsWithTagWhere(CiffTag tag, string isValue) {
		vector<CiffIFD*> matchingIFDs;
		if (mEntry.find(tag) != mEntry.end()) {
			CiffEntry* entry = mEntry[tag];
			if (entry.isString() && 0 == entry.getString().compare(isValue))
				matchingIFDs.push_back(this);
		}
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			vector<CiffIFD*> t = (*i).getIFDsWithTag(tag);
			for (UInt32 j = 0; j < t.size(); j++) {
				matchingIFDs.push_back(t[j]);
			}
		}
		return matchingIFDs;
	}

	CiffEntry* CiffIFD::getEntryRecursive(CiffTag tag) {
		if (mEntry.find(tag) != mEntry.end()) {
			return mEntry[tag];
		}
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			CiffEntry* entry = (*i).getEntryRecursive(tag);
			if (entry)
				return entry;
		}
		return null;
	}

	CiffEntry* CiffIFD::getEntryRecursiveWhere(CiffTag tag, UInt32 isValue) {
		if (mEntry.find(tag) != mEntry.end()) {
			CiffEntry* entry = mEntry[tag];
			if (entry.isInt() && entry.getInt() == isValue)
				return entry;
		}
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			CiffEntry* entry = (*i).getEntryRecursive(tag);
			if (entry)
				return entry;
		}
		return null;
	}

	CiffEntry* CiffIFD::getEntryRecursiveWhere(CiffTag tag, string isValue) {
		if (mEntry.find(tag) != mEntry.end()) {
			CiffEntry* entry = mEntry[tag];
			if (entry.isString() && 0 == entry.getString().compare(isValue))
				return entry;
		}
		for (vector<CiffIFD*>::iterator i = mSubIFD.begin(); i != mSubIFD.end(); ++i) {
			CiffEntry* entry = (*i).getEntryRecursive(tag);
			if (entry)
				return entry;
		}
		return null;
	}

	CiffEntry* CiffIFD::getEntry(CiffTag tag) {
		if (mEntry.find(tag) != mEntry.end()) {
			return mEntry[tag];
		}
		ThrowCPE("CiffIFD: CIFF Parser entry 0x%x not found.", tag);
		return 0;
	}


	bool CiffIFD::hasEntry(CiffTag tag) {
		return mEntry.find(tag) != mEntry.end();
	}

} // namespace RawSpeed
