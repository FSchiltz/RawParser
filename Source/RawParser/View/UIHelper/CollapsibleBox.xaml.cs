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
    public sealed partial class CollapsibleBox : UserControl
    {
        public bool IsOpen { get; set; } = true;
        public string Text { get { return Header.Text; } set { Header.Text = value; } }

        public CollapsibleBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty MainContentProperty = DependencyProperty.Register("MainContent",
            typeof(object),
            typeof(CollapsibleBox),
            new PropertyMetadata(default(object)));

        public object MainContent
        {
            get { return GetValue(MainContentProperty); }
            set { SetValue(MainContentProperty, value); }
        }

        private void Header_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (Container.Visibility == Visibility.Collapsed)
            {
                Container.Visibility = Visibility.Visible;
            }
            else
            {
                Container.Visibility = Visibility.Collapsed;
            }
        }
    }
}
