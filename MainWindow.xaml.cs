using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.CodeDom.Compiler;
using System.Configuration.Internal;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using System.Text.RegularExpressions;
using SngParser;
using System.Drawing;
using System.Windows.Interop;
using System.Drawing.Imaging;
using System.IO.Pipes;
using System.Drawing.Drawing2D;
using Microsoft.VisualBasic;
using SysConfig = System.Configuration;
using System.Collections.Specialized;

namespace FGH3ChartBrowser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string scanFolder;
        public List<SongEntry> songList;
        public int totalSongs;
        public int scannedSongs;
        public int scanProgress;
        public BackgroundWorker bgWorker;
        public string currentLoadingPhrase;
        public SysConfig.Configuration config;

        public MainWindow()
        {
            scanFolder = "";
            songList = new List<SongEntry>() { };
            InitializeComponent();
            totalSongs = 0;
            scannedSongs = 0;
            SongsDataGrid.ItemsSource = songList;
            bgWorker = new BackgroundWorker();
            Thread.Sleep(10);
            LoadConfig();
        }

        private void LoadConfig()
        {
            config = SysConfig.ConfigurationManager.OpenExeConfiguration(SysConfig.ConfigurationUserLevel.None);
            FGH3_Path_TxtBox.Text = config.AppSettings.Settings["fastgh3_exe_location"].Value;
            if (String.IsNullOrEmpty(FGH3_Path_TxtBox.Text))
            {
                FGH3_Path_TxtBox.Text = @"C:\Program Files (x86)\FastGH3\FastGH3.exe";
                config.AppSettings.Settings["fastgh3_exe_location"].Value = @"C:\Program Files (x86)\FastGH3\FastGH3.exe";
                config.Save();
            }
            scanFolder = config.AppSettings.Settings["charts_folder"].Value;
            Chart_Folder_TxtBox.Text = scanFolder;
        }

        private void GH3PathBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                DefaultExt = ".exe",
                Filter = "The game (FastGH3.exe)|FastGH3.exe",
                DefaultDirectory = "C:\\Program Files (x86)\\FastGH3"
            };

            Nullable<bool> result = dlg.ShowDialog();

            if (result.HasValue && result.Value)
            {
                string filename = dlg.FileName;
                FGH3_Path_TxtBox.Text = filename;
                config.AppSettings.Settings["fastgh3_exe_location"].Value = filename;
                config.Save();
            }
        }

        private async void ScanSongs(object sender, DoWorkEventArgs e)
        {
            string searchPath = scanFolder;
            BackgroundWorker bw = sender as BackgroundWorker;
            if (!String.IsNullOrWhiteSpace(searchPath) && System.IO.Path.Exists(searchPath) && bw != null)
            {
                IEnumerable<string> charts = Directory.EnumerateFiles(searchPath, "notes.chart", SearchOption.AllDirectories);
                IEnumerable<string> midis = Directory.EnumerateFiles(searchPath, "notes.mid", SearchOption.AllDirectories);
                IEnumerable<string> sngs = Directory.EnumerateFiles(searchPath, "*.sng", SearchOption.AllDirectories);
                // TO DO: implement a better way of counting songs?
                totalSongs = charts.Count<string>() + midis.Count<string>() + sngs.Count<string>();
                if (totalSongs > 0)
                {
                    songList.Clear();
                    scannedSongs = 0;
                    foreach (string chart in charts)
                    {
                        FileInfo fileInfo = new FileInfo(chart);
                        string chartDir = fileInfo.Directory.FullName;
                        string[] songIniFiles = Directory.GetFiles(chartDir, "song.ini", SearchOption.TopDirectoryOnly);
                        if (songIniFiles != null)
                        {
                            if (songIniFiles.Length > 0)
                            {
                                SongEntry songEntry = new SongEntry();
                                IConfiguration config = null;
                                string artist = "Unknown Artist";
                                string title = "Unknown Title";
                                string album = "Unknown Album";
                                string charter = "Unknown Charter";
                                string genre = "";
                                string loadingPhrase = "";
                                int year = 0;
                                try
                                {
                                    config = new ConfigurationBuilder().AddIniFile(songIniFiles[0]).Build();
                                    IConfigurationSection songSect = config.GetSection("song");
                                    artist = RemoveHtml(songSect["artist"]);
                                    title = RemoveHtml(songSect["name"]);
                                    album = RemoveHtml(songSect["album"]);
                                    charter = RemoveHtml(songSect["charter"]);
                                    genre = RemoveHtml(songSect["genre"]);
                                    loadingPhrase += RemoveHtml((songSect["loading_phrase"]+"").Replace("<br>", "\n"));
                                    int.TryParse(config.GetSection("song")["year"], out year);
                                }
                                catch { }
                                if (String.IsNullOrWhiteSpace(artist)) artist = "Unknown Artist";
                                if (String.IsNullOrWhiteSpace(title)) title = "Unknown Title";
                                if (String.IsNullOrWhiteSpace(album)) album = "Unknown Album";
                                if (String.IsNullOrWhiteSpace(charter)) charter = "Unknown Charter";
                                songEntry.Artist = "" + artist;
                                songEntry.Title = "" + title;
                                songEntry.Album = "" + album;
                                songEntry.Charter = "" + charter;
                                songEntry.Genre = genre;
                                songEntry.Year = year;
                                songEntry.Path = chart;
                                songEntry.LoadingPhrase = loadingPhrase;
                                songList.Add(songEntry);
                                
                                scannedSongs += 1;
                            }
                        }
                        else
                        {
                            totalSongs -= 1;
                        }
                        scanProgress = scannedSongs / totalSongs * 100;
                        bw.ReportProgress(scanProgress);
                    }
                    foreach (string midi in midis)
                    {
                        FileInfo fileInfo = new FileInfo(midi);
                        string chartDir = fileInfo.Directory.FullName;
                        string[] songIniFiles = Directory.GetFiles(chartDir, "song.ini", SearchOption.TopDirectoryOnly);
                        if (songIniFiles != null)
                        {
                            if (songIniFiles.Length > 0)
                            {
                                SongEntry songEntry = new SongEntry();
                                IConfiguration config = null;
                                string artist = "Unknown Artist";
                                string title = "Unknown Title";
                                string album = "Unknown Album";
                                string charter = "Unknown Charter";
                                string genre = "";
                                string loadingPhrase = "";
                                int year = 0;
                                try
                                {
                                    config = new ConfigurationBuilder().AddIniFile(songIniFiles[0]).Build();
                                    IConfigurationSection songSect = config.GetSection("song");
                                    artist = RemoveHtml(songSect["artist"]);
                                    title = RemoveHtml(songSect["name"]);
                                    album = RemoveHtml(songSect["album"]);
                                    charter = RemoveHtml(songSect["charter"]);
                                    genre = RemoveHtml(songSect["genre"]);
                                    loadingPhrase += RemoveHtml((songSect["loading_phrase"] + "").Replace("<br>","\n"));
                                    int.TryParse(config.GetSection("song")["year"], out year);
                                }
                                catch { }
                                if (String.IsNullOrWhiteSpace(artist))  artist = "Unknown Artist";
                                if (String.IsNullOrWhiteSpace(title))   title = "Unknown Title";
                                if (String.IsNullOrWhiteSpace(album))   album = "Unknown Album";
                                if (String.IsNullOrWhiteSpace(charter)) charter = "Unknown Charter";
                                songEntry.Artist = "" + artist;
                                songEntry.Title = "" + title;
                                songEntry.Album = "" + album;
                                songEntry.Charter = "" + charter;
                                songEntry.Genre = genre;
                                songEntry.Year = year;
                                songEntry.Path = midi;
                                songEntry.LoadingPhrase = loadingPhrase;
                                songList.Add(songEntry);

                                scannedSongs += 1;
                            }
                        }
                        else
                        {
                            totalSongs -= 1;
                        }
                        scanProgress = scannedSongs / totalSongs * 100;
                        bw.ReportProgress(scanProgress);
                    }
                    foreach (string sngPath in sngs)
                    {
                        FileInfo fileInfo = new FileInfo(sngPath);
                        // I think I got this done correctly...?
                        SongEntry songEntry = new SongEntry();
                        songEntry.Genre = "";
                        songEntry.LoadingPhrase = "";
                        songEntry.Year = 0;
                        try
                        {
                            Sng sngData = Sng.Load(sngPath);
                            songEntry.Artist        = sngData.meta["artist"];
                            songEntry.Title         = sngData.meta["name"];
                            songEntry.Album         = sngData.meta["album"];
                            songEntry.Genre         = sngData.meta["genre"];
                            songEntry.Charter       = sngData.meta["charter"];
                            songEntry.LoadingPhrase = sngData.meta["loading_phrase"];
                            int year = 0;
                            int.TryParse(sngData.meta["year"], out year);
                            songEntry.Year = year;
                        }
                        catch
                        {
                            songEntry.Artist = "Unknown Artist";
                            songEntry.Title = "Unknown Title";
                            songEntry.Album = "Unknown Album";
                            songEntry.Charter = "Unknown Charter";
                        }
                        songEntry.Path = sngPath;
                        songList.Add(songEntry);
                    }
                }
            }
        }

        private void UpdateScanProgress(object? sender, ProgressChangedEventArgs e)
        {

        }

        private void FinishedScanning(object? sender, RunWorkerCompletedEventArgs e)
        {
            SongsDataGrid.ItemsSource = songList;
            
            CollectionViewSource.GetDefaultView(SongsDataGrid.ItemsSource).Filter = this.SongFilter;
            ScanProgressBar.IsIndeterminate = false;
            ScanChartsBtn.IsEnabled = true;
        }

        private void ScanChartsBtn_Click(object sender, RoutedEventArgs e)
        {
            ScanChartsBtn.IsEnabled = false;

            scanFolder = Chart_Folder_TxtBox.Text;

            ScanProgressBar.IsIndeterminate = true;
            bgWorker.WorkerReportsProgress = true;
            bgWorker.DoWork += ScanSongs;
            bgWorker.ProgressChanged += UpdateScanProgress;
            bgWorker.RunWorkerCompleted += FinishedScanning;
            bgWorker.RunWorkerAsync();
        }

        public static string RemoveHtml(string input)
        {
            return Regex.Replace(input, "<[a-zA-Z/].*?>", String.Empty);
        }

        private void ChartsPathBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog();
            Nullable<bool> result = dlg.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Chart_Folder_TxtBox.Text = dlg.FolderName;
                config.AppSettings.Settings["charts_folder"].Value = dlg.FolderName;
                config.Save();
            }
        }

        private void PlaySongBtn_Click(object sender, RoutedEventArgs e)
        {
            PlaySong();
        }

        private bool SongFilter(object item)
        {
            if (String.IsNullOrEmpty(SearchTxtBox.Text))
            {
                return true;
            }
            else if (item != null)
            {
                if (item.GetType() == typeof(SongEntry))
                {
                    string search = SearchTxtBox.Text.Replace(" ", "").Replace("-", "");
                    SongEntry entry = (SongEntry)item;
                    string results = "";

                    if (entry.Artist != null) results += entry.Artist;
                    if (entry.Title != null) results += entry.Title;
                    if (entry.Artist != null) results += entry.Artist; // in case someone switches artist and title
                    if (entry.Album != null) results += entry.Album;
                    if (entry.Genre != null) results += entry.Genre;
                    if (entry.Charter != null) results += entry.Charter;
                    if (entry.Path != null) results += entry.Path;

                    results = results.Replace(" ", "").Replace("-", "");

                    return (results.Contains(search, StringComparison.OrdinalIgnoreCase));
                }
                else { return true; }
            }
            else return false;
            
        }

        private void SearchTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!SongFilter(SongsDataGrid.SelectedItem)) SongsDataGrid.SelectedItem = null;
            SongsDataGrid.Items.Filter = SongFilter;
            SongsDataGrid.Items.Refresh();
        }

        private void PlaySong()
        {
            int index = SongsDataGrid.SelectedIndex;
            if (index >= 0)
            {
                var entry = SongsDataGrid.SelectedItems[0];
                if (entry.GetType() == typeof(SongEntry))
                {
                    string shart = (entry as SongEntry).Path;
                    if (System.IO.Path.Exists(FGH3_Path_TxtBox.Text))
                        System.Diagnostics.Process.Start(FGH3_Path_TxtBox.Text, "\"" + shart + "\"");
                }
            }
        }

        private void FGH3SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.Path.Exists(FGH3_Path_TxtBox.Text))
                System.Diagnostics.Process.Start(FGH3_Path_TxtBox.Text, "-settings");
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public async void SetAlbumArt(string filepath)
        {
            BitmapImage bi = new BitmapImage(new Uri(filepath, UriKind.Absolute));
            AlbumRect.Fill = new ImageBrush(bi);
            AlbumRect.Visibility = Visibility.Visible;
        }
        public async void SetAlbumArt(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            BitmapImage bi;
            try
            {
                bi = (BitmapImage)Imaging.CreateBitmapSourceFromHBitmap(
                     hBitmap,
                     IntPtr.Zero,
                     Int32Rect.Empty,
                     BitmapSizeOptions.FromEmptyOptions());
                AlbumRect.Fill = new ImageBrush(bi);
            }
            finally
            {
                DeleteObject(hBitmap);
            }
            AlbumRect.Visibility = Visibility.Visible;
        }

        private void SongsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SongsDataGrid.SelectedItem != null)
            {
                var entry = SongsDataGrid.SelectedItem;
                if (entry.GetType() == typeof(SongEntry))
                {
                    SongEntry song = (SongEntry)entry;
                    currentLoadingPhrase = song.LoadingPhrase;
                    LoadingPhraseTxt.Text = song.LoadingPhrase;
                    if (song.Path.ToLower().EndsWith(".chart") || song.Path.ToLower().EndsWith(".mid"))
                    {
                        string? folder = new FileInfo(song.Path).DirectoryName;
                        string[] albumCandidates = Directory.GetFiles(folder + "", "album.*", SearchOption.TopDirectoryOnly);
                        if (albumCandidates.Length > 0)
                        {
                            SetAlbumArt(albumCandidates[0]);
                        }
                        else AlbumRect.Visibility = Visibility.Hidden;
                    }

                    if (song.Path.EndsWith(".sng"))
                    {
                        try
                        {
                            Sng sng = Sng.Load(song.Path);
                            bool foundAlbumArt = false;
                            foreach (var file in sng.files)
                            {
                                if (file.name.ToLower().Contains("album"))
                                {
                                    SetAlbumArt(new Bitmap(new MemoryStream(file.data)));
                                    foundAlbumArt = true;
                                    break;
                                }
                            }
                            if (!foundAlbumArt)
                            {
                                AlbumRect.Visibility = Visibility.Hidden;
                            }
                        }
                        catch (Exception ex) { }
                    }
                }
            }
            else AlbumRect.Visibility = Visibility.Hidden;
        }

        private void SongsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // PlaySong();  // TO DO: Find a better way to implement quickly selecting a song to play?
        }
    }
    public class SongEntry
    {
        public string Artist { get; set; }
        public string Title { get; set; }
        public string Album { get; set; }
        public string Charter { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public string Path { get; set; }
        public string LoadingPhrase { get; set; }
    }
}