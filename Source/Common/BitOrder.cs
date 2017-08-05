namespace PhotoNet.Common
{
    public enum BitOrder
    {
        Plain,  /* Memory order */
        Jpeg,   /* Input is added to stack byte by byte, and output is lifted from top */
        Jpeg16, /* Same as above, but 16 bits at the time */
        Jpeg32, /* Same as above, but 32 bits at the time */
    };
}