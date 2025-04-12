using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.IO.Packaging;

namespace StarforgeLauncher.data
{
    public class UpdateEntry
    {
        public string Version { get; set; }
        public string UpdateUrl { get; set; }
    }

    public class UpdateFile
    {
        public List<UpdateEntry> LaunchPad { get; set; }
        public List<UpdateEntry> Updater { get; set; }
        public List<UpdateEntry> Launcher { get; set; }
    }
    public static class LauncherUpdater
    {
        private static readonly string VersionFileUrl = "http://localhost/website/wordpress/launcher/starforge/launcher/version.json";
        private static string LocalLaunchPadVersion = ConfigFileVariables.launchPadVersion;
        private static string LocalUpdaterVersion = ConfigFileVariables.updaterVersion;
        private static string UpdateToVersion = "0";

        public static async Task<UpdateEntry> CheckForUpdater()
        {
            using HttpClient client = new HttpClient();
            try
            {
                string json = await client.GetStringAsync(VersionFileUrl);
                var updateFile = JsonConvert.DeserializeObject<UpdateFile>(json);

                // Pick which category you want to check (e.g. Launcher)
                var updates = updateFile?.Updater;

                if (updates != null && updates.Count > 0)
                {
                    // Get the latest version — assuming the last one is latest (could also sort if needed)
                    var latest = updates.Last(); // or .OrderByDescending(v => new Version(v.Version)).First();

                    UpdateToVersion = latest.Version;

                    if (new Version(latest.Version) > new Version(LocalUpdaterVersion))
                    {
                        System.Windows.MessageBox.Show(
                            $"Update available!\nRemote Version: {latest.Version}\nLocal Version: {LocalUpdaterVersion}",
                            "Update Available", MessageBoxButton.OK, MessageBoxImage.Information
                        );

                        return latest;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Update check failed: {ex.Message}",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }

            return null; // No update needed or error occurred
        }

        public static async Task<UpdateEntry> CheckForLaunchPadUpdate()
        {
            setStatusText("Checking for updates...");
            using HttpClient client = new HttpClient();
            try
            {
                string json = await client.GetStringAsync(VersionFileUrl);
                var updateFile = JsonConvert.DeserializeObject<UpdateFile>(json);

                // Pick which category you want to check (e.g. Launcher)
                var updates = updateFile?.LaunchPad;

                if (updates != null && updates.Count > 0)
                {
                    // Get the latest version — assuming the last one is latest (could also sort if needed)
                    var latest = updates.Last(); // or .OrderByDescending(v => new Version(v.Version)).First();

                    UpdateToVersion = latest.Version;

                    if (new Version(latest.Version) > new Version(LocalLaunchPadVersion))
                    {
                       // System.Windows.MessageBox.Show($"Update available!\nRemote Version: {latest.Version}\nLocal Version: {LocalLaunchPadVersion}", "Update Available", MessageBoxButton.OK, MessageBoxImage.Information); setStatusText($"Update Found: V{latest.Version}");
                        await DownloadUpdate(latest.UpdateUrl);
                        ConfigFileVariables.launchPadVersion = latest.Version;
                        ConfigManager.SaveConfig();
                        return latest;
                    } else
                    {
                        setStatusText("Starting LaunchPad.");
                        await Task.Delay(2000);
                        StartLaunchPad("StarforgeLaunchPad.exe");
                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Update check failed: {ex.Message}",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }

            return null; // No update needed or error occurred
        }
        public static string GetAncestorDirectory(string startPath, int levelsUp)
        {
            string? path = startPath;
            for (int i = 0; i < levelsUp; i++)
            {
                path = Directory.GetParent(path!)?.FullName
                    ?? throw new InvalidOperationException("Cannot move up beyond root directory.");
            }
            return path;
        }

        public static async Task DownloadUpdate(string updateUrl)
        {
            setStatusText("Downloading update...");
            string tempPath = Path.Combine(Path.GetTempPath(), "Launcher_Update.zip");
            await Task.Delay(1000); // Delay to ensure file locks are released

            // Initialize the HttpClientHandler with auto redirect enabled
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true
            };

            // Create HttpClient instance to download the file
            using HttpClient client = new HttpClient(handler);
            using var stream = await client.GetStreamAsync(updateUrl);
            using var file = new FileStream(tempPath, FileMode.Create);
            await stream.CopyToAsync(file);
            string zipCopy = Path.Combine(Path.GetTempPath(), "launcher_update_copy.zip");
            File.Copy(tempPath, zipCopy, true);
            // Extract zip
            string extractPath = "update";
            if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(zipCopy, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update"));

            KillProcessByName("StarforgeLaunchPad");
            await ApplyUpdateAsync();

            
        }
        public static async Task ApplyUpdateAsync()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string launchPadDir = Path.Combine(baseDir, "LaunchPad");
            string updateDir = Path.Combine(baseDir, "update");

            Debug.WriteLine($"Base Directory: {baseDir}");
            setStatusText("Applying update...");

            await Task.Delay(1000); // Make sure file locks are released

            if (Directory.Exists(updateDir))
            {
                foreach (var file in Directory.GetFiles(updateDir, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(updateDir, file);
                    string targetPath = Path.Combine(launchPadDir, relativePath);

                    string? targetDir = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    File.Copy(file, targetPath, true);
                    Debug.WriteLine($"Copied: {relativePath}");
                    setStatusText($"Copied: {relativePath}");
                }

                Debug.WriteLine("Update applied.");
                setStatusText("Update applied.");
                await Task.Delay(1000);
                // Now clean up the update folder
                try
                {
                    Directory.Delete(updateDir, true);
                    Debug.WriteLine("Cleaned up update folder.");
                    setStatusText("Cleaned up update folder.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Cleanup failed: {ex.Message}");
                    setStatusText($"Cleanup failed: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine("No update directory found.");
                setStatusText("No update directory found.");
            }

            // Restart LaunchPad
            StartLaunchPad("StarforgeLaunchPad.exe");
            Application.Current.Shutdown();
        }

        public static void StartLaunchPad(string launcherExeName)
        {
            string launcherPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaunchPad");
            string launchPadPath = Path.Combine(launcherPath, launcherExeName);

            if (File.Exists(launchPadPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = launchPadPath,
                    UseShellExecute = true
                });

                setStatusText($"Starting LaunchPad: {launcherExeName}");
            }
            else
            {
                Debug.WriteLine("Launcher executable not found!");
            }

        }
        public static void setStatusText(string text)
        {
            if (PageHandler.SelfREF != null)
            {
                PageHandler.SelfREF.StatusTextBlock.Text = text;
            }
        }
        public static void KillProcessByName(string exeNameWithoutExtension)
        {
            foreach (var proc in Process.GetProcessesByName(exeNameWithoutExtension))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit();
                    Debug.WriteLine($"Killed: {proc.ProcessName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to kill {proc.ProcessName}: {ex.Message}");
                }
            }
        }

    }
}
