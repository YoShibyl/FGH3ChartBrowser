using FGH3ChartBrowser;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using SngParser;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Internal;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;
using Vortice.XInput;
using SysConfig = System.Configuration;

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
        private bool isVerifyingSongs;
        private bool isForcedScan;

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
#if DEBUG
            MainWin.Title += " [DEBUG]";
#endif
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

            isVerifyingSongs = false;
            isForcedScan = false;

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

            baseJsonData = "{}";
            extraJsonData = "{}";
            LoadSourceJsons();

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
                    if (SongsDataGrid.Items.Count > 0 && !ScanBgWorker.IsBusy)
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
            AlbumTxt.Text = "Album: " + song.Album;
            GenreTxt.Text = "Genre: " + song.Genre;
            string yearStr = "";
            if (song.Year > 0) yearStr = int.Clamp(song.Year, 0, 99999).ToString();
            YearTxt.Content = $"Year: {yearStr}";
            CharterTxt.Text = $"Charter: {song.Charter}";
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
            SongLengthTxt.Content = $"Song Length:  {song.LengthString}";
            SongSourceTxt.Text = "Source: " + song.SourceName;

            SongLengthTxt.ToolTip = null;
            SongSourceTxt.ToolTip = null;
            SongTitleTxt.ToolTip = null;
            ArtistTxt.ToolTip = null;
            AlbumTxt.ToolTip = null;
            GenreTxt.ToolTip = null;
            CharterTxt.ToolTip = null;
            if (!String.IsNullOrWhiteSpace(song.FullSongLengthString)) SongLengthTxt.ToolTip = song.FullSongLengthString;
            if (!String.IsNullOrWhiteSpace(song.Source)) SongSourceTxt.ToolTip = $"{song.SourceName} ({song.Source})";
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
            AlbumTxt.Text = "Album: ";
            GenreTxt.Text = "Genre: ";
            YearTxt.Content = "Year: ";
            CharterTxt.Text = "Charter: ";
            LeadDiffTxt.Content = "Lead Intensity: ";
            BassDiffTxt.Content = "Bass Intensity: ";
            LeadStarsTxt.Content = "☆☆☆☆☆☆";
            BassStarsTxt.Content = "☆☆☆☆☆☆";
            SongSourceTxt.Text = "Source: ";
            SongLengthTxt.Content = "Length: ";
            SongTitleTxt.ToolTip = null;
            ArtistTxt.ToolTip = null;
            AlbumTxt.ToolTip = null;
            GenreTxt.ToolTip = null;
            CharterTxt.ToolTip = null;
            SongSourceTxt.ToolTip = null;
            SongLengthTxt.ToolTip = null;
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
            if (!Settings.Config.AppSettings.Settings.AllKeys.Contains("ui_theme"))
            {
                Settings.Config.AppSettings.Settings.Add("ui_theme", "0");
            }
            if (!Settings.Config.AppSettings.Settings.AllKeys.Contains("controller_index"))
            {
                Settings.Config.AppSettings.Settings.Add("controller_index", "0");
            }
            if (!Settings.Config.AppSettings.Settings.AllKeys.Contains("auto_scan"))
            {
                Settings.Config.AppSettings.Settings.Add("auto_scan", "true");
            }
            if (!Settings.Config.AppSettings.Settings.AllKeys.Contains("opensource_hash"))
            {
                Settings.Config.AppSettings.Settings.Add("opensource_hash", "");
            }
            Settings.AutoScan = Settings.Config.AppSettings.Settings["auto_scan"].Value.ToLower() == "true";
            uint.TryParse(Settings.Config.AppSettings.Settings["controller_index"].Value, out uint ci);
            Settings.SetControllerIndex(ci);
            scanFolder = Settings.Config.AppSettings.Settings["charts_folder"].Value;
            Chart_Folder_TxtBox.Text = scanFolder;
            Settings.SetSourceHash(Settings.Config.AppSettings.Settings["opensource_hash"].Value);
            Settings.Config.Save();

            if (Settings.AutoScan && Directory.Exists(scanFolder))
            {
                if (!System.IO.File.Exists("songcache.xml"))
                {
                    isForcedScan = true;
                }
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
            List<SongEntry> tmpSongList = new List<SongEntry>();
            isVerifyingSongs = false;
            if (System.IO.File.Exists("songcache.xml") && !isForcedScan)
            {
                List<SongEntry>? tmpSongCache = LoadSongCache();
                if (tmpSongCache != null)
                {
                    tmpSongList = tmpSongCache;
                    if (tmpSongList.Count > 0)
                    {
                        scannedSongs = tmpSongList.Count;
                        Debug.WriteLine($"Found {scannedSongs} songs in cache");
                    }
                    else isForcedScan = true;
                }
                else isForcedScan = true;
            }
            if (!String.IsNullOrWhiteSpace(searchPath) && System.IO.Path.Exists(searchPath) && isForcedScan && tmpSongList.Count == 0)
            {
                IEnumerable<string> charts = Directory.EnumerateFiles(searchPath, "notes.chart", SearchOption.AllDirectories);
                IEnumerable<string> midis = Directory.EnumerateFiles(searchPath, "notes.mid", SearchOption.AllDirectories);
                IEnumerable<string> sngs = Directory.EnumerateFiles(searchPath, "*.sng", SearchOption.AllDirectories);
                IEnumerable<string> songInis = Directory.EnumerateFiles(searchPath, "song.ini", SearchOption.AllDirectories);

                // totalSongs = charts.Count<string>() + midis.Count<string>() + sngs.Count<string>();
                totalSongs = songInis.Count<string>() + sngs.Count<String>();
                Debug.WriteLine("Found " + totalSongs + " charts to scan.");

                if (totalSongs > 0)
                {
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
                                SongEntry songEntry = new SongEntry();
                                songEntry.Path = chart;
                                IConfiguration songIniConfig = null;
                                string artist = "Unknown Artist";
                                string title = "Unknown Title";
                                string album = "Unknown Album";
                                string charter = "Unknown Charter";
                                string genre = "";
                                string loadingPhrase = "";
                                string source = "";
                                int year = 0;
                                long songLength = -1;
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
                                    long.TryParse(songIniConfig.GetSection("song")["song_length"], out songLength);
                                    string? iconStr = songIniConfig.GetSection("song")["icon"];
                                    if (!String.IsNullOrWhiteSpace(iconStr) && iconStr != "0" && iconStr != "-1")
                                    {
                                        source = (iconStr + "").ToLower();
                                    }
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
                                songEntry.LengthMilliseconds = songLength;
                                songEntry.Source = source;
                                songEntry.SourceName = songEntry.TryFindSourceNameAndIcon(Sources.baseJsonData, Sources.extraJsonData);

                                tmpSongList.Add(songEntry);

                                scannedSongs++;
                            }
                            else
                            {
                                scanErrors++;
                                Debug.WriteLine("No song.ini found for " + chart);
                            }
                        }
                        if (totalSongs > 0)
                            scanProgress = 100 * scannedSongs / totalSongs;
                        else scanProgress = 0;
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
                                SongEntry songEntry = new SongEntry();
                                songEntry.Path = midi;
                                IConfiguration songIniConfig = null;
                                string artist = "Unknown Artist";
                                string title = "Unknown Title";
                                string album = "Unknown Album";
                                string charter = "Unknown Charter";
                                string genre = "";
                                string loadingPhrase = "";
                                string source = "";
                                int year = 0;
                                long songLength = -1;
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
                                    long.TryParse(songIniConfig.GetSection("song")["song_length"], out songLength);
                                    string? iconStr = songIniConfig.GetSection("song")["icon"];
                                    if (!String.IsNullOrWhiteSpace(iconStr) && iconStr != "0" && iconStr != "-1")
                                    {
                                        source = (iconStr + "").ToLower();
                                    }
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
                                songEntry.LengthMilliseconds = songLength;
                                songEntry.Source = source;
                                songEntry.SourceName = songEntry.TryFindSourceNameAndIcon(Sources.baseJsonData, Sources.extraJsonData);

                                tmpSongList.Add(songEntry);

                                scannedSongs++;
                            }
                            else
                            {
                                scanErrors++;
                                Debug.WriteLine("No song.ini found for " + midi);
                            }
                        }
                        if (totalSongs > 0)
                            scanProgress = 100 * scannedSongs / totalSongs;
                        else scanProgress = 0;
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
                        SongEntry songEntry = new SongEntry();
                        songEntry.Path = sngPath;
                        songEntry.Genre = "";
                        songEntry.LoadingPhrase = "";
                        songEntry.Year = 0;
                        try
                        {
                            Sng sngData = Sng.Load(sngPath);
                            string artist;
                            string title;
                            string album;
                            string charter;
                            string genre;
                            string loadingPhrase;
                            string diffLeadStr;
                            string diffBassStr;
                            string songLengthStr;
                            string iconStr = "";
                            int diffLead = 0;
                            int diffBass = 0;
                            long songLength = -1;
                            sngData.meta.TryGetValue("artist", out artist);
                            sngData.meta.TryGetValue("name", out title);
                            sngData.meta.TryGetValue("album", out album);
                            sngData.meta.TryGetValue("genre", out genre);
                            sngData.meta.TryGetValue("charter", out charter);
                            sngData.meta.TryGetValue("loading_phrase", out loadingPhrase);
                            sngData.meta.TryGetValue("diff_guitar", out diffLeadStr);
                            sngData.meta.TryGetValue("diff_rhythm", out diffBassStr);
                            sngData.meta.TryGetValue("song_length", out songLengthStr);
                            sngData.meta.TryGetValue("icon", out iconStr);
                            int.TryParse(diffLeadStr, out diffLead);
                            int.TryParse(diffBassStr, out diffBass);
                            long.TryParse(songLengthStr, out songLength);
                            songEntry.IntensityLead = int.Clamp(diffLead, 0, 100);
                            songEntry.IntensityBass = int.Clamp(diffBass, 0, 100);
                            songEntry.Title = "" + RemoveHtml(title);
                            songEntry.Artist = "" + RemoveHtml(artist);
                            songEntry.Album = "" + RemoveHtml(album);
                            songEntry.Genre = "" + RemoveHtml(genre);
                            songEntry.Charter = "" + RemoveHtml(charter);
                            songEntry.LoadingPhrase = "" + RemoveHtml(("" + loadingPhrase).Replace("<br>", "\n"));
                            songEntry.LengthMilliseconds = songLength;
                            songEntry.Source = ("" + iconStr).ToLower();
                            songEntry.SourceName = songEntry.TryFindSourceNameAndIcon(Sources.baseJsonData, Sources.extraJsonData);
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
                            Debug.WriteLine("Error reading .sng file: " + sngPath);
                        }
                        songEntry.Path = sngPath;
                        tmpSongList.Add(songEntry);
                        scannedSongs++;
                        if (totalSongs > 0)
                            scanProgress = 100 * scannedSongs / totalSongs;
                        else scanProgress = 0;
                        bw.ReportProgress(scanProgress);
                    }
                }
            }
            Debug.WriteLine("Verifying songs");
            isVerifyingSongs = true;
            scannedSongs = 0;
            bw.ReportProgress(0);
            List<SongEntry> finalSongList = new List<SongEntry>(tmpSongList);
            totalSongs = tmpSongList.Count;
            foreach (SongEntry songEntry in tmpSongList)
            {
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
                if (!System.IO.Path.Exists(songEntry.Path))
                {
                    finalSongList.Remove(songEntry);
                }
                scannedSongs++;
                if (totalSongs > 0)
                    scanProgress = 100 * scannedSongs / totalSongs;
                else scanProgress = 0;
                bw.ReportProgress(scanProgress);
            }
            if (!bw.CancellationPending)
            {
                totalSongs = finalSongList.Count;
                songList.Clear();
                songList = finalSongList;
            }
        }

        private void UpdateScanProgress(object? sender, ProgressChangedEventArgs e)
        {
            if (!isForcedScan && !isVerifyingSongs)
            {
                ScanProgressTxt.Text = "Loading cached song list...";
                ScanProgressBar.Value = 0;
            }
            if (!isVerifyingSongs)
            {
                ScanProgressBar.Value = e.ProgressPercentage;
                ScanProgressTxt.Text = $"Scanning...  {scannedSongs} / {totalSongs}";
            }
            else
            {
                ScanProgressBar.Value = e.ProgressPercentage;
                ScanProgressTxt.Text = $"Verifying songs... {scannedSongs} / {totalSongs}";
            }
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
                if (!isForcedScan) ScanProgressTxt.Text += " in cache";
                if (scanErrors > 0) ScanProgressTxt.Text += $" ({scanErrors} errors)";
                SortDataGrid(SongsDataGrid, 1);
                Debug.WriteLine("Final song count: " + songList.Count);

                SaveSongCache(songList);

                isForcedScan = true;
            }
            else
            {
                ScanProgressTxt.Text = "Scan cancelled";
                isForcedScan = true;
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
                    if (entry.Source != null) results += entry.Source;
                    if (entry.SourceName != "Custom/Unknown") results += entry.SourceName;

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
            if (System.IO.Path.Exists(filepath))
            {
                BitmapImage bi = new BitmapImage(new Uri(filepath, UriKind.Absolute));
                bmpSrc = bi;
                AlbumRect.Fill = new ImageBrush(bi);
                AlbumRect.Visibility = Visibility.Visible;
            }
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
            if (System.IO.Path.Exists(song.Path))
            {
                if (song.Path.ToLower().EndsWith(".chart") || song.Path.ToLower().EndsWith(".mid"))
                {
                    try
                    {
                        string? folder = new FileInfo(song.Path).DirectoryName;
                        if (!String.IsNullOrEmpty(folder))
                        {
                            string[] albumCandidates = Directory.GetFiles(folder + "", "album.*", SearchOption.TopDirectoryOnly);
                            if (albumCandidates.Length > 0)
                            {
                                LoadAlbumArtFromBitmap(albumCandidates[0]);
                                SetAlbumArt(null, null);
                                AlbumRect.Visibility = Visibility.Visible;
                                AlbumClickBtn.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                AlbumClickBtn.Visibility = Visibility.Collapsed;
                                AlbumRect.Visibility = Visibility.Hidden;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AlbumClickBtn.Visibility = Visibility.Collapsed;
                        AlbumRect.Visibility = Visibility.Hidden;
                        Debug.WriteLine(ex.Message);
                    }
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
            else
            {
                AlbumClickBtn.Visibility = Visibility.Collapsed;
                AlbumRect.Visibility = Visibility.Hidden;
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
        }

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

        public string baseJsonData;
        public string extraJsonData;

        public void LoadSourceJsons()
        {
            if (File.Exists("opensource-base.json"))
            {
                try
                {
                    baseJsonData = File.ReadAllText("opensource-base.json");
                }
                catch { }
            }
            if (File.Exists("opensource-extra.json"))
            {
                try
                {
                    extraJsonData = File.ReadAllText("opensource-extra.json");
                }
                catch { }
            }
        }

        private List<SongEntry>? LoadSongCache()
        {
            string file = "songcache.xml";
            if (File.Exists(file))
            {
                List<SongEntry> listofa = new List<SongEntry>();
                XmlSerializer formatter = new XmlSerializer(typeof(List<SongEntry>));
                FileStream songCacheFile = new FileStream(file, FileMode.Open);
                if (songCacheFile.Length > 0)
                {
                    try
                    {
                        byte[] buffer = new byte[songCacheFile.Length];
                        songCacheFile.ReadExactly(buffer, 0, (int)songCacheFile.Length);
                        MemoryStream stream = new MemoryStream(buffer);
                        object? deserializedCache = (List<SongEntry>)formatter.Deserialize(stream);
                        songCacheFile.Close();
                        if (deserializedCache != null && deserializedCache.GetType() == typeof(List<SongEntry>))
                        {
                            return (List<SongEntry>)deserializedCache;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error reading song cache: " + ex.Message);
                    }
                }
                else
                {
                    songCacheFile.Close();
                }
            }
            return null;
        }

        private void SaveSongCache(List<SongEntry> songEntries)
        {
            string path = "songcache.xml";
            FileStream outFile = File.Create(path);
            XmlSerializer formatter = new XmlSerializer(typeof(List<SongEntry>));
            formatter.Serialize(outFile, songEntries);
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
        public long LengthMilliseconds { get; set; }
        public string LengthString
        {
            get
            {
                if (LengthMilliseconds <= 0) return "";
                TimeSpan t = TimeSpan.FromMilliseconds(LengthMilliseconds);
                if (t.Hours > 9)
                    return string.Format("Forever!", t.Hours, t.Minutes, t.Seconds);
                else if (t.Hours > 0)
                    return string.Format("{0:D1}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
                else
                    return string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
            }
        }
        public string? FullSongLengthString
        {
            get
            {
                if (LengthMilliseconds <= 0) return null;
                TimeSpan t = TimeSpan.FromMilliseconds(LengthMilliseconds);
                string lengthStr = "";
                string hourS = "hour";
                string minuteS = "minute";
                string secondS = "second";
                if (t.Minutes > 1) minuteS = "minutes";
                if (t.Seconds > 1) secondS = "seconds";
                if (t.Hours > 0)
                {
                    if (t.Hours > 1) hourS = "hours";
                    lengthStr = string.Format("{0:D1} {1}, {2:D1} {3}, {4:D1} {5}", t.Hours, hourS, t.Minutes, minuteS, t.Seconds, secondS);
                }
                else if (t.Minutes > 0)
                    lengthStr = string.Format("{0:D1} {1}, {2:D1} {3}", t.Minutes, minuteS, t.Seconds, secondS);
                else
                    lengthStr = string.Format("{0:0.0} {1}", (LengthMilliseconds / 1000.0), secondS);
                lengthStr = lengthStr.Replace(", 0 minute", "").Replace(", 0 second", "");
                return lengthStr;
            }
        }
        public string Source { get; set; }
        public string SourceName { get; set; }

        public string TryFindSourceNameAndIcon(string baseJson = "{}", string extraJson = "{}")
        {
            string sourceID = Source.ToLower();
            if (String.IsNullOrWhiteSpace(sourceID) || int.TryParse(sourceID, out int sourceInt)) return "Custom/Unknown";
            JsonDocument baseDoc = JsonDocument.Parse(baseJson);
            JsonDocument extraDoc = JsonDocument.Parse(extraJson);
            if (baseDoc.RootElement.TryGetProperty("sources", out JsonElement baseSourcesE))
            {
                foreach (JsonElement sauce in baseSourcesE.EnumerateArray())
                {
                    if (sauce.TryGetProperty("ids", out JsonElement idsElement))
                    {
                        foreach (JsonElement idee in idsElement.EnumerateArray())
                        {
                            if ((idee.GetString() + "").ToLower() == sourceID)
                            {
                                if (sauce.TryGetProperty("names", out JsonElement namesElement))
                                {
                                    if (namesElement.TryGetProperty("en-US", out JsonElement sauceName))
                                    {
                                        return sauceName.GetString() + "";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (extraDoc.RootElement.TryGetProperty("sources", out JsonElement extraSourcesE))
            {
                foreach (JsonElement sauce in extraSourcesE.EnumerateArray())
                {
                    if (sauce.TryGetProperty("ids", out JsonElement idsElement))
                    {
                        foreach (JsonElement idee in idsElement.EnumerateArray())
                        {
                            if ((idee.GetString() + "").ToLower() == sourceID)
                            {
                                if (sauce.TryGetProperty("names", out JsonElement namesElement))
                                {
                                    if (namesElement.TryGetProperty("en-US", out JsonElement sauceName))
                                    {
                                        return sauceName.GetString() + "";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return sourceID;
        }

        public SongEntry()
        {
            Artist = "Unknown Artist";
            Title = "Unknown Title";
            Album = "Unknown Album";
            Charter = "";
            Year = 0;
            Genre = "";
            Path = "";
            LoadingPhrase = "";
            IntensityLead = 0;
            IntensityBass = 0;
            LengthMilliseconds = 0;
            Source = "";
            SourceName = "";
        }
    }
    public class Sources
    {
        public static string baseJsonData = "{}";
        public static string extraJsonData = "{}";
        public static bool IsConnectedToInternetByPing()
        {
            try
            {
                Ping myPing = new Ping();
                string host = "8.8.8.8";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                return (reply.Status == IPStatus.Success);
            }
            catch (PingException)
            {
                return false;
            }
        }

        public static void LoadSourceJsons()
        {
            // TO DO: Change where these files are loaded from?
            if (File.Exists("opensource-base.json"))
            {
                try
                {
                    baseJsonData = File.ReadAllText("opensource-base.json");
                }
                catch { }
            }
            if (File.Exists("opensource-extra.json"))
            {
                try
                {
                    extraJsonData = File.ReadAllText("opensource-extra.json");
                }
                catch { }
            }
        }
        public static async Task<string> GetLatestCommitHash()
        {
            if (IsConnectedToInternetByPing())
            {

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                    var url = "https://api.github.com/repos/YARC-Official/OpenSource/commits?per_page=1";
                    string response = await client.GetStringAsync(url);

                    JsonDocument respDoc = JsonDocument.Parse(response);
                    if (respDoc.RootElement.GetArrayLength() > 0)
                    {
                        if (respDoc.RootElement[0].TryGetProperty("sha", out JsonElement shaElement))
                        {
                            Debug.WriteLine("Hash = " + shaElement);
                            return shaElement.GetString() + "";
                        }
                    }
                }
            }
            return "";
        }
        public static async void DownloadSources()
        {
            string baseJsonURL = "https://api.github.com/repos/YARC-Official/OpenSource/contents/base/index.json";
            string extraJsonURL = "https://api.github.com/repos/YARC-Official/OpenSource/contents/extra/index.json";
            string latestHash = await GetLatestCommitHash();

            bool isConnectedToInternet = IsConnectedToInternetByPing();

            if (isConnectedToInternet && (latestHash != Settings.lastSourceHash || !System.IO.File.Exists("opensource-base.json") || !System.IO.File.Exists("opensource-extra.json")))
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "C# App");
                    try
                    {
                        // Get base/index.json
                        var baseResponse = await client.GetAsync(baseJsonURL);
                        baseResponse.EnsureSuccessStatusCode();
                        var baseContentJson = await baseResponse.Content.ReadAsStringAsync();
                        JsonDocument baseContentDoc = JsonDocument.Parse(baseContentJson);
                        if (baseContentDoc.RootElement.TryGetProperty("download_url", out JsonElement baseContentElement))
                        {
                            var downloadUrl = baseContentElement.GetString() + "";
                            if (string.IsNullOrEmpty(downloadUrl))
                            {
                                Debug.WriteLine($"Error: index.json does not exist or download_url is missing.");
                                return;
                            }

                            var fileBytes = await client.GetByteArrayAsync(downloadUrl);
                            await System.IO.File.WriteAllBytesAsync("opensource-base.json", fileBytes);
                        }
                        // Get extra/index.json
                        var extraResponse = await client.GetAsync(extraJsonURL);
                        extraResponse.EnsureSuccessStatusCode();
                        var extraContentJson = await extraResponse.Content.ReadAsStringAsync();
                        JsonDocument extraContentDoc = JsonDocument.Parse(extraContentJson);
                        if (extraContentDoc.RootElement.TryGetProperty("download_url", out JsonElement extraContentElement))
                        {
                            var downloadUrl = extraContentElement.GetString() + "";
                            if (string.IsNullOrEmpty(downloadUrl))
                            {
                                Debug.WriteLine($"Error: index.json does not exist or download_url is missing.");
                                return;
                            }

                            var fileBytes = await client.GetByteArrayAsync(downloadUrl);
                            await System.IO.File.WriteAllBytesAsync("opensource-extra.json", fileBytes);
                        }

                        // Once complete, store the new hash to the config.
                        Settings.SetSourceHash(latestHash);
                        MessageBox.Show("Sources updated successfully.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (HttpRequestException e)
                    {
                        Debug.WriteLine($"Error downloading file: {e.Message}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"General error: {ex.Message}");
                    }
                }
            }
            else if (!isConnectedToInternet)
            {
                MessageBox.Show("No internet connection available. Cannot download latest sources.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            else
            {
                MessageBox.Show("Sources are already up to date.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Sources.LoadSourceJsons();
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

        public static string? lastSourceHash { get; set; }

        public static void SetSourceHash(string hash)
        {
            lastSourceHash = hash;
            Config.AppSettings.Settings["opensource_hash"].Value = hash;
            Config.Save();
            Debug.WriteLine("Set source hash to " + hash);
        }
    }
}