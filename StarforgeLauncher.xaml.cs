using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using StarforgeLauncher.data;

namespace StarforgeLauncher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = DownloadHandler.ItemREF;
            PageHandler.SelfREF = this;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            await OnAppStartAsync();
        }

        private async Task OnAppStartAsync()
        {
            await ConfigManager.InitializeConfig();
            Directory.CreateDirectory(LauncherClientVariables.LaunchPadDirectory);
            await LaunchPadCheckAsync();
        }

        private async Task LaunchPadCheckAsync()
        {
            await LaunchPadUpdater.CheckForLaunchPadUpdate();
        }

        public void SetStatusText(string text)
        {
            Dispatcher.Invoke(() => DownloadHandler.ItemREF.StatusText = text);
        }

        public void BeginDownloadProgress(string fileName, long totalBytes)
        {
            Dispatcher.Invoke(() => DownloadHandler.ItemREF.BeginDownload(fileName, totalBytes));
        }

        public void UpdateDownloadProgress(string fileName, long totalBytes, long bytesDownloaded, TimeSpan? eta)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadHandler.ItemREF.FileName = fileName;
                DownloadHandler.ItemREF.ReportProgress(bytesDownloaded, totalBytes, eta);
            });
        }

        public void SetIndeterminateProgress(string statusText, string fileName)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadHandler.ItemREF.StatusText = statusText;
                DownloadHandler.ItemREF.SetIndeterminateState(fileName);
            });
        }

        public void CompleteDownloadProgress(string statusText, string fileName)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadHandler.ItemREF.StatusText = statusText;
                DownloadHandler.ItemREF.MarkComplete(fileName);
            });
        }

        private void DragWindowArea(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
