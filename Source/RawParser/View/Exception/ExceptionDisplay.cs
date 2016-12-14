using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;

namespace RawEditor.View.Exception
{
    class ExceptionDisplay
    {
        public static async void DisplayAsync(string message)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
             {
                 //Do some UI-code that must be run on the UI thread.
                 var dialog = new MessageDialog(message)
                 {
                     Title = "Error"
                 };
                 dialog.Commands.Add(new UICommand { Label = "Ok", Id = 0 });
                 var res = await dialog.ShowAsync();
             });
        }
    }
}
