using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TripHaeven.Api.Configuration;

namespace TripHaeven.Api.Services;

public class EmailService
{
    private readonly string _senderEmail;
    private readonly string _smtpUser;
    private readonly string _smtpPass;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _senderEmail = !string.IsNullOrWhiteSpace(settings.SenderEmail) ? settings.SenderEmail : (configuration["SENDER_EMAIL"] ?? Environment.GetEnvironmentVariable("SENDER_EMAIL") ?? string.Empty);
        _smtpUser = !string.IsNullOrWhiteSpace(settings.SmtpUser) ? settings.SmtpUser : (configuration["SMTP_USER"] ?? Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty);
        _smtpPass = !string.IsNullOrWhiteSpace(settings.SmtpPass) ? settings.SmtpPass : (configuration["SMTP_PASS"] ?? Environment.GetEnvironmentVariable("SMTP_PASS") ?? string.Empty);
    }

    public async Task SendEmailAsync(string to, string subject, string text, string html = "")
    {
        try
        {
            using var client = new SmtpClient("smtp-brevo.com", 587)
            {
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_senderEmail, "TripHaeven QuickStay"),
                Subject = subject,
                Body = !string.IsNullOrEmpty(html) ? html : text,
                IsBodyHtml = !string.IsNullOrEmpty(html)
            };

            mailMessage.To.Add(to);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully to {Recipient}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Recipient}", to);
        }
    }
}
