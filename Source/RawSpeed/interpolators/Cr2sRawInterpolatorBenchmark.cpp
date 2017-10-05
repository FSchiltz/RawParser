/*
    RawSpeed - RAW file decoder.

    Copyright (C) 2017 Roman Lebedev

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA
*/

#include "interpolators/Cr2sRawInterpolator.h" // for Cr2sRawInterpolator
#include "benchmark/Common.h"                  // for areaToRectangle
#include "common/Common.h"                     // for roundUp, ushort16
#include "common/Point.h"                      // for iPoint2D
#include "common/RawImage.h"                   // for RawImage, ImageMetaData
#include <array>                               // for array
#include <benchmark/benchmark.h>               // for Benchmark, State, BEN...
#include <type_traits>                         // for integral_constant

using rawspeed::Cr2sRawInterpolator;
using rawspeed::RawImage;
using rawspeed::TYPE_USHORT16;
using rawspeed::iPoint2D;
using rawspeed::ushort16;
using std::array;
using std::integral_constant;

template <int N> using v = integral_constant<int, N>;

template <const iPoint2D& subsampling, typename version>
static inline void BM_Cr2sRawInterpolator(benchmark::State& state) {
  static const array<int, 3> sraw_coeffs = {{999, 1000, 1001}};
  static const int hue = 1269;

  const auto dim = areaToRectangle(state.range(0));
  RawImage mRaw = RawImage::create(dim, TYPE_USHORT16, 3);
  mRaw->metadata.subsampling = subsampling;

  Cr2sRawInterpolator i(mRaw, sraw_coeffs, hue);

  while (state.KeepRunning())
    i.interpolate(version::value);

  state.SetComplexityN(dim.area());
  state.SetItemsProcessed(state.complexity_length_n() * state.iterations());
  state.SetBytesProcessed(3UL * sizeof(ushort16) * state.items_processed());
}

static inline void CustomArguments(benchmark::internal::Benchmark* b) {
  b->RangeMultiplier(2);
#if 1
  b->Arg(256 << 20);
#else
  b->Range(1, 1024 << 20)->Complexity(benchmark::oN);
#endif
  b->Unit(benchmark::kMillisecond);
}

static constexpr const iPoint2D S422(2, 1);
BENCHMARK_TEMPLATE(BM_Cr2sRawInterpolator, S422, v<0>)->Apply(CustomArguments);
BENCHMARK_TEMPLATE(BM_Cr2sRawInterpolator, S422, v<1>)->Apply(CustomArguments);
BENCHMARK_TEMPLATE(BM_Cr2sRawInterpolator, S422, v<2>)->Apply(CustomArguments);

static constexpr const iPoint2D S420(2, 2);
BENCHMARK_TEMPLATE(BM_Cr2sRawInterpolator, S420, v<1>)->Apply(CustomArguments);
BENCHMARK_TEMPLATE(BM_Cr2sRawInterpolator, S420, v<2>)->Apply(CustomArguments);

BENCHMARK_MAIN();
