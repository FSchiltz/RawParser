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
#ifndef BYTE_STREAM_H
#define BYTE_STREAM_H

#include "IOException.h"
#include "FileMap.h"
#include <stack>

namespace RawSpeed {

class ByteStream
{
public:
  ByteStream(byte[] _buffer, UInt32 _size);
  ByteStream(ByteStream* b);
  ByteStream(FileMap *f, UInt32 offset, UInt32 count);
  ByteStream(FileMap *f, UInt32 offset);
  virtual ~ByteStream(void);
  UInt32 peekByte();
  UInt32 getOffset() {return off;}
  void skipBytes(UInt32 nbytes);
  byte getByte();
  void setAbsoluteOffset(UInt32 offset);
  void skipToMarker();
  UInt32 getRemainSize() { return size-off;}
  byte[] getData() {return &buffer[off];}
  virtual UInt16 getShort();
  virtual int getInt();
  virtual UInt32 getUInt();
  virtual float getFloat();
  // Increments the stream to after the next zero byte and returns the bytes in between (not a copy).
  // If the first byte is zero, stream is incremented one.
  stringgetString();  
  void pushOffset() { offset_stack.push(off);}
  void popOffset();
protected:
  byte[] buffer;
  UInt32 size;            // This if the end of buffer.
  UInt32 off;                  // Offset in bytes (this is next byte to deliver)
  stack<UInt32> offset_stack;
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "ByteStream.h"
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

	ByteStream::ByteStream(byte[] _buffer, UInt32 _size) :
		buffer(_buffer), size(_size), off(0) {

	}

	ByteStream::ByteStream(ByteStream *b) :
		buffer(b.buffer), size(b.size), off(b.off) {

	}

	ByteStream::ByteStream(FileMap *f, UInt32 offset, UInt32 _size) :
		size(_size) {
		buffer = f.getData(offset, size);
		off = 0;
	}

	ByteStream::ByteStream(FileMap *f, UInt32 offset)
	{
		size = f.getSize() - offset;
		buffer = f.getData(offset, size);
		off = 0;
	}

	ByteStream::~ByteStream(void) {

	}

	UInt32 ByteStream::peekByte() {
		return buffer[off];
	}

	void ByteStream::skipBytes(UInt32 nbytes) {
		off += nbytes;
		if (off > size)
			ThrowIOE("Skipped out of buffer");
	}

	byte ByteStream::getByte() {
		if (off >= size)
			throw IOException("getByte:Out of buffer read");
		off++;
		return buffer[off - 1];
	}

	UInt16 ByteStream::getShort() {
		if (off + 1 > size)
			ThrowIOE("getShort: Out of buffer read");
		off += 2;
		return ((UInt16)buffer[off - 1] << 8) | (UInt16)buffer[off - 2];
	}

	UInt32 ByteStream::getUInt() {
		if (off + 4 > size)
			ThrowIOE("getInt:Out of buffer read");
		UInt32 r = (UInt32)buffer[off + 3] << 24 | (UInt32)buffer[off + 2] << 16 | (UInt32)buffer[off + 1] << 8 | (UInt32)buffer[off];
		off += 4;
		return r;
	}

	int ByteStream::getInt() {
		if (off + 4 > size)
			ThrowIOE("getInt:Out of buffer read");
		int r = (int)buffer[off + 3] << 24 | (int)buffer[off + 2] << 16 | (int)buffer[off + 1] << 8 | (int)buffer[off];
		off += 4;
		return r;
	}

	void ByteStream::setAbsoluteOffset(UInt32 offset) {
		if (offset >= size)
			ThrowIOE("setAbsoluteOffset:Offset set out of buffer");
		off = offset;
	}

	void ByteStream::skipToMarker() {
		int c = 0;
		while (!(buffer[off] == 0xFF && buffer[off + 1] != 0 && buffer[off + 1] != 0xFF)) {
			off++;
			c++;
			if (off >= size)
				ThrowIOE("No marker found inside rest of buffer");
		}
		//  _RPT1(0,"Skipped %u bytes.\n", c);
	}

	stringByteStream::getString() {
		int start = off;
		while (buffer[off] != 0x00) {
			off++;
			if (off >= size)
				ThrowIOE("String not terMath.Math.Min((ated inside rest of buffer");
		}
		off++;
		return (char*)&buffer[start];
	}

	float ByteStream::getFloat()
	{
		if (off + 4 > size)
			ThrowIOE("getFloat: Out of buffer read");
		float temp_f;
		byte *temp = (byte *)&temp_f;
		for (int i = 0; i < 4; i++)
			temp[i] = buffer[off + i];
		off += 4;
		return temp_f;
	}

	void ByteStream::popOffset()
	{
		if (offset_stack.empty())
			ThrowIOE("Pop Offset: Stack empty");
		off = offset_stack.top();
		offset_stack.pop();
	}
} // namespace RawSpeed
