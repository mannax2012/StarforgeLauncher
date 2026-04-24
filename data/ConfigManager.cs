using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace StarforgeLauncher.data
{
    public static class LauncherClientVariables
    {
        public static string LaunchPadDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaunchPad");
    }

    public static class ConfigFileVariables
    {
        public static string launcherVersion = "0.1.11";
        public static string launchPadVersion = "0.0.0";
        public static string InstallDirectory = Path.Combine(LauncherClientVariables.LaunchPadDirectory, "Starforge-Client");
        public static bool isInstalled = false;
        public static bool isDirectorySet = false;
        public static bool rememberMe = false;
        public static string lastUsername = "";
    }

    class ConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(LauncherClientVariables.LaunchPadDirectory, "config.cfg");

        public static async Task InitializeConfig()
        {
            Directory.CreateDirectory(LauncherClientVariables.LaunchPadDirectory);

            string bundledLauncherVersion = ConfigFileVariables.launcherVersion;

            Debug.WriteLine("Config path: " + ConfigFilePath);
            Debug.WriteLine("Bundled launcherVersion: " + bundledLauncherVersion);

            if (!File.Exists(ConfigFilePath))
            {
                SaveConfig();
                await Task.Yield();
                LoadConfig();
                ConfigFileVariables.isDirectorySet = !string.IsNullOrWhiteSpace(ConfigFileVariables.InstallDirectory);
                return;
            }

            string configLauncherVersion = ReadConfigValue("launcherVersion") ?? "0.0.0";
            Debug.WriteLine("Config launcherVersion before sync: " + configLauncherVersion);

            LoadConfig();

            if (IsBundledVersionGreater(bundledLauncherVersion, configLauncherVersion))
            {
                SaveConfig(existingConfig =>
                {
                    existingConfig[nameof(ConfigFileVariables.launcherVersion)] = bundledLauncherVersion;
                });
                Debug.WriteLine("launcherVersion updated to: " + bundledLauncherVersion);
            }

            ConfigFileVariables.isDirectorySet = !string.IsNullOrWhiteSpace(ConfigFileVariables.InstallDirectory);
        }

        private static string? ReadConfigValue(string key)
        {
            if (!File.Exists(ConfigFilePath))
                return null;

            foreach (string line in File.ReadAllLines(ConfigFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                string currentKey = parts[0].Trim();
                string currentValue = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                if (string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                    return currentValue;
            }

            return null;
        }

        private static bool IsBundledVersionGreater(string bundledVersion, string configVersion)
        {
            if (Version.TryParse(bundledVersion, out Version? bundled) &&
                Version.TryParse(configVersion, out Version? config) &&
                bundled is not null &&
                config is not null)
            {
                return bundled > config;
            }

            return !string.Equals(bundledVersion, configVersion, StringComparison.OrdinalIgnoreCase);
        }

        public static void SaveConfig()
        {
            Directory.CreateDirectory(LauncherClientVariables.LaunchPadDirectory);

            Dictionary<string, string> existingConfig = ReadConfigValues();
            List<string> lines = new List<string>();
            foreach (var field in typeof(ConfigFileVariables).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                string name = field.Name;
                string value = field.GetValue(null)?.ToString() ?? "";
                lines.Add($"{name}={value}");
                existingConfig.Remove(name);
            }

            foreach (var entry in existingConfig)
            {
                lines.Add($"{entry.Key}={entry.Value}");
            }

            File.WriteAllLines(ConfigFilePath, lines);
        }

        public static void SaveConfig(Action<Dictionary<string, string>> updateExistingConfig)
        {
            Directory.CreateDirectory(LauncherClientVariables.LaunchPadDirectory);

            Dictionary<string, string> configValues = ReadConfigValues();
            foreach (var field in typeof(ConfigFileVariables).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (!configValues.ContainsKey(field.Name))
                {
                    configValues[field.Name] = field.GetValue(null)?.ToString() ?? "";
                }
            }

            updateExistingConfig(configValues);
            ApplyConfigValues(configValues);
            WriteConfigValues(configValues);
        }

        public static void LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
                return;

            ApplyConfigValues(ReadConfigValues());
        }

        private static Dictionary<string, string> ReadConfigValues()
        {
            Dictionary<string, string> configDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(ConfigFilePath))
                return configDict;

            foreach (string line in File.ReadAllLines(ConfigFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    continue;

                string[] parts = line.Split(new[] { '=' }, 2);
                string key = parts[0].Trim();
                string value = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                configDict[key] = value;
            }

            return configDict;
        }

        private static void ApplyConfigValues(Dictionary<string, string> configDict)
        {
            foreach (var field in typeof(ConfigFileVariables).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (!configDict.TryGetValue(field.Name, out string? value))
                    continue;

                try
                {
                    if (field.FieldType == typeof(int) && int.TryParse(value, out int intValue))
                        field.SetValue(null, intValue);
                    else if (field.FieldType == typeof(bool) && bool.TryParse(value, out bool boolValue))
                        field.SetValue(null, boolValue);
                    else if (field.FieldType == typeof(string))
                        field.SetValue(null, value);
                    else
                        field.SetValue(null, Convert.ChangeType(value, field.FieldType));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading {field.Name}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static void WriteConfigValues(Dictionary<string, string> configValues)
        {
            List<string> lines = new List<string>();
            foreach (var field in typeof(ConfigFileVariables).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                string name = field.Name;
                if (configValues.TryGetValue(name, out string? value))
                {
                    lines.Add($"{name}={value}");
                    configValues.Remove(name);
                }
            }

            foreach (var entry in configValues)
            {
                lines.Add($"{entry.Key}={entry.Value}");
            }

            File.WriteAllLines(ConfigFilePath, lines);
        }
    }
}
