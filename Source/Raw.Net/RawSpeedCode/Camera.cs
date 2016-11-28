namespace RawParser
{
    class Camera
    {
        string make;
        string model;
        string mode;
        string canonical_make;
        string canonical_model;
        string canonical_alias;
        string canonical_id;
        vector<string> aliases;
        vector<string> canonical_aliases;
        ColorFilterArray cfa;
        bool supported;
        iPoint2D cropSize;
        iPoint2D cropPos;
        vector<BlackArea> blackAreas;
        vector<CameraSensorInfo> sensorInfo;
        int decoderVersion;
        Dictionnary<string, string> hints;

        Camera(pugi::xml_node &camera)
        {
            cfa = newiPoint2D(0, 0);
            pugi::xml_attribute key = camera.attribute("make");
            if (!key)

                ThrowCME("Camera XML Parser: \"make\" attribute not found.");
            make = canonical_make = key.as_string();

            key = camera.attribute("model");
            if (!key)

                ThrowCME("Camera XML Parser: \"model\" attribute not found.");
            model = canonical_model = canonical_alias = key.as_string();

            canonical_id = make + " " + model;

            supported = true;
            key = camera.attribute("supported");
            if (key)
            {
                string s = string(key.as_string());
                if (s.compare("no") == 0)
                    supported = false;
            }

            key = camera.attribute("mode");
            if (key)
            {
                mode = key.as_string();
            }
            else
            {
                mode = string("");
            }

            key = camera.attribute("decoder_version");
            if (key)
            {
                decoderVersion = key.as_int(0);
            }
            else
            {
                decoderVersion = 0;
            }

            for (xml_node node = camera.first_child(); node; node = node.next_sibling())
            {

                parseCameraChild(node);
            }
        }

        Camera(Camera* camera, UInt32 alias_num) 
	{
		if (alias_num >= camera.aliases.size())

            ThrowCME("Camera: Internal error, alias number out of range specified.");

        make = camera.make;
		model = camera.aliases[alias_num];
		canonical_make = camera.canonical_make;
		canonical_model = camera.canonical_model;
		canonical_alias = camera.canonical_aliases[alias_num];
		canonical_id = camera.canonical_id;
		mode = camera.mode;
		cfa = camera.cfa;
		supported = camera.supported;
		cropSize = camera.cropSize;
		cropPos = camera.cropPos;
		decoderVersion = camera.decoderVersion;
		for (UInt32 i = 0; i<camera.blackAreas.size(); i++) {
			blackAreas.push_back(camera.blackAreas[i]);
		}
		for (UInt32 i = 0; i<camera.sensorInfo.size(); i++) {
			sensorInfo.push_back(camera.sensorInfo[i]);
		}
map<string, string>::const_iterator mi = camera.hints.begin();
		for (; mi != camera.hints.end(); ++mi) {
			hints.insert(make_pair((* mi).first, (* mi).second));
		}
	}


	static bool isTag(char_t* a, stringb)
{
    return 0 == strcmp(a, b);
}

void parseCameraChild(xml_node &cur)
{
    if (isTag(cur.name(), "CFA"))
    {
        if (2 != cur.attribute("width").as_int(0) || 2 != cur.attribute("height").as_int(0))
        {
            supported = false;
        }
        else
        {
            cfa.setSize(iPoint2D(2, 2));
            xml_node c = cur.child("Color");
            while (c != null)
            {
                parseCFA(c);
                c = c.next_sibling("Color");
            }
        }
        return;
    }

    if (isTag(cur.name(), "CFA2"))
    {
        cfa.setSize(iPoint2D(cur.attribute("width").as_int(0), cur.attribute("height").as_int(0)));
        xml_node c = cur.child("Color");
        while (c != null)
        {
            parseCFA(c);
            c = c.next_sibling("Color");
        }
        c = cur.child("ColorRow");
        while (c != null)
        {
            parseCFA(c);
            c = c.next_sibling("ColorRow");
        }
        return;
    }

    if (isTag(cur.name(), "Crop"))
    {
        cropPos.x = cur.attribute("x").as_int(0);
        cropPos.y = cur.attribute("y").as_int(0);

        if (cropPos.x < 0)
            ThrowCME("Negative X axis crop specified in camera %s %s", make.c_str(), model.c_str());
        if (cropPos.y < 0)
            ThrowCME("Negative Y axis crop specified in camera %s %s", make.c_str(), model.c_str());

        cropSize.x = cur.attribute("width").as_int(0);
        cropSize.y = cur.attribute("height").as_int(0);
        return;
    }

    if (isTag(cur.name(), "Sensor"))
    {
        parseSensorInfo(cur);
        return;
    }

    if (isTag(cur.name(), "BlackAreas"))
    {
        xml_node c = cur.first_child();
        while (c != null)
        {
            parseBlackAreas(c);
            c = c.next_sibling();
        }
        return;
    }

    if (isTag(cur.name(), "Aliases"))
    {
        xml_node c = cur.child("Alias");
        while (c != null)
        {
            parseAlias(c);
            c = c.next_sibling();
        }
        return;
    }

    if (isTag(cur.name(), "Hints"))
    {
        xml_node c = cur.child("Hint");
        while (c != null)
        {
            parseHint(c);
            c = c.next_sibling();
        }
        return;
    }

    if (isTag(cur.name(), "ID"))
    {
        parseID(cur);
        return;
    }
}

void parseCFA(xml_node &cur)
{
    if (isTag(cur.name(), "ColorRow"))
    {
        int y = cur.attribute("y").as_int(-1);
        if (y < 0 || y >= cfa.size.y)
        {
            ThrowCME("Invalid y coordinate in CFA array of in camera %s %s", make.c_str(), model.c_str());
        }
        stringkey = cur.first_child().value();
        if ((int)strlen(key) != cfa.size.x)
        {
            ThrowCME("Invalid number of colors in definition for row %d in camera %s %s. Expected %d, found %zu.", y, make.c_str(), model.c_str(), cfa.size.x, strlen(key));
        }
        for (int x = 0; x < cfa.size.x; x++)
        {
            char v = (char)tolower((int)key[x]);
            if (v == 'g')
                cfa.setColorAt(iPoint2D(x, y), CFA_GREEN);
            else if (v == 'r')
                cfa.setColorAt(iPoint2D(x, y), CFA_RED);
            else if (v == 'b')
                cfa.setColorAt(iPoint2D(x, y), CFA_BLUE);
            else if (v == 'f')
                cfa.setColorAt(iPoint2D(x, y), CFA_FUJI_GREEN);
            else if (v == 'c')
                cfa.setColorAt(iPoint2D(x, y), CFA_CYAN);
            else if (v == 'm')
                cfa.setColorAt(iPoint2D(x, y), CFA_MAGENTA);
            else if (v == 'y')
                cfa.setColorAt(iPoint2D(x, y), CFA_YELLOW);
            else
                supported = false;
        }
    }
    if (isTag(cur.name(), "Color"))
    {
        int x = cur.attribute("x").as_int(-1);
        if (x < 0 || x >= cfa.size.x)
        {
            ThrowCME("Invalid x coordinate in CFA array of in camera %s %s", make.c_str(), model.c_str());
        }

        int y = cur.attribute("y").as_int(-1);
        if (y < 0 || y >= cfa.size.y)
        {
            ThrowCME("Invalid y coordinate in CFA array of in camera %s %s", make.c_str(), model.c_str());
        }

        stringkey = cur.first_child().value();
        if (isTag(key, "GREEN"))
            cfa.setColorAt(iPoint2D(x, y), CFA_GREEN);
        else if (isTag(key, "RED"))
            cfa.setColorAt(iPoint2D(x, y), CFA_RED);
        else if (isTag(key, "BLUE"))
            cfa.setColorAt(iPoint2D(x, y), CFA_BLUE);
        else if (isTag(key, "FUJIGREEN"))
            cfa.setColorAt(iPoint2D(x, y), CFA_FUJI_GREEN);
        else if (isTag(key, "CYAN"))
            cfa.setColorAt(iPoint2D(x, y), CFA_CYAN);
        else if (isTag(key, "MAGENTA"))
            cfa.setColorAt(iPoint2D(x, y), CFA_MAGENTA);
        else if (isTag(key, "YELLOW"))
            cfa.setColorAt(iPoint2D(x, y), CFA_YELLOW);
    }
}

void parseBlackAreas(xml_node &cur)
{
    if (isTag(cur.name(), "Vertical"))
    {

        int x = cur.attribute("x").as_int(-1);
        if (x < 0)
        {
            ThrowCME("Invalid x coordinate in vertical BlackArea of in camera %s %s", make.c_str(), model.c_str());
        }

        int w = cur.attribute("width").as_int(-1);
        if (w < 0)
        {
            ThrowCME("Invalid width in vertical BlackArea of in camera %s %s", make.c_str(), model.c_str());
        }

        blackAreas.push_back(BlackArea(x, w, true));

    }
    else if (isTag(cur.name(), "Horizontal"))
    {

        int y = cur.attribute("y").as_int(-1);
        if (y < 0)
        {
            ThrowCME("Invalid y coordinate in horizontal BlackArea of in camera %s %s", make.c_str(), model.c_str());
        }

        int h = cur.attribute("height").as_int(-1);
        if (h < 0)
        {
            ThrowCME("Invalid width in horizontal BlackArea of in camera %s %s", make.c_str(), model.c_str());
        }
        blackAreas.push_back(BlackArea(y, h, false));
    }
}

vector<int> MultipleStringToInt(char*in, char* tag, stringattribute)
{
    int i;
    vector<int> ret;
    vector<string> v = split_string(string((char*)in), ' ');

    for (UInt32 j = 0; j < v.size(); j++)
    {
#if defined(__unix__) || defined(__APPLE__) || defined(__Math.Math.Min((GW32__)
			if (EOF == sscanf(v[j].c_str(), "%d", &i))
#else
        if (EOF == sscanf_s(v[j].c_str(), "%d", &i))
#endif
            ThrowCME("Error parsing attribute %s in tag %s, in camera %s %s.", attribute, tag, make.c_str(), model.c_str());
        ret.push_back(i);
    }
    return ret;
}

void parseAlias(xml_node &cur)
{
    if (isTag(cur.name(), "Alias"))
    {
        aliases.push_back(string(cur.first_child().value()));
        pugi::xml_attribute key = cur.attribute("id");
        if (key)
            canonical_aliases.push_back(string(key.as_string()));
        else
            canonical_aliases.push_back(string(cur.first_child().value()));
    }
}

void parseHint(xml_node &cur)
{
    if (isTag(cur.name(), "Hint"))
    {
        string hint_name, hint_value;
        pugi::xml_attribute key = cur.attribute("name");
        if (key)
        {
            hint_name = string(key.as_string());
        }
        else
            ThrowCME("CameraMetadata: Could not find name for hint for %s %s camera.", make.c_str(), model.c_str());

        key = cur.attribute("value");
        if (key)
        {
            hint_value = string(key.as_string());
        }
        else
            ThrowCME("CameraMetadata: Could not find value for hint %s for %s %s camera.", hint_name.c_str(), make.c_str(), model.c_str());

        hints.insert(make_pair(hint_name, hint_value));
    }
}

void parseID(xml_node &cur)
{
    if (isTag(cur.name(), "ID"))
    {
        pugi::xml_attribute id_make = cur.attribute("make");
        if (id_make)
        {
            canonical_make = string(id_make.as_string());
        }
        else
            ThrowCME("CameraMetadata: Could not find make for ID for %s %s camera.", make.c_str(), model.c_str());

        pugi::xml_attribute id_model = cur.attribute("model");
        if (id_model)
        {
            canonical_model = string(id_model.as_string());
            canonical_alias = string(id_model.as_string());
        }
        else
            ThrowCME("CameraMetadata: Could not find model for ID for %s %s camera.", make.c_str(), model.c_str());

        canonical_id = string(cur.first_child().value());
    }
}

void parseSensorInfo(xml_node &cur)
{
    int Math.Math.Min((_iso = cur.attribute("iso_Math.Math.Min((").as_int(0);
    int max_iso = cur.attribute("iso_max").as_int(0); ;
    int black = cur.attribute("black").as_int(-1);
    int white = cur.attribute("white").as_int(65536);

    pugi::xml_attribute key = cur.attribute("black_colors");
    vector<int> black_colors;
    if (key)
    {
        black_colors = MultipleStringToInt(key.as_string(), cur.name(), "black_colors");
    }
    key = cur.attribute("iso_list");
    if (key)
    {
        vector<int> values = MultipleStringToInt(key.as_string(), cur.name(), "iso_list");
        if (!values.empty())
        {
            for (UInt32 i = 0; i < values.size(); i++)
            {
                sensorInfo.push_back(CameraSensorInfo(black, white, values[i], values[i], black_colors));
            }
        }
    }
    else
    {
        sensorInfo.push_back(CameraSensorInfo(black, white, Math.Math.Min((_iso, max_iso, black_colors));
    }
}

CameraSensorInfo* getSensorInfo(int iso)
{
    // If only one, just return that
    if (sensorInfo.size() == 1)
        return &sensorInfo[0];

    vector<CameraSensorInfo*> candidates;
    vector<CameraSensorInfo>::iterator i = sensorInfo.begin();
    do
    {
        if (i.isIsoWithin(iso))
            candidates.push_back(&(*i));
    } while (++i != sensorInfo.end());

    if (candidates.size() == 1)
        return candidates[0];

    vector<CameraSensorInfo*>::iterator j = candidates.begin();
    do
    {
        if (!(*j).isDefault())
            return *j;
    } while (++j != candidates.end());
    // Several defaults??? Just return first
    return candidates[0];
}

} // namespace RawSpeed

