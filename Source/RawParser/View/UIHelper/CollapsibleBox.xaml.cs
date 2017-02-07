using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RawEditor.View.UIHelper
{
    public sealed partial class CollapsibleBox : UserControl
    {
        private bool open = true;
        public bool IsOpen
        {
            get { return open; }
            set
            {
                open = value;
                var sb = new Storyboard();
                DoubleAnimation animation;
                if (!IsOpen)
                {
                    height = Container.ActualHeight;
                    sb.Completed += (a, b) =>
                    {
                        Container.Visibility = Visibility.Collapsed;
                        icon.Symbol = Symbol.ShowBcc;
                    };
                    animation = new DoubleAnimation()
                    {
                        To = 0,
                        From = height,
                        EnableDependentAnimation = true,
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 100))
                    };
                }
                else
                {
                    Container.Visibility = Visibility.Visible;
                    animation = new DoubleAnimation()
                    {
                        To = height,
                        From = 0,
                        EnableDependentAnimation = true,
                        Duration = new Duration(new TimeSpan(0, 0, 0, 0, 100))
                    };
                    icon.Symbol = Symbol.HideBcc;
                }
                Storyboard.SetTargetProperty(animation, "Height");
                Storyboard.SetTarget(animation, Container);
                sb.Children.Add(animation);
                sb.Begin();
            }
        }
        public string Text { get { return Header.Text; } set { Header.Text = value; } }
        private double height;

        public CollapsibleBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty MainContentProperty = DependencyProperty.Register("MainContent", typeof(object), typeof(CollapsibleBox), new PropertyMetadata(default(object)));

        public object MainContent
        {
            get { return GetValue(MainContentProperty); }
            set { SetValue(MainContentProperty, value); }
        }

        public void Toggle() { IsOpen = !IsOpen; }
    }
}