#include "Class1.h"
#include "metadata/CameraMetadataException.h"

using namespace RawSpeedWrapper;
using namespace Platform;
using namespace rawspeed;

DecoderWrapper::DecoderWrapper()
{
	
}

bool DecoderWrapper::failed() {
	return error;
}

int DecoderWrapper::getImage(uchar8* data, int size) {
	RawParser parser(&Buffer(data, size));
	auto decoder = parser.getDecoder();
	decoder->failOnUnknown = false;
	decoder->checkSupport(metadata);
	decoder->decodeRaw();
	decoder->decodeMetaData(metadata);
	decoder->mRaw->scaleBlackWhite();


	//check if type is byte16
	//convert the image to an exportable type
	return 0;
}
