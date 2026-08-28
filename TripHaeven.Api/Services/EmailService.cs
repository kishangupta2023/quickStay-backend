using System.Net;
using System.Net.Mail;

namespace TripHaeven.Api.Services;

public class EmailService
{
    private readonly string _senderEmail;
    private readonly string _smtpUser;
    private readonly string _smtpPass;

    public EmailService(IConfiguration configuration)
    {
        _senderEmail = configuration["EmailSettings:SenderEmail"] ?? configuration["SENDER_EMAIL"] ?? Environment.GetEnvironmentVariable("SENDER_EMAIL") ?? "";
        _smtpUser = configuration["EmailSettings:SmtpUser"] ?? configuration["SMTP_USER"] ?? Environment.GetEnvironmentVariable("SMTP_USER") ?? "";
        _smtpPass = configuration["EmailSettings:SmtpPass"] ?? configuration["SMTP_PASS"] ?? Environment.GetEnvironmentVariable("SMTP_PASS") ?? "";
    }

    public async Task SendEmailAsync(string to, string subject, string text, string html = "")
    {
        try
        {
            var client = new SmtpClient("smtp-brevo.com", 587)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_senderEmail, "TripHaeven QuickStay"),
                Subject = subject,
                Body = !string.IsNullOrEmpty(html) ? html : text,
                IsBodyHtml = !string.IsNullOrEmpty(html)
            };

            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending email: {ex.Message}");
        }
    }
}
