using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace RawEditor.View.UIHelper
{
    class TextDisplay
    {
        public static void DisplayError(string message)
        {
            var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            Display(message, loader.GetString("Error"), loader.GetString("Ok"));
        }

        public static void DisplayWarning(string message)
        {
            var loader = new Windows.ApplicationModel.Resources.ResourceLoader();
            Display(message, loader.GetString("Warning"), loader.GetString("Ok"));
        }

        public static void Display(string message, string title, string button)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                //Do some UI-code that must be run on the UI thread.
                var dialog = new MessageDialog(message)
                {
                    Title = title
                };
                dialog.Commands.Add(new UICommand { Label = button, Id = 0 });
                await dialog.ShowAsync();
            });
        }
    }
}