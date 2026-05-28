using System;
using System.Security.Cryptography;

namespace StudentReportGenerator.Services
{
    public static class CryptoService
    {
        private const int SaltSize = 16; // 128-bit salt
        private const int KeySize = 32;  // 256-bit hash
        private const int Iterations = 100000; // High iteration count to slow down brute-force attacks
        private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            // Generate a random salt
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // Hash the password with the salt
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _hashAlgorithmName, KeySize);

            // Combine salt and hash into a single string for storage
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] storedPasswordHash = Convert.FromBase64String(parts[1]);

                // Re-hash the inputted password using the exact same salt and iterations
                byte[] hashToVerify = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _hashAlgorithmName, KeySize);

                // Use FixedTimeEquals to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(storedPasswordHash, hashToVerify);
            }
            catch
            {
                return false; // Failsafe if the hash is corrupted
            }
        }
    }
}