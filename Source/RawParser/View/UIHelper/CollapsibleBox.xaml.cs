using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RawEditor.View.UIHelper
{
    public sealed partial class CollapsibleBox : UserControl
    {
        public bool IsOpen { get; set; } = true;
        public string Text { get { return Header.Text; } set { Header.Text = value; } }
        private double height;

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
            if (IsOpen)
            {
                var sb = new Storyboard();
                height = Container.ActualHeight;
                var animation = new DoubleAnimation()
                {
                    To = 0,
                    From = height,
                    EnableDependentAnimation = true,
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 100))
                };
                Storyboard.SetTargetProperty(animation, "Height");
                Storyboard.SetTarget(animation, Container);
                sb.Children.Add(animation);
                sb.Completed += DoubleAnimation_Completed;
                sb.Begin();
                VisualStateManager.GoToState(this, "Collapsed", true);
            }
            else
            {
                Container.Visibility = Visibility.Visible;
                var sb = new Storyboard();
                var animation = new DoubleAnimation()
                {
                    To = height,
                    From = 0,
                    EnableDependentAnimation = true,
                    Duration = new Duration(new TimeSpan(0, 0, 0, 0, 100))
                };
                Storyboard.SetTargetProperty(animation, "Height");
                Storyboard.SetTarget(animation, Container);
                sb.Children.Add(animation);
                sb.Begin();
                icon.Symbol = Symbol.BackToWindow;
                VisualStateManager.GoToState(this, "Expanded", true);
            }
            IsOpen = !IsOpen;
        }

        private void DoubleAnimation_Completed(object sender, object e)
        {
            Container.Visibility = Visibility.Collapsed;
            icon.Symbol = Symbol.FullScreen;
        }
    }
}