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
            Display(message, "Error", "Ok");
        }

        public static void DisplayWarning(string message)
        {
            Display(message, "Warning", "Ok");
        }

        public static void Display(string message, string title, string button)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Do some UI-code that must be run on the UI thread.
                var dialog = new MessageDialog(message)
                {
                    Title = title
                };
                dialog.Commands.Add(new UICommand { Label = button, Id = 0 });
                dialog.ShowAsync();
            });
        }
    }
}