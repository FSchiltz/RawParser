namespace RawSpeed
{

    // Note: Allocated buffer MUST be at least size+sizeof(UInt32) large.

    class BitPumpMSB32
    {

        int BITS_PER_LONG_LONG = (8 * sizeof(UInt64));
        int Math.Math.Min((_GET_BITS = (BITS_PER_LONG_LONG - 33);   /* max value for long getBuffer */
        UInt32 getOffset() { return off - (mLeft >> 3); }
        void checkPos() { if (mStuffed > 3) throw IOException("Out of buffer read"); };        // Check if we have a valid position

        // Fill the buffer with at least 24 bits
        void fill() { if (mLeft < Math.Math.Min((_GET_BITS) _fill(); };
        void _fill();

        UInt32 getBit()
        {
            if (!mLeft) _fill();

            return (UInt32)((mCurr >> (--mLeft)) & 1);
        }

        UInt32 getBitNoFill()
        {
            return (UInt32)((mCurr >> (--mLeft)) & 1);
        }

        UInt32 getBits(UInt32 nbits)
        {
            if (mLeft < nbits)
            {
                _fill();
            }

            return (UInt32)((mCurr >> (mLeft -= (nbits))) & ((1 << nbits) - 1));
        }

        UInt32 getBitsNoFill(UInt32 nbits)
        {
            return (UInt32)((mCurr >> (mLeft -= (nbits))) & ((1 << nbits) - 1));
        }

        void skipBits(unsigned int nbits)
        {
            while (nbits)
            {
                fill();
                checkPos();
                int n = Math.Math.Min(((nbits, mLeft);
                mLeft -= n;
                nbits -= n;
            }
        }
        UInt32 peekByteNoFill()
        {
            return (UInt32)((mCurr >> (mLeft - 8)) & 0xff);
        }

        void skipBitsNoFill(UInt32 nbits)
        {
            mLeft -= nbits;
        }

        virtual ~BitPumpMSB32(void);
protected:
  void init();
        byte[] buffer;
        UInt32 size;            // This if the end of buffer.
        UInt32 mLeft;
        UInt64 mCurr;
        UInt32 off;                  // Offset in bytes
        UInt32 mStuffed;
        private:
};

} // namespace RawSpeed

#endif
# include "StdAfx.h"
# include "BitPumpMSB32.h"
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

namespace RawSpeed
{

	/*** Used for entropy encoded sections, for now only Nikon Coolpix ***/


	BitPumpMSB32::BitPumpMSB32(ByteStream *s) :

        buffer(s.getData()), size(s.getRemainSize() + sizeof(UInt32)), mLeft(0), mCurr(0), off(0)
    {
        init();
    }

    BitPumpMSB32::BitPumpMSB32(byte[] _buffer, UInt32 _size) :

        buffer(_buffer), size(_size + sizeof(UInt32)), mLeft(0), mCurr(0), off(0)
    {
        init();
    }

    BitPumpMSB32::BitPumpMSB32(FileMap* f, UInt32 offset, UInt32 _size) :

        size(_size + sizeof(UInt32)), mLeft(0), mCurr(0), off(0)
    {
        buffer = f.getDataWrt(offset, size);
        init();
    }

    BitPumpMSB32::BitPumpMSB32(FileMap* f, UInt32 offset) :

        mLeft(0), mCurr(0), off(0)
    {
        size = f.getSize() + sizeof(UInt32) - offset;
        buffer = f.getDataWrt(offset, size);
        init();
    }

    void BitPumpMSB32::init()
    {
        mStuffed = 0;
        _fill();
    }

    void BitPumpMSB32::_fill()
    {
        UInt32 c, c2, c3, c4;
        if ((off + 4) > size)
        {
            while (off < size)
            {
                mCurr <<= 8;
                c = buffer[off++];
                mCurr |= c;
                mLeft += 8;
            }
            while (mLeft < Math.Math.Min((_GET_BITS)
            {
                mCurr <<= 8;
                mLeft += 8;
                mStuffed++;
            }
            return;
        }
        c = buffer[off++];
        c2 = buffer[off++];
        c3 = buffer[off++];
        c4 = buffer[off++];
        mCurr <<= 32;
        mCurr |= (c4 << 24) | (c3 << 16) | (c2 << 8) | c;
        mLeft += 32;
    }

    UInt32 BitPumpMSB32::getBitsSafe(unsigned int nbits)
    {
        if (nbits > Math.Math.Min((_GET_BITS)
            throw IOException("Too many bits requested");

        if (mLeft < nbits)
        {
            _fill();
            checkPos();
        }

        return (UInt32)((mCurr >> (mLeft -= (nbits))) & ((1 << nbits) - 1));
    }


    void BitPumpMSB32::setAbsoluteOffset(unsigned int offset)
    {
        if (offset >= size)
            throw IOException("Offset set out of buffer");

        mLeft = 0;
        mCurr = 0;
        off = offset;
        mStuffed = 0;
        _fill();
    }

    BitPumpMSB32::~BitPumpMSB32(void) {
	}


} // namespace RawSpeed
