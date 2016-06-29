// dcraw.net - camera raw file decoder
// Copyright (C) 1997-2008  Dave Coffin, dcoffin a cybercom o net
// Copyright (C) 2008-2009  Sam Webster, Dave Brown
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System.Collections.Generic;

namespace dcraw
{
    public partial class Identifier
    {
        private static string[] corp =
		        { "Canon", "NIKON", "EPSON", "KODAK", "Kodak", "OLYMPUS", "PENTAX",
		        "MINOLTA", "Minolta", "Konica", "CASIO", "Sinar", "Phase One",
		        "SAMSUNG", "Mamiya" };

        private static void AddCamera(CameraData d)
        {
            IList<CameraData> list;
            fileSizeToData.TryGetValue(d.fsize, out list);
            if (list == null)
            {
                list = new List<CameraData>();
                fileSizeToData.Add(d.fsize, list);
            }
            list.Add(d);
        }

        static Identifier()
        {
            AddCamera(new CameraData(62464, "Kodak", "DC20", false));
            AddCamera(new CameraData(124928, "Kodak", "DC20", false));
            AddCamera(new CameraData(1652736, "Kodak", "DCS200", false));
            AddCamera(new CameraData(4159302, "Kodak", "C330", false));
            AddCamera(new CameraData(4162462, "Kodak", "C330", false));
            AddCamera(new CameraData(460800, "Kodak", "C603v", false));
            AddCamera(new CameraData(614400, "Kodak", "C603v", false));
            AddCamera(new CameraData(6163328, "Kodak", "C603", false));
            AddCamera(new CameraData(6166488, "Kodak", "C603", false));
            AddCamera(new CameraData(9116448, "Kodak", "C603y", false));
            AddCamera(new CameraData(311696, "ST Micro", "STV680 VGA", false)); /* SPYz */
            AddCamera(new CameraData(614400, "Kodak", "KAI-0340", false));
            AddCamera(new CameraData(787456, "Creative", "PC-CAM 600", false));
            AddCamera(new CameraData(1138688, "Minolta", "RD175", false));
            AddCamera(new CameraData(3840000, "Foculus", "531C", false));
            AddCamera(new CameraData(786432, "AVT", "F-080C", false));
            AddCamera(new CameraData(1447680, "AVT", "F-145C", false));
            AddCamera(new CameraData(1920000, "AVT", "F-201C", false));
            AddCamera(new CameraData(5067304, "AVT", "F-510C", false));
            AddCamera(new CameraData(10134608, "AVT", "F-510C", false));
            AddCamera(new CameraData(16157136, "AVT", "F-810C", false));
            AddCamera(new CameraData(1409024, "Sony", "XCD-SX910CR", false));
            AddCamera(new CameraData(2818048, "Sony", "XCD-SX910CR", false));
            AddCamera(new CameraData(3884928, "Micron", "2010", false));
            AddCamera(new CameraData(6624000, "Pixelink", "A782", false));
            AddCamera(new CameraData(13248000, "Pixelink", "A782", false));
            AddCamera(new CameraData(6291456, "RoverShot", "3320AF", false));
            AddCamera(new CameraData(6553440, "Canon", "PowerShot A460", false));
            AddCamera(new CameraData(6653280, "Canon", "PowerShot A530", false));
            AddCamera(new CameraData(6573120, "Canon", "PowerShot A610", false));
            AddCamera(new CameraData(9219600, "Canon", "PowerShot A620", false));
            AddCamera(new CameraData(10341600, "Canon", "PowerShot A720", false));
            AddCamera(new CameraData(10383120, "Canon", "PowerShot A630", false));
            AddCamera(new CameraData(12945240, "Canon", "PowerShot A640", false));
            AddCamera(new CameraData(15636240, "Canon", "PowerShot A650", false));
            AddCamera(new CameraData(5298000, "Canon", "PowerShot SD300", false));
            AddCamera(new CameraData(7710960, "Canon", "PowerShot S3 IS", false));
            AddCamera(new CameraData(5939200, "OLYMPUS", "C770UZ", false));
            AddCamera(new CameraData(1581060, "NIKON", "E900", true)); /* or E900s,E910 */
            AddCamera(new CameraData(2465792, "NIKON", "E950", true)); /* or E800,E700 */
            AddCamera(new CameraData(2940928, "NIKON", "E2100", true)); /* or E2500 */
            AddCamera(new CameraData(4771840, "NIKON", "E990", true)); /* or E995, Oly C3030Z */
            AddCamera(new CameraData(4775936, "NIKON", "E3700", true)); /* or Optio 33WR */
            AddCamera(new CameraData(5869568, "NIKON", "E4300", true)); /* or DiMAGE Z2 */
            AddCamera(new CameraData(5865472, "NIKON", "E4500", true));
            AddCamera(new CameraData(7438336, "NIKON", "E5000", true)); /* or E5700 */
            AddCamera(new CameraData(8998912, "NIKON", "COOLPIX S6", true));
            AddCamera(new CameraData(1976352, "CASIO", "QV-2000UX", true));
            AddCamera(new CameraData(3217760, "CASIO", "QV-3*00EX", true));
            AddCamera(new CameraData(6218368, "CASIO", "QV-5700", true));
            AddCamera(new CameraData(6054400, "CASIO", "QV-R41", true));
            AddCamera(new CameraData(7530816, "CASIO", "QV-R51", true));
            AddCamera(new CameraData(7684000, "CASIO", "QV-4000", true));
            AddCamera(new CameraData(4948608, "CASIO", "EX-S100", true));
            AddCamera(new CameraData(7542528, "CASIO", "EX-Z50", true));
            AddCamera(new CameraData(7753344, "CASIO", "EX-Z55", true));
            AddCamera(new CameraData(7426656, "CASIO", "EX-P505", true));
            AddCamera(new CameraData(9313536, "CASIO", "EX-P600", true));
            AddCamera(new CameraData(10979200, "CASIO", "EX-P700", true));
            AddCamera(new CameraData(3178560, "PENTAX", "Optio S", true));
            AddCamera(new CameraData(4841984, "PENTAX", "Optio S", true));
            AddCamera(new CameraData(6114240, "PENTAX", "Optio S4", true)); /* or S4i, CASIO EX-Z4 */
            AddCamera(new CameraData(10702848, "PENTAX", "Optio 750Z", true));
            AddCamera(new CameraData(12582980, "Sinar", "", false));
            AddCamera(new CameraData(33292868, "Sinar", "", false));
            AddCamera(new CameraData(44390468, "Sinar", "", false));
        }

        private readonly AdobeCoeff[] coeffTable = new[]
                                                       {
                                                           new AdobeCoeff("Apple QuickTake", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  17576, -3191, -3318, 5210, 6733, -1942,
                                                                                  9031, 1280, -124
                                                                              }), /* DJC */
                                                           new AdobeCoeff("Canon EOS D2000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  24542, -10860, -3401, -1490, 11370, -297,
                                                                                  2858, -605, 3225
                                                                              }),
                                                           new AdobeCoeff("Canon EOS D6000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  20482, -7172, -3125, -1033, 10410, -285,
                                                                                  2542, 226, 3136
                                                                              }),
                                                           new AdobeCoeff("Canon EOS D30", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9805, -2689, -1312, -5803, 13064, 3068,
                                                                                  -2438, 3075, 8775
                                                                              }),
                                                           new AdobeCoeff("Canon EOS D60", 0, 0xfa0,
                                                                          new short[]
                                                                              {
                                                                                  6188, -1341, -890, -7168, 14489, 2937,
                                                                                  -2640, 3228, 8483
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 5D", 0, 0xe6c,
                                                                          new short[]
                                                                              {
                                                                                  6347, -479, -972, -8297, 15954, 2480,
                                                                                  -1968, 2131, 7649
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 10D", 0, 0xfa0,
                                                                          new short[]
                                                                              {
                                                                                  8197, -2000, -1118, -6714, 14335, 2592,
                                                                                  -2536, 3178, 8266
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 20Da", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  14155, -5065, -1382, -6550, 14633, 2039,
                                                                                  -1623, 1824, 6561
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 20D", 0, 0xfff,
                                                                          new short[]
                                                                              {
                                                                                  6599, -537, -891, -8071, 15783, 2424,
                                                                                  -1983, 2234, 7462
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 30D", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  6257, -303, -1000, -7880, 15621, 2396,
                                                                                  -1714, 1904, 7046
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 40D", 0, 0x3f60,
                                                                          new short[]
                                                                              {
                                                                                  6071, -747, -856, -7653, 15365, 2441,
                                                                                  -2025, 2553, 7315
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 300D", 0, 0xfa0,
                                                                          new short[]
                                                                              {
                                                                                  8197, -2000, -1118, -6714, 14335, 2592,
                                                                                  -2536, 3178, 8266
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 350D", 0, 0xfff,
                                                                          new short[]
                                                                              {
                                                                                  6018, -617, -965, -8645, 15881, 2975,
                                                                                  -1530, 1719, 7642
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 400D", 0, 0xe8e,
                                                                          new short[]
                                                                              {
                                                                                  7054, -1501, -990, -8156, 15544, 2812,
                                                                                  -1278, 1414, 7796
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 450D", 0, 0x390d,
                                                                          new short[]
                                                                              {
                                                                                  5784, -262, -821, -7539, 15064, 2672,
                                                                                  -1982, 2681, 7427
                                                                              }),
                                                           new AdobeCoeff("Canon EOS 1000D", 0, 0xe43,
                                                                          new short[]
                                                                              {
                                                                                  7054, -1501, -990, -8156, 15544, 2812,
                                                                                  -1278, 1414, 7796
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1Ds Mark III", 0, 0x3bb0,
                                                                          new short[]
                                                                              {
                                                                                  5859, -211, -930, -8255, 16017, 2353,
                                                                                  -1732, 1887, 7448
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1Ds Mark II", 0, 0xe80,
                                                                          new short[]
                                                                              {
                                                                                  6517, -602, -867, -8180, 15926, 2378,
                                                                                  -1618, 1771, 7633
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1D Mark II N", 0, 0xe80,
                                                                          new short[]
                                                                              {
                                                                                  6240, -466, -822, -8180, 15825, 2500,
                                                                                  -1801, 1938, 8042
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1D Mark III", 0, 0x3bb0,
                                                                          new short[]
                                                                              {
                                                                                  6291, -540, -976, -8350, 16145, 2311,
                                                                                  -1714, 1858, 7326
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1D Mark II", 0, 0xe80,
                                                                          new short[]
                                                                              {
                                                                                  6264, -582, -724, -8312, 15948, 2504,
                                                                                  -1744, 1919, 8664
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1DS", 0, 0xe20,
                                                                          new short[]
                                                                              {
                                                                                  4374, 3631, -1743, -7520, 15212, 2472,
                                                                                  -2892, 3632, 8161
                                                                              }),
                                                           new AdobeCoeff("Canon EOS-1D", 0, 0xe20,
                                                                          new short[]
                                                                              {
                                                                                  6806, -179, -1020, -8097, 16415, 1687,
                                                                                  -3267, 4236, 7690
                                                                              }),
                                                           new AdobeCoeff("Canon EOS", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8197, -2000, -1118, -6714, 14335, 2592,
                                                                                  -2536, 3178, 8266
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A50", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -5300, 9846, 1776, 3436, 684, 3939, -5540
                                                                                  , 9879, 6200, -1404, 11175, 217
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A5", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -4801, 9475, 1952, 2926, 1611, 4094,
                                                                                  -5259, 10164, 5947, -1554, 10883, 547
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot G1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -4778, 9467, 2172, 4743, -1141, 4344,
                                                                                  -5146, 9908, 6077, -1566, 11051, 557
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot G2", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9087, -2693, -1049, -6715, 14382, 2537,
                                                                                  -2291, 2819, 7790
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot G3", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9212, -2781, -1073, -6573, 14189, 2605,
                                                                                  -2300, 2844, 7664
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot G5", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9757, -2872, -933, -5972, 13861, 2301,
                                                                                  -1622, 2328, 7212
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot G6", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9877, -3775, -871, -7613, 14807, 3072,
                                                                                  -1448, 1305, 7485
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot G9", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7368, -2141, -598, -5621, 13254, 2625,
                                                                                  -1418, 1696, 5743
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot Pro1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10062, -3522, -999, -7643, 15117, 2730,
                                                                                  -765, 817, 7323
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot Pro70", 34, 0,
                                                                          new short[]
                                                                              {
                                                                                  -4155, 9818, 1529, 3939, -25, 4522, -5521
                                                                                  , 9870, 6610, -2238, 10873, 1342
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot Pro90", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -4963, 9896, 2235, 4642, -987, 4294,
                                                                                  -5162, 10011, 5859, -1770, 11230, 577
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S30", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10566, -3652, -1129, -6552, 14662, 2006,
                                                                                  -2197, 2581, 7670
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S40", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8510, -2487, -940, -6869, 14231, 2900,
                                                                                  -2318, 2829, 9013
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S45", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8163, -2333, -955, -6682, 14174, 2751,
                                                                                  -2077, 2597, 8041
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S50", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8882, -2571, -863, -6348, 14234, 2288,
                                                                                  -1516, 2172, 6569
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S60", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8795, -2482, -797, -7804, 15403, 2573,
                                                                                  -1422, 1996, 7082
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S70", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9976, -3810, -832, -7115, 14463, 2906,
                                                                                  -901, 989, 7889
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A610", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  15591, -6402, -1592, -5365, 13198, 2168,
                                                                                  -1300, 1824, 5075
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A620", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  15265, -6193, -1558, -4125, 12116, 2010,
                                                                                  -888, 1639, 5220
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A630", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  14201, -5308, -1757, -6087, 14472, 1617,
                                                                                  -2191, 3105, 5348
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A640", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  13124, -5329, -1390, -3602, 11658, 1944,
                                                                                  -1612, 2863, 4885
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A650", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  9427, -3036, -959, -2581, 10671, 1911,
                                                                                  -1039, 1982, 4430
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot A720", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  14573, -5482, -1546, -1266, 9799, 1468,
                                                                                  -1040, 1912, 3810
                                                                              }),
                                                           new AdobeCoeff("Canon PowerShot S3 IS", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  14062, -5199, -1446, -4712, 12470, 2243,
                                                                                  -1286, 2028, 4836
                                                                              }),
                                                           new AdobeCoeff("CINE 650", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  3390, 480, -500, -800, 3610, 340, -550,
                                                                                  2336, 1192
                                                                              }),
                                                           new AdobeCoeff("CINE 660", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  3390, 480, -500, -800, 3610, 340, -550,
                                                                                  2336, 1192
                                                                              }),
                                                           new AdobeCoeff("CINE", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  20183, -4295, -423, -3940, 15330, 3985,
                                                                                  -280, 4870, 9800
                                                                              }),
                                                           new AdobeCoeff("Contax N Digital", 0, 0xf1e,
                                                                          new short[]
                                                                              {
                                                                                  7777, 1285, -1053, -9280, 16543, 2916,
                                                                                  -3677, 5679, 7060
                                                                              }),
                                                           new AdobeCoeff("EPSON R-D1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  6827, -1878, -732, -8429, 16012, 2564,
                                                                                  -704, 592, 7145
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix E550", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11044, -3888, -1120, -7248, 15168, 2208,
                                                                                  -1531, 2277, 8069
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix E900", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9183, -2526, -1078, -7461, 15071, 2574,
                                                                                  -2022, 2440, 8639
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix F8", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11044, -3888, -1120, -7248, 15168, 2208,
                                                                                  -1531, 2277, 8069
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix F7", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10004, -3219, -1201, -7036, 15047, 2107,
                                                                                  -1863, 2565, 7736
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S100FS", 514, 0,
                                                                          new short[]
                                                                              {
                                                                                  11521, -4355, -1065, -6524, 13767, 3058,
                                                                                  -1466, 1984, 6045
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S20Pro", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10004, -3219, -1201, -7036, 15047, 2107,
                                                                                  -1863, 2565, 7736
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S2Pro", 128, 0,
                                                                          new short[]
                                                                              {
                                                                                  12492, -4690, -1402, -7033, 15423, 1647,
                                                                                  -1507, 2111, 7697
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S3Pro", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11807, -4612, -1294, -8927, 16968, 1988,
                                                                                  -2120, 2741, 8006
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S5Pro", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  12300, -5110, -1304, -9117, 17143, 1998,
                                                                                  -1947, 2448, 8100
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S5000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8754, -2732, -1019, -7204, 15069, 2276,
                                                                                  -1702, 2334, 6982
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S5100", 0, 0x3e00,
                                                                          new short[]
                                                                              {
                                                                                  11940, -4431, -1255, -6766, 14428, 2542,
                                                                                  -993, 1165, 7421
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S5500", 0, 0x3e00,
                                                                          new short[]
                                                                              {
                                                                                  11940, -4431, -1255, -6766, 14428, 2542,
                                                                                  -993, 1165, 7421
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S5200", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9636, -2804, -988, -7442, 15040, 2589,
                                                                                  -1803, 2311, 8621
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S5600", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9636, -2804, -988, -7442, 15040, 2589,
                                                                                  -1803, 2311, 8621
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S6", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  12628, -4887, -1401, -6861, 14996, 1962,
                                                                                  -2198, 2782, 7091
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S7000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10190, -3506, -1312, -7153, 15051, 2238,
                                                                                  -2003, 2399, 7505
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S9000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10491, -3423, -1145, -7385, 15027, 2538,
                                                                                  -1809, 2275, 8692
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S9500", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10491, -3423, -1145, -7385, 15027, 2538,
                                                                                  -1809, 2275, 8692
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S9100", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  12343, -4515, -1285, -7165, 14899, 2435,
                                                                                  -1895, 2496, 8800
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM FinePix S9600", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  12343, -4515, -1285, -7165, 14899, 2435,
                                                                                  -1895, 2496, 8800
                                                                              }),
                                                           new AdobeCoeff("FUJIFILM IS-1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  21461, -10807, -1441, -2332, 10599, 1999,
                                                                                  289, 875, 7703
                                                                              }),
                                                           new AdobeCoeff("Imacon Ixpress", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  7025, -1415, -704, -5188, 13765, 1424,
                                                                                  -1248, 2742, 6038
                                                                              }),
                                                           new AdobeCoeff("KODAK NC2000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  13891, -6055, -803, -465, 9919, 642, 2121
                                                                                  , 82, 1291
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS315C", 8, 0,
                                                                          new short[]
                                                                              {
                                                                                  17523, -4827, -2510, 756, 8546, -137,
                                                                                  6113, 1649, 2250
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS330C", 8, 0,
                                                                          new short[]
                                                                              {
                                                                                  20620, -7572, -2801, -103, 10073, -396,
                                                                                  3551, -233, 2220
                                                                              }),
                                                           new AdobeCoeff("KODAK DCS420", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10868, -1852, -644, -1537, 11083, 484,
                                                                                  2343, 628, 2216
                                                                              }),
                                                           new AdobeCoeff("KODAK DCS460", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10592, -2206, -967, -1944, 11685, 230,
                                                                                  2206, 670, 1273
                                                                              }),
                                                           new AdobeCoeff("KODAK EOSDCS1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10592, -2206, -967, -1944, 11685, 230,
                                                                                  2206, 670, 1273
                                                                              }),
                                                           new AdobeCoeff("KODAK EOSDCS3B", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9898, -2700, -940, -2478, 12219, 206,
                                                                                  1985, 634, 1031
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS520C", 180, 0,
                                                                          new short[]
                                                                              {
                                                                                  24542, -10860, -3401, -1490, 11370, -297,
                                                                                  2858, -605, 3225
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS560C", 188, 0,
                                                                          new short[]
                                                                              {
                                                                                  20482, -7172, -3125, -1033, 10410, -285,
                                                                                  2542, 226, 3136
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS620C", 180, 0,
                                                                          new short[]
                                                                              {
                                                                                  23617, -10175, -3149, -2054, 11749, -272,
                                                                                  2586, -489, 3453
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS620X", 185, 0,
                                                                          new short[]
                                                                              {
                                                                                  13095, -6231, 154, 12221, -21, -2137, 895
                                                                                  , 4602, 2258
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS660C", 214, 0,
                                                                          new short[]
                                                                              {
                                                                                  18244, -6351, -2739, -791, 11193, -521,
                                                                                  3711, -129, 2802
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS720X", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11775, -5884, 950, 9556, 1846, -1286,
                                                                                  -1019, 6221, 2728
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS760C", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  16623, -6309, -1411, -4344, 13923, 323,
                                                                                  2285, 274, 2926
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS Pro SLR", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  5494, 2393, -232, -6427, 13850, 2846,
                                                                                  -1876, 3997, 5445
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS Pro 14nx", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  5494, 2393, -232, -6427, 13850, 2846,
                                                                                  -1876, 3997, 5445
                                                                              }),
                                                           new AdobeCoeff("Kodak DCS Pro 14", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7791, 3128, -776, -8588, 16458, 2039,
                                                                                  -2455, 4006, 6198
                                                                              }),
                                                           new AdobeCoeff("Kodak ProBack645", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  16414, -6060, -1470, -3555, 13037, 473,
                                                                                  2545, 122, 4948
                                                                              }),
                                                           new AdobeCoeff("Kodak ProBack", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  21179, -8316, -2918, -915, 11019, -165,
                                                                                  3477, -180, 4210
                                                                              }),
                                                           new AdobeCoeff("KODAK P712", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9658, -3314, -823, -5163, 12695, 2768,
                                                                                  -1342, 1843, 6044
                                                                              }),
                                                           new AdobeCoeff("KODAK P850", 0, 0xf7c,
                                                                          new short[]
                                                                              {
                                                                                  10511, -3836, -1102, -6946, 14587, 2558,
                                                                                  -1481, 1792, 6246
                                                                              }),
                                                           new AdobeCoeff("KODAK P880", 0, 0xfff,
                                                                          new short[]
                                                                              {
                                                                                  12805, -4662, -1376, -7480, 15267, 2360,
                                                                                  -1626, 2194, 7904
                                                                              }),
                                                           new AdobeCoeff("Leaf CMost", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  3952, 2189, 449, -6701, 14585, 2275,
                                                                                  -4536, 7349, 6536
                                                                              }),
                                                           new AdobeCoeff("Leaf Valeo 6", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  3952, 2189, 449, -6701, 14585, 2275,
                                                                                  -4536, 7349, 6536
                                                                              }),
                                                           new AdobeCoeff("Leaf Aptus 54S", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8236, 1746, -1314, -8251, 15953, 2428,
                                                                                  -3673, 5786, 5771
                                                                              }),
                                                           new AdobeCoeff("Leaf Aptus 65", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7914, 1414, -1190, -8777, 16582, 2280,
                                                                                  -2811, 4605, 5562
                                                                              }),
                                                           new AdobeCoeff("Leaf Aptus 75", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7914, 1414, -1190, -8777, 16582, 2280,
                                                                                  -2811, 4605, 5562
                                                                              }),
                                                           new AdobeCoeff("Leaf", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8236, 1746, -1314, -8251, 15953, 2428,
                                                                                  -3673, 5786, 5771
                                                                              }),
                                                           new AdobeCoeff("Mamiya ZD", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7645, 2579, -1363, -8689, 16717, 2015,
                                                                                  -3712, 5941, 5961
                                                                              }),
                                                           new AdobeCoeff("Micron 2010", 110, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  16695, -3761, -2151, 155, 9682, 163, 3433
                                                                                  , 951, 4904
                                                                              }),
                                                           new AdobeCoeff("Minolta DiMAGE 5", 0, 0xf7d,
                                                                          new short[]
                                                                              {
                                                                                  8983, -2942, -963, -6556, 14476, 2237,
                                                                                  -2426, 2887, 8014
                                                                              }),
                                                           new AdobeCoeff("Minolta DiMAGE 7Hi", 0, 0xf7d,
                                                                          new short[]
                                                                              {
                                                                                  11368, -3894, -1242, -6521, 14358, 2339,
                                                                                  -2475, 3056, 7285
                                                                              }),
                                                           new AdobeCoeff("Minolta DiMAGE 7", 0, 0xf7d,
                                                                          new short[]
                                                                              {
                                                                                  9144, -2777, -998, -6676, 14556, 2281,
                                                                                  -2470, 3019, 7744
                                                                              }),
                                                           new AdobeCoeff("Minolta DiMAGE A1", 0, 0xf8b,
                                                                          new short[]
                                                                              {
                                                                                  9274, -2547, -1167, -8220, 16323, 1943,
                                                                                  -2273, 2720, 8340
                                                                              }),
                                                           new AdobeCoeff("MINOLTA DiMAGE A200", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8560, -2487, -986, -8112, 15535, 2771,
                                                                                  -1209, 1324, 7743
                                                                              }),
                                                           new AdobeCoeff("Minolta DiMAGE A2", 0, 0xf8f,
                                                                          new short[]
                                                                              {
                                                                                  9097, -2726, -1053, -8073, 15506, 2762,
                                                                                  -966, 981, 7763
                                                                              }),
                                                           new AdobeCoeff("Minolta DiMAGE Z2", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  11280, -3564, -1370, -4655, 12374, 2282,
                                                                                  -1423, 2168, 5396
                                                                              }),
                                                           new AdobeCoeff("MINOLTA DYNAX 5", 0, 0xffb,
                                                                          new short[]
                                                                              {
                                                                                  10284, -3283, -1086, -7957, 15762, 2316,
                                                                                  -829, 882, 6644
                                                                              }),
                                                           new AdobeCoeff("MINOLTA DYNAX 7", 0, 0xffb,
                                                                          new short[]
                                                                              {
                                                                                  10239, -3104, -1099, -8037, 15727, 2451,
                                                                                  -927, 925, 6871
                                                                              }),
                                                           new AdobeCoeff("NIKON D100", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  5902, -933, -782, -8983, 16719, 2354,
                                                                                  -1402, 1455, 6464
                                                                              }),
                                                           new AdobeCoeff("NIKON D1H", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7577, -2166, -926, -7454, 15592, 1934,
                                                                                  -2377, 2808, 8606
                                                                              }),
                                                           new AdobeCoeff("NIKON D1X", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7702, -2245, -975, -9114, 17242, 1875,
                                                                                  -2679, 3055, 8521
                                                                              }),
                                                           new AdobeCoeff("NIKON D1", 0, 0,
                                                                          /* multiplied by 2.218750, 1.0, 1.148438 */
                                                                          new short[]
                                                                              {
                                                                                  16772, -4726, -2141, -7611, 15713, 1972,
                                                                                  -2846, 3494, 9521
                                                                              }),
                                                           new AdobeCoeff("NIKON D2H", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  5710, -901, -615, -8594, 16617, 2024,
                                                                                  -2975, 4120, 6830
                                                                              }),
                                                           new AdobeCoeff("NIKON D2X", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10231, -2769, -1255, -8301, 15900, 2552,
                                                                                  -797, 680, 7148
                                                                              }),
                                                           new AdobeCoeff("NIKON D40X", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8819, -2543, -911, -9025, 16928, 2151,
                                                                                  -1329, 1213, 8449
                                                                              }),
                                                           new AdobeCoeff("NIKON D40", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  6992, -1668, -806, -8138, 15748, 2543,
                                                                                  -874, 850, 7897
                                                                              }),
                                                           new AdobeCoeff("NIKON D50", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7732, -2422, -789, -8238, 15884, 2498,
                                                                                  -859, 783, 7330
                                                                              }),
                                                           new AdobeCoeff("NIKON D60", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8736, -2458, -935, -9075, 16894, 2251,
                                                                                  -1354, 1242, 8263
                                                                              }),
                                                           new AdobeCoeff("NIKON D700", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8139, -2171, -663, -8747, 16541, 2295,
                                                                                  -1925, 2008, 8093
                                                                              }),
                                                           new AdobeCoeff("NIKON D70", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7732, -2422, -789, -8238, 15884, 2498,
                                                                                  -859, 783, 7330
                                                                              }),
                                                           new AdobeCoeff("NIKON D80", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8629, -2410, -883, -9055, 16940, 2171,
                                                                                  -1490, 1363, 8520
                                                                              }),
                                                           new AdobeCoeff("NIKON D90", 0, 0xf00, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  9692, -2519, -831, -5396, 13053, 2344,
                                                                                  -1818, 2682, 7084
                                                                              }),
                                                           new AdobeCoeff("NIKON D200", 0, 0xfbc,
                                                                          new short[]
                                                                              {
                                                                                  8367, -2248, -763, -8758, 16447, 2422,
                                                                                  -1527, 1550, 8053
                                                                              }),
                                                           new AdobeCoeff("NIKON D300", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9030, -1992, -715, -8465, 16302, 2255,
                                                                                  -2689, 3217, 8069
                                                                              }),
                                                           new AdobeCoeff("NIKON D3", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8139, -2171, -663, -8747, 16541, 2295,
                                                                                  -1925, 2008, 8093
                                                                              }),
                                                           new AdobeCoeff("NIKON E950", 0, 0x3dd, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  -3746, 10611, 1665, 9621, -1734, 2114,
                                                                                  -2389, 7082, 3064, 3406, 6116, -244
                                                                              }),
                                                           new AdobeCoeff("NIKON E995", 0, 0, /* copied from E5000 */
                                                                          new short[]
                                                                              {
                                                                                  -5547, 11762, 2189, 5814, -558, 3342,
                                                                                  -4924, 9840, 5949, 688, 9083, 96
                                                                              }),
                                                           new AdobeCoeff("NIKON E2100", 0, 0,
                                                                          /* copied from Z2, new white balance */
                                                                          new short[]
                                                                              {
                                                                                  13142, -4152, -1596, -4655, 12374, 2282,
                                                                                  -1769, 2696, 6711
                                                                              }),
                                                           new AdobeCoeff("NIKON E2500", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -5547, 11762, 2189, 5814, -558, 3342,
                                                                                  -4924, 9840, 5949, 688, 9083, 96
                                                                              }),
                                                           new AdobeCoeff("NIKON E4300", 0, 0,
                                                                          /* copied from Minolta DiMAGE Z2 */
                                                                          new short[]
                                                                              {
                                                                                  11280, -3564, -1370, -4655, 12374, 2282,
                                                                                  -1423, 2168, 5396
                                                                              }),
                                                           new AdobeCoeff("NIKON E4500", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -5547, 11762, 2189, 5814, -558, 3342,
                                                                                  -4924, 9840, 5949, 688, 9083, 96
                                                                              }),
                                                           new AdobeCoeff("NIKON E5000", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -5547, 11762, 2189, 5814, -558, 3342,
                                                                                  -4924, 9840, 5949, 688, 9083, 96
                                                                              }),
                                                           new AdobeCoeff("NIKON E5400", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9349, -2987, -1001, -7919, 15766, 2266,
                                                                                  -2098, 2680, 6839
                                                                              }),
                                                           new AdobeCoeff("NIKON E5700", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  -5368, 11478, 2368, 5537, -113, 3148,
                                                                                  -4969, 10021, 5782, 778, 9028, 211
                                                                              }),
                                                           new AdobeCoeff("NIKON E8400", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7842, -2320, -992, -8154, 15718, 2599,
                                                                                  -1098, 1342, 7560
                                                                              }),
                                                           new AdobeCoeff("NIKON E8700", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8489, -2583, -1036, -8051, 15583, 2643,
                                                                                  -1307, 1407, 7354
                                                                              }),
                                                           new AdobeCoeff("NIKON E8800", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7971, -2314, -913, -8451, 15762, 2894,
                                                                                  -1442, 1520, 7610
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS C5050", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10508, -3124, -1273, -6079, 14294, 1901,
                                                                                  -1653, 2306, 6237
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS C5060", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10445, -3362, -1307, -7662, 15690, 2058,
                                                                                  -1135, 1176, 7602
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS C7070", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10252, -3531, -1095, -7114, 14850, 2436,
                                                                                  -1451, 1723, 6365
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS C70", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10793, -3791, -1146, -7498, 15177, 2488,
                                                                                  -1390, 1577, 7321
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS C80", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8606, -2509, -1014, -8238, 15714, 2703,
                                                                                  -942, 979, 7760
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-10", 0, 0xffc0,
                                                                          new short[]
                                                                              {
                                                                                  12745, -4500, -1416, -6062, 14542, 1580,
                                                                                  -1934, 2256, 6603
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-1", 0, 0xfff0,
                                                                          new short[]
                                                                              {
                                                                                  11846, -4767, -945, -7027, 15878, 1089,
                                                                                  -2699, 4122, 8311
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-20", 0, 0xffc0,
                                                                          new short[]
                                                                              {
                                                                                  13173, -4732, -1499, -5807, 14036, 1895,
                                                                                  -2045, 2452, 7142
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-300", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7828, -1761, -348, -5788, 14071, 1830,
                                                                                  -2853, 4518, 6557
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-330", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8961, -2473, -1084, -7979, 15990, 2067,
                                                                                  -2319, 3035, 8249
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-3", 0, 0xf99,
                                                                          new short[]
                                                                              {
                                                                                  9487, -2875, -1115, -7533, 15606, 2010,
                                                                                  -1618, 2100, 7389
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-400", 0, 0xfff0,
                                                                          new short[]
                                                                              {
                                                                                  6169, -1483, -21, -7107, 14761, 2536,
                                                                                  -2904, 3580, 8568
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-410", 0, 0xf6a,
                                                                          new short[]
                                                                              {
                                                                                  8856, -2582, -1026, -7761, 15766, 2082,
                                                                                  -2009, 2575, 7469
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-420", 0, 0xfd7,
                                                                          new short[]
                                                                              {
                                                                                  8746, -2425, -1095, -7594, 15612, 2073,
                                                                                  -1780, 2309, 7416
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-500", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  8136, -1968, -299, -5481, 13742, 1871,
                                                                                  -2556, 4205, 6630
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-510", 0, 0xf6a,
                                                                          new short[]
                                                                              {
                                                                                  8785, -2529, -1033, -7639, 15624, 2112,
                                                                                  -1783, 2300, 7817
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS E-520", 0, 0xfd2,
                                                                          new short[]
                                                                              {
                                                                                  8344, -2322, -1020, -7596, 15635, 2048,
                                                                                  -1748, 2269, 7287
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP350", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  12078, -4836, -1069, -6671, 14306, 2578,
                                                                                  -786, 939, 7418
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP3", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11766, -4445, -1067, -6901, 14421, 2707,
                                                                                  -1029, 1217, 7572
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP500UZ", 0, 0xfff,
                                                                          new short[]
                                                                              {
                                                                                  9493, -3415, -666, -5211, 12334, 3260,
                                                                                  -1548, 2262, 6482
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP510UZ", 0, 0xffe,
                                                                          new short[]
                                                                              {
                                                                                  10593, -3607, -1010, -5881, 13127, 3084,
                                                                                  -1200, 1805, 6721
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP550UZ", 0, 0xffe,
                                                                          new short[]
                                                                              {
                                                                                  11597, -4006, -1049, -5432, 12799, 2957,
                                                                                  -1029, 1750, 6516
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP560UZ", 0, 0xff9,
                                                                          new short[]
                                                                              {
                                                                                  10915, -3677, -982, -5587, 12986, 2911,
                                                                                  -1168, 1968, 6223
                                                                              }),
                                                           new AdobeCoeff("OLYMPUS SP570UZ", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11522, -4044, -1146, -4736, 12172, 2904,
                                                                                  -988, 1829, 6039
                                                                              }),
                                                           new AdobeCoeff("PENTAX *ist DL2", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10504, -2438, -1189, -8603, 16207, 2531,
                                                                                  -1022, 863, 12242
                                                                              }),
                                                           new AdobeCoeff("PENTAX *ist DL", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10829, -2838, -1115, -8339, 15817, 2696,
                                                                                  -837, 680, 11939
                                                                              }),
                                                           new AdobeCoeff("PENTAX *ist DS2", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10504, -2438, -1189, -8603, 16207, 2531,
                                                                                  -1022, 863, 12242
                                                                              }),
                                                           new AdobeCoeff("PENTAX *ist DS", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10371, -2333, -1206, -8688, 16231, 2602,
                                                                                  -1230, 1116, 11282
                                                                              }),
                                                           new AdobeCoeff("PENTAX *ist D", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9651, -2059, -1189, -8881, 16512, 2487,
                                                                                  -1460, 1345, 10687
                                                                              }),
                                                           new AdobeCoeff("PENTAX K10D", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9566, -2863, -803, -7170, 15172, 2112,
                                                                                  -818, 803, 9705
                                                                              }),
                                                           new AdobeCoeff("PENTAX K1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  11095, -3157, -1324, -8377, 15834, 2720,
                                                                                  -1108, 947, 11688
                                                                              }),
                                                           new AdobeCoeff("PENTAX K20D", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9427, -2714, -868, -7493, 16092, 1373,
                                                                                  -2199, 3264, 7180
                                                                              }),
                                                           new AdobeCoeff("PENTAX K200D", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9186, -2678, -907, -8693, 16517, 2260,
                                                                                  -1129, 1094, 8524
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-FZ8", 0, 0xf7f0,
                                                                          new short[]
                                                                              {
                                                                                  8986, -2755, -802, -6341, 13575, 3077,
                                                                                  -1476, 2144, 6379
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-FZ18", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9932, -3060, -935, -5809, 13331, 2753,
                                                                                  -1267, 2155, 5575
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-FZ30", 0, 0xf94c,
                                                                          new short[]
                                                                              {
                                                                                  10976, -4029, -1141, -7918, 15491, 2600,
                                                                                  -1670, 2071, 8246
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-FZ50", 0, 0xfff0,
                                                                          /* aka "LEICA V-LUX1" */
                                                                          new short[]
                                                                              {
                                                                                  7906, -2709, -594, -6231, 13351, 3220,
                                                                                  -1922, 2631, 6537
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-L10", 15, 0xf96,
                                                                          new short[]
                                                                              {
                                                                                  8025, -1942, -1050, -7920, 15904, 2100,
                                                                                  -2456, 3005, 7039
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-L1", 0, 0xf7fc,
                                                                          /* aka "LEICA DIGILUX 3" */
                                                                          new short[]
                                                                              {
                                                                                  8054, -1885, -1025, -8349, 16367, 2040,
                                                                                  -2805, 3542, 7629
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-LC1", 0, 0,
                                                                          /* aka "LEICA DIGILUX 2" */
                                                                          new short[]
                                                                              {
                                                                                  11340, -4069, -1275, -7555, 15266, 2448,
                                                                                  -2960, 3426, 7685
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-LX1", 0, 0xf7f0,
                                                                          /* aka "LEICA D-LUX2" */
                                                                          new short[]
                                                                              {
                                                                                  10704, -4187, -1230, -8314, 15952, 2501,
                                                                                  -920, 945, 8927
                                                                              }),
                                                           new AdobeCoeff("Panasonic DMC-LX2", 0, 0,
                                                                          /* aka "LEICA D-LUX3" */
                                                                          new short[]
                                                                              {
                                                                                  8048, -2810, -623, -6450, 13519, 3272,
                                                                                  -1700, 2146, 7049
                                                                              }),
                                                           new AdobeCoeff("Phase One H 20", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  1313, 1855, -109, -6715, 15908, 808, -327
                                                                                  , 1840, 6020
                                                                              }),
                                                           new AdobeCoeff("Phase One P 2", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  2905, 732, -237, -8134, 16626, 1476,
                                                                                  -3038, 4253, 7517
                                                                              }),
                                                           new AdobeCoeff("Phase One P 30", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  4516, -245, -37, -7020, 14976, 2173,
                                                                                  -3206, 4671, 7087
                                                                              }),
                                                           new AdobeCoeff("Phase One P 45", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  5053, -24, -117, -5684, 14076, 1702,
                                                                                  -2619, 4492, 5849
                                                                              }),
                                                           new AdobeCoeff("SAMSUNG GX-1", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  10504, -2438, -1189, -8603, 16207, 2531,
                                                                                  -1022, 863, 12242
                                                                              }),
                                                           new AdobeCoeff("Sinar", 0, 0, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  16442, -2956, -2422, -2877, 12128, 750,
                                                                                  -1136, 6066, 4559
                                                                              }),
                                                           new AdobeCoeff("SONY DSC-F828", 491, 0,
                                                                          new short[]
                                                                              {
                                                                                  7924, -1910, -777, -8226, 15459, 2998,
                                                                                  -1517, 2199, 6818, -7242, 11401, 3481
                                                                              }),
                                                           new AdobeCoeff("SONY DSC-R1", 512, 0,
                                                                          new short[]
                                                                              {
                                                                                  8512, -2641, -694, -8042, 15670, 2526,
                                                                                  -1821, 2117, 7414
                                                                              }),
                                                           new AdobeCoeff("SONY DSC-V3", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  7511, -2571, -692, -7894, 15088, 3060,
                                                                                  -948, 1111, 8128
                                                                              }),
                                                           new AdobeCoeff("SONY DSLR-A100", 0, 0xfeb,
                                                                          new short[]
                                                                              {
                                                                                  9437, -2811, -774, -8405, 16215, 2290,
                                                                                  -710, 596, 7181
                                                                              }),
                                                           new AdobeCoeff("SONY DSLR-A200", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9847, -3091, -928, -8485, 16345, 2225,
                                                                                  -715, 595, 7103
                                                                              }),
                                                           new AdobeCoeff("SONY DSLR-A300", 0, 0,
                                                                          new short[]
                                                                              {
                                                                                  9847, -3091, -928, -8485, 16345, 2225,
                                                                                  -715, 595, 7103
                                                                              }),
                                                           new AdobeCoeff("SONY DSLR-A350", 0, 0xffc,
                                                                          new short[]
                                                                              {
                                                                                  6038, -1484, -578, -9146, 16746, 2513,
                                                                                  -875, 746, 7217
                                                                              }),
                                                           new AdobeCoeff("SONY DSLR-A700", 254, 0x1ffe,
                                                                          new short[]
                                                                              {
                                                                                  5775, -805, -359, -8574, 16295, 2391,
                                                                                  -1943, 2341, 7249
                                                                              }),
                                                           new AdobeCoeff("SONY DSLR-A900", 254, 0x1ffe, /* DJC */
                                                                          new short[]
                                                                              {
                                                                                  6971, -1730, -794, -5763, 13529, 2236,
                                                                                  -1500, 2251, 6715
                                                                              })
                                                       };

        private static readonly byte[,] xlat = new byte[2, 256] {
            { 
                0xc1,0xbf,0x6d,0x0d,0x59,0xc5,0x13,0x9d,0x83,0x61,0x6b,0x4f,0xc7,0x7f,0x3d,0x3d,
		        0x53,0x59,0xe3,0xc7,0xe9,0x2f,0x95,0xa7,0x95,0x1f,0xdf,0x7f,0x2b,0x29,0xc7,0x0d,
		        0xdf,0x07,0xef,0x71,0x89,0x3d,0x13,0x3d,0x3b,0x13,0xfb,0x0d,0x89,0xc1,0x65,0x1f,
		        0xb3,0x0d,0x6b,0x29,0xe3,0xfb,0xef,0xa3,0x6b,0x47,0x7f,0x95,0x35,0xa7,0x47,0x4f,
		        0xc7,0xf1,0x59,0x95,0x35,0x11,0x29,0x61,0xf1,0x3d,0xb3,0x2b,0x0d,0x43,0x89,0xc1,
		        0x9d,0x9d,0x89,0x65,0xf1,0xe9,0xdf,0xbf,0x3d,0x7f,0x53,0x97,0xe5,0xe9,0x95,0x17,
		        0x1d,0x3d,0x8b,0xfb,0xc7,0xe3,0x67,0xa7,0x07,0xf1,0x71,0xa7,0x53,0xb5,0x29,0x89,
		        0xe5,0x2b,0xa7,0x17,0x29,0xe9,0x4f,0xc5,0x65,0x6d,0x6b,0xef,0x0d,0x89,0x49,0x2f,
		        0xb3,0x43,0x53,0x65,0x1d,0x49,0xa3,0x13,0x89,0x59,0xef,0x6b,0xef,0x65,0x1d,0x0b,
		        0x59,0x13,0xe3,0x4f,0x9d,0xb3,0x29,0x43,0x2b,0x07,0x1d,0x95,0x59,0x59,0x47,0xfb,
		        0xe5,0xe9,0x61,0x47,0x2f,0x35,0x7f,0x17,0x7f,0xef,0x7f,0x95,0x95,0x71,0xd3,0xa3,
		        0x0b,0x71,0xa3,0xad,0x0b,0x3b,0xb5,0xfb,0xa3,0xbf,0x4f,0x83,0x1d,0xad,0xe9,0x2f,
		        0x71,0x65,0xa3,0xe5,0x07,0x35,0x3d,0x0d,0xb5,0xe9,0xe5,0x47,0x3b,0x9d,0xef,0x35,
		        0xa3,0xbf,0xb3,0xdf,0x53,0xd3,0x97,0x53,0x49,0x71,0x07,0x35,0x61,0x71,0x2f,0x43,
		        0x2f,0x11,0xdf,0x17,0x97,0xfb,0x95,0x3b,0x7f,0x6b,0xd3,0x25,0xbf,0xad,0xc7,0xc5,
		        0xc5,0xb5,0x8b,0xef,0x2f,0xd3,0x07,0x6b,0x25,0x49,0x95,0x25,0x49,0x6d,0x71,0xc7 },
		    { 
                0xa7,0xbc,0xc9,0xad,0x91,0xdf,0x85,0xe5,0xd4,0x78,0xd5,0x17,0x46,0x7c,0x29,0x4c,
		        0x4d,0x03,0xe9,0x25,0x68,0x11,0x86,0xb3,0xbd,0xf7,0x6f,0x61,0x22,0xa2,0x26,0x34,
		        0x2a,0xbe,0x1e,0x46,0x14,0x68,0x9d,0x44,0x18,0xc2,0x40,0xf4,0x7e,0x5f,0x1b,0xad,
		        0x0b,0x94,0xb6,0x67,0xb4,0x0b,0xe1,0xea,0x95,0x9c,0x66,0xdc,0xe7,0x5d,0x6c,0x05,
		        0xda,0xd5,0xdf,0x7a,0xef,0xf6,0xdb,0x1f,0x82,0x4c,0xc0,0x68,0x47,0xa1,0xbd,0xee,
		        0x39,0x50,0x56,0x4a,0xdd,0xdf,0xa5,0xf8,0xc6,0xda,0xca,0x90,0xca,0x01,0x42,0x9d,
		        0x8b,0x0c,0x73,0x43,0x75,0x05,0x94,0xde,0x24,0xb3,0x80,0x34,0xe5,0x2c,0xdc,0x9b,
		        0x3f,0xca,0x33,0x45,0xd0,0xdb,0x5f,0xf5,0x52,0xc3,0x21,0xda,0xe2,0x22,0x72,0x6b,
		        0x3e,0xd0,0x5b,0xa8,0x87,0x8c,0x06,0x5d,0x0f,0xdd,0x09,0x19,0x93,0xd0,0xb9,0xfc,
		        0x8b,0x0f,0x84,0x60,0x33,0x1c,0x9b,0x45,0xf1,0xf0,0xa3,0x94,0x3a,0x12,0x77,0x33,
		        0x4d,0x44,0x78,0x28,0x3c,0x9e,0xfd,0x65,0x57,0x16,0x94,0x6b,0xfb,0x59,0xd0,0xc8,
		        0x22,0x36,0xdb,0xd2,0x63,0x98,0x43,0xa1,0x04,0x87,0x86,0xf7,0xa6,0x26,0xbb,0xd6,
		        0x59,0x4d,0xbf,0x6a,0x2e,0xaa,0x2b,0xef,0xe6,0x78,0xb6,0x4e,0xe0,0x2f,0xdc,0x7c,
		        0xbe,0x57,0x19,0x32,0x7e,0x2a,0xd0,0xb8,0xba,0x29,0x00,0x3c,0x52,0x7d,0xa8,0x49,
		        0x3b,0x2d,0xeb,0x25,0x49,0xfa,0xa3,0xaa,0x39,0xa7,0xc5,0xa7,0x50,0x11,0x36,0xfb,
		        0xc6,0x67,0x4a,0xf5,0xa5,0x12,0x65,0x7e,0xb0,0xdf,0xaf,0x4e,0xb3,0x61,0x7f,0x2f } };
    }
}
