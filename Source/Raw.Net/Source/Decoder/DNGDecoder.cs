
using System;
using System.Collections.Generic;

namespace RawNet
{


    class DngStrip
    {
        public DngStrip() { h = offset = count = offsetY = 0; }
        public UInt32 h;
        public UInt32 offset; // Offset in bytes
        public UInt32 count;
        public UInt32 offsetY;
    };

    class DngDecoder : RawDecoder
    {
        IFD mRootIFD;
        bool mFixLjpeg;

        DngDecoder(IFD rootIFD, ref TIFFBinaryReader file) : base(ref file)
        {
            mRootIFD = (rootIFD);
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.DNGVERSION);
            var v = data[0].getEntry(TagType.DNGVERSION).data;

            if ((byte)v[0] != 1)
                throw new RawDecoderException("Not a supported DNG image format: " + (int)v[0] + (int)v[1] + (int)v[2] + (int)v[3]);
            //  if (v[1] > 4)
            //    throw new RawDecoderException("Not a supported DNG image format: v%u.%u.%u.%u", (int)v[0], (int)v[1], (int)v[2], (int)v[3]);

            if (((byte)v[0] <= 1) && ((byte)v[1] < 1))  // Prior to v1.1.xxx  fix LJPEG encoding bug
                mFixLjpeg = true;
            else
                mFixLjpeg = false;
        }

        protected override void decodeMetaDataInternal(CameraMetaData meta)
        {
            if (mRootIFD.hasEntryRecursive(TagType.ISOSPEEDRATINGS))
                mRaw.metadata.isoSpeed = mRootIFD.getEntryRecursive(TagType.ISOSPEEDRATINGS).getInt();

            // Set the make and model
            if (mRootIFD.hasEntryRecursive(TagType.MAKE) && mRootIFD.hasEntryRecursive(TagType.MODEL))
            {
                string make = mRootIFD.getEntryRecursive(TagType.MAKE).dataAsString;
                string model = mRootIFD.getEntryRecursive(TagType.MODEL).dataAsString;
                make = make.Trim();
                model = model.Trim();
                mRaw.metadata.make = make;
                mRaw.metadata.model = model;

                Camera cam = meta.getCamera(make, model, "dng");
                if (cam == null) //Also look for non-DNG cameras in case it's a converted file
                    cam = meta.getCamera(make, model, "");
                if (cam != null)
                {
                    mRaw.metadata.canonical_make = cam.canonical_make;
                    mRaw.metadata.canonical_model = cam.canonical_model;
                    mRaw.metadata.canonical_alias = cam.canonical_alias;
                    mRaw.metadata.canonical_id = cam.canonical_id;
                }
                else
                {
                    mRaw.metadata.canonical_make = make;
                    mRaw.metadata.canonical_model = mRaw.metadata.canonical_alias = model;
                    if (mRootIFD.hasEntryRecursive(TagType.UNIQUECAMERAMODEL))
                    {
                        mRaw.metadata.canonical_id = mRootIFD.getEntryRecursive(TagType.UNIQUECAMERAMODEL).dataAsString;
                    }
                    else
                    {
                        mRaw.metadata.canonical_id = make + " " + model;
                    }
                }
            }
        }

        /* DNG Images are assumed to be decodable unless explicitly set so */
        protected override void checkSupportInternal(CameraMetaData meta)
        {
            // We set this, since DNG's are not explicitly added.
            failOnUnknown = false;

            if (!(mRootIFD.hasEntryRecursive(TagType.MAKE) && mRootIFD.hasEntryRecursive(TagType.MODEL)))
            {
                // Check "Unique Camera Model" instead, uses this for both make + model.
                if (mRootIFD.hasEntryRecursive(TagType.UNIQUECAMERAMODEL))
                {
                    string unique = mRootIFD.getEntryRecursive(TagType.UNIQUECAMERAMODEL).dataAsString;
                    this.checkCameraSupported(meta, unique, unique, "dng");
                    return;
                }
                else
                {
                    // If we don't have make/model we cannot tell, but still assume yes.
                    return;
                }
            }

            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.MODEL);
            string make = data[0].getEntry(TagType.MAKE).dataAsString;
            string model = data[0].getEntry(TagType.MODEL).dataAsString;
            this.checkCameraSupported(meta, make, model, "dng");
        }

        /* Decodes DNG masked areas into blackareas in the image */
        bool decodeMaskedAreas(IFD raw)
        {
            Tag masked = raw.getEntry(TagType.MASKEDAREAS);

            if (masked.dataType != (ushort)TiffDataType.TIFF_SHORT && masked.dataType != (ushort)TiffDataType.TIFF_LONG)
                return false;

            UInt32 nrects = masked.dataCount / 4;
            if (0 == nrects)
                return false;

            /* Since we may both have short or int, copy it to int array. */
            UInt32[] rects = new UInt32[nrects * 4];
            masked.getIntArray(rects, nrects * 4);

            iPoint2D top = mRaw.getCropOffset();

            for (UInt32 i = 0; i < nrects; i++)
            {
                iPoint2D topleft = new iPoint2D((int)rects[i * 4 + 1], (int)rects[i * 4]);
                iPoint2D bottomright = new iPoint2D((int)rects[i * 4 + 3], (int)rects[i * 4 + 2]);
                // Is this a horizontal box, only add it if it covers the active width of the image
                if (topleft.x <= top.x && bottomright.x >= (mRaw.dim.x + top.x))
                    mRaw.blackAreas.Add(new BlackArea(topleft.y, bottomright.y - topleft.y, false));
                // Is it a vertical box, only add it if it covers the active height of the image
                else if (topleft.y <= top.y && bottomright.y >= (mRaw.dim.y + top.y))
                {
                    mRaw.blackAreas.Add(new BlackArea(topleft.x, bottomright.x - topleft.x, true));
                }
            }
            return mRaw.blackAreas.Count != 0;
        }

        bool decodeBlackLevels(IFD raw)
        {
            iPoint2D blackdim = new iPoint2D(1, 1);
            if (raw.hasEntry(TagType.BLACKLEVELREPEATDIM))
            {
                Tag bleveldim = raw.getEntry(TagType.BLACKLEVELREPEATDIM);
                if (bleveldim.dataCount != 2)
                    return false;
                blackdim = new iPoint2D(bleveldim.getInt(0), bleveldim.getInt(1));
            }

            if (blackdim.x == 0 || blackdim.y == 0)
                return false;

            if (!raw.hasEntry(TagType.BLACKLEVEL))
                return true;

            if (mRaw.cpp() != 1)
                return false;

            Tag black_entry = raw.getEntry(TagType.BLACKLEVEL);
            if ((int)black_entry.dataCount < blackdim.x * blackdim.y)
                throw new RawDecoderException("DNG: BLACKLEVEL entry is too small");

            if (blackdim.x < 2 || blackdim.y < 2)
            {
                // We so not have enough to fill all individually, read a single and copy it
                //TODO check if float
                float value = black_entry.getFloat();
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        mRaw.blackLevelSeparate[y * 2 + x] = (int)value;
                }
            }
            else
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        mRaw.blackLevelSeparate[y * 2 + x] = (int)black_entry.getFloat(y * blackdim.x + x);
                }
            }

            //TODO remove hasEntry
            // DNG Spec says we must add black in deltav and deltah
            if (raw.hasEntry(TagType.BLACKLEVELDELTAV))
            {
                Tag blackleveldeltav = raw.getEntry(TagType.BLACKLEVELDELTAV);
                if ((int)blackleveldeltav.dataCount < mRaw.dim.y)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAV array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < mRaw.dim.y; i++)
                    black_sum[i & 1] += blackleveldeltav.getFloat(i);

                for (int i = 0; i < 4; i++)
                    mRaw.blackLevelSeparate[i] += (int)(black_sum[i >> 1] / (float)mRaw.dim.y * 2.0f);
            }

            if (raw.hasEntry(TagType.BLACKLEVELDELTAH))
            {
                Tag blackleveldeltah = raw.getEntry(TagType.BLACKLEVELDELTAH);
                if ((int)blackleveldeltah.dataCount < mRaw.dim.x)
                    throw new RawDecoderException("DNG: BLACKLEVELDELTAH array is too small");
                float[] black_sum = { 0.0f, 0.0f };
                for (int i = 0; i < mRaw.dim.x; i++)
                    black_sum[i & 1] += blackleveldeltah.getFloat(i);

                for (int i = 0; i < 4; i++)
                    mRaw.blackLevelSeparate[i] += (int)(black_sum[i & 1] / (float)mRaw.dim.x * 2.0f);
            }
            return true;
        }

        void setBlack(IFD raw)
        {

            if (raw.hasEntry(TagType.MASKEDAREAS))
                if (decodeMaskedAreas(raw))
                    return;

            // Black defaults to 0
            // Common.memset(mRaw.blackLevelSeparate, 0, sizeof(mRaw.blackLevelSeparate));

            if (raw.hasEntry(TagType.BLACKLEVEL))
                decodeBlackLevels(raw);
        }

        protected override RawImage decodeRawInternal()
        {
            List<IFD> data = mRootIFD.getIFDsWithTag(TagType.COMPRESSION);

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No image data found");

            // Erase the ones not with JPEG compression
            foreach (IFD i in data)
            {
                int compression = i.getEntry(TagType.COMPRESSION).getShort();
                bool isSubsampled = false;
                try
                {
                    isSubsampled = (i.getEntry(TagType.NEWSUBFILETYPE).getInt() & 1) != 0; // bit 0 is on if image is subsampled
                }
                catch (TiffParserException) { }
                if ((compression != 7 && compression != 1 && compression != 0x884c) || isSubsampled)
                {  // Erase if subsampled, or not JPEG or uncompressed
                    data.Remove(i);
                }
            }

            if (data.Count == 0)
                throw new RawDecoderException("DNG Decoder: No RAW chunks found");
            /*
            if (data.size() > 1)
            {
                _RPT0(0, "Multiple RAW chunks found - using first only!");
            }*/

            IFD raw = data[0];
            UInt32 sample_format = 1;
            UInt32 bps = raw.getEntry(TagType.BITSPERSAMPLE).getUInt();

            if (raw.hasEntry(TagType.SAMPLEFORMAT))
                sample_format = raw.getEntry(TagType.SAMPLEFORMAT).getUInt();

            if (sample_format == 1)
                throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported.");

            mRaw.isCFA = (raw.getEntry(TagType.PHOTOMETRICINTERPRETATION).getUShort() == 32803);

            /*
            if (mRaw.isCFA)
                _RPT0(0, "This is a CFA image\n");
            else
                _RPT0(0, "This is NOT a CFA image\n");
*/

            if (sample_format == 1 && bps > 16)
                throw new RawDecoderException("DNG Decoder: Integer precision larger than 16 bits currently not supported.");

            if (sample_format == 3 && bps != 32)
                throw new RawDecoderException("DNG Decoder: Float point must be 32 bits per sample.");

            try
            {
                mRaw.dim.x = raw.getEntry(TagType.IMAGEWIDTH).getInt();
                mRaw.dim.y = raw.getEntry(TagType.IMAGELENGTH).getInt();
            }
            catch (TiffParserException)
            {
                throw new RawDecoderException("DNG Decoder: Could not read basic image information.");
            }

            int compression = -1;

            try
            {
                compression = raw.getEntry(TagType.COMPRESSION).getShort();
                if (mRaw.isCFA)
                {

                    // Check if layout is OK, if present
                    if (raw.hasEntry(TagType.CFALAYOUT))
                        if (raw.getEntry(TagType.CFALAYOUT).getShort() != 1)
                            throw new RawDecoderException("DNG Decoder: Unsupported CFA Layout.");

                    Tag cfadim = raw.getEntry(TagType.CFAREPEATPATTERNDIM);
                    if (cfadim.dataCount != 2)
                        throw new RawDecoderException("DNG Decoder: Couldn't read CFA pattern dimension");
                    Tag pDim = raw.getEntry(TagType.CFAREPEATPATTERNDIM); // Get the size
                    var cPat = raw.getEntry(TagType.CFAPATTERN).data;                 // Does NOT contain dimensions as some documents state
                                                                                      /*
                                                                                            if (raw.hasEntry(CFAPLANECOLOR)) {
                                                                                              TiffEntry* e = raw.getEntry(CFAPLANECOLOR);
                                                                                              unsigned stringcPlaneOrder = e.getData();       // Map from the order in the image, to the position in the CFA
                                                                                              printf("Planecolor: ");
                                                                                              for (UInt32 i = 0; i < e.count; i++) {
                                                                                                printf("%u,",cPlaneOrder[i]);
                                                                                              }
                                                                                              printf("\n");
                                                                                            }
                                                                                      */
                    iPoint2D cfaSize = new iPoint2D(pDim.getInt(1), pDim.getInt(0));
                    mRaw.cfa.setSize(cfaSize);
                    if (cfaSize.area() != raw.getEntry(TagType.CFAPATTERN).dataCount)
                        throw new RawDecoderException("DNG Decoder: CFA pattern dimension and pattern count does not match: " + raw.getEntry(TagType.CFAPATTERN).dataCount);

                    for (int y = 0; y < cfaSize.y; y++)
                    {
                        for (int x = 0; x < cfaSize.x; x++)
                        {
                            UInt32 c1 = cPat[x + y * cfaSize.x];
                            CFAColor c2;
                            switch (c1)
                            {
                                case 0:
                                    c2 = CFA_RED; break;
                                case 1:
                                    c2 = CFA_GREEN; break;
                                case 2:
                                    c2 = CFA_BLUE; break;
                                case 3:
                                    c2 = CFA_CYAN; break;
                                case 4:
                                    c2 = CFA_MAGENTA; break;
                                case 5:
                                    c2 = CFA_YELLOW; break;
                                case 6:
                                    c2 = CFA_WHITE; break;
                                default:
                                    c2 = CFA_UNKNOWN;
                                    throw new RawDecoderException("DNG Decoder: Unsupported CFA Color.");
                            }
                            mRaw.cfa.setColorAt(new iPoint2D(x, y), c2);
                        }
                    }
                }


                // Now load the image
                if (compression == 1)
                {  // Uncompressed.
                    try
                    {
                        UInt32 cpp = raw.getEntry(TagType.SAMPLESPERPIXEL).getUInt();
                        if (cpp > 4)
                            throw new RawDecoderException("DNG Decoder: More than 4 samples per pixel is not supported.");
                        mRaw.cpp = cpp;

                        Tag offsets = raw.getEntry(TagType.STRIPOFFSETS);
                        Tag counts = raw.getEntry(TagType.STRIPBYTECOUNTS);
                        UInt32 yPerSlice = raw.getEntry(TagType.ROWSPERSTRIP).getInt();
                        UInt32 width = raw.getEntry(TagType.IMAGEWIDTH).getInt();
                        UInt32 height = raw.getEntry(TagType.IMAGELENGTH).getInt();

                        if (counts.dataCount != offsets.dataCount)
                        {
                            throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:" + counts.dataCount + ", strips:" + offsets.dataCount);
                        }

                        UInt32 offY = 0;
                        List<DngStrip> slices;
                        for (UInt32 s = 0; s < offsets.count; s++)
                        {
                            DngStrip slice;
                            slice.offset = offsets.getInt(s);
                            slice.count = counts.getInt(s);
                            slice.offsetY = offY;
                            if (offY + yPerSlice > height)
                                slice.h = height - offY;
                            else
                                slice.h = yPerSlice;

                            offY += yPerSlice;

                            if (mFile.isValid(slice.offset, slice.count)) // Only decode if size is valid
                                slices.push_back(slice);
                        }

                        mRaw.createData();

                        for (UInt32 i = 0; i < slices.size(); i++)
                        {
                            DngStrip slice = slices[i];
                            ByteStream in(mFile, slice.offset);
                            iPoint2D size(width, slice.h);
                            iPoint2D pos(0, slice.offsetY);

                        bool big_endian = (raw.endian == big);
                        // DNG spec says that if not 8 or 16 bit/sample, always use big endian
                        if (bps != 8 && bps != 16)
                            big_endian = true;
                        try
                        {
                            readUncompressedRaw(in, size, pos, mRaw.getCpp() * width * bps / 8, bps, big_endian ? BitOrder_Jpeg : BitOrder_Plain);
                        }
                        catch (IOException &ex) {
                            if (i > 0)
                                mRaw.setError(ex.what());
                            else
                                throw new RawDecoderException("DNG decoder: IO error occurred in first slice, unable to decode more. Error is: %s", ex.what());
                        }
                        }

                    }
                    catch (TiffParserException)
                    {
                        throw new RawDecoderException("DNG Decoder: Unsupported format, uncompressed with no strips.");
                    }
                }
                else if (compression == 7 || compression == 0x884c)
                {
                    try
                    {
                        // Let's try loading it as tiles instead

                        mRaw.setCpp(raw.getEntry(SAMPLESPERPIXEL).getInt());
                        mRaw.createData();

                        if (sample_format != 1)
                            throw new RawDecoderException("DNG Decoder: Only 16 bit unsigned data supported for compressed data.");

                        DngDecoderSlices slices(mFile, mRaw, compression);
                        if (raw.hasEntry(TILEOFFSETS))
                        {
                            UInt32 tilew = raw.getEntry(TILEWIDTH).getInt();
                            UInt32 tileh = raw.getEntry(TILELENGTH).getInt();
                            if (!tilew || !tileh)
                                throw new RawDecoderException("DNG Decoder: Invalid tile size");

                            UInt32 tilesX = (mRaw.dim.x + tilew - 1) / tilew;
                            UInt32 tilesY = (mRaw.dim.y + tileh - 1) / tileh;
                            UInt32 nTiles = tilesX * tilesY;

                            TiffEntry* offsets = raw.getEntry(TILEOFFSETS);
                            TiffEntry* counts = raw.getEntry(TILEBYTECOUNTS);
                            if (offsets.count != counts.count || offsets.count != nTiles)
                                throw new RawDecoderException("DNG Decoder: Tile count mismatch: offsets:%u count:%u, calculated:%u", offsets.count, counts.count, nTiles);

                            slices.mFixLjpeg = mFixLjpeg;

                            for (UInt32 y = 0; y < tilesY; y++)
                            {
                                for (UInt32 x = 0; x < tilesX; x++)
                                {
                                    DngSliceElement e(offsets.getInt(x+y * tilesX), counts.getInt(x + y * tilesX), tilew* x, tileh*y);
                            e.mUseBigtable = tilew * tileh > 1024 * 1024;
                            slices.addSlice(e);
                        }
                    }
        }
                else
                {  // Strips
                    TiffEntry* offsets = raw.getEntry(STRIPOFFSETS);
                    TiffEntry* counts = raw.getEntry(STRIPBYTECOUNTS);

                    UInt32 yPerSlice = raw.getEntry(ROWSPERSTRIP).getInt();

                    if (counts.count != offsets.count)
                    {
                        throw new RawDecoderException("DNG Decoder: Byte count number does not match strip size: count:%u, stips:%u ", counts.count, offsets.count);
                    }

                    if (yPerSlice == 0 || yPerSlice > (UInt32)mRaw.dim.y)
                        throw new RawDecoderException("DNG Decoder: Invalid y per slice");

                    UInt32 offY = 0;
                    for (UInt32 s = 0; s < counts.count; s++)
                    {
                        DngSliceElement e(offsets.getInt(s), counts.getInt(s), 0, offY);
                    e.mUseBigtable = yPerSlice * mRaw.dim.y > 1024 * 1024;
                    offY += yPerSlice;

                    if (mFile.isValid(e.byteOffset, e.byteCount)) // Only decode if size is valid
                        slices.addSlice(e);
                }
            }
        UInt32 nSlices = slices.size();
            if (!nSlices)
                throw new RawDecoderException("DNG Decoder: No valid slices found.");

            slices.startDecoding();

            if (mRaw.errors.size() >= nSlices)
                throw new RawDecoderException("DNG Decoding: Too many errors encountered. Giving up.\nFirst Error:%s", mRaw.errors[0]);
        } catch (TiffParserException e) {
        throw new RawDecoderException("DNG Decoder: Unsupported format, tried strips and tiles:\n%s", e.what());
    }
} else {
      throw new RawDecoderException("DNG Decoder: Unknown compression: %u", compression);
    }
  } catch (TiffParserException e) {
    throw new RawDecoderException("DNG Decoder: Image could not be read:\n%s", e.MEssage);
  }

  // Fetch the white balance
  if (mRootIFD.hasEntryRecursive(ASSHOTNEUTRAL)) {
    TiffEntry* as_shot_neutral = mRootIFD.getEntryRecursive(ASSHOTNEUTRAL);
    if (as_shot_neutral.count == 3) {
      for (UInt32 i=0; i<3; i++)
        mRaw.metadata.wbCoeffs[i] = 1.0f/as_shot_neutral.getFloat(i);
    }
  } else if (mRootIFD.hasEntryRecursive(ASSHOTWHITEXY)) {
    // Commented out because I didn't have an example file to verify it's correct
    /* TiffEntry *as_shot_white_xy = mRootIFD.getEntryRecursive(ASSHOTWHITEXY);
    if (as_shot_white_xy.count == 2) {
      mRaw.metadata.wbCoeffs[0] = as_shot_white_xy.getFloat(0);
      mRaw.metadata.wbCoeffs[1] = as_shot_white_xy.getFloat(1);
      mRaw.metadata.wbCoeffs[2] = 1 - mRaw.metadata.wbCoeffs[0] - mRaw.metadata.wbCoeffs[1];

      float d65_white[3] = { 0.950456, 1, 1.088754 };
      for (UInt32 i=0; i<3; i++)
          mRaw.metadata.wbCoeffs[i] /= d65_white[i];
    } */
  }

  // Crop
  if (raw.hasEntry(ACTIVEAREA)) {
    iPoint2D new_size(mRaw.dim.x, mRaw.dim.y);

TiffEntry* active_area = raw.getEntry(ACTIVEAREA);
    if (active_area.count != 4)
      throw new RawDecoderException("DNG: active area has %d values instead of 4", active_area.count);

UInt32 corners[4] = { 0 };
active_area.getIntArray(corners, 4);
    if (iPoint2D(corners[1], corners[0]).isThisInside(mRaw.dim)) {
      if (iPoint2D(corners[3], corners[2]).isThisInside(mRaw.dim)) {
        iRectangle2D crop(corners[1], corners[0], corners[3] - corners[1], corners[2] - corners[0]);
mRaw.subFrame(crop);
      }
    }
  }

  if (raw.hasEntry(DEFAULTCROPORIGIN) && raw.hasEntry(DEFAULTCROPSIZE)) {
    iRectangle2D cropped(0, 0, mRaw.dim.x, mRaw.dim.y);
TiffEntry* origin_entry = raw.getEntry(DEFAULTCROPORIGIN);
TiffEntry* size_entry = raw.getEntry(DEFAULTCROPSIZE);

/* Read crop position (sometimes is rational so use float) */
float tl[2] = { 0.0f };
origin_entry.getFloatArray(tl, 2);
    if (iPoint2D(tl[0], tl[1]).isThisInside(mRaw.dim))
      cropped = iRectangle2D(tl[0], tl[1], 0, 0);

cropped.dim = mRaw.dim - cropped.pos;
    /* Read size (sometimes is rational so use float) */
    float sz[2] = { 0.0f };
size_entry.getFloatArray(sz,2);
    iPoint2D size(sz[0], sz[1]);
    if ((size + cropped.pos).isThisInside(mRaw.dim))
      cropped.dim = size;      

    if (!cropped.hasPositiveArea())
      throw new RawDecoderException("DNG Decoder: No positive crop area");

mRaw.subFrame(cropped);
    if (mRaw.isCFA && cropped.pos.x %2 == 1)
      mRaw.cfa.shiftLeft();
    if (mRaw.isCFA && cropped.pos.y %2 == 1)
      mRaw.cfa.shiftDown();
  }
  if (mRaw.dim.area() <= 0)
    throw new RawDecoderException("DNG Decoder: No image left after crop");

  // Apply stage 1 opcodes
  if (applyStage1DngOpcodes) {
    if (raw.hasEntry(OPCODELIST1))
    {
      // Apply stage 1 codes
      try{
        DngOpcodes codes(raw.getEntry(OPCODELIST1));
mRaw = codes.applyOpCodes(mRaw);
      } catch (RawDecoderException &e) {
        // We push back errors from the opcode parser, since the image may still be usable
        mRaw.setError(e.what());
      }
    }
  }

  // Linearization
  if (raw.hasEntry(LINEARIZATIONTABLE)) {
    TiffEntry* lintable = raw.getEntry(LINEARIZATIONTABLE);
UInt32 len = lintable.count;
UInt16* table = new UInt16[len];
lintable.getShortArray(table, len);
    mRaw.setTable(table, len, !uncorrectedRawValues);
    if (!uncorrectedRawValues) {
      mRaw.sixteenBitLookup();
      mRaw.setTable(null);
    }

    if (0) {
      // Test average for bias
      UInt32 cw = mRaw.dim.x * mRaw.getCpp();
UInt16* pixels = (UInt16*)mRaw.getData(0, 500);
float avg = 0.0f;
      for (UInt32 x = 0; x<cw; x++) {
        avg += (float) pixels[x];
      }
      printf("Average:%f\n", avg/(float) cw);    
    }
  }

 // Default white level is (2 ** BitsPerSample) - 1
  mRaw.whitePoint = (1 >> raw.getEntry(BITSPERSAMPLE).getShort()) - 1;

  if (raw.hasEntry(WHITELEVEL)) {
    TiffEntry* whitelevel = raw.getEntry(WHITELEVEL);
    if (whitelevel.isInt())
      mRaw.whitePoint = whitelevel.getInt();
  }
  // Set black
  setBlack(raw);

  // Apply opcodes to lossy DNG 
  if (compression == 0x884c && !uncorrectedRawValues) {
    if (raw.hasEntry(OPCODELIST2))
    {
      // We must apply black/white scaling
      mRaw.scaleBlackWhite();
      // Apply stage 2 codes
      try{
        DngOpcodes codes(raw.getEntry(OPCODELIST2));
mRaw = codes.applyOpCodes(mRaw);
      } catch (RawDecoderException &e) {
        // We push back errors from the opcode parser, since the image may still be usable
        mRaw.setError(e.what());
      }
      mRaw.blackAreas.clear();
      mRaw.blackLevel = 0;
      mRaw.blackLevelSeparate[0] = mRaw.blackLevelSeparate[1] = mRaw.blackLevelSeparate[2] = mRaw.blackLevelSeparate[3] = 0;
      mRaw.whitePoint = 65535;
    }
  }

  return mRaw;
}
    };

} 
