using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace StarforgeLauncher.data
{
    public class UpdateEntry
    {
        public string Version { get; set; } = string.Empty;
        public string UpdateUrl { get; set; } = string.Empty;
    }

    public class UpdateFile
    {
        public List<UpdateEntry>? LaunchPad { get; set; }
        public List<UpdateEntry>? Updater { get; set; }
        public List<UpdateEntry>? Launcher { get; set; }
    }

    public static class LaunchPadUpdater
    {
        private static readonly string VersionFileUrl = "https://raw.githubusercontent.com/mannax2012/StarforgeLauncher/refs/heads/master/data/version.json";
        private const string LaunchPadExeName = "StarforgeLaunchPad.exe";
        public static string LatestLaunchPadVersion { get; private set; } = "0.0.0";
        public static async Task<UpdateEntry?> CheckForLaunchPadUpdate()
        {
            SetStatusText("Checking for updates.");
            DownloadHandler.ItemREF.Reset("Checking for updates.");

            try
            {
                using HttpClient client = CreateHttpClient();
                string json = await client.GetStringAsync(VersionFileUrl);
                UpdateFile? updateFile = JsonConvert.DeserializeObject<UpdateFile>(json);
                UpdateEntry? latest = GetLatest(updateFile?.LaunchPad);

                if (latest == null)
                {
                    SetStatusText("No LaunchPad update entries were found.");
                    await Task.Delay(1000);
                    StartLaunchPad(LaunchPadExeName);
                    Application.Current.Shutdown();
                    return null;
                }

                ConfigManager.LoadConfig();
                Version localVersion = new Version(ConfigFileVariables.launchPadVersion);
                Version remoteVersion = new Version(latest.Version);
                LatestLaunchPadVersion = latest.Version;
                if (remoteVersion <= localVersion)
                {
                    SetStatusText("Starting LaunchPad.");
                    DownloadHandler.ItemREF.Reset("Starting LaunchPad.");
                    await Task.Delay(800);
                    StartLaunchPad(LaunchPadExeName);
                    Application.Current.Shutdown();
                    return latest;
                }

                bool updateApplied = await DownloadAndApplyUpdateAsync(latest);
                return updateApplied ? latest : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Update check failed: {ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return null;
            }
        }

        private static async Task<bool> DownloadAndApplyUpdateAsync(UpdateEntry latest)
        {
            string stagingRoot = Path.Combine(Path.GetTempPath(), "StarforgeLauncherUpdate", Guid.NewGuid().ToString("N"));
            string downloadFileName = GetDownloadFileName(latest.UpdateUrl);

            Directory.CreateDirectory(stagingRoot);

            try
            {
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    string attemptDir = Path.Combine(stagingRoot, $"attempt_{attempt}");
                    string downloadedZipPath = Path.Combine(attemptDir, downloadFileName);
                    string extractPath = Path.Combine(attemptDir, "extract");

                    try
                    {
                        SafeDeleteDirectory(attemptDir);
                        Directory.CreateDirectory(attemptDir);

                        SetStatusText($"Downloading update v{LatestLaunchPadVersion}.");
                        await Task.Delay(150);

                        using HttpClient client = CreateHttpClient();
                        using HttpResponseMessage response = await client.GetAsync(latest.UpdateUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        long totalBytes = response.Content.Headers.ContentLength ?? 0;
                        PageHandler.SelfREF?.BeginDownloadProgress(downloadFileName, totalBytes);

                        await using (Stream stream = await response.Content.ReadAsStreamAsync())
                        await using (FileStream file = new FileStream(downloadedZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            await CopyToFileWithProgressAsync(stream, file, totalBytes, downloadFileName);
                        }

                        SetStatusText($"Extracting update v{LatestLaunchPadVersion}.");
                        PageHandler.SelfREF?.SetIndeterminateProgress($"Extracting update v{LatestLaunchPadVersion}.", downloadFileName);

                        Directory.CreateDirectory(extractPath);
                        ZipFile.ExtractToDirectory(downloadedZipPath, extractPath, true);

                        KillProcessByName("StarforgeLaunchPad");

                        bool applySucceeded = await ApplyUpdateAsync(extractPath);
                        if (!applySucceeded)
                        {
                            continue;
                        }

                        PersistLaunchPadVersion(latest.Version);

                        SetStatusText("Update applied.");
                        PageHandler.SelfREF?.CompleteDownloadProgress($"Update v{LatestLaunchPadVersion} applied.", "LaunchPad updated successfully");
                        await Task.Delay(1500);

                        StartLaunchPad(LaunchPadExeName);
                        Application.Current.Shutdown();
                        return true;
                    }
                    catch (InvalidDataException ex) when (ex.Message.Contains("End of Central Directory", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"ZIP extraction failed (attempt {attempt}): {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Update attempt {attempt} failed: {ex.Message}");
                    }

                    await Task.Delay(1200);
                }

                Debug.WriteLine("Update failed twice. Launching current version.");
                SetStatusText($"Update v{LatestLaunchPadVersion} failed. Starting previous version.");
                DownloadHandler.ItemREF.Reset("Update failed. Starting previous version.");
                await Task.Delay(1200);
                StartLaunchPad(LaunchPadExeName);
                Application.Current.Shutdown();
                return false;
            }
            finally
            {
                SafeDeleteDirectory(stagingRoot);
            }
        }

        public static async Task<bool> ApplyUpdateAsync(string updateDir)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string launchPadDir = Path.Combine(baseDir, "LaunchPad");

            Debug.WriteLine($"Base Directory: {baseDir}");
            SetStatusText($"Applying update v{LatestLaunchPadVersion}.");
            PageHandler.SelfREF?.SetIndeterminateProgress($"Applying update v{LatestLaunchPadVersion}.", "Copying updated files.");

            await Task.Delay(150);

            if (!Directory.Exists(updateDir))
            {
                Debug.WriteLine("No update directory found.");
                SetStatusText("No update directory found.");
                return false;
            }

            foreach (string file in Directory.GetFiles(updateDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(updateDir, file);
                string targetPath = Path.Combine(launchPadDir, relativePath);
                string? targetDir = Path.GetDirectoryName(targetPath);

                if (!string.IsNullOrWhiteSpace(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(file, targetPath, true);
                Debug.WriteLine($"Copied: {relativePath}");
            }

            return true;
        }

        public static void StartLaunchPad(string launcherExeName)
        {
            string launchPadPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaunchPad", launcherExeName);

            if (!File.Exists(launchPadPath))
            {
                Debug.WriteLine("Launcher executable not found!");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = launchPadPath,
                UseShellExecute = true
            });

            SetStatusText($"Starting LaunchPad: {launcherExeName}");
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

        public static void KillProcessByName(string exeNameWithoutExtension)
        {
            foreach (Process proc in Process.GetProcessesByName(exeNameWithoutExtension))
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

        private static async Task CopyToFileWithProgressAsync(Stream source, Stream destination, long totalBytes, string fileName)
        {
            byte[] buffer = new byte[81920];
            long totalRead = 0;
            long lastUiUpdateMs = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                int read = await source.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer, 0, read);
                totalRead += read;

                bool shouldUpdateUi = stopwatch.ElapsedMilliseconds - lastUiUpdateMs >= 125 || (totalBytes > 0 && totalRead >= totalBytes);
                if (!shouldUpdateUi)
                {
                    continue;
                }

                TimeSpan? eta = null;
                double elapsedSeconds = Math.Max(0.001d, stopwatch.Elapsed.TotalSeconds);
                double bytesPerSecond = totalRead / elapsedSeconds;

                if (totalBytes > 0 && bytesPerSecond > 0)
                {
                    double remainingSeconds = (totalBytes - totalRead) / bytesPerSecond;
                    eta = remainingSeconds > 0 ? TimeSpan.FromSeconds(remainingSeconds) : TimeSpan.Zero;
                }

                PageHandler.SelfREF?.UpdateDownloadProgress(fileName, totalBytes, totalRead, eta);
                lastUiUpdateMs = stopwatch.ElapsedMilliseconds;
            }

            await destination.FlushAsync();
            PageHandler.SelfREF?.UpdateDownloadProgress(fileName, totalBytes, totalRead, TimeSpan.Zero);
        }

        private static void PersistLaunchPadVersion(string version)
        {
            ConfigFileVariables.launchPadVersion = version;
            ConfigManager.SaveConfig();
            Debug.WriteLine($"Saved LaunchPad version {LatestLaunchPadVersion} after successful update: {version}");
        }

        private static UpdateEntry? GetLatest(IEnumerable<UpdateEntry>? updates)
        {
            return updates?
                .OrderBy(entry => new Version(entry.Version))
                .LastOrDefault();
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        private static string GetDownloadFileName(string updateUrl)
        {
            try
            {
                string name = Path.GetFileName(new Uri(updateUrl).AbsolutePath);
                return string.IsNullOrWhiteSpace(name) ? "LaunchPad_Update.zip" : name;
            }
            catch
            {
                return "LaunchPad_Update.zip";
            }
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup skipped for '{path}': {ex.Message}");
            }
        }

        private static void SetStatusText(string text)
        {
            PageHandler.SelfREF?.SetStatusText(text);
        }
    }
}
