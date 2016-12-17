# RawParser
=========
[![Join the chat at https://gitter.im/RawParser/Lobby](https://badges.gitter.im/RawParser/Lobby.svg)](https://gitter.im/RawParser/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

[![Percentage of issues still open](http://isitmaintained.com/badge/open/arimhan/RawParser.svg)](http://isitmaintained.com/project/arimhan/RawParser "Percentage of issues still open")

[![Build status](https://ci.appveyor.com/api/projects/status/sdvkbleoqohq9rmb/branch/master?svg=true)](https://ci.appveyor.com/project/arimhan/rawparser/branch/master)

## the app is now live on the store (https://www.microsoft.com/store/apps/9pfwlj4lxftf) 
###(I recommend uninstalling the preview and using the one in the store. The future releases will be available in the store one or 2 days after being here).

## About
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

