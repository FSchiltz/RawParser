#include "Class1.h"
#include "metadata/CameraMetadataException.h"

using namespace RawSpeedWrapper;

DecoderWrapper::DecoderWrapper()
{
}

bool DecoderWrapper::Init(const Platform::Array<uchar8>^ data, Platform::String^ xml) {
	if (metadata == NULL) {
		try {
			metadata = new CameraMetaData(xml->Data());		
		}
		catch (CameraMetadataException &e) {
			const size_t size = strlen(e.what()) + 1;
			wchar_t* wText = new wchar_t[size];
			mbstowcs(wText, e.what(), size);
			errorMessage = xml; //ref new Platform::String(wText);
			return false;
		}
	}

	auto lenght = data->Length;
	auto tempBuff = new uchar8[lenght];
	for (unsigned int i = 0; i <lenght; i++)
	{
		tempBuff[i] = data[i];
	}

	RawParser parser(&Buffer(tempBuff, lenght));
	decoder = parser.getDecoder();
	decoder->failOnUnknown = false;
	decoder->checkSupport(metadata);
}

Platform::Array<uchar8>^ DecoderWrapper::GetThumbnail()
{
	auto temp = ref new Platform::Array<uchar8>(10);
	return temp;
}

bool DecoderWrapper::failed() {
	return error;
}

Platform::Array<int>^ DecoderWrapper::GetImage() {	
	decoder->decodeRaw();
	//decoder->decodeMetaData(metadata);
	//decoder->mRaw->scaleBlackWhite();
	//check if type is byte16
	//auto data = decoder->mRaw->getData();
	//decoder->mRaw->dim.area();
	auto temp = ref new Platform::Array<int>(0);

	return temp;
}
