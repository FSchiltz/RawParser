#pragma once

#define __attribute__(x) 
#define __builtin_unreachable(x)
//#define assert(x)
#define ASAN_REGION_IS_POISONED(x) 0
#define __PRETTY_FUNCTION__ __FUNCSIG__
