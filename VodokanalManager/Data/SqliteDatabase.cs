using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace VodokanalManager.Data
{
    public class SqliteDatabase : IDatabase
    {
        private string _databasePath;
        private string _connectionString;
        private readonly IDialect _dialect = new SqliteDialect();

        public IDialect Dialect => _dialect;
        public string DatabasePath => _databasePath;
        public string ConnectionString => _connectionString;

        public SqliteDatabase(string customPath = null)
        {
            RegisterCustomFunctions();

            if (!string.IsNullOrWhiteSpace(customPath))
            {
                _databasePath = customPath;
            }
            else
            {
                _databasePath = DetermineDatabasePath();
            }

            BuildConnectionString();
        }

        private static void RegisterCustomFunctions()
        {
            try
            {
                SQLiteFunction.RegisterFunction(typeof(LoweruFunction));
                SQLiteFunction.RegisterFunction(typeof(RuCiCollation));
            }
            catch
            {
                // Повторная регистрация игнорируется
            }
        }

        private string DetermineDatabasePath()
        {
            // 1. Попытка создания в %AppData%\VodokanalManager\data.db
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "VodokanalManager");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return Path.Combine(folder, "data.db");
            }
            catch
            {
                // fallback
            }

            // 2. Попытка создания в app_data рядом с exe
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localFolder = Path.Combine(baseDir, "app_data");
                if (!Directory.Exists(localFolder))
                    Directory.CreateDirectory(localFolder);

                return Path.Combine(localFolder, "data.db");
            }
            catch
            {
                // fallback
            }

            // 3. Fallback в %TEMP%\VodokanalManager\data.db
            string tempFolder = Path.Combine(Path.GetTempPath(), "VodokanalManager");
            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            return Path.Combine(tempFolder, "data.db");
        }

        private void BuildConnectionString()
        {
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = _databasePath,
                ForeignKeys = true,
                DefaultTimeout = 15,
                JournalMode = SQLiteJournalModeEnum.Wal
            };
            _connectionString = builder.ConnectionString;
        }

        public void SetDatabasePath(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath))
                throw new ArgumentException("Путь к базе данных не может быть пустым.", nameof(newPath));

            string directory = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _databasePath = newPath;
            BuildConnectionString();
        }

        public void Reconnect()
        {
            BuildConnectionString();
        }

        public IDbConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
