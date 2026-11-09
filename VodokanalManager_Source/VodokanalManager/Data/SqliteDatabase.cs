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

            string localDb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.db");
            if (File.Exists(localDb))
            {
                _databasePath = localDb;
            }
            else if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
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
                
            }
        }

        private string DetermineDatabasePath()
        {
            
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localFile = Path.Combine(baseDir, "data.db");
                if (File.Exists(localFile))
                    return localFile;

                string localFolderFile = Path.Combine(baseDir, "app_data", "data.db");
                if (File.Exists(localFolderFile))
                    return localFolderFile;
            }
            catch
            {
                
            }

try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "VodokanalManager");
                string appDataFile = Path.Combine(folder, "data.db");
                if (File.Exists(appDataFile))
                    return appDataFile;

try
                {
                    string testWrite = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".write_test");
                    File.WriteAllText(testWrite, "1");
                    File.Delete(testWrite);
                    return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.db");
                }
                catch
                {
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                    return appDataFile;
                }
            }
            catch
            {
                
            }

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
