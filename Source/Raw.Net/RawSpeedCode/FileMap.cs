namespace RawSpeed
{

    /*************************************************************************
     * This is the basic file map
     *
     * It allows access to a file.
     * Base implementation is for a complete file that is already in memory.
     * This can also be done as a MemMap 
     * 
     *****************************/
    public class FileMap
    {

        int FILEMAP_MARGIN = 16;

        // Allocates the data array itself
        FileMap(UInt32 _size);
        // Data already allocated, if possible allocate 16 extra bytes.
        FileMap(byte[] _data, UInt32 _size);
        // A subset reusing the same data and starting at offset
        FileMap(FileMap* f, UInt32 offset);
        // A subset reusing the same data and starting at offset, with size bytes
        FileMap(FileMap* f, UInt32 offset, UInt32 size);
        ~FileMap(void);
  byte[] getData(UInt32 offset, UInt32 count);
        public byte[] getDataWrt(UInt32 offset, UInt32 count) { return (byte*)getData(offset, count); }
        UInt32 getSize() { return size; }
        bool isValid(UInt32 offset) { return offset < size; }
        bool isValid(UInt32 offset, UInt32 count);
        FileMap* clone();
        /* For testing purposes */
        void corrupt(int errors);
        FileMap* cloneRandomSize();
        private:
 byte[] data;
        UInt32 size;
        bool mOwnAlloc;
    };

} // namespace RawSpeed

#endif
#include "StdAfx.h"
#include "FileMap.h"
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

FileMap::FileMap(UInt32 _size) : size(_size)
    {
        if (!size)
            throw FileIOException("Filemap of 0 bytes not possible");
        data = (byte8*)_aligned_malloc(size + FILEMAP_MARGIN, 16);
        if (!data)
        {
            throw FileIOException("Not enough memory to open file.");
        }
        mOwnAlloc = true;
    }

    FileMap::FileMap(byte[] _data, UInt32 _size): data(_data), size(_size)
    {
        mOwnAlloc = false;
    }

    FileMap::FileMap(FileMap* f, UInt32 offset) {
  size = f.getSize()-offset;
  data = f.getDataWrt(offset, size+FILEMAP_MARGIN);
  mOwnAlloc = false;
}

FileMap::FileMap(FileMap* f, UInt32 offset, UInt32 size) {
  data = f.getDataWrt(offset, size+FILEMAP_MARGIN);
  mOwnAlloc = false;
}


FileMap* FileMap::clone()
{
    FileMap* new_map = new FileMap(size);
    memcpy(new_map.data, data, size);
    return new_map;
}

FileMap* FileMap::cloneRandomSize()
{
    UInt32 new_size = (rand() | (rand() << 15)) % size;
    FileMap* new_map = new FileMap(new_size);
    memcpy(new_map.data, data, new_size);
    return new_map;
}

void FileMap::corrupt(int errors)
{
    for (int i = 0; i < errors; i++)
    {
        UInt32 pos = (rand() | (rand() << 15)) % size;
        data[pos] = rand() & 0xff;
    }
}

bool FileMap::isValid(UInt32 offset, UInt32 count)
{
    UInt64 totaloffset = (UInt64)offset + (UInt64)count - 1;
    return (isValid(offset) && totaloffset < size);
}

byte[] FileMap::getData(UInt32 offset, UInt32 count)
{
    if (count == 0)
        throw IOException("FileMap: Trying to get a zero sized buffer?!");

    UInt64 totaloffset = (UInt64)offset + (UInt64)count - 1;
    UInt64 totalsize = (UInt64)size + FILEMAP_MARGIN;

    // Give out data up to FILEMAP_MARGIN more bytes than are really in the
    // file as that is useful for some of the BitPump code
    if (!isValid(offset) || totaloffset >= totalsize)
        throw IOException("FileMap: Attempting to read file out of bounds.");
    return &data[offset];
}

} // namespace RawSpeed
