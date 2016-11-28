#ifndef CAMERA_META_DATA_H
#define CAMERA_META_DATA_H

#include "Camera.h"
/* 
    RawSpeed - RAW file decoder.

    Copyright (C) 2009-2014 Klaus Post

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA

    http://www.klauspost.com
*/

namespace RawSpeed {

using namespace pugi;

class CameraMetaData
{
public:
  CameraMetaData();
  CameraMetaData(char *docname);
  virtual ~CameraMetaData(void);
  map<string,Camera*> cameras;
  map<UInt32,Camera*> chdkCameras;
  Camera* getCamera(string make, string model, string mode);
  bool hasCamera(string make, string model, string mode);
  Camera* getChdkCamera(UInt32 filesize);
  bool hasChdkCamera(UInt32 filesize);
  void disableMake(string make);
  void disableCamera(string make, string model);
protected:
  bool addCamera(Camera* cam);
};

} // namespace RawSpeed

#include "StdAfx.h"
#include "CameraMetaData.h"
  /*
  RawSpeed - RAW file decoder.

  Copyright (C) 2009-2014 Klaus Post

  This library is free software; you can redistribute it and/or
  modify it under the terms of the GNU Lesser General Public
  License as published by the Free Software Foundation; either
  version 2 of the License, or (at your option) any later version.

  This library is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
  Lesser General Public License for more details.

  You should have received a copy of the GNU Lesser General Public
  License along with this library; if not, write to the Free Software
  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA

  http://www.klauspost.com
  */

namespace RawSpeed {

	CameraMetaData::CameraMetaData() {
	}

	CameraMetaData::CameraMetaData(char *docname) {
		xml_document doc;
		xml_parse_result result = doc.load_file(docname);

		if (!result) {
			ThrowCME("CameraMetaData: XML Document could not be parsed successfully. Error was: %s in %s",
				result.description(), doc.child("node").attribute("attr").value());
		}
		xml_node cameras = doc.child("Cameras");

		for (xml_node camera = cameras.child("Camera"); camera; camera = camera.next_sibling("Camera")) {
			Camera *cam = new Camera(camera);

			if (!addCamera(cam)) continue;

			// Create cameras for aliases.
			for (UInt32 i = 0; i < cam.aliases.size(); i++) {
				addCamera(new Camera(cam, i));
			}
		}
	}

	CameraMetaData::~CameraMetaData(void) {
		map<string, Camera*>::iterator i = cameras.begin();
		for (; i != cameras.end(); ++i) {
			delete((*i).second);
		}
	}

	Camera* CameraMetaData::getCamera(string make, string model, string mode) {
		string id = string(make).append(model).append(mode);
		if (cameras.end() == cameras.find(id))
			return null;
		return cameras[id];
	}

	bool CameraMetaData::hasCamera(string make, string model, string mode) {
		string id = string(make).append(model).append(mode);
		if (cameras.end() == cameras.find(id))
			return false;
		return true;
	}

	Camera* CameraMetaData::getChdkCamera(UInt32 filesize) {
		if (chdkCameras.end() == chdkCameras.find(filesize))
			return null;
		return chdkCameras[filesize];
	}

	bool CameraMetaData::hasChdkCamera(UInt32 filesize) {
		return chdkCameras.end() != chdkCameras.find(filesize);
	}

	bool CameraMetaData::addCamera(Camera* cam)
	{
		string id = string(cam.make).append(cam.model).append(cam.mode);
		if (cameras.end() != cameras.find(id)) {
			writeLog(DEBUG_PRIO_WARNING, "CameraMetaData: Duplicate entry found for camera: %s %s, Skipping!\n", cam.make.c_str(), cam.model.c_str());
			delete(cam);
			return false;
		}
		else {
			cameras[id] = cam;
		}
		if (string::npos != cam.mode.find("chdk")) {
			if (cam.hints.find("filesize") == cam.hints.end()) {
				writeLog(DEBUG_PRIO_WARNING, "CameraMetaData: CHDK camera: %s %s, no \"filesize\" hint set!\n", cam.make.c_str(), cam.model.c_str());
			}
			else {
				UInt32 size;
				stringstream fsize(cam.hints.find("filesize").second);
				fsize >> size;
				chdkCameras[size] = cam;
				// writeLog(DEBUG_PRIO_WARNING, "CHDK camera: %s %s size:%u\n", cam.make.c_str(), cam.model.c_str(), size);
			}
		}
		return true;
	}

	void CameraMetaData::disableMake(string make)
	{
		map<string, Camera*>::iterator i = cameras.begin();
		for (; i != cameras.end(); ++i) {
			Camera* cam = (*i).second;
			if (0 == cam.make.compare(make)) {
				cam.supported = false;
			}
		}
	}

	void CameraMetaData::disableCamera(string make, string model)
	{
		map<string, Camera*>::iterator i = cameras.begin();
		for (; i != cameras.end(); ++i) {
			Camera* cam = (*i).second;
			if (0 == cam.make.compare(make) && 0 == cam.model.compare(model)) {
				cam.supported = false;
			}
		}
	}

} // namespace RawSpeed

