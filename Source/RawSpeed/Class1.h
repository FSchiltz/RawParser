#pragma once

#include "RawSpeed-API.h"

using namespace rawspeed;

namespace RawSpeedWrapper
{
	public value struct ImageMetaDataWrapper {
	public:

		// Aspect ratio of the pixels, usually 1 but some cameras need scaling
		// <1 means the image needs to be stretched vertically, (0.5 means 2x)
		// >1 means the image needs to be stretched horizontally (2 mean 2x)
		double pixelAspectRatio;

		// White balance coefficients of the image
		//float wbCoeffs[4];

		// How many pixels far down the left edge and far up the right edge the image
		// corners are when the image is rotated 45 degrees in Fuji rotated sensors.
		uint32 fujiRotationPos;
		/*
		iPoint2D subsampling;
		std::string make;
		std::string model;
		std::string mode;

		std::string canonical_make;
		std::string canonical_model;
		std::string canonical_alias;
		std::string canonical_id;*/

		// ISO speed. If known the value is set, otherwise it will be '0'.
		int isoSpeed;
	};

	public value struct RawImageWrapper {
	public:
		int height;
		int width;			
	private:
	};

	public value struct CameraMetadataWrapper {
	public:
		int size;
	};

	public ref class DecoderWrapper sealed
	{
	public:
		DecoderWrapper(Windows::Foundation::Collections::IVector<int>^ meta);
		int getImage(uchar8* data, int size);
		bool failed();
	private:
		bool error;
		CameraMetaData* metadata;
	};
}
