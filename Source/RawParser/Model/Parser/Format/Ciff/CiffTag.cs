namespace RawNet.Ciff
{
    public enum CiffTag
    {
        Null = 0x0000,
        MakeModel = 0x080a,
        ShotInfo = 0x102a,
        WhiteBalance = 0x10a9,
        SensorInfo = 0x1031,
        ImageInfo = 0x1810,
        DecoderTable = 0x1835,
        RawData = 0x2005,
        Subifd = 0x300a,
        Exif = 0x300b,
    };
}
