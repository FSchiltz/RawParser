using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace RawNet
{
    internal class Camera
    {
        public string make;
        public string model;
        public string mode;
        public string canonical_make;
        public string canonical_model;
        public string canonical_alias;
        public string canonical_id;
        public List<string> aliases = new List<string>();
        public List<string> canonical_aliases = new List<string>();
        public ColorFilterArray cfa = new ColorFilterArray(new Point2D(0, 0));
        public bool supported { get; set; }
        public Point2D cropSize = new Point2D();
        public Point2D cropPos = new Point2D();
        public List<BlackArea> blackAreas = new List<BlackArea>();
        public List<CameraSensorInfo> sensorInfo = new List<CameraSensorInfo>();
        public int decoderVersion { private set; get; }
        public Dictionary<string, string> hints = new Dictionary<string, string>();

        public Camera(XElement camera)
        {
            var key = camera.Attribute("make");
            if (key == null)
                throw new CameraMetadataException("Camera XML Parser: make element not found.");
            make = canonical_make = key.Value;

            key = camera.Attribute("model");
            if (key == null)

                throw new CameraMetadataException("Camera XML Parser: model element not found.");
            model = canonical_model = canonical_alias = key.Value;

            canonical_id = make + " " + model;

            supported = true;
            key = camera.Attribute("supported");
            if (key != null)
            {
                string s = key.Value;
                if (s == "no")
                    supported = false;
            }

            key = camera.Attribute("mode");
            if (key != null)
            {
                mode = key.Value;
            }
            else
            {
                mode = "";
            }

            key = camera.Attribute("decoder_version");
            if (key != null)
            {
                decoderVersion = Int32.Parse(key.Value);
            }
            else
            {
                decoderVersion = 0;
            }

            foreach (XElement node in camera.Descendants())
            {
                parseCameraChild(node);
            }
        }

        public Camera(Camera camera, UInt32 alias_num)
        {
            if (alias_num >= camera.aliases.Count)
                throw new CameraMetadataException("Camera: Internal error, alias number out of range specified.");

            make = camera.make;
            model = camera.aliases[(int)alias_num];
            canonical_make = camera.canonical_make;
            canonical_model = camera.canonical_model;
            canonical_alias = camera.canonical_aliases[(int)alias_num];
            canonical_id = camera.canonical_id;
            mode = camera.mode;
            cfa = camera.cfa;
            supported = camera.supported;
            cropSize = camera.cropSize;
            cropPos = camera.cropPos;
            decoderVersion = camera.decoderVersion;
            for (Int32 i = 0; i < camera.blackAreas.Count; i++)
            {
                blackAreas.Add(camera.blackAreas[i]);
            }
            for (Int32 i = 0; i < camera.sensorInfo.Count; i++)
            {
                sensorInfo.Add(camera.sensorInfo[i]);
            }
            foreach (KeyValuePair<string, string> mi in camera.hints)
            {
                hints.Add(mi.Key, mi.Value);
            }
        }

        void parseCameraChild(XElement cur)
        {
            if (cur.Name == "CFA")
            {
                if (2 != Int32.Parse(cur.Attribute("width").Value) || 2 != Int32.Parse(cur.Attribute("height").Value))
                {
                    supported = false;
                }
                else
                {
                    cfa.setSize(new Point2D(2, 2));
                    var c = cur.Elements("Color");
                    foreach (XElement x in c)
                    {
                        parseCFA(x);
                    }
                }
                return;
            }

            if (cur.Name == "CFA2")
            {
                cfa.setSize(new Point2D(Int32.Parse(cur.Attribute("width").Value), Int32.Parse(cur.Attribute("height").Value)));
                var c = cur.Elements("Color");
                foreach (var x in c)
                {
                    parseCFA(x);
                }
                c = cur.Elements("ColorRow");
                foreach (var x in c)
                {
                    parseCFA(x);
                }
                return;
            }

            if (cur.Name == "Crop")
            {
                cropPos.x = Int32.Parse(cur.Attribute("x").Value);
                cropPos.y = Int32.Parse(cur.Attribute("y").Value);

                if (cropPos.x < 0)
                    throw new CameraMetadataException("Negative X axis crop specified in camera " + make + " " + model);
                if (cropPos.y < 0)
                    throw new CameraMetadataException("Negative Y axis crop specified in camera " + make + " " + model);

                cropSize.x = Int32.Parse(cur.Attribute("width").Value);
                cropSize.y = Int32.Parse(cur.Attribute("height").Value);
                return;
            }

            if (cur.Name == "Sensor")
            {
                parseSensorInfo(cur);
                return;
            }

            if (cur.Name == "BlackAreas")
            {
                var c = cur.Elements();
                foreach (var x in c)
                {
                    parseBlackAreas(x);
                }
                return;
            }

            if (cur.Name == "Aliases")
            {
                var c = cur.Descendants("Alias");
                foreach (var x in c)
                {
                    parseAlias(x);
                }
                return;
            }

            if (cur.Name == "Hints")
            {
                var c = cur.Descendants("Hint");
                foreach (var x in c)
                {
                    parseHint(x);
                }
                return;
            }

            if (cur.Name == "ID")
            {
                parseID(cur);
                return;
            }
        }

        void parseCFA(XElement cur)
        {
            if (cur.Name == "ColorRow")
            {
                int y = Int32.Parse(cur.Attribute("y").Value); //-1 asdefault
                if (y < 0 || y >= cfa.size.y)
                {
                    throw new CameraMetadataException("Invalid y coordinate in CFA array of in camera " + make + " " + model);
                }
                string key = cur.Value;
                if (key.Length != cfa.size.x)
                {
                    throw new CameraMetadataException("Invalid number of colors in definition for row " + y + " in camera" + make + " " + model + ".Found " + key.Length + ".");
                }
                for (int x = 0; x < cfa.size.x; x++)
                {
                    //not efficient, TODO move the tolower
                    char v = key.ToLower()[x];
                    if (v == 'g')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.GREEN);
                    else if (v == 'r')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.RED);
                    else if (v == 'b')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.BLUE);
                    else if (v == 'f')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.FUJI_GREEN);
                    else if (v == 'c')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.CYAN);
                    else if (v == 'm')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.MAGENTA);
                    else if (v == 'y')
                        cfa.setColorAt(new Point2D(x, y), CFAColor.YELLOW);
                    else
                        supported = false;
                }
            }
            if (cur.Name == "Color")
            {
                int x = Int32.Parse(cur.Attribute("x").Value);//.as_int(-1);
                if (x < 0 || x >= cfa.size.x)
                {
                    throw new CameraMetadataException("Invalid x coordinate in CFA array of in camera " + make + " " + model);
                }

                int y = Int32.Parse(cur.Attribute("y").Value);// as_int(-1);
                if (y < 0 || y >= cfa.size.y)
                {
                    throw new CameraMetadataException("Invalid y coordinate in CFA array of in camera " + make + " " + model);
                }

                string key = cur.Value;
                if (key == "GREEN")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.GREEN);
                else if (key == "RED")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.RED);
                else if (key == "BLUE")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.BLUE);
                else if (key == "FUJIGREEN")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.FUJI_GREEN);
                else if (key == "CYAN")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.CYAN);
                else if (key == "MAGENTA")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.MAGENTA);
                else if (key == "YELLOW")
                    cfa.setColorAt(new Point2D(x, y), CFAColor.YELLOW);
            }
        }

        void parseBlackAreas(XElement cur)
        {
            if (cur.Name == "Vertical")
            {

                int x = Int32.Parse(cur.Attribute("x").Value);//as_int(-1);
                if (x < 0)
                {
                    throw new CameraMetadataException("Invalid x coordinate in vertical BlackArea of in camera " + make + " " + model);
                }

                int w = Int32.Parse(cur.Attribute("width").Value);// as_int(-1);
                if (w < 0)
                {
                    throw new CameraMetadataException("Invalid width in vertical BlackArea of in camera " + make + " " + model);
                }

                blackAreas.Add(new BlackArea(x, w, true));

            }
            else if (cur.Name == "Horizontal")
            {

                int y = Int32.Parse(cur.Attribute("y").Value);// as_int(-1);
                if (y < 0)
                {
                    throw new CameraMetadataException("Invalid y coordinate in horizontal BlackArea of in camera " + make + " " + model);
                }

                int h = Int32.Parse(cur.Attribute("height").Value);// as_int(-1);
                if (h < 0)
                {
                    throw new CameraMetadataException("Invalid width in horizontal BlackArea of in camera " + make + " " + model);
                }
                blackAreas.Add(new BlackArea(y, h, false));
            }
        }

        List<int> MultipleStringToInt(string inStr, string tag, string Element)
        {
            int i;
            List<int> ret = new List<int>();
            string[] v = inStr.Split(new char[] { ' ' });

            for (UInt32 j = 0; j < v.Length; j++)
            {
                try
                {
                    i = Int32.Parse(v[j]);
                    ret.Add(i);
                }
                catch (Exception e) when (e is FormatException || e is ArgumentNullException || e is OverflowException)
                {
                    throw new CameraMetadataException("Error parsing Element " + Element.ToString() + " in tag " + tag + ", in camera " + make + " " + model + " .");
                }
            }
            return ret;
        }

        void parseAlias(XElement cur)
        {
            if (cur.Name == "Alias")
            {
                aliases.Add(cur.Value);
                XAttribute key = cur.Attribute("id");
                if (key != null)
                    canonical_aliases.Add(key.Name.ToString());
                else
                    canonical_aliases.Add(cur.Value);
            }
        }

        void parseHint(XElement cur)
        {
            if (cur.Name == "Hint")
            {
                string hint_name, hint_value;
                XAttribute key = cur.Attribute("name");
                if (key != null)
                {
                    hint_name = key.Value;
                }
                else
                    throw new CameraMetadataException("CameraMetadata: Could not find name for hint for " + make + " " + model + " camera.");

                key = cur.Attribute("value");
                if (key != null)
                {
                    hint_value = key.Value;
                }
                else
                    throw new CameraMetadataException("CameraMetadata: Could not find value for hint " + hint_name + " for " + make + " " + model + "  camera.");

                hints.Add(hint_name, hint_value);
            }
        }

        void parseID(XElement cur)
        {
            if (cur.Name == "ID")
            {
                XAttribute id_make = cur.Attribute("make");
                if (id_make != null)
                {
                    canonical_make = id_make.Value;
                }
                else
                    throw new CameraMetadataException("CameraMetadata: Could not find make for ID for " + make + " " + model + " camera.");

                XAttribute id_model = cur.Attribute("model");
                if (id_model != null)
                {
                    canonical_model = id_model.Value;
                    canonical_alias = id_model.Value;
                }
                else
                    throw new CameraMetadataException("CameraMetadata: Could not find model for ID for " + make + " " + model + " camera.");

                canonical_id = cur.Value;
            }
        }

        protected void parseSensorInfo(XElement cur)
        {

            int min_iso = (cur.Attribute("iso_min") == null) ? 0 : Int32.Parse(cur.Attribute("iso_min").Value);//.as_int(0);
            int max_iso = (cur.Attribute("iso_max") == null) ? 0 : Int32.Parse(cur.Attribute("iso_max").Value);// as_int(0); ;
            int black = (cur.Attribute("black") == null) ? -1 : Int32.Parse(cur.Attribute("black").Value);// as_int(-1);
            int white = (cur.Attribute("white") == null) ? 65536 : Int32.Parse(cur.Attribute("white").Value);// as_int(65536);

            XElement key = cur.Element("black_colors");
            List<int> black_colors = new List<int>();
            if (key != null)
            {
                black_colors = MultipleStringToInt(key.Value, cur.Name.ToString(), "black_colors");
            }
            key = cur.Element("iso_list");
            if (key != null)
            {
                List<int> values = MultipleStringToInt(key.Value, cur.Name.ToString(), "iso_list");
                if (values.Count != 0)
                {
                    for (UInt32 i = 0; i < values.Count; i++)
                    {
                        sensorInfo.Add(new CameraSensorInfo(black, white, values[(int)i], values[(int)i], black_colors));
                    }
                }
            }
            else
            {
                sensorInfo.Add(new CameraSensorInfo(black, white, min_iso, max_iso, black_colors));
            }
        }

        public CameraSensorInfo getSensorInfo(int iso)
        {
            // If only one, just return that
            if (sensorInfo.Count == 1)
                return sensorInfo[0];

            List<CameraSensorInfo> candidates = new List<CameraSensorInfo>();
            foreach (CameraSensorInfo i in sensorInfo)
            {
                if (i.isIsoWithin(iso))
                    candidates.Add(i);
            }

            if (candidates.Count == 1)
                return candidates[0];

            foreach (CameraSensorInfo j in candidates)
            {
                if (!j.isDefault())
                    return j;
            }
            return null;
        }
    }
}

