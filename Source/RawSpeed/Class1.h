#pragma once

#include "RawSpeed-API.h"

using namespace rawspeed;

namespace RawSpeedWrapper
{
	public value struct PointWrapper {
	public:
		int x;
		int y;
	};

	public value struct RawImageWrapper {
	public:// Aspect ratio of the pixels, usually 1 but some cameras need scaling
		// <1 means the image needs to be stretched vertically, (0.5 means 2x)
		// >1 means the image needs to be stretched horizontally (2 mean 2x)
		double pixelAspectRatio;

		// White balance coefficients of the image
		double red, green, blue;

		// How many pixels far down the left edge and far up the right edge the image
		// corners are when the image is rotated 45 degrees in Fuji rotated sensors.
		uint32 fujiRotationPos;

		PointWrapper subsampling;
		Platform::String ^ make;
		Platform::String ^ model;
		Platform::String ^ mode;

		Platform::String ^ canonical_make;
		Platform::String ^ canonical_model;
		Platform::String ^ canonical_alias;
		Platform::String ^ canonical_id;

		Platform::String ^ Mode;
		Platform::String ^ Copyright;

		int Rotation;
		int IsoSpeed;
		double Exposure;
		double Aperture;
		double Focal;
		Platform::String ^ Lens;

		Platform::String ^ TimeTake;
		Platform::String ^ TimeModify;

		PointWrapper rawdim;
		int OriginalRotation;

		Platform::String ^ Comment;

		int ColorSpace;

		// ISO speed. If known the value is set, otherwise it will be '0'.
		int isoSpeed;
		PointWrapper size, offset;
		int whitePoint;
		int black;
	};

	public value struct ThumbImageWrapper {
	public:
		PointWrapper size;
		int type;
		int cpp;
	};

	public ref class DecoderWrapper sealed
	{
	public:
		DecoderWrapper();
		Platform::Array<int>^ GetImage();
		bool Init(const Platform::Array<uchar8>^ data, Platform::String^ xml);
		Platform::Array<uchar8>^ GetThumbnail();
		bool failed();
		property RawImageWrapper rawImage;
		property ThumbImageWrapper thumbImage;
		property Platform::String^ errorMessage;
	private:
		CameraMetaData *metadata;
		bool error;
		std::unique_ptr<rawspeed::RawDecoder> decoder;
	};
}
