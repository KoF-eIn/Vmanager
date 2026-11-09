using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VodokanalManager.Tests
{
    [TestClass]
    public class SqlScriptParsingTests
    {
        [TestMethod]
        public void SplitByGo_SplitsTsqlBatchesCorrectly()
        {
            string tsql = @"
CREATE DATABASE VodokanalDB;
GO
USE VodokanalDB;
go
CREATE TABLE Test (Id INT);
GO
";

            var batches = SplitScriptByGo(tsql);

            Assert.AreEqual(3, batches.Count);
            StringAssert.Contains(batches[0], "CREATE DATABASE");
            StringAssert.Contains(batches[1], "USE VodokanalDB");
            StringAssert.Contains(batches[2], "CREATE TABLE Test");
        }

        private static List<string> SplitScriptByGo(string script)
        {
            var result = new List<string>();
            var lines = script.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentBatch = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line.Trim(), @"^GO\b", RegexOptions.IgnoreCase))
                {
                    string batch = currentBatch.ToString().Trim();
                    if (!string.IsNullOrEmpty(batch))
                    {
                        result.Add(batch);
                    }
                    currentBatch.Clear();
                }
                else
                {
                    currentBatch.AppendLine(line);
                }
            }

            string finalBatch = currentBatch.ToString().Trim();
            if (!string.IsNullOrEmpty(finalBatch))
            {
                result.Add(finalBatch);
            }

            return result;
        }
    }
}
