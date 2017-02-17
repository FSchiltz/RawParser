using RawNet.Format.Tiff;
using System;
using System.Collections.Generic;

namespace RawNet.Dng
{

    class DngOpcode
    {
        public DngOpcode() { }

        /* Will be called exactly once, when input changes */
        /* Can be used for preparing pre-calculated values, etc */
        public virtual RawImage<ushort> CreateOutput(RawImage<ushort> input) { return input; }

        /* Will be called for actual processing */
        /* If multiThreaded is true, it will be called several times, */
        /* otherwise only once */
        /* Properties of out will not have changed from createOutput */
        public virtual void Apply(RawImage<ushort> input, ref RawImage<ushort> output, UInt32 startY, UInt32 endY) { }

        public Rectangle2D aoi = new Rectangle2D();
        public int flags;
        public enum Flags
        {
            MultiThreaded = 1,
            PureLookup = 2
        };
    };


    class DngOpcodes
    {
        List<DngOpcode> opcodes = new List<DngOpcode>();
        static UInt32 GetULong(byte[] ptr)
        {
            return (UInt32)ptr[1] << 24 | (UInt32)ptr[2] << 16 | (UInt32)ptr[3] << 8 | ptr[4];
            //return (UInt32)ptr[0] << 24 | (UInt32)ptr[1] << 16 | (UInt32)ptr[2] << 8 | (UInt32)ptr[3];
        }

        unsafe public DngOpcodes(Tag entry)
        {
            using (TiffBinaryReaderBigEndian reader = new TiffBinaryReaderBigEndian(entry.GetByteArray()))
            {
                UInt32 entry_size = entry.dataCount;

                if (entry_size < 20)
                    throw new RawDecoderException("Not enough bytes to read a single opcode");

                UInt32 opcode_count = reader.ReadUInt32();
                uint bytes_used = 4;
                for (UInt32 i = 0; i < opcode_count; i++)
                {
                    //if ((int)entry_size - bytes_used < 16)
                    // throw new RawDecoderException("DngOpcodes: Not enough bytes to read a new opcode");
                    reader.BaseStream.Position = bytes_used;
                    UInt32 code = reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    UInt32 expected_size = reader.ReadUInt32();
                    bytes_used += 16;
                    int opcode_used = 0;
                    switch (code)
                    {
                        /*
                        case 4:
                            mOpcodes.Add(new OpcodeFixBadPixelsConstant(&data[bytes_used], entry_size - bytes_used, &opcode_used));
                            break;
                        case 5:
                            mOpcodes.Add(new OpcodeFixBadPixelsList(&data[bytes_used], entry_size - bytes_used, &opcode_used));
                            break;*/
                        case 6:
                            opcodes.Add(new OpcodeTrimBounds(reader, entry_size - bytes_used, ref opcode_used));
                            break;
                        case 7:
                            opcodes.Add(new OpcodeMapTable(reader, entry_size - bytes_used, ref opcode_used, bytes_used));
                            break;
                        case 8:
                            opcodes.Add(new OpcodeMapPolynomial(reader, entry_size - bytes_used, ref opcode_used, bytes_used));
                            break;
                        case 9:
                            opcodes.Add(new OpcodeGainMap(reader, entry_size - bytes_used, ref opcode_used, bytes_used));
                            break;
                        /*
                    case 10:
                        mOpcodes.Add(new OpcodeDeltaPerRow(&data[bytes_used], entry_size - bytes_used, &opcode_used));
                        break;
                    case 11:
                        mOpcodes.Add(new OpcodeDeltaPerCol(&data[bytes_used], entry_size - bytes_used, &opcode_used));
                        break;
                    case 12:
                        mOpcodes.Add(new OpcodeScalePerRow(&data[bytes_used], entry_size - bytes_used, &opcode_used));
                        break;
                    case 13:
                        mOpcodes.Add(new OpcodeScalePerCol(&data[bytes_used], entry_size - bytes_used, &opcode_used));
                        break;*/
                        default:
                            // Throw Error if not marked as optional
                            /*if ((flags & 1) == 0)
                                throw new RawDecoderException("DngOpcodes: Unsupported Opcode: " + code);*/
                            break;
                    }
                    //if (opcode_used != expected_size)
                    //throw new RawDecoderException("DngOpcodes: Inconsistent length of opcode");
                    bytes_used += (uint)opcode_used;
                }
            }
        }

        public RawImage<ushort> ApplyOpCodes(RawImage<ushort> img)
        {
            int codes = opcodes.Count;
            for (int i = 0; i < codes; i++)
            {
                RawImage<ushort> img_out = opcodes[i].CreateOutput(img);
                Rectangle2D fullImage = new Rectangle2D(0, 0, img.raw.dim.Width, img.raw.dim.Height);

                if (!opcodes[i].aoi.IsThisInside(fullImage))
                    throw new RawDecoderException("Area of interest not inside image!");
                if (opcodes[i].aoi.HasPositiveArea())
                {
                    opcodes[i].Apply(img, ref img_out, opcodes[i].aoi.Top, opcodes[i].aoi.Bottom);
                    img = img_out;
                }
            }
            return img;
        }
    }
    /*

    public class OpcodeFixBadPixelsConstant : DngOpcode
    {
        virtual RawImage<ushort>  createOutput(RawImage<ushort>  input);
        virtual void apply(RawImage<ushort>  input, RawImage<ushort>  output, UInt32 startY, UInt32 endY);
        public int mValue;
    };


    class OpcodeFixBadPixelsList : DngOpcode
    {
        public:
    OpcodeFixBadPixelsList(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used);
        virtual ~OpcodeFixBadPixelsList(void) {};
    virtual void apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY);
        private:
    vector<UInt32> bad_pos;
    };
    */

    class OpcodeTrimBounds : DngOpcode
    {
        UInt64 mTop, mLeft, mBottom, mRight;
        /***************** OpcodeTrimBounds   ****************/

        public OpcodeTrimBounds(TiffBinaryReader parameters, UInt32 param_max_bytes, ref Int32 bytes_used)
        {
            if (param_max_bytes < 16)
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");
            mTop = parameters.ReadUInt16();
            mLeft = parameters.ReadUInt16();
            mBottom = parameters.ReadUInt16();
            mRight = parameters.ReadUInt16();
            bytes_used = 16;
        }

        public override void Apply(RawImage<ushort> input, ref RawImage<ushort> output, UInt32 startY, UInt32 endY)
        {
            Rectangle2D crop = new Rectangle2D((uint)mLeft, (uint)mTop, (uint)(mRight - mLeft), (uint)(mBottom - mTop));
            output.raw.offset = crop.TopLeft;
            output.raw.dim = crop.BottomRight;
        }

    };

    class OpcodeMapTable : DngOpcode
    {
        UInt64 firstPlane, planes, rowPitch, colPitch;
        UInt16[] Lookup = new UInt16[65536];
        /***************** OpcodeMapTable   ****************/

        public OpcodeMapTable(TiffBinaryReader parameters, ulong param_max_bytes, ref Int32 bytes_used, uint offset)
        {
            if (param_max_bytes < 36)
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");
            uint h1 = parameters.ReadUInt32();
            uint w1 = parameters.ReadUInt32();
            uint h2 = parameters.ReadUInt32();
            uint w2 = parameters.ReadUInt32();
            aoi.SetAbsolute(w1, h1, w2, h2);
            firstPlane = parameters.ReadUInt32();
            planes = parameters.ReadUInt32();
            rowPitch = parameters.ReadUInt32();
            colPitch = parameters.ReadUInt32();
            if (planes == 0)
                throw new RawDecoderException("Zero planes");
            if (rowPitch == 0 || colPitch == 0)
                throw new RawDecoderException("Invalid Pitch");

            int tablesize = (int)parameters.ReadUInt32();
            bytes_used = 36;

            if (tablesize <= 0)
                throw new RawDecoderException("Table size must be positive");
            if (tablesize > 65536)
                throw new RawDecoderException("A map with more than 65536 entries not allowed");

            if (param_max_bytes < 36 + ((UInt64)tablesize * 2))
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");

            for (int i = 0; i <= 65535; i++)
            {
                int location = Math.Min(tablesize - 1, i);
                parameters.BaseStream.Position = 36 + 2 * location + offset;
                Lookup[i] = parameters.ReadUInt16();
            }

            bytes_used += tablesize * 2;
            flags = (int)Flags.MultiThreaded | (int)Flags.PureLookup;
        }

        public override RawImage<ushort> CreateOutput(RawImage<ushort> input)
        {
            if (firstPlane > input.raw.cpp)
                throw new RawDecoderException("Not that many planes in actual image");

            if (firstPlane + planes > input.raw.cpp)
                throw new RawDecoderException("Not that many planes in actual image");

            return input;
        }

        public unsafe override void Apply(RawImage<ushort> input, ref RawImage<ushort> output, UInt32 startY, UInt32 endY)
        {
            for (UInt64 y = startY; y < endY; y += rowPitch)
            {
                fixed (UInt16* t = &output.raw.rawView[(int)y * output.raw.dim.Width + aoi.Left])
                {
                    var src = t;
                    // Add offset, so this is always first plane
                    src += firstPlane;
                    for (ulong x = 0; x < aoi.Width; x += colPitch)
                    {
                        for (uint p = 0; p < planes; p++)
                        {
                            src[x * output.raw.cpp + p] = Lookup[src[x * output.raw.cpp + p]];
                        }
                    }
                }
            }
        }
    }

    class OpcodeMapPolynomial : DngOpcode
    {

        UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mDegree;
        double[] mCoefficient = new double[9];
        UInt16[] mLookup = new UInt16[65536];
        public OpcodeMapPolynomial(TiffBinaryReader parameters, UInt32 param_max_bytes, ref int bytes_used, uint offset)
        {
            if (param_max_bytes < 36)
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");
            uint h1 = parameters.ReadUInt32();
            uint w1 = parameters.ReadUInt32();
            uint h2 = parameters.ReadUInt32();
            uint w2 = parameters.ReadUInt32();
            aoi.SetAbsolute(w1, h1, w2, h2);
            mFirstPlane = parameters.ReadUInt32();
            mPlanes = parameters.ReadUInt32();
            mRowPitch = parameters.ReadUInt32();
            mColPitch = parameters.ReadUInt32();
            if (mPlanes == 0)
                throw new RawDecoderException("Zero planes");
            if (mRowPitch == 0 || mColPitch == 0)
                throw new RawDecoderException("Invalid Pitch");

            mDegree = parameters.ReadUInt32();
            bytes_used = 36;
            if (mDegree > 8)
                throw new RawDecoderException("A polynomial with more than 8 degrees not allowed");
            if (param_max_bytes < 36 + (mDegree * 8))
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");
            for (UInt64 i = 0; i <= mDegree; i++)
            {
                parameters.BaseStream.Position = (long)(36 + 8 * i) + offset;
                mCoefficient[i] = Convert.ToDouble(parameters.ReadBytes(8));
            }
            bytes_used += (int)(8 * mDegree + 8);
            flags = (int)Flags.MultiThreaded | (int)Flags.PureLookup;
        }

        public override RawImage<ushort> CreateOutput(RawImage<ushort> input)
        {
            if (mFirstPlane > input.raw.cpp)
                throw new RawDecoderException("Not that many planes in actual image");

            if (mFirstPlane + mPlanes > input.raw.cpp)
                throw new RawDecoderException("Not that many planes in actual image");

            // Create lookup
            for (int i = 0; i < 65536; i++)
            {
                double in_val = i / 65536.0;
                double val = mCoefficient[0];
                for (UInt64 j = 1; j <= mDegree; j++)
                    val += mCoefficient[j] * Math.Pow(in_val, j);
                mLookup[i] = (ushort)Common.Clampbits((int)(val * 65535.5), 16);
            }
            return input;
        }

        public unsafe override void Apply(RawImage<ushort> input, ref RawImage<ushort> output, UInt32 startY, UInt32 endY)
        {
            for (UInt64 y = startY; y < endY; y += mRowPitch)
            {
                fixed (UInt16* t = &output.raw.rawView[(int)y * output.raw.dim.Width + aoi.Left])
                {
                    var src = t;
                    // Add offset, so this is always first plane
                    src += mFirstPlane;
                    for (UInt64 x = 0; x < aoi.Width; x += mColPitch)
                    {
                        for (UInt64 p = 0; p < mPlanes; p++)
                        {
                            src[x * output.raw.cpp + p] = mLookup[src[x * output.raw.cpp + p]];
                        }
                    }
                }
            }
        }
    }

    class OpcodeGainMap : DngOpcode
    {
        UInt64 firstPlane, planes, rowPitch, colPitch, mapPointsV, mapPointsH, mapPlanes;
        double mapSpacingV, mapSpacingH, mapOriginV, mapOriginH;
        double[,,] gain;
        //double[] coefficient = new double[9];
        //UInt16[] lookup = new UInt16[65536];

        public OpcodeGainMap(TiffBinaryReaderBigEndian parameters, UInt32 param_max_bytes, ref int bytes_used, uint offset)
        {
            if (param_max_bytes < 36)
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");
            uint h1 = parameters.ReadUInt32();
            uint w1 = parameters.ReadUInt32();
            uint h2 = parameters.ReadUInt32();
            uint w2 = parameters.ReadUInt32();
            aoi.SetAbsolute(w1, h1, w2, h2);
            firstPlane = parameters.ReadUInt32();
            planes = parameters.ReadUInt32();
            rowPitch = parameters.ReadUInt32();
            colPitch = parameters.ReadUInt32();
            if (planes == 0)
                throw new RawDecoderException("Zero planes");
            if (rowPitch == 0 || colPitch == 0)
                throw new RawDecoderException("Invalid Pitch");

            mapPointsV = parameters.ReadUInt32();
            mapPointsH = parameters.ReadUInt32();
            mapSpacingV = parameters.ReadDouble() * h2;
            mapSpacingH = parameters.ReadDouble() * w2;
            mapOriginV = parameters.ReadDouble();
            mapOriginH = parameters.ReadDouble();
            mapPlanes = parameters.ReadUInt32();
            gain = new double[mapPointsV, mapPointsH, mapPlanes];
            bytes_used = 76;
            if (param_max_bytes < 36 + (mapPointsV * mapPointsH * mapPlanes * 8))
                throw new RawDecoderException("Not enough data to read parameters, only " + param_max_bytes + " bytes left.");
            for (UInt64 i = 0; i < mapPointsV; i++)
            {
                for (UInt64 j = 0; j < mapPointsH; j++)
                {
                    for (UInt64 k = 0; k < mapPlanes; k++)
                    {
                        gain[i, j, k] = parameters.ReadSingle();
                    }
                }
            }
            bytes_used += (int)(8 * mapPointsV * mapPointsH * mapPlanes);
            flags = (int)Flags.MultiThreaded | (int)Flags.PureLookup;
        }

        public override RawImage<ushort> CreateOutput(RawImage<ushort> input)
        {
            if (firstPlane > input.raw.cpp)
                throw new RawDecoderException("Not that many planes in actual image");

            if (firstPlane + planes > input.raw.cpp)
                throw new RawDecoderException("Not that many planes in actual image");

            /*
            // Create lookup
            for (int i = 0; i < 65536; i++)
            {
                double in_val = i / 65536.0;
                double val = coefficient[0];
                for (UInt64 j = 1; j <= degree; j++)
                    val += coefficient[j] * Math.Pow(in_val, j);
                lookup[i] = (ushort)Common.Clampbits((int)(val * 65535.5), 16);
            }*/
            return input;
        }

        public unsafe override void Apply(RawImage<ushort> input, ref RawImage<ushort> output, UInt32 startY, UInt32 endY)
        {
            for (UInt64 y = startY; y < endY; y += rowPitch)
            {
                ulong realY = (y - startY);
                fixed (UInt16* t = &output.raw.rawView[(int)y * output.raw.UncroppedDim.Width + aoi.Left])
                {
                    var src = t;
                    // Add offset, so this is always first plane
                    src += firstPlane;
                    for (UInt64 x = 0; x < aoi.Width; x += colPitch)
                    {
                        for (UInt64 p = 0; p < planes; p++)
                        {
                            //if outside the bound, last pixel or first
                            src[x] = (ushort)(src[x] * gain[(int)((y - startY) / mapSpacingV), (int)(x / mapSpacingH), 0]);
                        }
                    }
                }
            }
        }
    }
}

/*
class OpcodeDeltaPerRow : DngOpcode
{
    UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
    float* mDelta;
};

class OpcodeDeltaPerCol : public DngOpcode
{
public:
OpcodeDeltaPerCol(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used);
virtual ~OpcodeDeltaPerCol(void);
virtual RawImage& createOutput(RawImage<ushort>  &in);
virtual void apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY);
private:
UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
float* mDelta;
int* mDeltaX;
};

class OpcodeScalePerRow : public DngOpcode
{
public:
OpcodeScalePerRow(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used);
virtual ~OpcodeScalePerRow(void) {};
virtual RawImage& createOutput(RawImage<ushort>  &in);
virtual void apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY);
private:
UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
float* mDelta;
};

class OpcodeScalePerCol : public DngOpcode
{
public:
OpcodeScalePerCol(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used);
virtual ~OpcodeScalePerCol(void);
virtual RawImage& createOutput(RawImage<ushort>  &in);
virtual void apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY);
private:
UInt64 mFirstPlane, mPlanes, mRowPitch, mColPitch, mCount;
float* mDelta;
int* mDeltaX;
};*/


/*

    OpcodeFixBadPixelsConstant::OpcodeFixBadPixelsConstant(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used)
{
  if (param_max_bytes< 8)
    throw new RawDecoderException("OpcodeFixBadPixelsConstant: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
    mValue = getLong(&parameters[0]);
  // Bayer Phase not used
  * bytes_used = 8;
    mFlags = MultiThreaded;
}

RawImage& OpcodeFixBadPixelsConstant::createOutput(RawImage<ushort>  &in )
{
  // These limitations are present within the DNG SDK as well.
  if (in.getDataType() != TYPE_USHORT16)
    throw new RawDecoderException("OpcodeFixBadPixelsConstant: Only 16 bit images supported");

  if (in.getCpp() > 1)
    throw new RawDecoderException("OpcodeFixBadPixelsConstant: This operation is only supported with 1 component");

  return in;
}

void OpcodeFixBadPixelsConstant::apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY)
{
    iPoint2D crop = in.getCropOffset();
    UInt32 offset = crop.x | (crop.y << 16);
    vector<UInt32> bad_pos;
    for (UInt32 y = startY; y < endY; y++)
    {
        UInt16* src = (UInt16*)out.getData(0, y);
        for (UInt32 x = 0; x < (UInt32)in.dim.x; x++) {
        if (src[x] == mValue)
        {
            bad_pos.Add(offset + ((UInt32)x | (UInt32)y << 16));
        }
    }
}
  if (!bad_pos.empty()) {
    pthread_mutex_lock(&out.mBadPixelMutex);
    out.mBadPixelPositions.insert(out.mBadPixelPositions.end(), bad_pos.begin(), bad_pos.end());
    pthread_mutex_unlock(&out.mBadPixelMutex);
  }

}


OpcodeFixBadPixelsList::OpcodeFixBadPixelsList(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used)
{
  if (param_max_bytes< 12)
    throw new RawDecoderException("OpcodeFixBadPixelsList: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
// Skip phase - we don't care
UInt64 BadPointCount = getULong(&parameters[4]);
UInt64 BadRectCount = getULong(&parameters[8]);
bytes_used[0] = 12;

  if (12 + BadPointCount* 8 + BadRectCount* 16 > (UInt64) param_max_bytes)
    throw new RawDecoderException("OpcodeFixBadPixelsList: Ran out parameter space, only %u bytes left.", param_max_bytes);

  // Read points
  for (UInt64 i = 0; i<BadPointCount; i++) {
    UInt32 BadPointRow = (UInt32)getLong(&parameters[bytes_used[0]]);
UInt32 BadPointCol = (UInt32)getLong(&parameters[bytes_used[0] + 4]);
bytes_used[0] += 8;
    bad_pos.Add(BadPointRow | (BadPointCol << 16));
  }

  // Read rects
  for (UInt64 i = 0; i<BadRectCount; i++) {
    UInt32 BadRectTop = (UInt32)getLong(&parameters[bytes_used[0]]);
UInt32 BadRectLeft = (UInt32)getLong(&parameters[bytes_used[0] + 4]);
UInt32 BadRectBottom = (UInt32)getLong(&parameters[bytes_used[0]]);
UInt32 BadRectRight = (UInt32)getLong(&parameters[bytes_used[0] + 4]);
bytes_used[0] += 16;
    if (BadRectTop<BadRectBottom && BadRectLeft<BadRectRight) {
      for (UInt32 y = BadRectLeft; y <= BadRectRight; y++) {
        for (UInt32 x = BadRectTop; x <= BadRectBottom; x++) {
          bad_pos.Add(x | (y << 16));
        }
      }
    }
  }
}

void OpcodeFixBadPixelsList::apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY)
{
    iPoint2D crop = in.getCropOffset();
    UInt32 offset = crop.x | (crop.y << 16);
    for (vector<UInt32>::iterator i = bad_pos.begin(); i != bad_pos.end(); ++i)
    {
        UInt32 pos = offset + (*i);
    out.mBadPixelPositions.Add(pos);
    }
}*/




/*  

OpcodeDeltaPerRow::OpcodeDeltaPerRow(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used)
{
  if (param_max_bytes< 36)
    throw new RawDecoderException("OpcodeDeltaPerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
mPlanes = getLong(&parameters[20]);
mRowPitch = getLong(&parameters[24]);
mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    throw new RawDecoderException("OpcodeDeltaPerRow: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    throw new RawDecoderException("OpcodeDeltaPerRow: Invalid Pitch");

mCount = getLong(&parameters[32]);
  * bytes_used = 36;
  if (param_max_bytes< 36 + (mCount*4))
    throw new RawDecoderException("OpcodeDeltaPerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64) mAoi.getHeight() != mCount)
    throw new RawDecoderException("OpcodeDeltaPerRow: Element count (%llu) does not match height of area (%d.", mCount, mAoi.getHeight());

  for (UInt64 i = 0; i<mCount; i++)
    mDelta[i] = getFloat(&parameters[36 + 4 * i]);
  * bytes_used += 4* mCount;
mFlags = MultiThreaded;
}


RawImage& OpcodeDeltaPerRow::createOutput(RawImage<ushort>  &in )
{
  if (mFirstPlane > in.getCpp())
    throw new RawDecoderException("OpcodeDeltaPerRow: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    throw new RawDecoderException("OpcodeDeltaPerRow: Not that many planes in actual image");

  return in;
}

void OpcodeDeltaPerRow::apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY)
{
    if (in.getDataType() == TYPE_USHORT16) {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            UInt16* src = (UInt16*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            int delta = (int)(65535.0f * mDelta[y]);
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = clampbits(16, delta + src[x * cpp + p]);
                }
            }
        }
    } else {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            float* src = (float*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            float delta = mDelta[y];
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = delta + src[x * cpp + p];
                }
            }
        }
    }
}

OpcodeDeltaPerCol::OpcodeDeltaPerCol(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used)
{
  if (param_max_bytes< 36)
    throw new RawDecoderException("OpcodeDeltaPerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
mPlanes = getLong(&parameters[20]);
mRowPitch = getLong(&parameters[24]);
mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    throw new RawDecoderException("OpcodeDeltaPerCol: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    throw new RawDecoderException("OpcodeDeltaPerCol: Invalid Pitch");

mCount = getLong(&parameters[32]);
  * bytes_used = 36;
  if (param_max_bytes< 36 + (mCount*4))
    throw new RawDecoderException("OpcodeDeltaPerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64) mAoi.getWidth() != mCount)
    throw new RawDecoderException("OpcodeDeltaPerRow: Element count (%llu) does not match width of area (%d).", mCount, mAoi.getWidth());

  for (UInt64 i = 0; i<mCount; i++)
    mDelta[i] = getFloat(&parameters[36 + 4 * i]);
  * bytes_used += 4* mCount;
mFlags = MultiThreaded;
  mDeltaX = null;
}

OpcodeDeltaPerCol::~OpcodeDeltaPerCol( void )
{
  if (mDeltaX)
    delete[] mDeltaX;
  mDeltaX = null;
}


RawImage& OpcodeDeltaPerCol::createOutput(RawImage<ushort>  &in )
{
  if (mFirstPlane > in.getCpp())
    throw new RawDecoderException("OpcodeDeltaPerCol: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    throw new RawDecoderException("OpcodeDeltaPerCol: Not that many planes in actual image");

  if (in.getDataType() == TYPE_USHORT16) {
    if (mDeltaX)
      delete[] mDeltaX;
    int w = mAoi.getWidth();
mDeltaX = new int[w];
    for (int i = 0; i<w; i++)
      mDeltaX[i] = (int) (65535.0f * mDelta[i] + 0.5f);
  }
  return in;
}

void OpcodeDeltaPerCol::apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY)
{
    if (in.getDataType() == TYPE_USHORT16) {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            UInt16* src = (UInt16*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = clampbits(16, mDeltaX[x] + src[x * cpp + p]);
                }
            }
        }
    } else {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            float* src = (float*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = mDelta[x] + src[x * cpp + p];
                }
            }
        }
    }
}


OpcodeScalePerRow::OpcodeScalePerRow(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used)
{
  if (param_max_bytes< 36)
    throw new RawDecoderException("OpcodeScalePerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
mPlanes = getLong(&parameters[20]);
mRowPitch = getLong(&parameters[24]);
mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    throw new RawDecoderException("OpcodeScalePerRow: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    throw new RawDecoderException("OpcodeScalePerRow: Invalid Pitch");

mCount = getLong(&parameters[32]);
  * bytes_used = 36;
  if (param_max_bytes< 36 + (mCount*4))
    throw new RawDecoderException("OpcodeScalePerRow: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64) mAoi.getHeight() != mCount)
    throw new RawDecoderException("OpcodeScalePerRow: Element count (%llu) does not match height of area (%d).", mCount, mAoi.getHeight());

  for (UInt64 i = 0; i<mCount; i++)
    mDelta[i] = getFloat(&parameters[36 + 4 * i]);
  * bytes_used += 4* mCount;
mFlags = MultiThreaded;
}


RawImage& OpcodeScalePerRow::createOutput(RawImage<ushort>  &in )
{
  if (mFirstPlane > in.getCpp())
    throw new RawDecoderException("OpcodeScalePerRow: Not that many planes in actual image");

  if (mFirstPlane+mPlanes > in.getCpp())
    throw new RawDecoderException("OpcodeScalePerRow: Not that many planes in actual image");

  return in;
}

void OpcodeScalePerRow::apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY)
{
    if (in.getDataType() == TYPE_USHORT16) {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            UInt16* src = (UInt16*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            int delta = (int)(1024.0f * mDelta[y]);
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = clampbits(16, (delta * src[x * cpp + p] + 512) >> 10);
                }
            }
        }
    } else {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            float* src = (float*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            float delta = mDelta[y];
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = delta * src[x * cpp + p];
                }
            }
        }
    }
}


OpcodeScalePerCol::OpcodeScalePerCol(byte[] parameters, UInt32 param_max_bytes, UInt32* bytes_used)
{
  if (param_max_bytes< 36)
    throw new RawDecoderException("OpcodeScalePerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
mAoi.setAbsolute(getLong(&parameters[4]), getLong(&parameters[0]), getLong(&parameters[12]), getLong(&parameters[8]));
  mFirstPlane = getLong(&parameters[16]);
mPlanes = getLong(&parameters[20]);
mRowPitch = getLong(&parameters[24]);
mColPitch = getLong(&parameters[28]);
  if (mPlanes == 0)
    throw new RawDecoderException("OpcodeScalePerCol: Zero planes");
  if (mRowPitch == 0 || mColPitch == 0)
    throw new RawDecoderException("OpcodeScalePerCol: Invalid Pitch");

mCount = getLong(&parameters[32]);
  * bytes_used = 36;
  if (param_max_bytes< 36 + (mCount*4))
    throw new RawDecoderException("OpcodeScalePerCol: Not enough data to read parameters, only %u bytes left.", param_max_bytes);
  if ((UInt64) mAoi.getWidth() != mCount)
    throw new RawDecoderException("OpcodeScalePerCol: Element count (%llu) does not match width of area (%d).", mCount, mAoi.getWidth());

  for (UInt64 i = 0; i<mCount; i++)
    mDelta[i] = getFloat(&parameters[36 + 4 * i]);
  * bytes_used += 4* mCount;
mFlags = MultiThreaded;
  mDeltaX = null;
}

OpcodeScalePerCol::~OpcodeScalePerCol( void )
{
  if (mDeltaX)
    delete[] mDeltaX;
  mDeltaX = null;
}


RawImage<ushort>  OpcodeScalePerCol::createOutput(RawImage<ushort>  &in )
{
    if (mFirstPlane > in.getCpp())
    throw new RawDecoderException("OpcodeScalePerCol: Not that many planes in actual image");

    if (mFirstPlane + mPlanes > in.getCpp())
    throw new RawDecoderException("OpcodeScalePerCol: Not that many planes in actual image");

    if (in.getDataType() == TYPE_USHORT16) {
        if (mDeltaX)
            delete[] mDeltaX;
        int w = mAoi.getWidth();
        mDeltaX = new int[w];
        for (int i = 0; i < w; i++)
            mDeltaX[i] = (int)(1024.0f * mDelta[i]);
    }
    return in;
}

void OpcodeScalePerCol::apply(RawImage<ushort>  &in, RawImage<ushort>  &out, UInt32 startY, UInt32 endY)
{
    if (in.getDataType() == TYPE_USHORT16) {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            UInt16* src = (UInt16*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = clampbits(16, (mDeltaX[x] * src[x * cpp + p] + 512) >> 10);
                }
            }
        }
    } else {
        int cpp = out.getCpp();
        for (UInt64 y = startY; y < endY; y += mRowPitch)
        {
            float* src = (float*)out.getData(mAoi.getLeft(), y);
            // Add offset, so this is always first plane
            src += mFirstPlane;
            for (UInt64 x = 0; x < (UInt64)mAoi.getWidth(); x += mColPitch)
            {
                for (UInt64 p = 0; p < mPlanes; p++)
                {
                    src[x * cpp + p] = mDelta[x] * src[x * cpp + p];
                }
            }
        }
    }
}

}
}
*/
