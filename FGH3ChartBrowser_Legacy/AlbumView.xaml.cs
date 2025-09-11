using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FGH3ChartBrowser
{
    /// <summary>
    /// Interaction logic for AlbumView.xaml
    /// </summary>
    public partial class AlbumView : Window
    {
        private double resizeStep = 50.0;
        private double _initialWidth;
        private double _initialHeight;
        private bool lockScrollZoom;

        public AlbumView()
        {
            InitializeComponent();
            lockScrollZoom = false;
            this.IsManipulationEnabled = true;
        }
        private void CopyAlbumArtToClipboard(object sender, RoutedEventArgs e)
        {
            ImageBrush brush = (ImageBrush)this.Background;
            ImageSource imgsrc = brush.ImageSource;
            Clipboard.SetImage((BitmapSource)imgsrc);
        }
        private void ExitAlbumView(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AlbumViewWin_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (lockScrollZoom) return;
            _initialWidth = this.Width;
            _initialHeight = this.Height;

            if (e.Delta > 0) // Mouse wheel scrolled up
            {
                if (this.Width + resizeStep > SystemParameters.PrimaryScreenWidth ||
                    this.Width + SystemParameters.WindowCaptionHeight > SystemParameters.PrimaryScreenHeight)
                {
                    return; // Prevent exceeding screen dimensions
                }
                // Increase width and height
                this.Width += resizeStep;
                this.Height += resizeStep;
            }
            else // Mouse wheel scrolled down
            {
                if (this.Width > this.MinWidth + resizeStep
                    && this.Width - resizeStep + SystemParameters.WindowCaptionHeight > this.MinHeight)
                {
                    this.Width -= resizeStep;
                    this.Height = this.Width + SystemParameters.WindowCaptionHeight;
                }
            }
        }

        private void AlbumViewWin_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            lockScrollZoom = true; // Prevent mouse wheel zooming during touch manipulation
            _initialWidth = this.Width;
            _initialHeight = this.Height;
            e.ManipulationContainer = this;
        }

        private void AlbumViewWin_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            double newWidth = this.Width * e.DeltaManipulation.Scale.X;
            double newHeight = newWidth + SystemParameters.WindowCaptionHeight;
            bool canZoomOut = true;
            bool canZoomIn = true;
            if (newWidth > SystemParameters.PrimaryScreenWidth || newHeight > SystemParameters.PrimaryScreenHeight
                || newWidth > this.MaxWidth || newHeight > this.MaxHeight)
            {
                canZoomIn = false; // Prevent exceeding screen dimensions
            }
            if (newWidth < this.MinWidth || newHeight < this.MinHeight)
            {
                canZoomOut = false; // Prevent going below minimum size
            }
            if (!canZoomIn && e.DeltaManipulation.Scale.X > 1.0) return;
            if (!canZoomOut && e.DeltaManipulation.Scale.X < 1.0) return;
            this.Width = newWidth;
            this.Height = newHeight;
        }

        private void AlbumViewWin_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            lockScrollZoom = false; // Re-enable mouse wheel zooming
            if (this.Height != this.Width + SystemParameters.WindowCaptionHeight
                && this.Width + SystemParameters.WindowCaptionHeight <= this.MaxHeight)
            {
                this.Height = this.Width + SystemParameters.WindowCaptionHeight; // Maintain aspect ratio, accounting for title bar
            }
        }
    }
}
