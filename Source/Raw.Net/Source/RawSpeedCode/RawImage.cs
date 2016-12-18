using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RawSpeed
{

    public enum RawImageType { TYPE_USHORT16, TYPE_FLOAT32 };

    public class RawImageWorker
    {
        public enum RawImageWorkerTask
        {
            SCALE_VALUES = 1, FIX_BAD_PIXELS = 2, APPLY_LOOKUP = 3 | 0x1000, FULL_IMAGE = 0x1000
        };

        RawImageData data;
        RawImageWorkerTask task;
        int start_y;
        int end_y;
    };

    public class TableLookUp
    {
        int ntables;
        public UInt16[] tables;
        public bool dither;
    };


    public class ImageMetaData
    {
        // Aspect ratio of the pixels, usually 1 but some cameras need scaling
        // <1 means the image needs to be stretched vertically, (0.5 means 2x)
        // >1 means the image needs to be stretched horizontally (2 mean 2x)
        double pixelAspectRatio;

        // White balance coefficients of the image
        float[] wbCoeffs = new float[4];

        // How many pixels far down the left edge and far up the right edge the image 
        // corners are when the image is rotated 45 degrees in Fuji rotated sensors.
        UInt32 fujiRotationPos;

        iPoint2D subsampling;
        string make;
        string model;
        string mode;

        string canonical_make;
        string canonical_model;
        string canonical_alias;
        string canonical_id;

        // ISO speed. If known the value is set, otherwise it will be '0'.
        int isoSpeed;

        ImageMetaData()
        {
            subsampling.x = subsampling.y = 1;
            isoSpeed = 0;
            pixelAspectRatio = 1;
            fujiRotationPos = 0;
            wbCoeffs[0] = NAN;
            wbCoeffs[1] = NAN;
            wbCoeffs[2] = NAN;
            wbCoeffs[3] = NAN;
        }
    }

    public class RawImageData
    {
        UInt32 getCpp() { return cpp; }
        UInt32 getBpp() { return bpp; }
        RawImageType getDataType() { return dataType; }
        bool isAllocated() { return !!data; }
        iPoint2D dim;
        UInt32 pitch;
        bool isCFA;
        ColorFilterArray cfa;
        int blackLevel;
        int[] blackLevelSeparate = new int[4];
        int whitePoint;
        List<BlackArea> blackAreas;
        /* Vector containing silent errors that occurred doing decoding, that may have lead to */
        /* an incomplete image. */
        List<String> errors;
        Mutex errMutex;   // Mutex for above
                          /* Vector containing the positions of bad pixels */
                          /* Format is x | (y << 16), so maximum pixel position is 65535 */
        List<UInt32> mBadPixelPositions;    // Positions of zeroes that must be interpolated
        Mutex mBadPixelMutex;   // Mutex for above, must be used if more than 1 thread is accessing vector
        byte[] mBadPixelMap;
        UInt32 mBadPixelMapPitch;
        bool mDitherScale;           // Should upscaling be done with dither to mimize banding?
        ImageMetaData metadata;

        RawImageType dataType;
        UInt32 dataRefCount;
        byte[] data;
        UInt32 cpp;      // Components per pixel
        UInt32 bpp;      // Bytes per pixel.

        Mutex mymutex;
        iPoint2D mOffset;
        iPoint2D uncropped_dim;
        public TableLookUp table;

        public RawImageData()
        {

            dim = new iPoint2D(0, 0);
            isCFA = (true);
            cfa = (new iPoint2D(0, 0));
            blackLevel = (-1);
            whitePoint = (65536);
            blackLevelSeparate[0] = blackLevelSeparate[1] = blackLevelSeparate[2] = blackLevelSeparate[3] = -1;
            pthread_mutex_init(&mymutex, null);
            mBadPixelMap = null;
            pthread_mutex_init(&errMutex, null);
            pthread_mutex_init(&mBadPixelMutex, null);
            mDitherScale = true;
        }

        public RawImageData(iPoint2D _dim, UInt32 _bpc, UInt32 _cpp)
        {

            dim = (_dim); isCFA = (_cpp == 1); cfa = (new iPoint2D(0, 0));
            blackLevel = (-1); whitePoint = (65536);
            cpp = (_cpp); bpp = (_bpc * _cpp);
            blackLevelSeparate[0] = blackLevelSeparate[1] = blackLevelSeparate[2] = blackLevelSeparate[3] = -1;
            mBadPixelMap = null;
            mDitherScale = true;
            createData();
            pthread_mutex_init(&mymutex, null);
            pthread_mutex_init(&errMutex, null);
            pthread_mutex_init(&mBadPixelMutex, null);
        }
    };


    public class RawImageDataU16 : RawImageData
    {
        // setWithLookUp will set a single pixel by using the lookup table if supplied,
        // You must supply the destination where the value should be written, and a pointer to
        // a value that will be used to store a random counter that can be reused between calls.
        // this needs to be inline to speed up tight decompressor loops
        void setWithLookUp(UInt16 value, ref byte[] dst, UInt32[] random)
        {
            UInt16[] dest = (ushort16*)dst;
            if (table == null)
            {
                *dest = value;
                return;
            }
            if (table.dither)
            {
                UInt32[] t = table.tables;
                UInt32 lookup = t[value];
                UInt32 basevalue = lookup & 0xffff;
                UInt32 delta = lookup >> 16;
                UInt32 r = *random;

                UInt32 pix = basevalue + ((delta * (r & 2047) + 1024) >> 12);
                *random = 15700 * (r & 65535) + (r >> 16);
                *dest = pix;
                return;
            }
            UInt16[] t = table.tables;
            dst = t[value];
        }
    };

    public class RawImageDataFloat : RawImageData
    {
    };

    public class RawImage
    {
        RawImage create(RawImageType type)
        {
            switch (type)
            {
                case RawImageDataType.TYPE_USHORT16:
                    return new RawImageDataU16();
                case TYPE_FLOAT32:
                    return new RawImageDataFloat();
                default:
                    Debug.Write("RawImage::create: Unknown Image type!\n");
                    break;
            }
            return null;
        }

        RawImage create(iPoint2D dim, RawImageType type, UInt32 componentsPerPixel)
        {
            switch (type)
            {
                case TYPE_USHORT16:
                    return new RawImageDataU16(dim, componentsPerPixel);
                default:
                    Debug.Write("RawImage::create: Unknown Image type!\n");
                    break;
            }
            return null;
        }
    }
}








    void RawImageData::createData()
    {
        if (dim.x > 65535 || dim.y > 65535)
            ThrowRDE("RawImageData: Dimensions too large for allocation.");
        if (dim.x <= 0 || dim.y <= 0)
            ThrowRDE("RawImageData: Dimension of one sides is less than 1 - cannot allocate image.");
        if (data)
            ThrowRDE("RawImageData: Duplicate data allocation in createData.");
        pitch = (((dim.x * bpp) + 15) / 16) * 16;
        data = (byte8*)_aligned_malloc(pitch * dim.y, 16);
        if (!data)
            ThrowRDE("RawImageData::createData: Memory Allocation failed.");
        uncropped_dim = dim;
    }

    void RawImageData::destroyData()
    {
        if (data)
            _aligned_free(data);
        if (mBadPixelMap)
            _aligned_free(mBadPixelMap);
        data = 0;
        mBadPixelMap = 0;
    }

    void RawImageData::setCpp(UInt32 val)
    {
        if (data)
            ThrowRDE("RawImageData: Attempted to set Components per pixel after data allocation");
        if (val > 4)
            ThrowRDE("RawImageData: Only up to 4 components per pixel is support - attempted to set: %d", val);
        bpp /= cpp;
        cpp = val;
        bpp *= val;
    }

    byte8* RawImageData::getData()
    {
        if (!data)
            ThrowRDE("RawImageData::getData - Data not yet allocated.");
        return &data[mOffset.y * pitch + mOffset.x * bpp];
    }

    byte8* RawImageData::getData(UInt32 x, UInt32 y)
    {
        if ((int)x >= dim.x)
            ThrowRDE("RawImageData::getData - X Position outside image requested.");
        if ((int)y >= dim.y)
        {
            ThrowRDE("RawImageData::getData - Y Position outside image requested.");
        }

        x += mOffset.x;
        y += mOffset.y;

        if (!data)
            ThrowRDE("RawImageData::getData - Data not yet allocated.");

        return &data[y * pitch + x * bpp];
    }

    byte8* RawImageData::getDataUncropped(UInt32 x, UInt32 y)
    {
        if ((int)x >= uncropped_dim.x)
            ThrowRDE("RawImageData::getDataUncropped - X Position outside image requested.");
        if ((int)y >= uncropped_dim.y)
        {
            ThrowRDE("RawImageData::getDataUncropped - Y Position outside image requested.");
        }

        if (!data)
            ThrowRDE("RawImageData::getDataUncropped - Data not yet allocated.");

        return &data[y * pitch + x * bpp];
    }

    iPoint2D RawImageData::getUncroppedDim()
    {
        return uncropped_dim;
    }

    iPoint2D RawImageData::getCropOffset()
    {
        return mOffset;
    }

    void RawImageData::subFrame(iRectangle2D crop)
    {
        if (!crop.dim.isThisInside(dim - crop.pos))
        {
            writeLog(DEBUG_PRIO_WARNING, "WARNING: RawImageData::subFrame - Attempted to create new subframe larger than original size. Crop skipped.\n");
            return;
        }
        if (crop.pos.x < 0 || crop.pos.y < 0 || !crop.hasPositiveArea())
        {
            writeLog(DEBUG_PRIO_WARNING, "WARNING: RawImageData::subFrame - Negative crop offset. Crop skipped.\n");
            return;
        }

        mOffset += crop.pos;
        dim = crop.dim;
    }

    void RawImageData::setError( char* err)
    {
        pthread_mutex_lock(&errMutex);
        errors.push_back(_strdup(err));
        pthread_mutex_unlock(&errMutex);
    }

    void RawImageData::createBadPixelMap()
    {
        if (!isAllocated())
            ThrowRDE("RawImageData::createBadPixelMap: (internal) Bad pixel map cannot be allocated before image.");
        mBadPixelMapPitch = (((uncropped_dim.x / 8) + 15) / 16) * 16;
        mBadPixelMap = (byte8*)_aligned_malloc(mBadPixelMapPitch * uncropped_dim.y, 16);
        memset(mBadPixelMap, 0, mBadPixelMapPitch * uncropped_dim.y);
        if (!mBadPixelMap)
            ThrowRDE("RawImageData::createData: Memory Allocation failed.");
    }

    RawImage::RawImage(RawImageData* p) : p_(p)
    {
        pthread_mutex_lock(&p_.mymutex);
        ++p_.dataRefCount;
        pthread_mutex_unlock(&p_.mymutex);
    }

    RawImage::RawImage(RawImage& p) : p_(p.p_)
    {
        pthread_mutex_lock(&p_.mymutex);
        ++p_.dataRefCount;
        pthread_mutex_unlock(&p_.mymutex);
    }


    void RawImageData::copyErrorsFrom(RawImage other)
    {
        for (UInt32 i = 0; i < other.errors.size(); i++)
        {
            setError(other.errors[i]);
        }
    }

    void RawImageData::transferBadPixelsToMap()
    {
        if (mBadPixelPositions.empty())
            return;

        if (!mBadPixelMap)
            createBadPixelMap();

        for (vector<UInt32>::iterator i = mBadPixelPositions.begin(); i != mBadPixelPositions.end(); ++i)
        {
            UInt32 pos = *i;
            UInt32 pos_x = pos & 0xffff;
            UInt32 pos_y = pos >> 16;
            mBadPixelMap[mBadPixelMapPitch * pos_y + (pos_x >> 3)] |= 1 << (pos_x & 7);
        }
        mBadPixelPositions.clear();
    }

    void RawImageData::fixBadPixels()
    {
#if !defined (EMULATE_DCRAW_BAD_PIXELS)

        /* Transfer if not already done */
        transferBadPixelsToMap();

#if 0 // For testing purposes
  if (!mBadPixelMap)
    createBadPixelMap();
  for (int y = 400; y < 700; y++){
    for (int x = 1200; x < 1700; x++) {
      mBadPixelMap[mBadPixelMapPitch * y + (x >> 3)] |= 1 << (x&7);
    }
  }
#endif

        /* Process bad pixels, if any */
        if (mBadPixelMap)
            startWorker(RawImageWorker::FIX_BAD_PIXELS, false);

        return;

#else  // EMULATE_DCRAW_BAD_PIXELS - not recommended, testing purposes only

  for (vector<UInt32>::iterator i=mBadPixelPositions.begin(); i != mBadPixelPositions.end(); ++i) {
    UInt32 pos = *i;
    UInt32 pos_x = pos&0xffff;
    UInt32 pos_y = pos>>16;
    UInt32 total = 0;
    UInt32 div = 0;
    // 0 side covered by unsignedness.
    for (UInt32 r=pos_x-2; r<=pos_x+2 && r<(UInt32)uncropped_dim.x; r+=2) {
      for (UInt32 c=pos_y-2; c<=pos_y+2 && c<(UInt32)uncropped_dim.y; c+=2) {
        ushort16* pix = (ushort16*)getDataUncropped(r,c);
        if (*pix) {
          total += *pix;
          div++;
        }
      }
    }
    ushort16* pix = (ushort16*)getDataUncropped(pos_x,pos_y);
    if (div) {
      pix[0] = total / div;
    }
  }
#endif

    }

    void RawImageData::startWorker(RawImageWorker::RawImageWorkerTask task, bool cropped)
    {
        int height = (cropped) ? dim.y : uncropped_dim.y;
        if (task & RawImageWorker::FULL_IMAGE)
        {
            height = uncropped_dim.y;
        }

        int threads = getThreadCount();
        if (threads <= 1)
        {
            RawImageWorker worker(this, task, 0, height);
            worker.performTask();
            return;
        }

# ifndef NO_PTHREAD
        RawImageWorker** workers = new RawImageWorker*[threads];
        int y_offset = 0;
        int y_per_thread = (height + threads - 1) / threads;

        for (int i = 0; i < threads; i++)
        {
            int y_end = Math.Math.Min(((y_offset + y_per_thread, height);
            workers[i] = new RawImageWorker(this, task, y_offset, y_end);
            workers[i].startThread();
            y_offset = y_end;
        }
        for (int i = 0; i < threads; i++)
        {
            workers[i].waitForThread();
            delete workers[i];
        }
        delete[] workers;
#else
        ThrowRDE("Unreachable");
#endif
    }

    void RawImageData::fixBadPixelsThread(int start_y, int end_y)
    {
        int gw = (uncropped_dim.x + 15) / 32;
# ifdef __AFL_COMPILER
        int bad_count = 0;
#endif
        for (int y = start_y; y < end_y; y++)
        {
            UInt32* bad_map = (UInt32*)&mBadPixelMap[y * mBadPixelMapPitch];
            for (int x = 0; x < gw; x++)
            {
                // Test if there is a bad pixel within these 32 pixels
                if (bad_map[x] != 0)
                {
                    byte8* bad = (byte8*)&bad_map[x];
                    // Go through each pixel
                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            if (1 == ((bad[i] >> j) & 1))
                            {
# ifdef __AFL_COMPILER
                                if (bad_count++ > 100)
                                    ThrowRDE("The bad pixels are too damn high!");
#endif
                                fixBadPixel(x * 32 + i * 8 + j, y, 0);
                            }
                        }
                    }
                }
            }
        }
    }

    void RawImageData::blitFrom(RawImage src, iPoint2D srcPos, iPoint2D size, iPoint2D destPos)
    {
        iRectangle2D src_rect(srcPos, size);
        iRectangle2D dest_rect(destPos, size);
        src_rect = src_rect.getOverlap(iRectangle2D(iPoint2D(0, 0), src.dim));
        dest_rect = dest_rect.getOverlap(iRectangle2D(iPoint2D(0, 0), dim));

        iPoint2D blitsize = src_rect.dim.getSmallest(dest_rect.dim);
        if (blitsize.area() <= 0)
            return;

        // TODO: Move offsets after crop.
        BitBlt(getData(dest_rect.pos.x, dest_rect.pos.y), pitch, src.getData(src_rect.pos.x, src_rect.pos.y), src.pitch, blitsize.x * bpp, blitsize.y);
    }

    /* Does not take cfa into consideration */
    void RawImageData::expandBorder(iRectangle2D validData)
    {
        validData = validData.getOverlap(iRectangle2D(0, 0, dim.x, dim.y));
        if (validData.pos.x > 0)
        {
            for (int y = 0; y < dim.y; y++)
            {
                byte8* src_pos = getData(validData.pos.x, y);
                byte8* dst_pos = getData(validData.pos.x - 1, y);
                for (int x = validData.pos.x; x >= 0; x--)
                {
                    for (UInt32 i = 0; i < bpp; i++)
                    {
                        dst_pos[i] = src_pos[i];
                    }
                    dst_pos -= bpp;
                }
            }
        }

        if (validData.getRight() < dim.x)
        {
            int pos = validData.getRight();
            for (int y = 0; y < dim.y; y++)
            {
                byte8* src_pos = getData(pos - 1, y);
                byte8* dst_pos = getData(pos, y);
                for (int x = pos; x < dim.x; x++)
                {
                    for (UInt32 i = 0; i < bpp; i++)
                    {
                        dst_pos[i] = src_pos[i];
                    }
                    dst_pos += bpp;
                }
            }
        }

        if (validData.pos.y > 0)
        {
            byte8* src_pos = getData(0, validData.pos.y);
            for (int y = 0; y < validData.pos.y; y++)
            {
                byte8* dst_pos = getData(0, y);
                memcpy(dst_pos, src_pos, dim.x * bpp);
            }
        }
        if (validData.getBottom() < dim.y)
        {
            byte8* src_pos = getData(0, validData.getBottom() - 1);
            for (int y = validData.getBottom(); y < dim.y; y++)
            {
                byte8* dst_pos = getData(0, y);
                memcpy(dst_pos, src_pos, dim.x * bpp);
            }
        }
    }

    void RawImageData::clearArea(iRectangle2D area, byte8 val /*= 0*/ )
    {
        area = area.getOverlap(iRectangle2D(iPoint2D(0, 0), dim));

        if (area.area() <= 0)
            return;

        for (int y = area.getTop(); y < area.getBottom(); y++)
            memset(getData(area.getLeft(), y), val, area.getWidth() * bpp);
    }


    RawImage& RawImage::operator=(RawImage & p)
    {
        if (this == &p)      // Same object?
            return *this;      // Yes, so skip assignment, and just return *this.
        pthread_mutex_lock(&p_.mymutex);
        // Retain the old RawImageData before overwriting it
        RawImageData * old = p_;
        p_ = p.p_;
        // Increment use on new data
        ++p_.dataRefCount;
        // If the RawImageData previously used by "this" is unused, delete it.
        if (--old.dataRefCount == 0)
        {
            pthread_mutex_unlock(&(old.mymutex));
            delete old;
        }
        else
        {
            pthread_mutex_unlock(&(old.mymutex));
        }
        return *this;
    }

    void* RawImageWorkerThread(void* _this)
    {
        RawImageWorker* me = (RawImageWorker*)_this;
        me.performTask();
        return null;
    }

    RawImageWorker::RawImageWorker(RawImageData* _img, RawImageWorkerTask _task, int _start_y, int _end_y)
{
  data = _img;
  start_y = _start_y;
  end_y = _end_y;
  task = _task;
#ifndef NO_PTHREAD
  pthread_attr_init(&attr);
  pthread_attr_setdetachstate(&attr, PTHREAD_CREATE_JOINABLE);
#endif
}

RawImageWorker::~RawImageWorker()
{
# ifndef NO_PTHREAD
    pthread_attr_destroy(&attr);
#endif
}

#ifndef NO_PTHREAD
void RawImageWorker::startThread()
{
    /* Initialize and set thread detached attribute */
    pthread_create(&threadid, &attr, RawImageWorkerThread, this);
}

void RawImageWorker::waitForThread()
{
    void* status;
    pthread_join(threadid, &status);
}
#endif

void RawImageWorker::performTask()
{
    try
    {
        switch (task)
        {
            case SCALE_VALUES:
                data.scaleValues(start_y, end_y);
                break;
            case FIX_BAD_PIXELS:
                data.fixBadPixelsThread(start_y, end_y);
                break;
            case APPLY_LOOKUP:
                data.doLookup(start_y, end_y);
                break;
            default:
                _ASSERTE(false);
        }
    }
    catch (RawDecoderException e)
    {
        data.setError(e.what());
    }
    catch (TiffParserException e)
    {
        data.setError(e.what());
    }
    catch (IOException e)
    {
        data.setError(e.what());
    }
}

void RawImageData::sixteenBitLookup()
{
    if (table == null)
    {
        return;
    }
    startWorker(RawImageWorker::APPLY_LOOKUP, true);
}

void RawImageData::setTable(TableLookUp* t)
{
    if (table != null)
    {
        delete table;
    }
    table = t;
}

void RawImageData::setTable(ushort16* table, int nfilled, bool dither)
{
    TableLookUp* t = new TableLookUp(1, dither);
    t.setTable(0, table, nfilled);
    this.setTable(t);
}

int TABLE_SIZE = 65536 * 2;

// Creates n numre of tables.
TableLookUp::TableLookUp(int _ntables, bool _dither) : ntables(_ntables), dither(_dither)
{
    tables = null;
    if (ntables < 1)
    {
        ThrowRDE("Cannot construct 0 tables");
    }
    tables = new ushort16[ntables * TABLE_SIZE];
    memset(tables, 0, sizeof(ushort16) * ntables * TABLE_SIZE);
}

TableLookUp::~TableLookUp()
{
    if (tables != null)
    {
        delete[] tables;
        tables = null;
    }
}


void TableLookUp::setTable(int ntable, ushort16* table, int nfilled)
{
    if (ntable > ntables)
    {
        ThrowRDE("Table lookup with number greater than number of tables.");
    }
    ushort16* t = &tables[ntable * TABLE_SIZE];
    if (!dither)
    {
        for (int i = 0; i < 65536; i++)
        {
            t[i] = (i < nfilled) ? table[i] : table[nfilled - 1];
        }
        return;
    }
    for (int i = 0; i < nfilled; i++)
    {
        int center = table[i];
        int lower = i > 0 ? table[i - 1] : center;
        int upper = i < (nfilled - 1) ? table[i + 1] : center;
        int delta = upper - lower;
        t[i * 2] = center - ((upper - lower + 2) / 4);
        t[i * 2 + 1] = delta;
    }

    for (int i = nfilled; i < 65536; i++)
    {
        t[i * 2] = table[nfilled - 1];
        t[i * 2 + 1] = 0;
    }
    t[0] = t[1];
    t[TABLE_SIZE - 1] = t[TABLE_SIZE - 2];
}


ushort16* TableLookUp::getTable(int n)
{
    if (n > ntables)
    {
        ThrowRDE("Table lookup with number greater than number of tables.");
    }
    return &tables[n * TABLE_SIZE];
}


} // namespace RawSpeed
