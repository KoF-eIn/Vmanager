using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace VodokanalManager.Services
{
    public class AppSettingsData
    {
        public bool AutoFillDemoData { get; set; } = true;
        public string CustomDatabasePath { get; set; } = string.Empty;
        public string LastLogin { get; set; } = "admin";
    }

    public interface ISettingsService
    {
        AppSettingsData Settings { get; }
        void Load();
        void Save();
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        public AppSettingsData Settings { get; private set; } = new AppSettingsData();

        public SettingsService()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "VodokanalManager");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                _settingsFilePath = Path.Combine(folder, "settings.json");
            }
            catch
            {
                _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            }

            Load();
        }

        public void Load()
        {
            if (!File.Exists(_settingsFilePath))
            {
                Settings = new AppSettingsData();
                return;
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath, Encoding.UTF8);
                Settings = new AppSettingsData();

                var autoMatch = Regex.Match(json, @"""AutoFillDemoData""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
                if (autoMatch.Success)
                {
                    Settings.AutoFillDemoData = bool.Parse(autoMatch.Groups[1].Value);
                }

                var pathMatch = Regex.Match(json, @"""CustomDatabasePath""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
                if (pathMatch.Success)
                {
                    Settings.CustomDatabasePath = Regex.Unescape(pathMatch.Groups[1].Value);
                }

                var loginMatch = Regex.Match(json, @"""LastLogin""\s*:\s*""([^""]*)""", RegexOptions.IgnoreCase);
                if (loginMatch.Success)
                {
                    Settings.LastLogin = Regex.Unescape(loginMatch.Groups[1].Value);
                }
            }
            catch
            {
                Settings = new AppSettingsData();
            }
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"AutoFillDemoData\": {(Settings.AutoFillDemoData ? "true" : "false")},");
                sb.AppendLine($"  \"CustomDatabasePath\": \"{EscapeJson(Settings.CustomDatabasePath)}\",");
                sb.AppendLine($"  \"LastLogin\": \"{EscapeJson(Settings.LastLogin)}\"");
                sb.AppendLine("}");

                File.WriteAllText(_settingsFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
