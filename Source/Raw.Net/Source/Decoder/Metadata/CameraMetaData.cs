using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using Windows.Storage;

namespace RawNet
{

    public class CameraMetaData
    {
        Dictionary<string, Camera> cameras = new Dictionary<string, Camera>();
        Dictionary<UInt32, Camera> chdkCameras = new Dictionary<uint, Camera>();


        public CameraMetaData(Stream docFile)
        {
            XDocument doc = XDocument.Load(docFile);

            if (doc == null)
            {
                throw new CameraMetadataException("CameraMetaData: XML Document could not be parsed successfully. Error was: %s in %s");
            }
            var cameras = doc.Elements("Cameras");

            foreach (XElement camera in cameras.Elements())
            {
                Camera cam = new Camera(camera);

                if (!addCamera(cam)) continue;

                // Create cameras for aliases.
                for (UInt32 i = 0; i < cam.aliases.Count; i++)
                {
                    addCamera(new Camera(cam, i));
                }
            }
        }


        public Camera getCamera(string make, string model, string mode)
        {
            string id = "" + make.Trim() + model.Trim() + mode.Trim();
            cameras.TryGetValue(id, out var value);
            if (null == value)
                return null;
            return cameras[id];
        }

        public bool hasCamera(string make, string model, string mode)
        {
            string id = "" + make.Trim() + model.Trim() + mode.Trim();
            cameras.TryGetValue(id, out var value);
            if (null == value)
                return false;
            return true;
        }

        public Camera getChdkCamera(UInt32 filesize)
        {
            chdkCameras.TryGetValue(filesize, out var value);
            if (null == value)
                return null;
            return chdkCameras[filesize];
        }

        public bool hasChdkCamera(UInt32 filesize)
        {
            chdkCameras.TryGetValue(filesize, out var value);
            return null != value;
        }

        public bool addCamera(Camera cam)
        {
            string id = "" + cam.make.Trim() + cam.model.Trim() + cam.mode.Trim();
            cameras.TryGetValue(id, out Camera tmp);
            if (null != tmp)
            {
                Debug.Write("CameraMetaData: Duplicate entry found for camera: " + cam.make + " " + cam.model + ", Skipping!");

                return false;
            }
            else
            {
                cameras.Add(id, cam);
            }

            if (cam.mode.Contains("chdk"))
            {
                cam.hints.TryGetValue("filesize", out string tmpStr);
                if (tmpStr == null)
                {
                    Debug.Write("CameraMetaData: CHDK camera: " + cam.make + " " + cam.model + ", no " + tmpStr + " hint set!");
                }
                else
                {
                    UInt32 size = 0;
                    cam.hints.TryGetValue("filesize", out string fsize);
                    //TODO add a remplaement function   
                    size = UInt32.Parse(fsize);
                    chdkCameras.Add(size, cam);
                    // writeLog(DEBUG_PRIO_WARNING, "CHDK camera: %s %s size:%u\n", cam.make.c_str(), cam.model.c_str(), size);
                }
            }
            return true;
        }
    }
}

