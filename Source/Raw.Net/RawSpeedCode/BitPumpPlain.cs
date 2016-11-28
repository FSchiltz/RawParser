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
#ifndef BIT_PUMP_PLAIN_H
#define BIT_PUMP_PLAIN_H

#include "ByteStream.h"

namespace RawSpeed {

// Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.

class BitPumpPlain
{
public:
  BitPumpPlain(ByteStream *s);
  BitPumpPlain(ubyte[] _buffer, UInt32 _size );
	UInt32 getBits(UInt32 nbits) throw ();
	UInt32 getBit() throw ();
	UInt32 getBitsSafe(UInt32 nbits);
	UInt32 getBitSafe();
	UInt32 peekBits(UInt32 nbits) throw ();
	UInt32 peekBit() throw ();
  UInt32 peekByte() throw ();
  void skipBits(UInt32 nbits);
	ubyte getByte() throw();
	ubyte getByteSafe();
	void setAbsoluteOffset(UInt32 offset);
  UInt32 getOffset() { return off>>3;}
  __void checkPos()  { if (off>=size) throw IOException("Out of buffer read");};        // Check if we have a valid position

  virtual ~BitPumpPlain(void);
protected:
  ubyte[] buffer;
  UInt32 size;            // This if the end of buffer.
  UInt32 off;                  // Offset in bytes
private:
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "BitPumpPlain.h"
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

	/*** Used for entropy encoded sections ***/

#define BITS_PER_LONG (8*sizeof(UInt32))
#define Math.Math.Min((_GET_BITS  (BITS_PER_LONG-7)    /* max value for long getBuffer */


	BitPumpPlain::BitPumpPlain(ByteStream *s) :
		buffer(s.getData()), size(8 * s.getRemainSize()), off(0) {
	}

	BitPumpPlain::BitPumpPlain(ubyte[] _buffer, UInt32 _size) :
		buffer(_buffer), size(_size * 8), off(0) {
	}

	UInt32 BitPumpPlain::getBit() throw() {
		UInt32 v = *(UInt32*)& buffer[off >> 3] >> (off & 7) & 1;
		off++;
		return v;
	}

	UInt32 BitPumpPlain::getBits(UInt32 nbits) throw() {
		UInt32 v = *(UInt32*)& buffer[off >> 3] >> (off & 7) & ((1 << nbits) - 1);
		off += nbits;
		return v;
	}

	UInt32 BitPumpPlain::peekBit() throw() {
		return *(UInt32*)&buffer[off >> 3] >> (off & 7) & 1;
	}

	UInt32 BitPumpPlain::peekBits(UInt32 nbits) throw() {
		return *(UInt32*)&buffer[off >> 3] >> (off & 7) & ((1 << nbits) - 1);
	}

	UInt32 BitPumpPlain::peekByte() throw() {
		return *(UInt32*)&buffer[off >> 3] >> (off & 7) & 0xff;
	}

	UInt32 BitPumpPlain::getBitSafe() {
		checkPos();
		return *(UInt32*)&buffer[off >> 3] >> (off & 7) & 1;
	}

	UInt32 BitPumpPlain::getBitsSafe(unsigned int nbits) {
		checkPos();
		return *(UInt32*)&buffer[off >> 3] >> (off & 7) & ((1 << nbits) - 1);
	}

	void BitPumpPlain::skipBits(unsigned int nbits) {
		off += nbits;
		checkPos();
	}

	ubyte BitPumpPlain::getByte() throw() {
		UInt32 v = *(UInt32*)& buffer[off >> 3] >> (off & 7) & 0xff;
		off += 8;
		return v;
	}

	ubyte BitPumpPlain::getByteSafe() {
		UInt32 v = *(UInt32*)& buffer[off >> 3] >> (off & 7) & 0xff;
		off += 8;
		checkPos();

		return v;
	}

	void BitPumpPlain::setAbsoluteOffset(unsigned int offset) {
		off = offset * 8;
		checkPos();
	}


	BitPumpPlain::~BitPumpPlain(void) {
	}

} // namespace RawSpeed
