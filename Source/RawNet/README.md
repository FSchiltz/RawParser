# The parser
This is a C# library for use in any UWP application.
It can read the exif, the thumbnail and the raw image from a lot of camera model

It's made from my own work, from dcraw.c(https://www.cybercom.net/~dcoffin/dcraw/) and from RawSpeed(https://github.com/klauspost/rawspeed)

## How to use
### Setup
Just add the folder Parser to your project and include the namespace RawNet
### Open the image
To open an image, call the function:
```
public static RawDecoder GetDecoder(ref Stream stream, string fileType)
```
Where stream is any Stream object supported by the C# framework opened over the file and fileType is the extension of the file starting with a dot (it's not case sensitive).
example:
```
RawDecoder decoder = RawParser.GetDecoder(ref stream, ".NEF");
```
### Get and display the thumbnail
To get the thumbnailobject representing the thumbnail of the file, call the DecodeThumb() function of the opened decoder. It will return null if the file doesn't contain any thumbnail or if it's not yet supported. If thethumbnail is not null, call the GetSoftwareBitmap() function of the thumbanil to return a bitmap object that can be used inside the application.
example:
```
thumbnail = decoder.DecodeThumb();
if (thumbnail != null)
{
      DisplayImage(thumbnail.GetSoftwareBitmap());        
}
```

### Reading the raw
To read the raw image call the function DecodeRaw() of the decoder. This will fill the rawImage field of the decoder with the image.
It's recommended to call DecodeMetadata() before to fill the image with usefull information but it's not needed if you only want to extract the raw value.
```
decoder.DecodeRaw();
decoder.DecodeMetadata();
RawImage raw = decoder.rawImage;
```
The image may need further processing like debayerisation and color correction depending on the file format.
