using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace RawEditor.View.UIHelper
{
    public sealed partial class CropUIHelper : UserControl
    {
        public double Top { get; set; } = 0;
        public double Left { get; set; } = 0;
        public double Bottom { get; set; } = 1;
        public double Right { get; set; } = 1;
        private bool isTopDragging = false;
        private bool isRightDragging = false;
        private Point topClickPos = new Point(0, 0), rightClickPos = new Point(0, 0);

        public CropUIHelper()
        {
            this.InitializeComponent();
            // ResetCrop();
        }

        public void SetSize(int w, int h)
        {
            //set the size
            CropZone.Height = CropSelection.Height = (h - 1);
            CropZone.Width = CropSelection.Width = (w - 1);
            Thumb.Height = Thumb2.Height = CropZone.Height;
            Thumb.Width = Thumb2.Width = CropZone.Width;
            //reset crop
            ResetCrop();
        }

        public void ResetCrop()
        {
            //move the four ellipse to the correct position
            Top = Left = 0;
            Bottom = Right = 1;
            CropSelection.Clip = new RectangleGeometry()
            {
                Rect = new Rect(0, 0, CropZone.Width, CropZone.Height)
            };
            MoveEllipse();
        }

        public void MoveEllipse()
        {
            double controlSize = (RightControl.Height / 2);
            Canvas.SetLeft(TopControl, Left * CropZone.Width - controlSize);
            //Canvas.SetLeft(LeftControl, Left * CropZone.Width);
            Canvas.SetLeft(RightControl, Right * CropZone.Width - controlSize);
            //Canvas.SetLeft(RightControl, Right * CropZone.Width);
            Canvas.SetTop(TopControl, Top * CropZone.Height - controlSize);
            //Canvas.SetTop(LeftControl, Bottom * CropZone.Height);
            Canvas.SetTop(RightControl, Bottom * CropZone.Height - controlSize);
            //Canvas.SetTop(RightControl, Top * CropZone.Height);
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
                //currentPosition.X = currentPosition.X - relative.X;
                //currentPosition.Y = currentPosition.Y - relative.Y;
                //check if not outside bound
                if (currentPosition.Y >= 0 && currentPosition.Y < Canvas.GetTop(RightControl))
                {
                    Canvas.SetTop(TopControl, currentPosition.Y - controlSize);
                    CropSelection.Clip = new RectangleGeometry()
                    {
                        Rect = new Rect(CropSelection.Clip.Rect.Left, currentPosition.Y, CropSelection.Clip.Rect.Width,
                        Canvas.GetTop(RightControl) - currentPosition.Y + controlSize)
                    };
                }

                if (currentPosition.X >= 0 && currentPosition.X < Canvas.GetLeft(RightControl))
                {
                    Canvas.SetLeft(TopControl, currentPosition.X - controlSize);
                    if (currentPosition.Y >= 0 && currentPosition.Y < Canvas.GetTop(RightControl))
                    {
                        Canvas.SetTop(TopControl, currentPosition.Y - controlSize);
                        CropSelection.Clip = new RectangleGeometry()
                        {
                            Rect = new Rect(currentPosition.X, CropSelection.Clip.Rect.Top,
                            Canvas.GetLeft(RightControl) - currentPosition.X + controlSize, CropSelection.Clip.Rect.Height)
                        };
                    }
                }
            }
            else if (isRightDragging)
            {
                Point currentPosition = e.GetCurrentPoint(CropZone).Position;
                Point relative = e.GetCurrentPoint(RightControl).Position;
                double controlSize = (RightControl.Height / 2);
                //currentPosition.X -= relative.X;
                //currentPosition.Y -= relative.Y;*/
                if ((int)currentPosition.Y <= CropZone.Height && (int)currentPosition.Y > Canvas.GetTop(TopControl))
                {
                    Canvas.SetTop(RightControl, currentPosition.Y - controlSize);
                    CropSelection.Clip = new RectangleGeometry()
                    {
                        Rect = new Rect(CropSelection.Clip.Rect.Left, CropSelection.Clip.Rect.Top,
                            CropSelection.Clip.Rect.Width, currentPosition.Y - Canvas.GetTop(TopControl) - controlSize)
                    };
                }
                if ((int)currentPosition.X <= CropZone.Width && (int)currentPosition.X > Canvas.GetLeft(TopControl))
                {
                    Canvas.SetLeft(RightControl, currentPosition.X - controlSize);
                    CropSelection.Clip = new RectangleGeometry()
                    {
                        Rect = new Rect(CropSelection.Clip.Rect.Left, CropSelection.Clip.Rect.Top,
                            currentPosition.X - Canvas.GetLeft(TopControl) - controlSize, CropSelection.Clip.Rect.Height)
                    };
                }
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
            Bottom = ((Canvas.GetTop(RightControl) + controlSize) / CropZone.Height) - Top;
            Right = ((Canvas.GetLeft(RightControl) + controlSize) / CropZone.Width) - Left;
        }

        public void SetThumbAsync(SoftwareBitmap image)
        {
            CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Do some UI-code that must be run on the UI thread.
                //display the image preview
                Thumb.Source = null;
                if (image != null)
                {
                    WriteableBitmap bitmap = new WriteableBitmap(image.PixelWidth, image.PixelHeight);
                    image.CopyToBuffer(bitmap.PixelBuffer);
                    image.Dispose();
                    Thumb.Source = bitmap;
                    Thumb2.Source = bitmap;
                }
            });
        }
    }
}
