using RawNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace RawEditor.View.UIHelper
{
    public sealed partial class SaveDialog : ContentDialog
    {
        public SaveDialog(string suggestedFileName)
        {
            InitializeComponent();
            /*
            foreach (KeyValuePair<string, List<string>> format in FormatHelper.SaveSupportedFormat)
            {
                savePicker.FileTypeChoices.Add(format.Key, format.Value);
            }
            StorageFile file = await savePicker.PickSaveFileAsync();*/
        }

        public object Result { get; internal set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {

        }
    }
}
