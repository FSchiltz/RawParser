namespace RawNet
{
    public enum BitOrder
    {
        BitOrder_Plain,  /* Memory order */
        BitOrder_Jpeg,   /* Input is added to stack byte by byte, and output is lifted from top */
        BitOrder_Jpeg16, /* Same as above, but 16 bits at the time */
        BitOrder_Jpeg32, /* Same as above, but 32 bits at the time */
    };
}