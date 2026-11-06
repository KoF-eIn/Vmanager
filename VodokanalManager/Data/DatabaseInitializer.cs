using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace VodokanalManager.Data
{
    public class DatabaseInitializer
    {
        private readonly IDatabase _database;

        public DatabaseInitializer(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public void Initialize(bool autoFillDemoData = true)
        {
            using (var connection = _database.CreateConnection())
            {
                bool isSchemaPresent = CheckIfSchemaExists(connection);
                if (isSchemaPresent)
                {
                    return;
                }

                // Создание схемы и базовых данных при первом запуске
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ExecuteScript(connection, transaction, LoadSqlResource("01_schema.sql"));
                        ExecuteScript(connection, transaction, LoadSqlResource("02_views.sql"));
                        ExecuteScript(connection, transaction, LoadSqlResource("03_reference.sql"));

                        // Если включено автозаполнение демо-данными при создании БД
                        if (autoFillDemoData)
                        {
                            ExecuteScript(connection, transaction, LoadSqlResource("04_demo.sql"));
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void ClearDemoData()
        {
            using (var connection = _database.CreateConnection())
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;

                        // Очистка всех оперативных данных (каскадно или явно)
                        cmd.CommandText = @"
                            DELETE FROM Payments;
                            DELETE FROM MeterReadings;
                            DELETE FROM Meters;
                            DELETE FROM Users WHERE RoleId = 4; -- удаление учетных записей абонентов
                            DELETE FROM Subscribers;
                        ";
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public void BackupDatabase(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Путь для резервной копии не указан.", nameof(destinationPath));

            string sourcePath = _database.DatabasePath;
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("Файл базы данных не найден.", sourcePath);

            string targetDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        public void RestoreDatabase(string sourceBackupPath)
        {
            if (!File.Exists(sourceBackupPath))
                throw new FileNotFoundException("Файл резервной копии не найден.", sourceBackupPath);

            string currentPath = _database.DatabasePath;
            string backupTemp = currentPath + ".pre_restore_bak";

            if (File.Exists(currentPath))
            {
                File.Copy(currentPath, backupTemp, overwrite: true);
            }

            try
            {
                File.Copy(sourceBackupPath, currentPath, overwrite: true);
                _database.Reconnect();
            }
            catch
            {
                if (File.Exists(backupTemp))
                {
                    File.Copy(backupTemp, currentPath, overwrite: true);
                }
                throw;
            }
            finally
            {
                if (File.Exists(backupTemp))
                {
                    try { File.Delete(backupTemp); } catch { }
                }
            }
        }

        private bool CheckIfSchemaExists(IDbConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AppInfo';";
                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result) > 0;
            }
        }

        private static void ExecuteScript(IDbConnection connection, IDbTransaction transaction, string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return;

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static string LoadSqlResource(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = $"VodokanalManager.Database.sqlite.{fileName}";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            // Попытка загрузить из папки Database/sqlite рядом со сборкой или проектом
            string[] probePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "sqlite", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Database", "sqlite", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Database", "sqlite", fileName)
            };

            foreach (var path in probePaths)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path, Encoding.UTF8);
                }
            }

            throw new InvalidOperationException($"SQL-скрипт '{fileName}' не найден ни как внедрённый ресурс, ни на диске.");
        }
    }
}
