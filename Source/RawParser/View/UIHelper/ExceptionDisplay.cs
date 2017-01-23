using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace RawEditor.View.UIHelper
{
    class ExceptionDisplay
    {
        public static void Display(string message)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                //Do some UI-code that must be run on the UI thread.
                var dialog = new MessageDialog(message)
                {
                    Title = "Error"
                };
                dialog.Commands.Add(new UICommand { Label = "Ok", Id = 0 });
                dialog.ShowAsync();
            });
        }
    }
}
