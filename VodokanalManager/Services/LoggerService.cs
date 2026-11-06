using System;
using System.IO;
using System.Text;

namespace VodokanalManager.Services
{
    public interface ILoggerService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception ex = null);
        string LogFilePath { get; }
    }

    public class LoggerService : ILoggerService
    {
        private readonly string _logFilePath;
        private readonly object _lockObj = new object();

        public string LogFilePath => _logFilePath;

        public LoggerService()
        {
            _logFilePath = DetermineLogPath();
        }

        private static string DetermineLogPath()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "VodokanalManager");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return Path.Combine(folder, "log.txt");
            }
            catch
            {
                try
                {
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
                }
                catch
                {
                    return Path.Combine(Path.GetTempPath(), "VodokanalManager_log.txt");
                }
            }
        }

        public void LogInfo(string message) => WriteEntry("INFO", message);
        public void LogWarning(string message) => WriteEntry("WARN", message);

        public void LogError(string message, Exception ex = null)
        {
            var sb = new StringBuilder(message);
            if (ex != null)
            {
                sb.AppendLine().Append("Исключение: ").Append(ex.GetType().FullName).Append(": ").Append(ex.Message);
                sb.AppendLine().Append(ex.StackTrace);
            }
            WriteEntry("ERROR", sb.ToString());
        }

        private void WriteEntry(string level, string message)
        {
            lock (_lockObj)
            {
                try
                {
                    string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, line, Encoding.UTF8);
                }
                catch
                {
                    // Игнорируем ошибки логирования
                }
            }
        }
    }
}
