using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VodokanalManager.Tests
{
    [TestClass]
    public class ValidationTests
    {
        [TestMethod]
        public void AccountNo_Validation_AcceptsValidAccounts()
        {
            string validAcc1 = "100245";
            string validAcc2 = "200101";
            string invalidAcc = "  ";

            Assert.IsTrue(IsValidAccount(validAcc1));
            Assert.IsTrue(IsValidAccount(validAcc2));
            Assert.IsFalse(IsValidAccount(invalidAcc));
        }

        [TestMethod]
        public void DatabaseName_Validation_RegexCheck()
        {
            // Регулярное выражение из ТЗ: ^[A-Za-z0-9_]{1,50}$
            var regex = new Regex(@"^[A-Za-z0-9_]{1,50}$");

            Assert.IsTrue(regex.IsMatch("VodokanalDB"));
            Assert.IsTrue(regex.IsMatch("vodokanal_test_1"));
            Assert.IsFalse(regex.IsMatch("db; DROP TABLE Users;--"));
            Assert.IsFalse(regex.IsMatch(""));
        }

        private static bool IsValidAccount(string acc)
        {
            return !string.IsNullOrWhiteSpace(acc) && Regex.IsMatch(acc.Trim(), @"^[0-9A-Za-z\-]{4,20}$");
        }
    }
}
