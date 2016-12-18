#ifndef BYTE_STREAM_SWAP_H
#define BYTE_STREAM_SWAP_H

#include "ByteStream.h"

#include "IOException.h"

namespace RawSpeed {

class ByteStreamSwap :
  public ByteStream
{
public:
  ByteStreamSwap(byte[] _buffer, UInt32 _size);
  ByteStreamSwap(ByteStreamSwap* b);
  ByteStreamSwap(FileMap *f, UInt32 offset, UInt32 count);
  ByteStreamSwap(FileMap *f, UInt32 offset);
  virtual UInt16 getShort();
  virtual int getInt();
  virtual ~ByteStreamSwap(void);
  virtual UInt32 getUInt();
};

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "ByteStreamSwap.h"

namespace RawSpeed {


	ByteStreamSwap::ByteStreamSwap(byte[] _buffer, UInt32 _size) :
		ByteStream(_buffer, _size)
	{}

	ByteStreamSwap::ByteStreamSwap(ByteStreamSwap* b) :
		ByteStream(b)
	{}

	ByteStreamSwap::ByteStreamSwap(FileMap *f, UInt32 offset, UInt32 _size) :
		ByteStream(f, offset, _size)
	{}

	ByteStreamSwap::ByteStreamSwap(FileMap *f, UInt32 offset) :
		ByteStream(f, offset)
	{}

	ByteStreamSwap::~ByteStreamSwap(void)
	{
	}

	UInt16 ByteStreamSwap::getShort() {
		if (off + 1 >= size)
			throw IOException("getShort: Out of buffer read");
		UInt32 a = buffer[off++];
		UInt32 b = buffer[off++];
		return (UInt16)((a << 8) | b);
	}

	/* NOTE: Actually unused, so not tested */
	int ByteStreamSwap::getInt() {
		if (off + 4 >= size)
			throw IOException("getInt: Out of buffer read");
		int r = (int)buffer[off] << 24 | (int)buffer[off + 1] << 16 | (int)buffer[off + 2] << 8 | (int)buffer[off + 3];
		off += 4;
		return r;
	}
	UInt32 ByteStreamSwap::getUInt() {
		if (off + 4 >= size)
			throw IOException("getUInt: Out of buffer read");
		UInt32 r = (UInt32)buffer[off] << 24 | (UInt32)buffer[off + 1] << 16 | (UInt32)buffer[off + 2] << 8 | (UInt32)buffer[off + 3];
		off += 4;
		return r;
	}

} // namespace RawSpeed
