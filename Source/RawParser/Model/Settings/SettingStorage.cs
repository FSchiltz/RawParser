using RawNet;
using System;
using Windows.Storage;
using Windows.UI.Xaml;

namespace RawEditor
{
    public enum ThemeEnum { Dark, Light, System }

    static class SettingStorage
    {
        static private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        private static string def = "Default";
        private static uint version = 1;
        //check if settings already exists
        public static double ImageBoxBorder
        {
            get { return GetDoubleSetting("imageBoxBorder"); }
            set { localSettings.Values["imageBoxBorder"] = value; }
        }
        public static FactorValue PreviewFactor
        {
            get
            {
                Enum.TryParse(GetStringSetting("previewFactor"), out FactorValue res);
                return res;
            }
            set { localSettings.Values["previewFactor"] = value.ToString(); }
        }
        public static string SaveFormat
        {
            get { return GetStringSetting("saveFormat"); }
            set { localSettings.Values["saveFormat"] = value; }
        }

        public static DemosAlgorithm DemosAlgo
        {
            get
            {
                Enum.TryParse(GetStringSetting("demosAlgo"), out DemosAlgorithm res);
                return res;
            }
            set { localSettings.Values["demosAlgo"] = value.ToString(); }
        }

        public static ThemeEnum SelectedTheme
        {
            get
            {
                Enum.TryParse(GetStringSetting("Theme"), out ThemeEnum res);
                return res;
            }
            set
            {
                localSettings.Values["Theme"] = value.ToString();
            }
        }

        public static void Init()
        {
            localSettings.Values["imageBoxBorder" + def] = 0.05;
            localSettings.Values["previewFactor" + def] = FactorValue.Auto.ToString();
            localSettings.Values["saveFormat" + def] = ".jpg";
            localSettings.Values["autoPreviewFormat" + def] = false;
            localSettings.Values["demosAlgo" + def] = DemosAlgorithm.NearNeighbour.ToString();
            localSettings.Values["Theme" + def] = ThemeEnum.System.ToString();
            if (localSettings.Values["version"] == null || (uint)localSettings.Values["version"] < version)
                Reset();
            localSettings.Values["version"] = version;

        }

        //ToDO replace by getonread member
        private static bool GetBoolSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                bool t = (bool)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + def];
            }
            return (bool)localSettings.Values[name];
        }

        private static int GetIntSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                int t = (int)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + def];
            }
            return (int)localSettings.Values[name];
        }

        private static double GetDoubleSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                double t = (double)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + def];
            }
            return (double)localSettings.Values[name];
        }

        private static string GetStringSetting(string name)
        {
            if (localSettings.Values[name] != null)
            {
                string t = (string)localSettings.Values[name];
            }
            else
            {
                localSettings.Values[name] = localSettings.Values[name + def];
            }
            return (string)localSettings.Values[name];
        }

        internal static void Reset()
        {
            localSettings.Values["imageBoxBorder"] = localSettings.Values["imageBoxBorder" + def];
            localSettings.Values["previewFactor"] = localSettings.Values["previewFactor" + def];
            localSettings.Values["saveFormat"] = localSettings.Values["saveFormat" + def];
            localSettings.Values["autoPreviewFormat"] = localSettings.Values["autoPreviewFormat" + def];
            localSettings.Values["demosAlgo"] = localSettings.Values["demosAlgo" + def];
            localSettings.Values["Theme"] = localSettings.Values["Theme" + def];
        }
    }
}
