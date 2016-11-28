using System;

namespace RawSpeed
{
    public enum CiffTag
    {
        CIFF_null = 0x0000,
        CIFF_MAKEMODEL = 0x080a,
        CIFF_SHOTINFO = 0x102a,
        CIFF_WHITEBALANCE = 0x10a9,
        CIFF_SENSORINFO = 0x1031,
        CIFF_IMAGEINFO = 0x1810,
        CIFF_DECODERTABLE = 0x1835,
        CIFF_RAWDATA = 0x2005,
        CIFF_SUBIFD = 0x300a,
        CIFF_EXIF = 0x300b,
    };
}
