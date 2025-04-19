using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace StarforgeLauncher.data
{

    public static class LauncherClientVariables
    {
        public static string LaunchPadDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LaunchPad");

    }
    public static class ConfigFileVariables
    {
        public static string launcherVersion = "0.0.1";
        public static string launchPadVersion = "0.0.1";
        public static string InstallDirectory = Path.Combine(LauncherClientVariables.LaunchPadDirectory, "Starforge");
        public static bool isInstalled = false;
        public static bool isDirectorySet = false;
    }

    class ConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(LauncherClientVariables.LaunchPadDirectory, "config.cfg");

        public static async Task InitializeConfig()
        {
            if (!System.IO.File.Exists(ConfigFilePath))
            {
                if (!System.IO.File.Exists(LauncherClientVariables.LaunchPadDirectory))
                {
                    System.IO.Directory.CreateDirectory(LauncherClientVariables.LaunchPadDirectory);
                }

                //System.IO.File.Create(ConfigFilePath);
                SaveConfig();
                await Task.Delay(1000);
                LoadConfig();
            }
            else
            {
                LoadConfig();
            }
        }

        public static void SaveConfig()
        {

            List<string> lines = new List<string>();
            foreach (var field in typeof(ConfigFileVariables).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                string name = field.Name;
                string value = field.GetValue(null)?.ToString() ?? "";
                lines.Add($"{name}={value}");
            }
            System.IO.File.WriteAllLines(ConfigFilePath, lines);
        }
        public static void LoadConfig()
        {

            if (!System.IO.File.Exists(ConfigFilePath)) return;

            string[] configLines = System.IO.File.ReadAllLines(ConfigFilePath);
            Dictionary<string, string> configDict = new Dictionary<string, string>();

            foreach (string line in configLines)
            {
                if (!string.IsNullOrWhiteSpace(line) && line.Contains("="))
                {
                    string[] parts = line.Split('=');
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    configDict[key] = value;
                }
            }

            // Update fields in ConfigFileVariables dynamically
            foreach (var field in typeof(ConfigFileVariables).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                if (configDict.TryGetValue(field.Name, out string value))
                {
                    try
                    {
                        if (field.FieldType == typeof(int) && int.TryParse(value, out int intValue))
                            field.SetValue(null, intValue);
                        else if (field.FieldType == typeof(bool) && bool.TryParse(value, out bool boolValue))
                            field.SetValue(null, boolValue);
                        else
                            field.SetValue(null, Convert.ChangeType(value, field.FieldType));
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error loading {field.Name}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
