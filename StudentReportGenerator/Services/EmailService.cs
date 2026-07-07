using System;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Sends the generated report directly to a parent by SMTP, using the teacher's own school
    /// email account and app password (configured in Settings). No third-party mail relay is
    /// involved — the app connects straight to the SMTP server the teacher provides.
    /// </summary>
    public static class EmailService
    {
        private const int SmtpTimeoutMs = 30_000;

        /// <summary>
        /// Sends a plain-text email via SMTP with SSL/TLS safely without early disposal runtime crashes.
        /// </summary>
        public static async Task<bool> SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            string smtpServer,
            int smtpPort,
            string username,
            SecureString securePassword)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                throw new ArgumentException("Recipient email address is required.", nameof(toEmail));
            if (string.IsNullOrWhiteSpace(smtpServer))
                throw new ArgumentException("SMTP server address is required.", nameof(smtpServer));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("SMTP username is required.", nameof(username));
            if (securePassword == null || securePassword.Length == 0)
                throw new ArgumentException("SMTP password is required.", nameof(securePassword));

            MailMessage? mailMessage = null;

            try
            {
                mailMessage = new MailMessage
                {
                    From = new MailAddress(username),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };
                mailMessage.To.Add(new MailAddress(toEmail));

                var smtpClient = new SmtpClient(smtpServer, smtpPort)
                {
                    EnableSsl = true,
                    Timeout = SmtpTimeoutMs,
                    Credentials = new NetworkCredential(username, securePassword)
                };

                try
                {
                    await smtpClient.SendMailAsync(mailMessage);
                    return true;
                }
                finally
                {
                    smtpClient.Dispose();
                }
            }
            catch (SmtpException smtpEx)
            {
                throw new InvalidOperationException($"SMTP delivery failed (status {smtpEx.StatusCode}): {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                throw new InvalidOperationException($"Email failed: {ex.Message}", ex);
            }
            finally
            {
                mailMessage?.Dispose();
            }
        }

        /// <summary>Convenience overload accepting a plaintext password (e.g. immediately after
        /// decrypting it with <see cref="CryptoService.DecryptSecret"/>); wraps it in a
        /// short-lived <see cref="SecureString"/> before delegating to the main overload.</summary>
        public static Task<bool> SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            string smtpServer,
            int smtpPort,
            string username,
            string plaintextPassword)
        {
            using var securePassword = new SecureString();
            foreach (char c in plaintextPassword)
                securePassword.AppendChar(c);
            securePassword.MakeReadOnly();

            return SendEmailAsync(toEmail, subject, body, smtpServer, smtpPort, username, securePassword);
        }
    }
}