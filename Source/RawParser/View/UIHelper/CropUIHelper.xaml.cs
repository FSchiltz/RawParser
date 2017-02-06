using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace RawEditor.View.UIHelper
{
    public sealed partial class CropUIHelper : UserControl
    {
        public double Top
        {
            get
            {
                if (rotation == 1)
                {
                    return 1 - width;
                }
                else if (rotation == 2)
                {
                    return 1 - height;
                }
                else if (rotation == 3)
                {
                    return left;
                }
                else
                    return top;
            }
            set
            {
                if (value > 1) top = 1;
                else if (value < 0) top = 0;
                else top = value;
            }
        }
        public double Left
        {
            get
            {
                if (rotation == 1)
                {
                    return top;
                }
                else if (rotation == 2)
                    return 1 - width;
                else if (rotation == 3)
                    return 1 - height;
                else
                    return left;
            }
            set
            {
                if (value > 1) left = 1;
                else if (value < 0) left = 0;
                else left = value;
            }
        }
        public double Bottom
        {
            get
            {
                if (rotation % 2 == 1)
                    return width - left;
                else
                    return height - top;
            }
            set
            {
                if (value > 1) height = 1;
                else if (value < 0) height = 0;
                else height = value;
            }
        }
        public double Right
        {
            get
            {
                if (rotation % 2 == 1)
                    return height - top;
                else
                    return width - left;
            }
            set
            {
                if (value > 1) width = 1;
                else if (value < 0) width = 0;
                else width = value;
            }
        }

        public int rotation = 0;
        public bool isTopDragging = false, isRightDragging = false;
        private Point topClickPos = new Point(0, 0), rightClickPos = new Point(0, 0);
        private double left, top, width, height;

        public CropUIHelper()
        {
            InitializeComponent();
            ResetCrop();
        }

        public void SetSize(int width, int height, int rotation)
        {
            //set the size
            CropZone.Height = CropSelection.Height = (height - 1);
            CropZone.Width = CropSelection.Width = (width - 1);
            Thumb.Height = Thumb2.Height = CropZone.Height;
            Thumb.Width = Thumb2.Width = CropZone.Width;
            
            //move crop control to correct position
            if (rotation != this.rotation)
            {
                ResetCrop();
                this.rotation = rotation;
            }
            else
            {
                MoveEllipse();
            }
        }

        public void ResetCrop()
        {
            //move the four ellipse to the correct position
            top = left = 0;
            height = width = 1;
            MoveEllipse();
        }

        public void MoveEllipse()
        {
            double controlSize = (RightControl.Height / 2);
            Canvas.SetLeft(TopControl, left * CropZone.Width - controlSize);
            Canvas.SetTop(TopControl, top * CropZone.Height - controlSize);

            Canvas.SetLeft(RightControl, (width) * CropZone.Width - controlSize);
            Canvas.SetTop(RightControl, (height) * CropZone.Height - controlSize);
            CropSelection.Clip = new RectangleGeometry()
            {
                Rect = new Rect(left * CropZone.Width, top * CropZone.Height, (width - left) * CropZone.Width, (height - top) * CropZone.Height)
            };
        }

        private void TopControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            isTopDragging = true;
            e.Handled = true;
        }


        private void RightControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            isRightDragging = true;
            e.Handled = true;
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (isTopDragging)
            {
                Point currentPosition = e.GetCurrentPoint(CropZone).Position;
                double controlSize = (RightControl.Height / 2);
                //check if not outside bound
                double y = currentPosition.Y, x = currentPosition.X;
                if (y < 0) y = 0;
                else if (y > Canvas.GetTop(RightControl)) y = Canvas.GetTop(RightControl);
                if (x < 0) x = 0;
                else if (x > Canvas.GetLeft(RightControl)) x = Canvas.GetLeft(RightControl);

                Canvas.SetTop(TopControl, y - controlSize);
                Canvas.SetLeft(TopControl, x - controlSize);
                CropSelection.Clip = new RectangleGeometry()
                {
                    Rect = new Rect(x, y, Canvas.GetLeft(RightControl) - x + controlSize, Canvas.GetTop(RightControl) - y + controlSize)
                };
            }
            else if (isRightDragging)
            {
                Point currentPosition = e.GetCurrentPoint(CropZone).Position;
                double controlSize = (RightControl.Height / 2);
                double x = currentPosition.X, y = currentPosition.Y;
                if (y > CropZone.Height) y = CropZone.Height;
                else if (y < Canvas.GetTop(TopControl)) y = Canvas.GetTop(TopControl);
                if (x > CropZone.Width) x = CropZone.Width;
                else if (x < Canvas.GetLeft(TopControl)) x = Canvas.GetLeft(TopControl);

                Canvas.SetTop(RightControl, y - controlSize);
                Canvas.SetLeft(RightControl, x - controlSize);
                CropSelection.Clip = new RectangleGeometry()
                {
                    Rect = new Rect(CropSelection.Clip.Rect.Left, CropSelection.Clip.Rect.Top,
                        x - Canvas.GetLeft(TopControl) - controlSize, y - Canvas.GetTop(TopControl) - controlSize)
                };
            }
            e.Handled = true;
        }

        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            isTopDragging = false;
            isRightDragging = false;
            double controlSize = (RightControl.Height / 2);
            Top = (Canvas.GetTop(TopControl) + controlSize) / CropZone.Height;
            Left = (Canvas.GetLeft(TopControl) + controlSize) / CropZone.Width;
            Bottom = ((Canvas.GetTop(RightControl) + controlSize) / CropZone.Height);
            Right = ((Canvas.GetLeft(RightControl) + controlSize) / CropZone.Width);
        }

        public void SetThumbAsync(SoftwareBitmap image)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Do some UI-code that must be run on the UI thread.
                //display the image preview
                Thumb.Source = Thumb2.Source = null;
                if (image != null)
                {
                    WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                    image.CopyToBuffer(bitmap.PixelBuffer);
                    image.Dispose();
                    Thumb.Source = Thumb2.Source = bitmap;
                }
            });
        }
    }
}
