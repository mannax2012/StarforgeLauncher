using System.Diagnostics;
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
using System.IO;
using StarforgeLauncher.data;
using System.Runtime.Serialization;

namespace StarforgeLauncher
{ 
    public partial class MainWindow : Window
    {
        public  MainWindow()
        {
            InitializeComponent();
            PageHandler.SelfREF = this;
            OnAppStart();
           
        }

        public void OnAppStart()
        {

            if (!Directory.Exists(LauncherClientVariables.LaunchPadDirectory))
            {
                Directory.CreateDirectory(LauncherClientVariables.LaunchPadDirectory);
            }

            if (!Directory.Exists(ConfigFileVariables.InstallDirectory))
            {
                Directory.CreateDirectory(ConfigFileVariables.InstallDirectory);
            }
            UpdaterCheck();
            LaunchPadCheck();
        }
        public async void UpdaterCheck()
        {
            bool needsUpdate = false;
            var updateInfo = await LauncherUpdater.CheckForUpdater();

            if (updateInfo != null)
            {
                Version remoteVersion = new Version(updateInfo.Version);
                Version localVersion = new Version(ConfigFileVariables.updaterVersion);

                needsUpdate = remoteVersion > localVersion;
            }

            if (updateInfo != null && (needsUpdate))
            { 
                //await LauncherUpdater.DownloadUpdate(updateInfo.UpdateUrl);
                needsUpdate = false;
            }
            else
            {
                System.Windows.MessageBox.Show($"Does not Needs Update!", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public async void LaunchPadCheck()
        {
            bool needsUpdate = false;
            var updateInfo = await LauncherUpdater.CheckForLaunchPadUpdate();

            if (updateInfo != null)
            {
                Version remoteVersion = new Version(updateInfo.Version);
                Version localVersion = new Version(ConfigFileVariables.launchPadVersion);

                needsUpdate = remoteVersion > localVersion;
            }

            if (updateInfo != null && (needsUpdate))
            {
                await LauncherUpdater.DownloadUpdate(updateInfo.UpdateUrl);
                needsUpdate = false;
            }
        }
        public void Hello_Click(object sender, RoutedEventArgs e)
        {
            string updaterExe = System.IO.Path.Combine("LaunchPad", "StarforgeLaunchPad.exe");

            if (File.Exists(updaterExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterExe,
                    UseShellExecute = true
                });

                System.Windows.Application.Current.Shutdown(); // Exit current app
            }
            else
            {
                System.Windows.MessageBox.Show($"Updater executable not found. Path: {updaterExe}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
