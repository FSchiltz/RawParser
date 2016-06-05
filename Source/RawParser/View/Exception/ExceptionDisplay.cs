using System;
using Windows.UI.Popups;

namespace RawParserUWP.View.Exception
{
    class ExceptionDisplay
    {
        public static async void display(string message) {
            var dialog = new MessageDialog(message);
            dialog.Title = "Error";
            dialog.Commands.Add(new UICommand { Label = "Ok", Id = 0 });
            var res = await dialog.ShowAsync();

        }
    }
}
