# RawParser
=========
## About

[![Join the chat at https://gitter.im/RawParser/Lobby](https://badges.gitter.im/RawParser/Lobby.svg)](https://gitter.im/RawParser/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
This is a Windows universal application that allows editing of raw files and other image format.
The main goal is to provide a way for people who wants to edit the raw file from their camera on there phone over continuum.

This is 2 separate project, a parser and an application. For more information about the parser, see the subfolder Raw.net

## Device supported
Windows phone and windows 10 computer with at least the anniversary update. (if requested, previous version of windows 10 could be added).
Support for Xbox and hololens could be added if someone can test them.

## Roadmap
- Complete support of DNG 
- Find a better name
- Create a better icon
- Translation in other language
- History view with undo/redo
- Presets
- Import and export of preset 
- Better demosaic algorithms
- Support for crop and rotation of image (part of the code is already there)

## File support:
### input:
  - Nikon nef (12/14 bits)
  - Tiff (uncompressed only)
  - Jpeg
  - Png 
  - DNG (uncompressed and Ljpeg)
  
### output:
  - Tiff
  - PPM (for testing but the only export format that produce the correct color for now)
  - Jpeg
  - PNG

## Installation
### From the store (recommended)
The app will be available at [this address when available]
(https://www.microsoft.com/store/apps/9pfwlj4lxftf)
If you hav ealready insatlledot from the appx directly,please uninstall before insatlling from the store.

### From the github
You need to activatee the developper mod and then dowloading the package from the latest release and opening it from the file explorer on your device.
(on phone the installation will be done in the background).
