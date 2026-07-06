using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    public class CryptoServiceTests
    {
        [Fact]
        public void HashPassword_ThenVerify_Succeeds()
        {
            string stored = CryptoService.HashPassword("Teacher!2026");

            Assert.True(CryptoService.VerifyPassword("Teacher!2026", stored));
        }

        [Fact]
        public void VerifyPassword_WrongPassword_Fails()
        {
            string stored = CryptoService.HashPassword("Teacher!2026");

            Assert.False(CryptoService.VerifyPassword("wrong-password", stored));
        }

        [Fact]
        public void HashPassword_ProducesSaltHashFormat_WithUniqueSalts()
        {
            string first = CryptoService.HashPassword("same-password");
            string second = CryptoService.HashPassword("same-password");

            Assert.Equal(2, first.Split(':').Length);
            Assert.NotEqual(first, second); // random salt per call
        }

        [Fact]
        public void VerifyPassword_RejectsEncryptSecretOutput()
        {
            // Regression guard for the master-password lockout bug: a password stored via
            // EncryptSecret (reversible DPAPI) must never satisfy VerifyPassword.
            string wronglyStored = CryptoService.EncryptSecret("Teacher!2026");

            Assert.False(CryptoService.VerifyPassword("Teacher!2026", wronglyStored));
        }

        [Fact]
        public void VerifyPassword_EmptyOrMalformedStoredHash_Fails()
        {
            Assert.False(CryptoService.VerifyPassword("anything", string.Empty));
            Assert.False(CryptoService.VerifyPassword("anything", "not-a-valid-hash"));
            Assert.False(CryptoService.VerifyPassword("anything", "only:two:parts:here"));
        }

        [Fact]
        public void EncryptSecret_ThenDecrypt_RoundTrips()
        {
            string secret = "sk-test-api-key-1234567890";

            string encrypted = CryptoService.EncryptSecret(secret);

            Assert.NotEqual(secret, encrypted);
            Assert.Equal(secret, CryptoService.DecryptSecret(encrypted));
        }

        [Fact]
        public void DecryptSecret_InvalidCiphertext_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, CryptoService.DecryptSecret("garbage-not-base64"));
            Assert.Equal(string.Empty, CryptoService.DecryptSecret(string.Empty));
        }
    }
}
