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
    public static class LaunchPadUpdater
    {
        private static readonly string VersionFileUrl = "http://launcher.malevolentgaming.net/starforge/version.json";
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
                    ConfigManager.LoadConfig();

                    if (new Version(latest.Version) > new Version(ConfigFileVariables.launchPadVersion))
                    {
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
            string tempPath = Path.Combine(Path.GetTempPath(), "LaunchPad_Update.zip");
            string zipCopy = Path.Combine(Path.GetTempPath(), "launcher_update_copy.zip");
            string extractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update");

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    setStatusText($"Downloading update...");
                    await Task.Delay(1000); // Allow file locks to clear

                    HttpClientHandler handler = new HttpClientHandler { AllowAutoRedirect = true };
                    using HttpClient client = new HttpClient(handler);
                    using var stream = await client.GetStreamAsync(updateUrl);
                    using var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(file);

                    File.Copy(tempPath, zipCopy, true);

                    if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);

                    // Try extracting
                    ZipFile.ExtractToDirectory(zipCopy, extractPath);

                    // Success, continue update
                    KillProcessByName("StarforgeLaunchPad");
                    await ApplyUpdateAsync();

                    return; // Done
                }
                catch (InvalidDataException ex) when (ex.Message.Contains("End of Central Directory"))
                {
                    Debug.WriteLine($"ZIP extraction failed (attempt {attempt}): {ex.Message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Update attempt {attempt} failed: {ex.Message}");
                }

                // Small delay between retries
                await Task.Delay(1500);
            }

            // Failed after 2 attempts — fallback
            Debug.WriteLine("Update failed twice. Launching current version.");
            setStatusText("Update failed. Starting previous version...");

            await Task.Delay(2000);
            StartLaunchPad("StarforgeLaunchPad.exe");
            Application.Current.Shutdown();
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

                    if (targetDir != null && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

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
