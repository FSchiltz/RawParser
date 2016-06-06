using Windows.Storage;

namespace RawParser.Model.Settings
{
    class Settings
    {
        static private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        //ToDO replace by getonread member
        public static bool getBoolSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                bool t = (bool)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + "Default"];
            }
            return (bool)localSettings.Values[name];
        }

        public static int getIntSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                int t = (int)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + "Default"];
            }
            return (int)localSettings.Values[name];
        }

        public static double geDoubleSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                double t = (double)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + "Default"];
            }
            return (double)localSettings.Values[name];
        }

        public static string getStringSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                string t = (string)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + "Default"];
            }
            return (string)localSettings.Values[name];
        }


        public static void InitSettings()
        {
            string def = "Default";
            //checkif settings already exists
            if (localSettings.Values["imageBoxBorder" + def] == null)
                localSettings.Values["imageBoxBorder" + def] = 0.05;
            if (localSettings.Values["previewFactord" + def] == null)
                localSettings.Values["previewFactor" + def] = 4;
            if (localSettings.Values["saveFormat" + def] == null)
                localSettings.Values["saveFormat" + def] = ".jpg";
            if (localSettings.Values["autoPreviewFactor" + def] == null)
                localSettings.Values["autoPreviewFactor" + def] = true;
        }
    }
}
