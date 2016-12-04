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

using System;

namespace dcraw
{
    public abstract class RawLoader
    {
        protected readonly DcRawState state;

        protected RawLoader(DcRawState state)
        {
            this.state = state;
        }

        public abstract void LoadRaw();
    }

    public abstract class UnImplementedRawLoader : RawLoader
    {
        protected UnImplementedRawLoader(DcRawState state) : base(state) {}

        public override void LoadRaw()
        {
            throw new NotImplementedException();
        }
    }

    #region Unimplemented raw loaders
    internal class sinar_4shot_load_raw : UnImplementedRawLoader
    {
        public sinar_4shot_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class imacon_full_load_raw : UnImplementedRawLoader
    {
        public imacon_full_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class kodak_thumb_load_raw : UnImplementedRawLoader
    {
        public kodak_thumb_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class kodak_65000_load_raw : UnImplementedRawLoader
    {
        public kodak_65000_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class kodak_ycbcr_load_raw : UnImplementedRawLoader
    {
        public kodak_ycbcr_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class kodak_rgb_load_raw : UnImplementedRawLoader
    {
        public kodak_rgb_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class pentax_k10_load_raw : UnImplementedRawLoader
    {
        public pentax_k10_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }
    }

    internal class sony_arw_load_raw : UnImplementedRawLoader
    {
        public sony_arw_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class sony_arw2_load_raw : UnImplementedRawLoader
    {
        public sony_arw2_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class kodak_262_load_raw : UnImplementedRawLoader
    {
        public kodak_262_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class olympus_e300_load_raw : UnImplementedRawLoader
    {
        public olympus_e300_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class phase_one_load_raw : UnImplementedRawLoader
    {
        public phase_one_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class phase_one_load_raw_c : UnImplementedRawLoader
    {
        public phase_one_load_raw_c(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class canon_sraw_load_raw : UnImplementedRawLoader
    {
        public canon_sraw_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class canon_600_load_raw : UnImplementedRawLoader
    {
        public canon_600_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class foveon_load_raw : UnImplementedRawLoader
    {
        public foveon_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class kodak_yrgb_load_raw : UnImplementedRawLoader
    {
        public kodak_yrgb_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class casio_qv5700_load_raw : UnImplementedRawLoader
    {
        public casio_qv5700_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class quicktake_100_load_raw : UnImplementedRawLoader
    {
        public quicktake_100_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class kodak_dc120_load_raw : UnImplementedRawLoader
    {
        public kodak_dc120_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class kodak_jpeg_load_raw : UnImplementedRawLoader
    {
        public kodak_jpeg_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class rollei_load_raw : UnImplementedRawLoader
    {
        public rollei_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class sony_load_raw : UnImplementedRawLoader
    {
        public sony_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class canon_compressed_load_raw : UnImplementedRawLoader
    {
        public canon_compressed_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class kodak_radc_load_raw : UnImplementedRawLoader
    {
        public kodak_radc_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class leaf_hdr_load_raw : UnImplementedRawLoader
    {
        public leaf_hdr_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class hasselblad_load_raw : UnImplementedRawLoader
    {
        public hasselblad_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class panasonic_load_raw : UnImplementedRawLoader
    {
        public panasonic_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class eight_bit_load_raw : UnImplementedRawLoader
    {
        public eight_bit_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class nikon_e900_load_raw : UnImplementedRawLoader
    {
        public nikon_e900_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class minolta_rd175_load_raw : UnImplementedRawLoader
    {
        public minolta_rd175_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class unpacked_load_raw : UnImplementedRawLoader
    {
        public unpacked_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class fuji_load_raw : UnImplementedRawLoader
    {
        public fuji_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class canon_a5_load_raw : UnImplementedRawLoader
    {
        public canon_a5_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class nokia_load_raw : UnImplementedRawLoader
    {
        public nokia_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class adobe_dng_load_raw_nc : UnImplementedRawLoader
    {
        public adobe_dng_load_raw_nc(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }

    internal class olympus_e410_load_raw : UnImplementedRawLoader
    {
        public olympus_e410_load_raw(DcRawState state)
            : base(state)
        {
            throw new NotImplementedException();
        }

        
    }
    #endregion
}
