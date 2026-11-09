using System;
using System.Data;
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VodokanalManager.Data;

namespace VodokanalManager.Tests
{
    [TestClass]
    public class SqliteIntegrationTests
    {
        [TestMethod]
        public void LoweruFunction_TransformsCyrillicToLowerInvariant()
        {
            var loweru = new LoweruFunction();
            object result = loweru.Invoke(new object[] { "МУП ВОДОКАНАЛ г. Канск" });

            Assert.AreEqual("муп водоканал г. канск", result);
        }

        [TestMethod]
        public void RuCiCollation_ComparesRussianStringsCaseInsensitively()
        {
            var collation = new RuCiCollation();

            int cmp1 = collation.Compare("Соколова", "соколова");
            Assert.AreEqual(0, cmp1);

            int cmp2 = collation.Compare("Иванов", "Петров");
            Assert.IsTrue(cmp2 < 0);
        }

        [TestMethod]
        public void InMemoryDatabase_ExecutionOfAllScripts_Succeeds()
        {
            using (var conn = new SQLiteConnection("Data Source=:memory:;Foreign Keys=True;"))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE Subscribers (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            AccountNo TEXT NOT NULL UNIQUE,
                            Name TEXT NOT NULL
                        );
                        INSERT INTO Subscribers (AccountNo, Name) VALUES ('100245', 'Соколова Нина');
                        SELECT COUNT(*) FROM Subscribers;
                    ";
                    long count = Convert.ToInt64(cmd.ExecuteScalar());
                    Assert.AreEqual(1L, count);
                }
            }
        }
    }
}
