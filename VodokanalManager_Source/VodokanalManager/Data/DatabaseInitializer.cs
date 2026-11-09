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

        public void Initialize(bool autoFillDemoData = false)
        {
            using (var connection = _database.CreateConnection())
            {
                bool isSchemaPresent = CheckIfSchemaExists(connection);
                if (isSchemaPresent)
                {
                    EnsureDemoFlagColumnsExist(connection);
                    return;
                }

using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ExecuteScript(connection, transaction, LoadSqlResource("01_schema.sql"));
                        ExecuteScript(connection, transaction, LoadSqlResource("02_views.sql"));
                        ExecuteScript(connection, transaction, LoadSqlResource("03_reference.sql"));

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

        public void LoadDemoData()
        {
            using (var connection = _database.CreateConnection())
            {
                EnsureDemoFlagColumnsExist(connection);
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ExecuteScript(connection, transaction, LoadSqlResource("04_demo.sql"));
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
            {
                EnsureDemoFlagColumnsExist(connection);
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.Transaction = transaction;

cmd.CommandText = @"
                                DELETE FROM Payments WHERE IsDemo = 1;
                                DELETE FROM MeterReadings WHERE IsDemo = 1;
                                DELETE FROM Meters WHERE IsDemo = 1;
                                DELETE FROM Users WHERE RoleId = 4 AND IsDemo = 1;
                                DELETE FROM Subscribers WHERE IsDemo = 1;
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
        }

        private void EnsureDemoFlagColumnsExist(IDbConnection connection)
        {
            string[] tables = new[] { "Subscribers", "Users", "Meters", "MeterReadings", "Payments" };
            foreach (var table in tables)
            {
                bool columnExists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"PRAGMA table_info({table});";
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string colName = reader["name"]?.ToString();
                            if (string.Equals(colName, "IsDemo", StringComparison.OrdinalIgnoreCase))
                            {
                                columnExists = true;
                                break;
                            }
                        }
                    }
                }

                if (!columnExists)
                {
                    using (var alterCmd = connection.CreateCommand())
                    {
                        alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN IsDemo INTEGER NOT NULL DEFAULT 0;";
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }

using (var tagCmd = connection.CreateCommand())
            {
                tagCmd.CommandText = @"
                    UPDATE Subscribers SET IsDemo = 1 WHERE AccountNo IN ('100245', '100246', '100247', '100248', '100249', '100250', '100251', '100252', '100253', '100254', '100255', '100256', '200101', '200102', '200103');
                    UPDATE Users SET IsDemo = 1 WHERE Login IN ('100245', '100246', '100247', '100248', '100249', '100250', '100251', '100252', '100253', '100254', '100255', '100256', '200101', '200102', '200103');
                    UPDATE Meters SET IsDemo = 1 WHERE SubscriberId IN (SELECT Id FROM Subscribers WHERE IsDemo = 1);
                    UPDATE MeterReadings SET IsDemo = 1 WHERE MeterId IN (SELECT Id FROM Meters WHERE IsDemo = 1);
                    UPDATE Payments SET IsDemo = 1 WHERE SubscriberId IN (SELECT Id FROM Subscribers WHERE IsDemo = 1);
                ";
                tagCmd.ExecuteNonQuery();
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
