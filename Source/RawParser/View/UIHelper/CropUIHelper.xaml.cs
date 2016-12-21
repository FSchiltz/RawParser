using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RawEditor.View.UIHelper
{
    public sealed partial class CropUIHelper : UserControl
    {
        public double Top { get; set; } = 0.2;
        public double Left { get; set; } = 0.2;
        public double Bottom { get; set; } = 0.8;
        public double Right { get; set; } = 0.8;
        public CropUIHelper()
        {
            this.InitializeComponent();
            ResetCrop();
        }

        public void SetSize()
        {
            //set the size
            
            //reset crop
            ResetCrop();
        }

        public void ResetCrop()
        {
            //move the four ellipse to the correct position
        }
    }
}
