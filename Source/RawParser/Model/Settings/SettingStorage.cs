using Windows.Storage;

namespace RawEditor.Model.Settings
{
    static class SettingStorage
    {
        static private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        private static string def = "Default";
        //checkif settings already exists
        public static double imageBoxBorder
        {
            get { return getDoubleSetting("imageBoxBorder"); }
            set { localSettings.Values["imageBoxBorder"] = value; }
        }
        public static int previewFactor
        {
            get { return getIntSetting("previewFactor"); }
            set { localSettings.Values["previewFactor"] = value; }
        }
        public static string saveFormat
        {
            get { return getStringSetting("saveFormat"); }
            set { localSettings.Values["saveFormat"] = value; }
        }
        public static bool autoPreviewFactor
        {
            get { return getBoolSetting("autoPreviewFormat"); }
            set { localSettings.Values["autoPreviewFormat"] = value; }
        }

        public static void init()
        {
            localSettings.Values["imageBoxBorder" + def] = 0.05;
            localSettings.Values["previewFactor" + def] = 4;
            localSettings.Values["saveFormat" + def] = ".jpg";
            localSettings.Values["autoPreviewFormat" + def] = true;
        }

        //ToDO replace by getonread member
        private static bool getBoolSetting(string name)
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

        private static int getIntSetting(string name)
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

        private static double getDoubleSetting(string name)
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

        private static string getStringSetting(string name)
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
    }
}
