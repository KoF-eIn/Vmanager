using System;
using System.Security.Cryptography;
using System.Text;

namespace VodokanalManager.Services
{
    public static class PasswordGenerator
    {
        private const string Chars = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Generate(int length = 8)
        {
            if (length < 6) length = 8;
            var sb = new StringBuilder(length);
            byte[] randomBytes = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }

            for (int i = 0; i < length; i++)
            {
                int index = randomBytes[i] % Chars.Length;
                sb.Append(Chars[index]);
            }

            return sb.ToString();
        }
    }
}
