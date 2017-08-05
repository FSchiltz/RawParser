namespace RawNet.Jpeg
{
    public enum JpegMarker
    {       /* JPEG marker codes			*/
        Stuff = 0x00,
        Sof0 = 0xc0,  /* baseline DCT				*/
        Sof1 = 0xc1,  /* extended sequential DCT		*/
        Sof2 = 0xc2,  /* progressive DCT			*/
        Sof3 = 0xc3,  /* lossless (sequential)		*/

        Sof5 = 0xc5,  /* differential sequential DCT		*/
        Sof6 = 0xc6,  /* differential progressive DCT		*/
        Sof7 = 0xc7,  /* differential lossless		*/

        JPG = 0xc8,   /* JPEG extensions			*/
        Sof9 = 0xc9,  /* extended sequential DCT		*/
        Sof10 = 0xca, /* progressive DCT			*/
        Sof11 = 0xcb, /* lossless (sequential)		*/

        Sof13 = 0xcd, /* differential sequential DCT		*/
        Sof14 = 0xce, /* differential progressive DCT		*/
        Sof15 = 0xcf, /* differential lossless		*/

        DHT = 0xc4,   /* define Huffman tables		*/

        DAC = 0xcc,   /* define arithmetic conditioning table	*/

        Rest0 = 0xd0,  /* restart				*/
        Rest1 = 0xd1,  /* restart				*/
        Rest2 = 0xd2,  /* restart				*/
        Rest3 = 0xd3,  /* restart				*/
        Rest4 = 0xd4,  /* restart				*/
        Rest5 = 0xd5,  /* restart				*/
        Rest6 = 0xd6,  /* restart				*/
        Rest7 = 0xd7,  /* restart				*/

        SOI = 0xd8,   /* start of image			*/
        EOI = 0xd9,   /* end of image				*/
        SOS = 0xda,   /* start of scan			*/
        DQT = 0xdb,   /* define quantization tables		*/
        DNL = 0xdc,   /* define number of lines		*/
        DRI = 0xdd,   /* define restart interval		*/
        DHP = 0xde,   /* define hierarchical progression	*/
        Expand = 0xdf,   /* expand reference image(s)		*/

        App0 = 0xe0,  /* Application marker, used for JFIF	*/
        App1 = 0xe1,  /* Application marker			*/
        App2 = 0xe2,  /* Application marker			*/
        App3 = 0xe3,  /* Application marker			*/
        App4 = 0xe4,  /* Application marker			*/
        App5 = 0xe5,  /* Application marker			*/
        App6 = 0xe6,  /* Application marker			*/
        App7 = 0xe7,  /* Application marker			*/
        App8 = 0xe8,  /* Application marker			*/
        App9 = 0xe9,  /* Application marker			*/
        App10 = 0xea, /* Application marker			*/
        App11 = 0xeb, /* Application marker			*/
        App12 = 0xec, /* Application marker			*/
        App13 = 0xed, /* Application marker			*/
        App14 = 0xee, /* Application marker, used by Adobe	*/
        App15 = 0xef, /* Application marker			*/

        JPG0 = 0xf0,  /* reserved for JPEG extensions		*/
        JPG13 = 0xfd, /* reserved for JPEG extensions		*/
        Comment = 0xfe,   /* comment				*/

        Temp = 0x01,   /* temporary use			*/
        Fill = 0xFF
    };
}

