# RawParser
=========
[![Join the chat at https://gitter.im/RawParser/Lobby](https://badges.gitter.im/RawParser/Lobby.svg)](https://gitter.im/RawParser/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![Percentage of issues still open](http://isitmaintained.com/badge/open/arimhan/RawParser.svg)](http://isitmaintained.com/project/arimhan/RawParser "Percentage of issues still open")

Appveyor doens't work with the library but the project compile locally
[![Build status](https://ci.appveyor.com/api/projects/status/sdvkbleoqohq9rmb/branch/master?svg=true)](https://ci.appveyor.com/project/arimhan/rawparser/branch/master)

## the app is now live on the store (https://www.microsoft.com/store/apps/9pfwlj4lxftf) 
###(I recommend uninstalling the preview and using the one in the store. The future releases will be available in the store one or 2 days after being here).

## About
This is a Windows universal application that allows editing of raw files and other image format.
The main goal is to provide a way for people who wants to edit the raw file from their camera on there phone over continuum.
## Device supported
Windows phone and windows 10 computer with at least the anniversary update. (if requested, previous version of windows 10 could be added).
Support for Xbox and hololens could be added if someone can test them.

## Roadmap
- Complete support of DNG 
- Find a better app identity
- Translation in other language
- History view with undo/redo
- Presets
- Import and export of preset 
- Better demosaic algorithms
- Support for crop (part of the code is already there)

## File support:
### input:
  - Nikon nef (12/14 bits)
  - Tiff (uncompressed only)
  - Jpeg
  - Png 
  - DNG (uncompressed and Ljpeg)
  - Pef (pentax raw)
  - orf (olympus raw)
  - raw (panasonic raw)
  - cr2 (canon raw)
  - SRW (sony raw)
  
### output:
  - Tiff
  - Jpeg
  - PNG

## Installation
### From the store (recommended)
The app is available at [this address]
(https://www.microsoft.com/store/apps/9pfwlj4lxftf)
If you have already installed directly from the appx, please uninstall before installing from the store.

### Sideloading
You need to activate the developper mode and download the .appxbundle file and open it from the file explorer on your device (on phone the installation will be done in the background).
 You can get release from the github release (those are the build pushed on the store) or on appveyor build (more advanced but not tested).
 
 ## The parser
 This is a C# library for use in any UWP application.
It can read the exif, the thumbnail and the raw image from a lot of camera model

It's made from my own work, from dcraw.c(https://www.cybercom.net/~dcoffin/dcraw/) and from RawSpeed(https://github.com/klauspost/rawspeed)

### How to use
####Setup
Just add the folder Parser to your project and include the namespace RawNet
####Open the image
To opne and image, call the function:
```
public static RawDecoder GetDecoder(ref Stream stream, string fileType)
```
Where stream is any Stream object supported by the C# framework opened over the file and fileType is the extension of the file starting with a dot (it's not case sensitive).
example:
```
RawDecoder decoder = RawParser.GetDecoder(ref stream, file.FileType);
```
####Get and display the thumbnail
To get the thumbnailobject representing the thumbnail of the file, call the DecodeThumb() function of the opened decoder. It will return null if the file doesn't contain any thumbnail or if it's not yet supported. If thethumbnail is not null, call the GetSoftwareBitmap() function of the thumbanil to return a bitmap object that can be used inside the application.
example:
```
thumbnail = decoder.DecodeThumb();
if (thumbnail != null)
{
      DisplayImage(thumbnail.GetSoftwareBitmap());        
}
```

####Reading the raw
To read the raw image call the function DecodeRaw() of the decoder. This will fill the rawImage field of the decoder with the image.
It's recommended to call DecodeMetadata() before to fill the image with usefull information but it's not needed if you only want to extract the raw value.
```
decoder.DecodeRaw();
decoder.DecodeMetadata();
RawImage raw = decoder.rawImage;
```
The image may need further processing like debayerisation and color correction depending on the file format.

###Camera supported
- Sony srw
- Nikon Nef (only rgb files)
- Canon cr2
- Panasonic raw
- Olympus raw
- DNG
- Tiff
- JPEG
- PNG

###TODO
- add more cameras
- Correct color from camer to other color space
- Get the correct Exif with the correct value

