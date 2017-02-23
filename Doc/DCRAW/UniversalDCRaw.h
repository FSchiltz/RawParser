#pragma once
#define DCRAW_VERSION "9.27"

#define NODEPS
#define _CRT_SECURE_NO_WARNINGS 1

#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#define _USE_MATH_DEFINES
#include <ctype.h>
#include <errno.h>
#include <fcntl.h>
#include <float.h>
#include <limits.h>
#include <math.h>
#include <setjmp.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include <sys/types.h>
#include <string>


#define fseeko fseek
#define ftello ftell

#ifdef __CYGWIN__
#include <io.h>
#endif
#ifdef _WIN32
#include <sys/utime.h>
#include <winsock2.h>
#pragma comment(lib, "ws2_32.lib")
#define snprintf _snprintf
#define strcasecmp _stricmp
#define strncasecmp _strnicmp
typedef __int64 INT64;
typedef unsigned __int64 UINT64;
#else
#include <unistd.h>
#include <utime.h>
#include <netinet/in.h>
typedef long long INT64;
typedef unsigned long long UINT64;
#endif

#ifdef NODEPS
#define NO_JASPER
#define NO_JPEG
#define NO_LCMS
#endif
#ifndef NO_JASPER
#include <jasper/jasper.h>	/* Decode Red camera movies */
#endif
#ifndef NO_JPEG
#include <jpeglib.h>		/* Decode compressed Kodak DC120 photos */
#endif				/* and Adobe Lossy DNGs */
#ifndef NO_LCMS
#include <lcms2.h>		/* Support color profiles */
#endif
#ifdef LOCALEDIR
#include <libintl.h>
#define _(String) gettext(String)
#else
#define _(String) (String)
#endif

#if !defined(uchar)
#define uchar unsigned char
#endif
#if !defined(ushort)
#define ushort unsigned short
#endif


/*
All global variables are defined here, and all functions that
access them are prefixed with "CLASS".  Note that a thread-safe
C++ class cannot have non-const static local variables.
*/
FILE *ifp, *ofp;
short order;
const char *ifname;
char *meta_data, xtrans[6][6], xtrans_abs[6][6];
char cdesc[5], desc[512], make[64], model[64], model2[64], artist[64];
float flash_used, canon_ev, iso_speed, shutter, aperture, focal_len;
time_t timestamp;
off_t strip_offset, data_offset;
off_t thumb_offset, meta_offset, profile_offset;
unsigned shot_order, kodak_cbpp, exif_cfa, unique_id;
unsigned thumb_length, meta_length, profile_length;
unsigned thumb_misc, *oprof, fuji_layout, shot_select = 0, multi_out = 0;
unsigned tiff_nifds, tiff_samples, tiff_bps, tiff_compress;
unsigned black, maximum, mix_green, raw_color, zero_is_bad;
unsigned zero_after_ff, is_raw, dng_version, is_foveon, data_error;
unsigned tile_width, tile_length, gpsdata[32], load_flags;
unsigned flip, tiff_flip, filters, colors;
ushort raw_height, raw_width, height, width, top_margin, left_margin;
ushort shrink, iheight, iwidth, fuji_width, thumb_width, thumb_height;
ushort *raw_image, (*image)[4], cblack[4102];
ushort white[8][8], curve[0x10000], cr2_slice[3], sraw_mul[4];
double pixel_aspect, aber[4] = { 1,1,1,1 }, gamm[6] = { 0.45,4.5,0,0,0,0 };
float bright = 1, user_mul[4] = { 0,0,0,0 }, threshold = 0;
int mask[8][4];
int half_size = 0, four_color_rgb = 0, document_mode = 0, highlight = 0;
int verbose = 0, use_auto_wb = 0, use_camera_wb = 0, use_camera_matrix = 1;
int output_color = 1, output_bps = 8, output_tiff = 0, med_passes = 0;
int no_auto_bright = 0;
unsigned greybox[4] = { 0, 0, UINT_MAX, UINT_MAX };
float cam_mul[4], pre_mul[4], cmatrix[3][4], rgb_cam[3][4];
const double xyz_rgb[3][3] = {			/* XYZ from RGB */
	{ 0.412453, 0.357580, 0.180423 },
	{ 0.212671, 0.715160, 0.072169 },
	{ 0.019334, 0.119193, 0.950227 } };
const float d65_white[3] = { 0.950456, 1, 1.088754 };
int histogram[4][0x2000];
void(*write_thumb)(), (*write_fun)();
void(*load_raw)(), (*thumb_load_raw)();
jmp_buf failure;

struct decode {
	struct decode *branch[2];
	int leaf;
} first_decode[2048], *second_decode, *free_decode;

struct tiff_ifd {
	int width, height, bps, comp, phint, offset, flip, samples, bytes;
	int tile_width, tile_length;
	float shutter;
} tiff_ifd[10];

struct ph1 {
	int format, key_off, tag_21a;
	int black, split_col, black_col, split_row, black_row;
	float tag_210;
} ph1;

#define CLASS

#define FORC(cnt) for (c=0; c < cnt; c++)
#define FORC3 FORC(3)
#define FORC4 FORC(4)
#define FORCC FORC(colors)

#define SQR(x) ((x)*(x))
#define ABS(x) (((int)(x) ^ ((int)(x) >> 31)) - ((int)(x) >> 31))
#define MIN(a,b) ((a) < (b) ? (a) : (b))
#define MAX(a,b) ((a) > (b) ? (a) : (b))
#define LIM(x,min,max) MAX(min,MIN(x,max))
#define ULIM(x,y,z) ((y) < (z) ? LIM(x,y,z) : LIM(x,z,y))
#define CLIP(x) LIM((int)(x),0,65535)
#define SWAP(a,b) { a=a+b; b=a-b; a=a-b; }

/*
In order to inline this calculation, I make the risky
assumption that all filter patterns can be described
by a repeating pattern of eight rows and two columns

Do not use the FC or BAYER macros with the Leaf CatchLight,
because its pattern is 16x16, not 2x8.

Return values are either 0/1/2/3 = G/M/C/Y or 0/1/2/3 = R/G1/B/G2

PowerShot 600	PowerShot A50	PowerShot Pro70	Pro90 & G1
0xe1e4e1e4:	0x1b4e4b1e:	0x1e4b4e1b:	0xb4b4b4b4:

0 1 2 3 4 5	  0 1 2 3 4 5	  0 1 2 3 4 5	  0 1 2 3 4 5
0 G M G M G M	0 C Y C Y C Y	0 Y C Y C Y C	0 G M G M G M
1 C Y C Y C Y	1 M G M G M G	1 M G M G M G	1 Y C Y C Y C
2 M G M G M G	2 Y C Y C Y C	2 C Y C Y C Y
3 C Y C Y C Y	3 G M G M G M	3 G M G M G M
4 C Y C Y C Y	4 Y C Y C Y C
PowerShot A5	5 G M G M G M	5 G M G M G M
0x1e4e1e4e:	6 Y C Y C Y C	6 C Y C Y C Y
7 M G M G M G	7 M G M G M G
0 1 2 3 4 5
0 C Y C Y C Y
1 G M G M G M
2 C Y C Y C Y
3 M G M G M G

All RGB cameras use one of these Bayer grids:

0x16161616:	0x61616161:	0x49494949:	0x94949494:

0 1 2 3 4 5	  0 1 2 3 4 5	  0 1 2 3 4 5	  0 1 2 3 4 5
0 B G B G B G	0 G R G R G R	0 G B G B G B	0 R G R G R G
1 G R G R G R	1 B G B G B G	1 R G R G R G	1 G B G B G B
2 B G B G B G	2 G R G R G R	2 G B G B G B	2 R G R G R G
3 G R G R G R	3 B G B G B G	3 R G R G R G	3 G B G B G B
*/

#define RAW(row,col) \
	raw_image[(row)*raw_width+(col)]

#define FC(row,col) \
	(filters >> ((((row) << 1 & 14) + ((col) & 1)) << 1) & 3)

#define BAYER(row,col) \
	image[((row) >> shrink)*iwidth + ((col) >> shrink)][FC(row,col)]

#define BAYER2(row,col) \
	image[((row) >> shrink)*iwidth + ((col) >> shrink)][fcol(row,col)]
