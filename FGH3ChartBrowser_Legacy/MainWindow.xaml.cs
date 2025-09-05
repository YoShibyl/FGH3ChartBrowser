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
using Vortice.XInput;
using System.Windows.Threading;
using System.Configuration;
using FGH3ChartBrowser;

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

        public AlbumView albumView;
        public SettingsDialog settingsDialog;

        private long pressTimeRepeat;
        private long pressTimeDpadD;
        private long pressTimeDpadU;
        private long lastLaunchTime;
        private bool isPressingD;
        private bool isPressingU;
        private bool isPressingFretG;
        private bool isPressingFretR;
        private bool isPressingFretY;
        private bool isPressingFretB;
        private bool isPressingFretO;
        private bool isPressingStart;
        private bool startingGame;

        public MainWindow()
        {
            if (!File.Exists("FGH3ChartBrowser_Legacy.dll.config"))
            {
                File.WriteAllText("FGH3ChartBrowser_Legacy.dll.config", "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<configuration>\r\n\t<appSettings>\r\n\t\t<add key=\"fastgh3_exe_location\" value=\"C:\\Program Files (x86)\\FastGH3\\FastGH3.exe\"/>\r\n\t\t<add key=\"charts_folder\" value=\"\"/>\r\n\t\t<add key=\"auto_scan\" value=\"true\"/>\r\n\t</appSettings>\r\n</configuration>");
                Thread.Sleep(1000);
            }
            
            scanFolder = "";
            songList = new List<SongEntry>() { };
            InitializeComponent();

            MainWin.Title += $" v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
            ScanProgressBar.Value = 0;
            RefreshSongInfo();
            totalSongs = 0;
            scannedSongs = 0;
            scanErrors = 0;
            SongsDataGrid.ItemsSource = songList;
            ScanBgWorker = new BackgroundWorker();
            ScanBgWorker.WorkerReportsProgress = true;
            ScanBgWorker.WorkerSupportsCancellation = true;
            ScanBgWorker.DoWork += ScanSongs;
            ScanBgWorker.ProgressChanged += UpdateScanProgress;
            ScanBgWorker.RunWorkerCompleted += FinishedScanning;
            AlbumLoadBgWorker = new BackgroundWorker(); // soon?
            currentLoadingPhrase = "";
            Settings.Config = SysConfig.ConfigurationManager.OpenExeConfiguration(SysConfig.ConfigurationUserLevel.None);
            bmpSrc = new BitmapImage();
            bmp = new Bitmap(4, 4);

            lastLaunchTime = DateTime.Now.Ticks;
            isPressingD = false;
            isPressingU = false;
            isPressingFretG = false;
            isPressingFretR = false;
            isPressingFretY = false;
            isPressingFretB = false;
            isPressingFretO = false;
            startingGame = false;
            pressTimeDpadD = DateTime.Now.Ticks;
            pressTimeDpadU = DateTime.Now.Ticks;
            pressTimeRepeat = DateTime.Now.Ticks / 10000;

            DispatcherTimer inputTimer = new DispatcherTimer();
            inputTimer.Interval = TimeSpan.FromMilliseconds(1);
            inputTimer.Tick += InputTimer_Tick;
            inputTimer.Start();

            LoadConfig();
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        public bool IsForeground()
        {
            Window window = Application.Current.MainWindow;
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            IntPtr foregroundWindow = GetForegroundWindow();
            return windowHandle == foregroundWindow;
        }

        // Loop runs roughly every millisecond (about 1000x per second)
        private void InputTimer_Tick(object? sender, EventArgs e)
        {
            if (!ScanBgWorker.IsBusy)
            {
                if (System.Diagnostics.Process.GetProcessesByName("game").Length < 1
                    && System.Diagnostics.Process.GetProcessesByName("game!").Length < 1)
                {
                    PlaySongBtn.IsEnabled = true;
                    RandomSongBtn.IsEnabled = true;
                    FeelingLuckyBtn.IsEnabled = true;
                }
                else
                {
                    PlaySongBtn.IsEnabled = false;
                    RandomSongBtn.IsEnabled = false;
                    FeelingLuckyBtn.IsEnabled = false;
                }
            }
            else
            {
                PlaySongBtn.IsEnabled = false;
                RandomSongBtn.IsEnabled = false;
                FeelingLuckyBtn.IsEnabled = false;
            }

            bool connected = XInput.GetState(Settings.controllerIndex, out State state);
            if (connected && IsForeground())
            {
                GamepadButtons buttons = state.Gamepad.Buttons;

                if (!isPressingFretB && state.Gamepad.Buttons.HasFlag(GamepadButtons.X))
                {
                    SearchTxtBox.Focus();
                }
                if (!isPressingFretY && state.Gamepad.Buttons.HasFlag(GamepadButtons.Y))
                {
                    if (SongsDataGrid.Items.Count > 0)
                    {
                        System.Random rand = new System.Random();
                        SongsDataGrid.SelectedIndex = rand.Next(0, SongsDataGrid.Items.Count);
                        if (SongsDataGrid.SelectedItem != null) SongsDataGrid.ScrollIntoView(SongsDataGrid.SelectedItem);
                    }
                }

                isPressingFretG = state.Gamepad.Buttons.HasFlag(GamepadButtons.A);
                isPressingFretR = state.Gamepad.Buttons.HasFlag(GamepadButtons.B);
                isPressingFretY = state.Gamepad.Buttons.HasFlag(GamepadButtons.Y);
                isPressingFretB = state.Gamepad.Buttons.HasFlag(GamepadButtons.X);
                isPressingFretO = state.Gamepad.Buttons.HasFlag(GamepadButtons.LeftShoulder);
                isPressingStart = state.Gamepad.Buttons.HasFlag(GamepadButtons.Start);

                if (!isPressingD && state.Gamepad.Buttons.HasFlag(GamepadButtons.DPadDown))
                {
                    int scroll = 1;
                    if (isPressingFretO) scroll = 6; // scroll more if holding orange
                    pressTimeDpadD = DateTime.Now.Ticks;
                    StrumDown(scroll);
                    isPressingD = true;
                }
                else if (isPressingD && !state.Gamepad.Buttons.HasFlag(GamepadButtons.DPadDown))
                {
                    isPressingD = false;
                }
                if (!isPressingU && state.Gamepad.Buttons.HasFlag(GamepadButtons.DPadUp))
                {
                    int scroll = 1;
                    if (isPressingFretO) scroll = 6; // scroll more if holding orange
                    pressTimeDpadU = DateTime.Now.Ticks;
                    StrumUp(scroll);
                    isPressingU = true;
                }
                else if (isPressingU && !state.Gamepad.Buttons.HasFlag(GamepadButtons.DPadUp))
                {
                    isPressingU = false;
                }
            }
            // handle starting game when pressing Start button on controller
            if (isPressingStart && !startingGame && IsForeground())
            {
                startingGame = true;
            }
            if (!isPressingStart && startingGame)
            {
                startingGame = false;
                if (IsForeground() && (DateTime.Now.Ticks - lastLaunchTime) / 10000 >= 2000)
                {
                    PlaySong();
                }
            }

            long TimeSincePressD = (DateTime.Now.Ticks - pressTimeDpadD) / 10000;
            long TimeSincePressU = (DateTime.Now.Ticks - pressTimeDpadU) / 10000;

            if (DateTime.Now.Ticks / 10000 - pressTimeRepeat >= 42)
            {
                int scroll = 1;
                if (isPressingFretO) scroll = 6; // scroll more if holding orange
                if (isPressingD)
                {
                    isPressingU = false;
                    if (TimeSincePressD >= 350)
                    {
                        StrumDown(scroll);
                    }
                }
                if (isPressingU)
                {
                    isPressingD = false;
                    if (TimeSincePressU >= 350)
                    {
                        StrumUp(scroll);
                    }
                }
            }
            SongEntry? song = SongsDataGrid.SelectedItem as SongEntry;
            if (song != null) RefreshAlbum(song);
        }
        private void StrumDown(int amount = 1)
        {
            if (!ScanBgWorker.IsBusy)
            {
                if (SongsDataGrid.SelectedIndex < SongsDataGrid.Items.Count - 1 && SongsDataGrid.Items.Count > 0)
                {
                    int index = SongsDataGrid.SelectedIndex;
                    index += amount;
                    if (index > SongsDataGrid.Items.Count - 1) index = SongsDataGrid.Items.Count - 1;
                    SongsDataGrid.SelectedIndex = index;
                    if (SongsDataGrid.SelectedItem != null) SongsDataGrid.ScrollIntoView(SongsDataGrid.SelectedItem);
                }
                SongsDataGrid.Focus();
            }
            pressTimeRepeat = DateTime.Now.Ticks / 10000;
        }
        private void StrumUp(int amount = 1)
        {
            if (!ScanBgWorker.IsBusy)
            {
                {
                    if (SongsDataGrid.SelectedIndex > 0)
                    {
                        int index = SongsDataGrid.SelectedIndex;
                        index -= amount;
                        if (index < 0) index = 0;
                        SongsDataGrid.SelectedIndex = index;
                        if (SongsDataGrid.SelectedItem != null) SongsDataGrid.ScrollIntoView(SongsDataGrid.SelectedItem);
                    }
                }
                SongsDataGrid.Focus();
            }
            pressTimeRepeat = DateTime.Now.Ticks / 10000;
        }

        private void RefreshSongInfo(SongEntry song)
        {
            LoadingPhraseTxt.Text = song.LoadingPhrase;
            SongTitleTxt.Text = song.Title;
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

            SongTitleTxt.ToolTip = null;
            ArtistTxt.ToolTip = null;
            AlbumTxt.ToolTip = null;
            GenreTxt.ToolTip = null;
            CharterTxt.ToolTip = null;
            if (!String.IsNullOrWhiteSpace(song.Title)) SongTitleTxt.ToolTip = song.Title;
            if (!String.IsNullOrWhiteSpace(song.Artist)) ArtistTxt.ToolTip = song.Artist;
            if (!String.IsNullOrWhiteSpace(song.Album)) AlbumTxt.ToolTip = song.Album;
            if (!String.IsNullOrWhiteSpace(song.Genre)) GenreTxt.ToolTip = song.Genre;
            if (!String.IsNullOrWhiteSpace(song.Charter)) CharterTxt.ToolTip = song.Charter;
        }
        private void RefreshSongInfo()
        {
            LoadingPhraseTxt.Text = "";
            SongTitleTxt.Text = "";
            ArtistTxt.Text = "";
            AlbumTxt.Text = "Album:  ";
            GenreTxt.Text = "Genre:  ";
            YearTxt.Content = "Year:  ";
            CharterTxt.Text = "Charter:  ";
            LeadDiffTxt.Content = "Lead Intensity:  ";
            BassDiffTxt.Content = "Bass Intensity:  ";
            LeadStarsTxt.Content = "☆☆☆☆☆☆";
            BassStarsTxt.Content = "☆☆☆☆☆☆";
            SongTitleTxt.ToolTip = null;
            ArtistTxt.ToolTip = null;
            AlbumTxt.ToolTip = null;
            GenreTxt.ToolTip = null;
            CharterTxt.ToolTip = null;
        }

        private void LoadConfig()
        {

            FGH3_Path_TxtBox.Text = Settings.Config.AppSettings.Settings["fastgh3_exe_location"].Value;
            if (String.IsNullOrEmpty(FGH3_Path_TxtBox.Text))
            {
                FGH3_Path_TxtBox.Text = @"C:\Program Files (x86)\FastGH3\FastGH3.exe";
                Settings.Config.AppSettings.Settings["fastgh3_exe_location"].Value = @"C:\Program Files (x86)\FastGH3\FastGH3.exe";
                Settings.Config.Save();
            }
            if (!Settings.Config.AppSettings.Settings.AllKeys.Contains("controller_index"))
            {
                Settings.Config.AppSettings.Settings.Add("controller_index", "0");
            }
            if (!Settings.Config.AppSettings.Settings.AllKeys.Contains("auto_scan"))
            {
                Settings.Config.AppSettings.Settings.Add("auto_scan", "true");
            }
            Settings.AutoScan = Settings.Config.AppSettings.Settings["auto_scan"].Value.ToLower() == "true";
            uint.TryParse(Settings.Config.AppSettings.Settings["controller_index"].Value, out uint ci);
            Settings.SetControllerIndex(ci);
            scanFolder = Settings.Config.AppSettings.Settings["charts_folder"].Value;
            Chart_Folder_TxtBox.Text = scanFolder;
            Settings.Config.Save();

            if (Settings.AutoScan && Directory.Exists(scanFolder))
            {
                if (Directory.Exists(Chart_Folder_TxtBox.Text))
                {
                    ScanProgressTxt.Text = "Scanning...";
                    ScanChartsBtn.Content = "Cancel Scan";
                    ScanChartsBtn.IsCancel = true;
                    ChartsPathBrowseBtn.IsEnabled = false;
                    Chart_Folder_TxtBox.IsEnabled = false;
                    RandomSongBtn.IsEnabled = false;
                    FeelingLuckyBtn.IsEnabled = false;
                    scanFolder = Chart_Folder_TxtBox.Text;
                    ScanBgWorker.RunWorkerAsync();
                }
            }
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
                Settings.Config.AppSettings.Settings["fastgh3_exe_location"].Value = filename;
                Settings.Config.Save();
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
                    List<SongEntry> tmpSongList = new List<SongEntry>();
                    scannedSongs = 0;
                    scanErrors = 0;
                    foreach (string chart in charts)
                    {
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
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
                                tmpSongList.Add(songEntry);

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
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
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
                                    loadingPhrase += RemoveHtml((songSect["loading_phrase"] + "").Replace("<br>", "\n"));
                                    int.TryParse(songIniConfig.GetSection("song")["year"], out year);
                                    int diffLead = 0;
                                    int diffBass = 0;
                                    int.TryParse(songIniConfig.GetSection("song")["diff_guitar"], out diffLead); if (!int.TryParse(songIniConfig.GetSection("song")["diff_bass"], out diffBass))
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
                                tmpSongList.Add(songEntry);

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
                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
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
                            songEntry.Title = "" + RemoveHtml(title);
                            songEntry.Artist = "" + RemoveHtml(artist);
                            songEntry.Album = "" + RemoveHtml(album);
                            songEntry.Genre = "" + RemoveHtml(genre);
                            songEntry.Charter = "" + RemoveHtml(charter);
                            songEntry.LoadingPhrase = "" + RemoveHtml(("" + loadingPhrase).Replace("<br>", "\n"));
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
                            totalSongs--;
                        }
                        songEntry.Path = sngPath;
                        tmpSongList.Add(songEntry);
                        scannedSongs++;
                        scanProgress = 100 * scannedSongs / totalSongs;
                        bw.ReportProgress(scanProgress);
                    }
                    if (!bw.CancellationPending)
                    {
                        songList.Clear();
                        songList = tmpSongList;
                    }
                }
            }
        }

        private void UpdateScanProgress(object? sender, ProgressChangedEventArgs e)
        {
            ScanProgressBar.Value = e.ProgressPercentage;
            ScanProgressTxt.Text = $"Scanning...  {scannedSongs} / {totalSongs}";
        }

        private void FinishedScanning(object? sender, RunWorkerCompletedEventArgs e)
        {
            SongsDataGrid.IsEnabled = true;
            SongsDataGrid.ItemsSource = songList;
            ScanChartsBtn.Content = "Scan Songs";
            ScanChartsBtn.IsCancel = false;
            ChartsPathBrowseBtn.IsEnabled = true;
            Chart_Folder_TxtBox.IsEnabled = true;
            RandomSongBtn.IsEnabled = true;
            FeelingLuckyBtn.IsEnabled = true;
            if (!e.Cancelled)
            {
                CollectionViewSource.GetDefaultView(SongsDataGrid.ItemsSource).Filter = this.SongFilter;
                ScanProgressBar.Value = 100;
                ScanProgressTxt.Text = $"{songList.Count} songs found";
                if (scanErrors > 0) ScanProgressTxt.Text += $" ({scanErrors} errors)";
                SortDataGrid(SongsDataGrid, 1);
            }
            else
            {
                ScanProgressTxt.Text = "Scan cancelled";
            }
        }

        public static void SortDataGrid(DataGrid dataGrid, int columnIndex = 0, ListSortDirection sortDirection = ListSortDirection.Ascending)
        {
            var column = dataGrid.Columns[columnIndex];
            dataGrid.Items.SortDescriptions.Clear();
            dataGrid.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, sortDirection));

            foreach (var col in dataGrid.Columns)
            {
                col.SortDirection = null;
            }
            column.SortDirection = sortDirection;

            dataGrid.Items.Refresh();
        }

        private void AlbumClick(object sender, RoutedEventArgs e)
        {
            if (SongsDataGrid.SelectedItem != null && AlbumRect.Visibility == Visibility.Visible)
            {
                SongEntry song = (SongEntry)SongsDataGrid.SelectedItem;
                if (albumView != null) albumView.Close();
                albumView = new AlbumView();
                
                albumView.Title += $": {song.Album}";
                albumView.Background = AlbumRect.Fill;

                albumView.Show();
            }
        }

        private void CopyAlbumArtToClipboard(object sender, RoutedEventArgs e)
        {
            if (AlbumRect.Visibility == Visibility.Visible)
            {
                ImageBrush brush = (ImageBrush)AlbumRect.Fill;
                ImageSource imgsrc = brush.ImageSource;
                Clipboard.SetImage((BitmapSource)imgsrc);
            }
        }

        private void ScanChartsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(Chart_Folder_TxtBox.Text))
            {
                MessageBox.Show("You must specify a folder to scan for songs.\nClick the Browse for Folder button and select a folder containing charts.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            else if (Directory.Exists(Chart_Folder_TxtBox.Text))
            {
                ScanChartsBtn.Content = "Cancel Scan";
                ScanChartsBtn.IsCancel = true;
                ChartsPathBrowseBtn.IsEnabled = false;
                Chart_Folder_TxtBox.IsEnabled = false;
                RandomSongBtn.IsEnabled = false;
                FeelingLuckyBtn.IsEnabled = false;

                scanFolder = Chart_Folder_TxtBox.Text;

                if (ScanBgWorker.IsBusy) { ScanBgWorker.CancelAsync(); }
                else { ScanBgWorker.RunWorkerAsync(); }
            }
            else
            {
                MessageBox.Show($"Folder not found: {Chart_Folder_TxtBox.Text}", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        public static string RemoveHtml(string input)
        {
            return Regex.Replace(input, "<[a-zA-Z/].*?>", String.Empty).Trim();
        }

        private void ChartsPathBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog();
            Nullable<bool> result = dlg.ShowDialog();
            if (result.HasValue && result.Value)
            {
                if (Chart_Folder_TxtBox.Text != dlg.FolderName)
                {
                    Chart_Folder_TxtBox.Text = dlg.FolderName;
                    Settings.Config.AppSettings.Settings["charts_folder"].Value = dlg.FolderName;
                    Settings.Config.Save();
                    if (Settings.GetAutoScan())
                    {
                        // auto scan
                    }
                }
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
            if (SongsDataGrid.SelectedItem == null && SongsDataGrid.Items.Count >= 0)
            {
                SongsDataGrid.SelectedIndex = 0;
            }
            else
            {
                SongsDataGrid.ScrollIntoView(SongsDataGrid.SelectedItem);
            }
        }

        private void PlaySong()
        {
            if (System.Diagnostics.Process.GetProcessesByName("game").Length < 1
                && System.Diagnostics.Process.GetProcessesByName("game!").Length < 1)
            {
                lastLaunchTime = DateTime.Now.Ticks;
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
        }

        private void FGH3SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (System.IO.Path.Exists(FGH3_Path_TxtBox.Text))
                Process.Start(FGH3_Path_TxtBox.Text, "-settings");
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
            AlbumRect.Stretch = Stretch.Uniform;
            AlbumRect.Visibility = Visibility.Visible;
            AlbumClickBtn.Visibility = Visibility.Visible;
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
                    RefreshAlbum(song);
                }
            }
            else
            {
                RefreshSongInfo();
                AlbumClickBtn.Visibility = Visibility.Hidden;
                AlbumRect.Visibility = Visibility.Hidden;
            }
        }
        private void RefreshAlbum(SongEntry song)
        {
            if (song.Path.ToLower().EndsWith(".chart") || song.Path.ToLower().EndsWith(".mid"))
            {
                string? folder = new FileInfo(song.Path).DirectoryName;
                string[] albumCandidates = Directory.GetFiles(folder + "", "album.*", SearchOption.TopDirectoryOnly);
                if (albumCandidates.Length > 0)
                {
                    LoadAlbumArtFromBitmap(albumCandidates[0]);
                    SetAlbumArt(null, null);
                    AlbumRect.Visibility = Visibility.Visible;
                    AlbumClickBtn.Visibility = Visibility.Visible;
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

                            LoadAlbumArtFromBitmap(new Bitmap(new MemoryStream(file.data)));
                            SetAlbumArt(null, null);
                            AlbumRect.Visibility = Visibility.Visible;
                            AlbumClickBtn.Visibility = Visibility.Visible;
                            break;
                        }
                    }
                    if (!foundAlbumArt)
                    {
                        AlbumClickBtn.Visibility = Visibility.Collapsed;
                        AlbumRect.Visibility = Visibility.Hidden;
                    }
                }
                catch
                {
                    AlbumClickBtn.Visibility = Visibility.Collapsed;
                    AlbumRect.Visibility = Visibility.Hidden;
                }
            }
        }
        private void SongsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

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
                        Process.Start("explorer.exe", $"/select, \"{song.Path}\"");
                    else
                        Process.Start("explorer.exe", songDir);
                }
            }
        }

        private void SongsDataGrid_ContextOpened(object sender, RoutedEventArgs e)
        {
            if (SongsDataGrid.SelectedItem == null && sender.GetType() == typeof(ContextMenu))
            {
                ((ContextMenu)sender).IsOpen = false;
            }
            else if (SongsDataGrid.SelectedItem != null)
            {
                if (SongsDataGrid.SelectedItem.GetType() == typeof(SongEntry))
                {
                    SongEntry song = (SongEntry)SongsDataGrid.SelectedItem;
                    PlaySongCtxMenuItem.Header = $"Play \"{song.Title}\"";
                }
            }
        }

        private void AlbumMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (AlbumRect.Visibility == Visibility.Hidden && sender.GetType() == typeof(ContextMenu))
            {
                ((ContextMenu)sender).IsOpen = false;
            }
        }
        
        private void OtherSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (settingsDialog != null) { settingsDialog.Close(); }
            settingsDialog = new SettingsDialog();
            
            settingsDialog.Show();
        } // */

        private void RandomSongBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SongsDataGrid.Items.Count > 0)
            {
                System.Random rand = new System.Random();
                SongsDataGrid.SelectedIndex = rand.Next(0, SongsDataGrid.Items.Count);
                if (SongsDataGrid.SelectedItem != null) SongsDataGrid.ScrollIntoView(SongsDataGrid.SelectedItem);
            }
        }

        private void FeelingLuckyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SongsDataGrid.Items.Count > 0)
            {
                System.Random rand = new System.Random();
                SongsDataGrid.SelectedIndex = rand.Next(0, SongsDataGrid.Items.Count);
                if (SongsDataGrid.SelectedItem != null) SongsDataGrid.ScrollIntoView(SongsDataGrid.SelectedItem);
                if (IsForeground() && (DateTime.Now.Ticks - lastLaunchTime) / 10000 >= 2000)
                {
                    PlaySong();
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
    public class Settings
    {
        public static Configuration Config { get; set; }
        public static uint controllerIndex { get; set; }
        public static bool AutoScan { get; set; }

        public static void SetControllerIndex(uint ci)
        {
            controllerIndex = ci;
            Config.AppSettings.Settings["controller_index"].Value = controllerIndex.ToString();
            Config.Save();
        }
        public static uint GetControllerIndex()
        {
            return controllerIndex;
        }

        public static void SetAutoScan(bool? value)
        {
            bool newval = value == true;
            AutoScan = newval;
            Config.AppSettings.Settings["auto_scan"].Value = newval.ToString().ToLower();
            Config.Save();
        }
        public static bool GetAutoScan()
        {
            return AutoScan;
        }
    }
}