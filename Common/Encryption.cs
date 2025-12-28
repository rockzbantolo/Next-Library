using System;

namespace Next.Common
{
    public static class Encryption
    {
        private static readonly byte[] Salt = { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        public static string Encrypt(string plainText, string passPhrase = "YourLibrarySecret")
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            using var password = new System.Security.Cryptography.Rfc2898DeriveBytes(passPhrase, Salt);
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = password.GetBytes(32);
            aes.IV = password.GetBytes(16);
            using var ms = new System.IO.MemoryStream();
            using (var cs = new System.Security.Cryptography.CryptoStream(ms, aes.CreateEncryptor(), System.Security.Cryptography.CryptoStreamMode.Write))
                cs.Write(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText, string passPhrase = "YourLibrarySecret")
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            var cipherBytes = Convert.FromBase64String(cipherText);
            using var password = new System.Security.Cryptography.Rfc2898DeriveBytes(passPhrase, Salt);
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Key = password.GetBytes(32);
            aes.IV = password.GetBytes(16);
            using var ms = new System.IO.MemoryStream();
            using (var cs = new System.Security.Cryptography.CryptoStream(ms, aes.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Write))
                cs.Write(cipherBytes, 0, cipherBytes.Length);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
