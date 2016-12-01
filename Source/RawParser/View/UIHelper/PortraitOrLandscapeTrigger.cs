using Windows.UI.Xaml;

namespace App1.StateTriggers
{
    public class PortraitOrLandscapeTrigger : StateTriggerBase
    {
        private readonly Window _window;
        private PortraitLandscapeMode _mode;


        public PortraitOrLandscapeTrigger()
        {
            _window = Window.Current;
            _window.SizeChanged += _window_SizeChanged;
            _window_SizeChanged(null, null);
        }

        public PortraitLandscapeMode Mode
        {
            get { return _mode; }
            set
            {
                _mode = value;
                _window_SizeChanged(null, null);
            }
        }

        private void _window_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            var bounds = _window.Bounds;
            if (Mode == PortraitLandscapeMode.Portrait)
            {
                SetActive(bounds.Height > bounds.Width);
            }
            else
            {
                SetActive(bounds.Height <= bounds.Width);
            }
        }
    }

    public enum PortraitLandscapeMode
    {
        Portrait,
        Landscape,
    }
}
