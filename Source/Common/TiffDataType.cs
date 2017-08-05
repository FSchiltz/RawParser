namespace PhotoNet.Common
{
    /*
  * Tag data type information.
  *
  * Note: RATIONALs are the ratio of two 32-bit integer values.
  */
    public enum TiffDataType
    {
        NOTYPE = 0, /* placeholder */
        BYTE = 1, /* 8-bit unsigned integer */
        ASCII = 2, /* 8-bit bytes w/ last byte null */
        SHORT = 3, /* 16-bit unsigned integer */
        LONG = 4, /* 32-bit unsigned integer */
        RATIONAL = 5, /* 64-bit unsigned fraction */
        SBYTE = 6, /* !8-bit signed integer */
        UNDEFINED = 7, /* !8-bit untyped data */
        SSHORT = 8, /* !16-bit signed integer */
        SLONG = 9, /* !32-bit signed integer */
        SRATIONAL = 10, /* !64-bit signed fraction */
        FLOAT = 11, /* !32-bit IEEE floating point */
        DOUBLE = 12, /* !64-bit IEEE floating point */
        OFFSET = 13, /* 32-bit unsigned offset used in ORF at least */
    };

    public static class DataTypeMethods
    {
        public static int GetTypeSize(this TiffDataType id)
        {
            switch (id)
            {
                case TiffDataType.BYTE:
                case TiffDataType.ASCII:
                case TiffDataType.SBYTE:
                case TiffDataType.UNDEFINED:
                    return 1;
                case TiffDataType.SHORT:
                case TiffDataType.SSHORT:
                    return 2;
                case TiffDataType.LONG:
                case TiffDataType.SLONG:
                case TiffDataType.FLOAT:
                case TiffDataType.OFFSET:
                    return 4;
                case TiffDataType.RATIONAL:
                case TiffDataType.DOUBLE:
                case TiffDataType.SRATIONAL:
                    return 8;
                case TiffDataType.NOTYPE:
                default:
                    return 0;
            }
        }
    }
}
