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
        public Progress<int> progress;

        public BackgroundWorker bgWorker;

        public MainWindow()
        {
            scanFolder = "";
            songList = new List<SongEntry>() { };
            InitializeComponent();
            totalSongs = 0;
            scannedSongs = 0;
            progress = new Progress<int>(x => ScanProgressBar.Value = x);
            SongsDataGrid.ItemsSource = songList;
            bgWorker = new BackgroundWorker();
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
                // TO DO: implement a better way of counting songs?
                totalSongs = charts.Count<string>() + midis.Count<string>();
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
                                try
                                {
                                    config = new ConfigurationBuilder().AddIniFile(songIniFiles[0]).Build();
                                    string songIniSection = "song";
                                    artist = RemoveHtml(config.GetSection(songIniSection)["artist"]);
                                    title = RemoveHtml(config.GetSection(songIniSection)["name"]);
                                    album = RemoveHtml(config.GetSection(songIniSection)["album"]);
                                    charter = RemoveHtml(config.GetSection(songIniSection)["charter"]);
                                    genre = RemoveHtml(config.GetSection(songIniSection)["genre"]);
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
                                songEntry.Path = chart;
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
                                try
                                {
                                    config = new ConfigurationBuilder().AddIniFile(songIniFiles[0]).Build();
                                    string songIniSection = "song";
                                    artist = RemoveHtml(config.GetSection(songIniSection)["artist"]);
                                    title = RemoveHtml(config.GetSection(songIniSection)["name"]);
                                    album = RemoveHtml(config.GetSection(songIniSection)["album"]);
                                    charter = RemoveHtml(config.GetSection(songIniSection)["charter"]);
                                    genre = RemoveHtml(config.GetSection(songIniSection)["genre"]);
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
                                songEntry.Path = midi;
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
                }
            }
        }

        private async void UpdateScanProgress(object? sender, ProgressChangedEventArgs e)
        {
            // scanProgress = e.ProgressPercentage;
            
            // await Task.Delay(1);
        }

        private async void FinishedScanning(object? sender, RunWorkerCompletedEventArgs e)
        {
            ScanProgressBar.IsIndeterminate = false;
            SongsDataGrid.ItemsSource = songList;
            CollectionViewSource.GetDefaultView(SongsDataGrid.ItemsSource).Filter = this.SongFilter;

            ScanChartsBtn.IsEnabled = true;
        }

        private async void ScanChartsBtn_Click(object sender, RoutedEventArgs e)
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
            else
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
        }

        private void SearchTxtBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SongsDataGrid.Items.Filter = SongFilter;
            SongsDataGrid.Items.Refresh();
        }

        private void SongListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // PlaySong();  // not working
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
                    System.Diagnostics.Process.Start(FGH3_Path_TxtBox.Text, "\"" + shart + "\"");
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
        public string Genre { get; set; }
        public string Path { get; set; }
    }
}