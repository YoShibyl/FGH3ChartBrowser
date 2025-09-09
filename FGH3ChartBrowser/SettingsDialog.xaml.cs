using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using Vortice.XInput;
using Windows.ApplicationModel.Search;

namespace FGH3ChartBrowser
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        string aspyrConfigPath = Environment.ExpandEnvironmentVariables("%USERPROFILE%\\AppData\\Local\\Aspyr\\FastGH3\\AspyrConfig.xml");

        public SettingsDialog()
        {
            InitializeComponent();
            ciCombo.SelectedIndex = (int)Settings.GetControllerIndex();
            AutoScanChk.IsChecked = Settings.GetAutoScan();
            ReadAspyrConfig();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ciCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Settings.SetControllerIndex((uint)ciCombo.SelectedIndex);
        }

        private void AutoScanChk_Checked(object sender, RoutedEventArgs e)
        {
            Settings.SetAutoScan(AutoScanChk.IsChecked);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void WidthHeight_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
        }

        private void ReadAspyrConfig()
        {
            if (System.IO.File.Exists(aspyrConfigPath))
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(aspyrConfigPath);
                XmlNodeList xmlSL = xml.GetElementsByTagName("s");

                if (xmlSL.Count > 0)
                {
                    foreach (XmlNode s in xmlSL)
                    {
                        var attrId = s.Attributes["id"]?.Value;
                        switch (attrId)
                        {
                            case "Video.Width":
                                gameWinWidthTxt.Text = s.InnerText;
                                break;
                            case "Video.Height":
                                gameWinHeightTxt.Text = s.InnerText;
                                break;
                        }
                    }
                }
            }
            else
            {
                gameWinWidthTxt.IsEnabled = false;
                gameWinHeightTxt.IsEnabled = false;
                saveResolutionBtn.IsEnabled = false;
            }
        }

        private void WriteAspyrConfig()
        {
            if (System.IO.File.Exists(aspyrConfigPath))
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(aspyrConfigPath);
                XmlNodeList xmlSL = xml.GetElementsByTagName("s");

                if (xmlSL.Count > 0)
                {
                    foreach (XmlNode s in xmlSL)
                    {
                        var attrId = s.Attributes["id"]?.Value;
                        switch (attrId)
                        {
                            case "Video.Width":
                                s.InnerText = gameWinWidthTxt.Text;
                                break;
                            case "Video.Height":
                                s.InnerText = gameWinHeightTxt.Text;
                                break;
                        }
                    }
                    xml.Save(aspyrConfigPath);
                }
            }
        }

        private void WatermarkBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer", "https://github.com/YoShibyl/FGH3ChartBrowser");
        }

        private void saveResolutionBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteAspyrConfig();
        }

        private void resComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selectedRes = ((ComboBoxItem)resComboBox.SelectedItem).Content.ToString();
            if (selectedRes != null && selectedRes.Contains('x'))
            {
                string[] dimensions = selectedRes.Split('x');
                if (dimensions.Length == 2)
                {
                    gameWinWidthTxt.Text = dimensions[0];
                    gameWinHeightTxt.Text = dimensions[1];
                }
            }
        }

        private void RefreshSourcesBtn_Click(object sender, RoutedEventArgs e)
        {
            Sources.DownloadSources();
        }
    }
}
