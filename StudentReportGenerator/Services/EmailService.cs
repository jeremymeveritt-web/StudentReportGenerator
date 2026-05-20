using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace StudentReportGenerator.Services
{
    public static class EmailService
    {
        public static async Task<bool> SendEmailAsync(string toEmail, string subject, string body, string smtpServer, int smtpPort, string username, string password)
        {
            try
            {
                using (SmtpClient client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(username, password);

                    MailMessage mailMessage = new MailMessage
                    {
                        From = new MailAddress(username),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = false
                    };
                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Email failed: {ex.Message}");
            }
        }
    }
}