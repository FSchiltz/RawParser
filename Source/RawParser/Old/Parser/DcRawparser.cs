using System;
using System.Collections.Generic;
using System.IO;
using RawParser.Format.IFD;
using RawParser.Parser;
using dcraw;

namespace RawParser
{
    internal class DCRawParser : AParser
    {
        DcRawState state = new DcRawState();

        public override void Parse(Stream s)
        {
            DcRawState state = new DcRawState();
            state.inFilename = "";
            state.ifp = new RawStream(s);

            Identifier id = new Identifier(state);
            id.identify(state.ifp);

            // Works this far...

            switch ((state.flip + 3600) % 360)
            {
                case 270:
                    state.flip = 5;
                    break;
                case 180:
                    state.flip = 3;
                    break;
                case 90:
                    state.flip = 6;
                    break;
            }

            /*
            // TODO: need writer
            Tiff t = new Tiff(state);
            state.write_fun = t.write_ppm_tiff;*/

            if (state.load_raw is kodak_ycbcr_load_raw)
            {
                state.height += state.height & 1;
                state.width += state.width & 1;
            }


            if (state.is_raw == 0)
            {
                throw new FormatException("File not supported");
            }

            if (state.is_raw == 0) return;

            state.shrink = (state.filters != 0 &&
                            (state.half_size || state.threshold != 0 || state.aber[0] != 1 || state.aber[2] != 1))
                               ? (ushort)1
                               : (ushort)0;
            state.iheight = (state.height + state.shrink) >> state.shrink;
            state.iwidth = (state.width + state.shrink) >> state.shrink;


            if (state.use_camera_matrix && state.cmatrix[0, 0] > 0.25)
            {
                Array.Copy(state.cmatrix, state.rgb_cam, state.cmatrix.Length);
            }
            state.raw_color = true;

            state.image = new ushort[state.iheight * state.iwidth * 4];

            if (state.shot_select >= state.is_raw)
            {
                throw new FormatException("File name incorrect");
            }

            state.ifp.Seek(state.data_offset, SeekOrigin.Begin);

            state.load_raw.LoadRaw();
            height = (uint)state.raw_height;
            width = (uint)state.raw_width;
            colorDepth = (ushort)state.output_bps;
            cfa = null;
            camMul = new double[] { 1, 1, 1, 1 };
            black = new double[4];
            curve = null;
        }

        public override Dictionary<ushort, Tag> parseExif()
        {
            return null;
        }

        public override byte[] parsePreview()
        {
            return null;
        }

        public override ushort[] parseRAWImage()
        {
            return state.image;
            throw new NotImplementedException();
        }

        public override byte[] parseThumbnail()
        {
            return null;
        }
    }
}