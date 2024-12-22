using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
using Vortice.XInput;

namespace FGH3ChartBrowser
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            ciCombo.SelectedIndex = (int)Settings.GetControllerIndex();
            AutoScanChk.IsChecked = Settings.GetAutoScan();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FGH3ChartBrowser.Settings.SetControllerIndex((uint)ciCombo.SelectedIndex);
        }

        private void AutoScanChk_Checked(object sender, RoutedEventArgs e)
        {
            Settings.SetAutoScan(AutoScanChk.IsChecked);
        }

        private void WatermarkBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer", "https://github.com/YoShibyl/FGH3ChartBrowser");
        }
    }
}
