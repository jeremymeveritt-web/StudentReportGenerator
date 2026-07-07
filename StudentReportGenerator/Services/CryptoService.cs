using System;
using System.Security.Cryptography;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// All cryptographic primitives for the app live here, split into two distinct concerns
    /// that must never be confused with one another:
    ///
    /// 1. <b>One-way password hashing</b> (<see cref="HashPassword"/> / <see cref="VerifyPassword"/>) —
    ///    used exclusively for the teacher's master settings password. Salted PBKDF2 is irreversible
    ///    by design: there is no "DecryptPassword" method because the plaintext is never recoverable,
    ///    only re-derivable for comparison.
    ///
    /// 2. <b>Reversible field-level encryption</b> (<see cref="EncryptSecret"/> / <see cref="DecryptSecret"/>) —
    ///    used for values the app must read back in plaintext later, such as AI provider API keys and
    ///    the SMTP password, via Windows DPAPI.
    ///
    /// Mixing these two up was a real production bug: a master password saved with
    /// <see cref="EncryptSecret"/> instead of <see cref="HashPassword"/> can never satisfy
    /// <see cref="VerifyPassword"/>, permanently locking the teacher out of Settings. See
    /// StudentReportGenerator.Tests.CryptoServiceTests for the regression test that guards against this.
    /// </summary>
    public static class CryptoService
    {
        private const int SaltSize = 16; // 128-bit salt
        private const int KeySize = 32;  // 256-bit derived key
        private const int Iterations = 100_000; // High iteration count to slow down offline brute-force attacks
        private static readonly HashAlgorithmName _hashAlgorithmName = HashAlgorithmName.SHA256;

        /// <summary>
        /// One-way hashes a password using PBKDF2-SHA256 with a fresh random salt per call.
        /// The salt and derived key are Base64-encoded and joined as "salt:hash" so both are
        /// self-contained in a single stored string. There is no corresponding decrypt method —
        /// this is intentionally irreversible.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _hashAlgorithmName, KeySize);

            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifies a plaintext password against a hash previously produced by <see cref="HashPassword"/>.
        /// Re-derives the hash using the same salt and iteration count, then compares in constant time
        /// via <see cref="CryptographicOperations.FixedTimeEquals"/> to avoid leaking timing information
        /// that could help an attacker guess the password byte-by-byte.
        /// </summary>
        /// <param name="storedHash">Must be in the "salt:hash" format produced by <see cref="HashPassword"/>;
        /// any other format (including output from <see cref="EncryptSecret"/>) will safely return false.</param>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;

            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] storedPasswordHash = Convert.FromBase64String(parts[1]);

                byte[] hashToVerify = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _hashAlgorithmName, KeySize);

                return CryptographicOperations.FixedTimeEquals(storedPasswordHash, hashToVerify);
            }
            catch
            {
                // Malformed/corrupted stored hash — treat as "does not match" rather than throwing.
                return false;
            }
        }

        // --- Field-level encryption for API keys & SMTP passwords ---

        /// <summary>
        /// Fixed DPAPI entropy for field-level secrets. This is NOT the security boundary — Windows
        /// DPAPI already scopes decryption to the current Windows user account (<see cref="DataProtectionScope.CurrentUser"/>),
        /// so this constant being visible in a public repository does not weaken protection. It exists
        /// purely as defence-in-depth. See SECURITY.md for the full threat model.
        /// </summary>
        private static readonly byte[] FieldEntropy = System.Text.Encoding.UTF8.GetBytes("FacultyFlow_API_Secure_V1");

        /// <summary>Encrypts a secret (API key, SMTP password) with DPAPI, recoverable only by the same Windows user account.</summary>
        public static string EncryptSecret(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText)) return string.Empty;
            byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, FieldEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>Reverses <see cref="EncryptSecret"/>. Returns an empty string if the ciphertext is invalid,
        /// corrupted, or was encrypted under a different Windows user account — never throws.</summary>
        public static string DecryptSecret(string encryptedBase64)
        {
            if (string.IsNullOrWhiteSpace(encryptedBase64)) return string.Empty;
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, FieldEntropy, DataProtectionScope.CurrentUser);
                return System.Text.Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
