using RawParser.Model.ImageDisplay;
using RawParser.Model.Parser;
using RawParserUWP.Model.Exception;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// Pour plus d'informations sur le modèle d'élément Page vierge, consultez la page http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RawParserUWP
{
    /// <summary>
    /// Une page vide peut être utilisée seule ou constituer une page de destination au sein d'un frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private RawImage currentRawImage { set; get; }
        private bool imageSelected { set; get; }

        public MainPage()
        {
            this.InitializeComponent();
            appBarImageChoose.Click += new RoutedEventHandler(appBarImageChooseClick);
            imageSelected = false;
        }

        private async void appBarImageChooseClick(object sender, RoutedEventArgs e)
        {
            FileOpenPicker filePicker = new FileOpenPicker();
            filePicker.ViewMode = PickerViewMode.Thumbnail;
            filePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            filePicker.FileTypeFilter.Add(".nef");
            filePicker.FileTypeFilter.Add(".tiff");
            filePicker.FileTypeFilter.Add(".dng");
            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                // Application now has read/write access to the picked file
                try
                {
                    //Open the file with the correct parser
                    Parser parser;
                    switch (file.FileType.ToUpper())
                    {
                        case ".NEF":
                            parser = new NEFParser();
                            break;
                        case ".DNG":
                            parser = new DNGParser();
                            break;
                        case ".TIFF":
                            parser = new DNGParser();
                            break;
                        default: throw new Exception("File not supported");//todo change exception types
                    }
                    Stream stream = (await file.OpenReadAsync()).AsStreamForRead();
                    Task t = Task.Run(() => this.currentRawImage = parser.parse(stream));
                    t.Wait();
                    ExceptionDisplay.display("test");
                    //display the image
                    imageBox.Source = currentRawImage.getImageasBitMap();
                }
                catch (Exception ex)
                {
                    ExceptionDisplay.display(ex.Message + ex.StackTrace + file.FileType.ToUpper());
                }
            }
            else
            {
                //TODO
            }
        }
    }
}
