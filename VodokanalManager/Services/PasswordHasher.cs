using System;
using System.Security.Cryptography;

namespace VodokanalManager.Services
{
    public interface IPasswordHasher
    {
        string HashPassword(string password, out string saltBase64);
        bool VerifyPassword(string password, string hashBase64, string saltBase64);
    }

    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltByteSize = 16;
        private const int HashByteSize = 32;
        private const int Pbkdf2Iterations = 10000;

        public string HashPassword(string password, out string saltBase64)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltByteSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            saltBase64 = Convert.ToBase64String(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations))
            {
                byte[] hash = pbkdf2.GetBytes(HashByteSize);
                return Convert.ToBase64String(hash);
            }
        }

        public bool VerifyPassword(string password, string hashBase64, string saltBase64)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64))
                return false;

            try
            {
                byte[] salt = Convert.FromBase64String(saltBase64);
                byte[] expectedHash = Convert.FromBase64String(hashBase64);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations))
                {
                    byte[] actualHash = pbkdf2.GetBytes(HashByteSize);
                    return SlowEquals(expectedHash, actualHash);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool SlowEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
