using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RawSpeed
{
    class AriDecoder : RawDecoder
    {
        protected UInt32 mWidth, mHeight, mIso;
        string mModel;
        string mEncoder;
        UInt32 mDataOffset, mDataSize;
        float[] mWB = new float[3];

        AriDecoder(FileMap file):base(file)
        {
            if (mFile.getSize() < 4096)
            {
                ThrowRDE("ARRI: File too small (no header)");
            }
            try
            {
                ByteStream s;
                if (getHostEndianness() == Endianness.little)
                {
                    s = new ByteStream(ref mFile, 8);
                }
                else
                {
                    s = new ByteStreamSwap(ref mFile, 8);
                }
                mDataOffset = s.getInt();
                UInt32 sompublicber = s.getInt(); // Value: 3?
                UInt32 segmentLength = s.getInt(); // Value: 0x3c = length
                if (sompublicber != 3 || segmentLength != 0x3c)
                {
                    ThrowRDE("Unknown values in ARRIRAW header, %d, %d", sompublicber, segmentLength);
                }
                mWidth = s.getInt();
                mHeight = s.getInt();
                s.setAbsoluteOffset(0x40);
                mDataSize = s.getInt();

                // Smells like whitebalance
                s.setAbsoluteOffset(0x5c);
                mWB[0] = s.getFloat();  // 1.3667001 in sample
                mWB[1] = s.getFloat();  // 1.0000000 in sample
                mWB[2] = s.getFloat();  // 1.6450000 in sample

                // Smells like iso
                s.setAbsoluteOffset(0xb8);
                mIso = s.getInt();  // 100 in sample

                s.setAbsoluteOffset(0x29c - 8);
                mModel = s.getString();
                s.setAbsoluteOffset(0x2a4 - 8);
                mEncoder = s.getString();
            }
            catch (IOException &e) {
                ThrowRDE("ARRI: IO Exception:%s", e.what());
            }
            }


            RawImage decodeRawInternal()
            {
                mRaw.dim = iPoint2D(mWidth, mHeight);
                mRaw.createData();

                startThreads();

                mRaw.whitePoint = 4095;
                return mRaw;
            }

            void decodeThreaded(RawDecoderThread* t)
            {
                UInt32 startOff = mDataOffset + t.start_y * ((mWidth * 12) / 8);
                BitPumpMSB32 bits(mFile, startOff);

                UInt32 hw = mWidth >> 1;
                for (UInt32 y = t.start_y; y < t.end_y; y++)
                {
                    UInt16* dest = (UInt16*)mRaw.getData(0, y);
                    for (UInt32 x = 0; x < hw; x++)
                    {
                        UInt32 a = bits.getBits(12);
                        UInt32 b = bits.getBits(12);
                        dest[x * 2] = b;
                        dest[x * 2 + 1] = a;
                        bits.checkPos();
                    }
                }
            }
            public void checkSupportInternal(CameraMetaData meta)
            {
                if (meta.hasCamera("ARRI", mModel, mEncoder))
                {
                    this.checkCameraSupported(meta, "ARRI", mModel, mEncoder);
                }
                else
                {
                    this.checkCameraSupported(meta, "ARRI", mModel, "");
                }
            }

            public void decodeMetaDataInternal(CameraMetaData meta)
            {
                mRaw.cfa.setCFA(iPoint2D(2, 2), CFA_GREEN, CFA_RED, CFA_BLUE, CFA_GREEN2);
                mRaw.metadata.wbCoeffs[0] = mWB[0];
                mRaw.metadata.wbCoeffs[1] = mWB[1];
                mRaw.metadata.wbCoeffs[2] = mWB[2];
                if (meta.hasCamera("ARRI", mModel, mEncoder))
                {
                    setMetaData(meta, "ARRI", mModel, mEncoder, mIso);
                }
                else
                {
                    setMetaData(meta, "ARRI", mModel, "", mIso);
                }
            }


        }
