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
        public AlbumView()
        {
            InitializeComponent();
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
    }
}
