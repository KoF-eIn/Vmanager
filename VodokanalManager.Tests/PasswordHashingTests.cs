using Microsoft.VisualStudio.TestTools.UnitTesting;
using VodokanalManager.Services;

namespace VodokanalManager.Tests
{
    [TestClass]
    public class PasswordHashingTests
    {
        [TestMethod]
        public void HashPassword_GeneratesSaltAndHash_VerifiesSuccessfully()
        {
            var hasher = new PasswordHasher();
            string password = "AdminPassword_2026";

            string hash = hasher.HashPassword(password, out string salt);

            Assert.IsFalse(string.IsNullOrWhiteSpace(hash));
            Assert.IsFalse(string.IsNullOrWhiteSpace(salt));

            bool isValid = hasher.VerifyPassword(password, hash, salt);
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void VerifyPassword_RejectsIncorrectPassword()
        {
            var hasher = new PasswordHasher();
            string correctPassword = "CorrectPassword123";
            string wrongPassword = "WrongPassword123";

            string hash = hasher.HashPassword(correctPassword, out string salt);

            bool isValid = hasher.VerifyPassword(wrongPassword, hash, salt);
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void VerifyPassword_PrecomputedHashes_AuthenticateProperly()
        {
            var hasher = new PasswordHasher();

string saltAdmin = "JTpn5IIfAH5rDZTTxChEWA==";
            string hashAdmin = "VuGeycb49ak2f12dyIjmjDCfJ/17J5utppF6dcGgslQ=";

            Assert.IsTrue(hasher.VerifyPassword("admin", hashAdmin, saltAdmin));
            Assert.IsFalse(hasher.VerifyPassword("notadmin", hashAdmin, saltAdmin));
        }
    }
}
