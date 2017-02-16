namespace RawNet.Jpeg
{
    /*
    * The following structure stores basic information about one component.
    */
    public struct JpegComponentInfo
    {
        /*
        * These values are fixed over the whole image.
        * They are read from the SOF marker.
        */
        public uint componentId;     /* identifier for this component (0..255) */
        public uint componentIndex;  /* its index in SOF or cPtr.compInfo[]   */

        /*
        * Huffman table selector (0..3). The value may vary
        * between scans. It is read from the SOS marker.
        */
        public uint dcTblNo;
        public uint superH; // Horizontal Supersampling
        public uint superV; // Vertical Supersampling

        public override string ToString()
        {
            return "id:" + componentId + " index:" + componentIndex + " table:" + dcTblNo + " subH:" + superH + " subV:" + superV;
        }
    };
}

