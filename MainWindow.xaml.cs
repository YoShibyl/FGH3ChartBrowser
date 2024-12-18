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
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Reflection;

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
        public int scanErrors;
        public BackgroundWorker ScanBgWorker;
        public BackgroundWorker AlbumLoadBgWorker;
        public string currentLoadingPhrase;
        public SysConfig.Configuration config;
        public BitmapSource bmpSrc;
        public Bitmap bmp;

        public MainWindow()
        {
            scanFolder = "";
            songList = new List<SongEntry>() { };
            InitializeComponent();
            // TO DO: Figure out dark mode theme?
            MainWin.Title += $" v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
            ScanProgressBar.Value = 0;
            RefreshSongInfo();
            totalSongs = 0;
            scannedSongs = 0;
            scanErrors = 0;
            SongsDataGrid.ItemsSource = songList;
            ScanBgWorker = new BackgroundWorker();
            ScanBgWorker.WorkerReportsProgress = true;
            ScanBgWorker.DoWork += ScanSongs;
            ScanBgWorker.ProgressChanged += UpdateScanProgress;
            ScanBgWorker.RunWorkerCompleted += FinishedScanning;
            AlbumLoadBgWorker = new BackgroundWorker(); // soon?
            currentLoadingPhrase = "";
            config = SysConfig.ConfigurationManager.OpenExeConfiguration(SysConfig.ConfigurationUserLevel.None);
            bmpSrc = new BitmapImage();
            bmp = new Bitmap(4,4);
            LoadConfig();
        }

        private void RefreshSongInfo(SongEntry song)
        {
            LoadingPhraseTxt.Text = song.LoadingPhrase;
            SongTitleTxt.Content = song.Title;
            ArtistTxt.Text = song.Artist;
            AlbumTxt.Text = "Album:  " + song.Album;
            GenreTxt.Text = "Genre:  " + song.Genre;
            string yearStr = "";
            if (song.Year > 0) yearStr = int.Clamp(song.Year, 0, 99999).ToString();
            YearTxt.Content = $"Year:  {yearStr}";
            CharterTxt.Text = $"Charter:  {song.Charter}";
            LeadDiffTxt.Content = $"Lead Intensity:  {int.Clamp(song.IntensityLead, 0, 99)}";
            BassDiffTxt.Content = $"Bass Intensity:  {int.Clamp(song.IntensityBass, 0, 99)}";
            if (song.IntensityLead > 99) LeadDiffTxt.Content += "+";
            if (song.IntensityBass > 99) BassDiffTxt.Content += "+";
            string starsLead = "";
            string starsBass = "";
            if (song.IntensityLead >= 0)
            {
                int i = 1;
                for (i = 1; i <= 9; i++)
                {
                    if (i <= song.IntensityLead) starsLead += "★";
                    else if (i <= 6) starsLead += "☆";
                }
                if (song.IntensityLead > 9) starsLead += "+";
            }
            if (song.IntensityBass >= 0)
            {
                int i = 1;
                for (i = 1; i <= 9; i++)
                {
                    if (i <= song.IntensityBass) starsBass += "★";
                    else if (i <= 6) starsBass += "☆";
                }
                if (song.IntensityBass > 9) starsBass += "+";
            }
            LeadStarsTxt.Content = starsLead;
            BassStarsTxt.Content = starsBass;
        }
        private void RefreshSongInfo()
        {
            LoadingPhraseTxt.Text = "";
            SongTitleTxt.Content = "";
            ArtistTxt.Text = "";
            AlbumTxt.Text = "Album:  ";
            GenreTxt.Text = "Genre:  ";
            YearTxt.Content = "Year:  ";
            CharterTxt.Text = "Charter:  ";
            LeadDiffTxt.Content = "Lead Intensity:  ";
            BassDiffTxt.Content = "Bass Intensity:  ";
            LeadStarsTxt.Content = "☆☆☆☆☆☆";
            BassStarsTxt.Content = "☆☆☆☆☆☆";
        }

        private void LoadConfig()
        {
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

        private async void ScanSongs(object? sender, DoWorkEventArgs e)
        {
            string searchPath = scanFolder;
            BackgroundWorker bw = sender as BackgroundWorker;
            if (!String.IsNullOrWhiteSpace(searchPath) && System.IO.Path.Exists(searchPath))
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
                    scanErrors = 0;
                    foreach (string chart in charts)
                    {
                        FileInfo fileInfo = new FileInfo(chart);
                        string chartDir = "" + fileInfo.Directory?.FullName;
                        string[] songIniFiles = Directory.GetFiles(chartDir, "song.ini", SearchOption.TopDirectoryOnly);
                        if (songIniFiles != null)
                        {
                            if (songIniFiles.Length > 0)
                            {
                                SongEntry songEntry = new SongEntry(chart);
                                IConfiguration songIniConfig = null;
                                string artist = "Unknown Artist";
                                string title = "Unknown Title";
                                string album = "Unknown Album";
                                string charter = "Unknown Charter";
                                string genre = "";
                                string loadingPhrase = "";
                                int year = 0;
                                try
                                {
                                    songIniConfig = new ConfigurationBuilder().AddIniFile(songIniFiles[0]).Build();
                                    IConfigurationSection songSect = songIniConfig.GetSection("song");
                                    artist = RemoveHtml(songSect["artist"] + "");
                                    title = RemoveHtml(songSect["name"] + "");
                                    album = RemoveHtml(songSect["album"] + "");
                                    charter = RemoveHtml(songSect["charter"] + "");
                                    genre = RemoveHtml(songSect["genre"] + "");
                                    loadingPhrase += RemoveHtml((songSect["loading_phrase"] + "").Replace("<br>", "\n"));
                                    int.TryParse(songIniConfig.GetSection("song")["year"], out year);
                                    int diffLead = 0;
                                    int diffBass = 0;
                                    int.TryParse(songIniConfig.GetSection("song")["diff_guitar"], out diffLead);
                                    if (!int.TryParse(songIniConfig.GetSection("song")["diff_bass"], out diffBass))
                                    {
                                        int.TryParse(songIniConfig.GetSection("song")["diff_rhythm"], out diffBass);
                                    }
                                    songEntry.IntensityLead = int.Clamp(diffLead, 0, 100);
                                    songEntry.IntensityBass = int.Clamp(diffBass, 0, 100);
                                }
                                catch { scanErrors++; }
                                songEntry.Artist = "" + artist;
                                songEntry.Title = "" + title;
                                songEntry.Album = "" + album;
                                songEntry.Charter = "" + charter;
                                songEntry.Genre = genre;
                                songEntry.Year = year;
                                songEntry.Path = chart;
                                songEntry.LoadingPhrase = loadingPhrase;
                                songList.Add(songEntry);
                                
                                scannedSongs++;
                            }
                        }
                        else
                        {
                            totalSongs -= 1;
                            scanErrors++;
                        }
                        scanProgress = 100 * scannedSongs / totalSongs;
                        bw.ReportProgress(scanProgress);
                    }
                    foreach (string midi in midis)
                    {
                        FileInfo fileInfo = new FileInfo(midi);
                        string chartDir = "" + fileInfo.Directory?.FullName;
                        string[] songIniFiles = Directory.GetFiles(chartDir, "song.ini", SearchOption.TopDirectoryOnly);
                        if (songIniFiles != null)
                        {
                            if (songIniFiles.Length > 0)
                            {
                                SongEntry songEntry = new SongEntry(midi);
                                IConfiguration songIniConfig = null;
                                string artist = "Unknown Artist";
                                string title = "Unknown Title";
                                string album = "Unknown Album";
                                string charter = "Unknown Charter";
                                string genre = "";
                                string loadingPhrase = "";
                                int year = 0;
                                try
                                {
                                    songIniConfig = new ConfigurationBuilder().AddIniFile(songIniFiles[0]).Build();
                                    IConfigurationSection songSect = songIniConfig.GetSection("song");
                                    artist = RemoveHtml(songSect["artist"] + "");
                                    title = RemoveHtml(songSect["name"] + "");
                                    album = RemoveHtml(songSect["album"] + "");
                                    charter = RemoveHtml(songSect["charter"] + "");
                                    genre = RemoveHtml(songSect["genre"] + "");
                                    loadingPhrase += RemoveHtml((songSect["loading_phrase"] + "").Replace("<br>","\n"));
                                    int.TryParse(songIniConfig.GetSection("song")["year"], out year);
                                    int diffLead = 0;
                                    int diffBass = 0;
                                    int.TryParse(songIniConfig.GetSection("song")["diff_guitar"], out diffLead);        if (!int.TryParse(songIniConfig.GetSection("song")["diff_bass"], out diffBass))
                                    {
                                        int.TryParse(songIniConfig.GetSection("song")["diff_rhythm"], out diffBass);
                                    }
                                    songEntry.IntensityLead = int.Clamp(diffLead, 0, 100);
                                    songEntry.IntensityBass = int.Clamp(diffBass, 0, 100);
                                }
                                catch { scanErrors++; }
                                songEntry.Artist = "" + artist;
                                songEntry.Title = "" + title;
                                songEntry.Album = "" + album;
                                songEntry.Charter = "" + charter;
                                songEntry.Genre = genre;
                                songEntry.Year = year;
                                songEntry.Path = midi;
                                songEntry.LoadingPhrase = loadingPhrase;
                                songList.Add(songEntry);

                                scannedSongs++;
                            }
                        }
                        else
                        {
                            totalSongs -= 1;
                            scanErrors++;
                        }
                        scanProgress = 100 * scannedSongs / totalSongs;
                        bw.ReportProgress(scanProgress);

                    }
                    foreach (string sngPath in sngs)
                    {
                        FileInfo fileInfo = new FileInfo(sngPath);
                        SongEntry songEntry = new SongEntry(sngPath);
                        songEntry.Genre = "";
                        songEntry.LoadingPhrase = "";
                        songEntry.Year = 0;
                        try
                        {
                            Sng sngData = Sng.Load(sngPath);
                            string artist = "";
                            string title = "";
                            string album = "";
                            string charter = "";
                            string genre = "";
                            string loadingPhrase = "";
                            string diffLeadStr = "";
                            string diffBassStr = "";
                            int diffLead = 0;
                            int diffBass = 0;
                            sngData.meta.TryGetValue("artist", out artist);
                            sngData.meta.TryGetValue("name", out title);
                            sngData.meta.TryGetValue("album", out album);
                            sngData.meta.TryGetValue("genre", out genre);
                            sngData.meta.TryGetValue("charter", out charter);
                            sngData.meta.TryGetValue("loading_phrase", out loadingPhrase);
                            sngData.meta.TryGetValue("diff_guitar", out diffLeadStr);
                            sngData.meta.TryGetValue("diff_rhythm", out diffBassStr);
                            int.TryParse(diffLeadStr, out diffLead);
                            int.TryParse(diffBassStr, out diffBass);
                            songEntry.IntensityLead = int.Clamp(diffLead, 0, 100);
                            songEntry.IntensityBass = int.Clamp(diffBass, 0, 100);
                            songEntry.Title         = "" + RemoveHtml(title);
                            songEntry.Artist        = "" + RemoveHtml(artist);
                            songEntry.Album         = "" + RemoveHtml(album);
                            songEntry.Genre         = "" + RemoveHtml(genre);
                            songEntry.Charter       = "" + RemoveHtml(charter);
                            songEntry.LoadingPhrase = "" + RemoveHtml(("" + loadingPhrase).Replace("<br>","\n"));
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
                            scanErrors++;
                        }
                        songEntry.Path = sngPath;
                        songList.Add(songEntry);
                        scannedSongs++;
                        scanProgress = 100 * scannedSongs / totalSongs;
                        bw.ReportProgress(scanProgress);
                    }
                }
            }
        }

        private void UpdateScanProgress(object? sender, ProgressChangedEventArgs e)
        {
            ScanProgressBar.Value = e.ProgressPercentage;
            ScanProgressTxt.Text = $"{scannedSongs} / {totalSongs}";
        }


        private void FinishedScanning(object? sender, RunWorkerCompletedEventArgs e)
        {
            SongsDataGrid.ItemsSource = songList;
            CollectionViewSource.GetDefaultView(SongsDataGrid.ItemsSource).Filter = this.SongFilter;
            ScanProgressBar.Value = 100;
            ScanProgressTxt.Text = $"{songList.Count} songs found";
            if (scanErrors > 0) ScanProgressTxt.Text += $" ({scanErrors} errors)";
            ChartsPathBrowseBtn.IsEnabled = true;
            Chart_Folder_TxtBox.IsEnabled = true;
            ScanChartsBtn.Content = "Scan Songs";
            ScanChartsBtn.IsEnabled = true;
        }

        private void AlbumClick(object sender, RoutedEventArgs e)
        {
            // TO DO: figure out making a popup with album image
            
        }

        private void ScanChartsBtn_Click(object sender, RoutedEventArgs e)
        {
            ScanChartsBtn.IsEnabled = false;
            ScanChartsBtn.Content = "Scanning...";
            ChartsPathBrowseBtn.IsEnabled = false;
            Chart_Folder_TxtBox.IsEnabled = false;

            scanFolder = Chart_Folder_TxtBox.Text;

            ScanBgWorker.RunWorkerAsync();
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
                if (entry != null)
                {
                    if (entry.GetType() == typeof(SongEntry))
                    {
                        string shart = (entry as SongEntry)?.Path + "";
                        if (System.IO.Path.Exists(FGH3_Path_TxtBox.Text))
                            System.Diagnostics.Process.Start(FGH3_Path_TxtBox.Text, "\"" + shart + "\"");
                    }
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

        public async void LoadAlbumArtFromBitmap(string filepath)
        {
            BitmapImage bi = new BitmapImage(new Uri(filepath, UriKind.Absolute));
            bmpSrc = bi;
            AlbumRect.Fill = new ImageBrush(bi);
            AlbumRect.Visibility = Visibility.Visible;
        }
        public async void LoadAlbumArtFromBitmap(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            BitmapSource bi;
            try
            {
                bi = Imaging.CreateBitmapSourceFromHBitmap(
                     hBitmap,
                     IntPtr.Zero,
                     Int32Rect.Empty,
                     BitmapSizeOptions.FromEmptyOptions());
                bmpSrc = bi;
                AlbumRect.Fill = new ImageBrush(bi);
                AlbumRect.Visibility = Visibility.Visible;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }
        public async void LoadAlbumArt(object? sender, DoWorkEventArgs e)
        {
            LoadAlbumArtFromBitmap(bmp);
        }
        private void SetAlbumArt(object? sender, RunWorkerCompletedEventArgs? e)
        {
            AlbumRect.Fill = new ImageBrush(bmpSrc);
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
                    RefreshSongInfo(song);
                    if (song.Path.ToLower().EndsWith(".chart") || song.Path.ToLower().EndsWith(".mid"))
                    {
                        string? folder = new FileInfo(song.Path).DirectoryName;
                        string[] albumCandidates = Directory.GetFiles(folder + "", "album.*", SearchOption.TopDirectoryOnly);
                        if (albumCandidates.Length > 0)
                        {
                            LoadAlbumArtFromBitmap(albumCandidates[0]);
                            SetAlbumArt(null, null);
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
                                if (file.name.ToLower().StartsWith("album"))
                                {
                                    foundAlbumArt = true;
                                    
                                    // bmp = new Bitmap(new MemoryStream(file.data));
                                    LoadAlbumArtFromBitmap(new Bitmap(new MemoryStream(file.data)));
                                    SetAlbumArt(null, null);
                                    // AlbumLoadBgWorker.DoWork += LoadAlbumArt;
                                    // AlbumLoadBgWorker.WorkerReportsProgress = false;
                                    // AlbumLoadBgWorker.RunWorkerCompleted += SetAlbumArt;
                                    // AlbumLoadBgWorker.RunWorkerAsync();
                                    break;
                                }
                            }
                            if (!foundAlbumArt)
                            {
                                AlbumRect.Visibility = Visibility.Hidden;
                            }
                        }
                        catch { }
                    }
                }
            }
            else AlbumRect.Visibility = Visibility.Hidden;
        }

        private void SongsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // PlaySong();  // TO DO: Find a better way to implement quickly selecting a song to play?
        }

        private void OpenInExplorerBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SongsDataGrid.SelectedItem != null)
            {
                var entry = SongsDataGrid.SelectedItem;
                if (entry.GetType() == typeof(SongEntry))
                {
                    SongEntry song = (SongEntry)entry;
                    FileInfo fileInfo = new FileInfo(song.Path);
                    string songDir = "" + fileInfo.Directory?.FullName;
                    if (File.Exists(song.Path))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{song.Path}\"");
                    else
                        System.Diagnostics.Process.Start("explorer.exe", songDir);
                }
            }
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
        public int IntensityLead { get; set; }
        public int IntensityBass { get; set; }

        public SongEntry(string path = "", string artist = "Unknown Artist", string title = "Unknown Title", string album = "Unknown Album", string charter = "", int year = 0, string genre = "", string loadingPhrase = "", int intensityLead = 0, int intensityBass = 0)
        {
            Artist = artist;
            Title = title;
            Album = album;
            Charter = charter;
            Year = year;
            Genre = genre;
            Path = path;
            LoadingPhrase = loadingPhrase;
            IntensityLead = intensityLead;
            IntensityBass = intensityBass;
        }
    }
}