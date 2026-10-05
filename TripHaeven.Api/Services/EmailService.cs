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
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _senderEmail = !string.IsNullOrWhiteSpace(settings.SenderEmail) ? settings.SenderEmail : (configuration["SENDER_EMAIL"] ?? Environment.GetEnvironmentVariable("SENDER_EMAIL") ?? string.Empty);
        _smtpUser = !string.IsNullOrWhiteSpace(settings.SmtpUser) ? settings.SmtpUser : (configuration["SMTP_USER"] ?? Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty);
        _smtpPass = !string.IsNullOrWhiteSpace(settings.SmtpPass) ? settings.SmtpPass : (configuration["SMTP_PASS"] ?? Environment.GetEnvironmentVariable("SMTP_PASS") ?? string.Empty);
        _smtpHost = !string.IsNullOrWhiteSpace(settings.SmtpHost) ? settings.SmtpHost : (configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp-relay.brevo.com");
        _smtpPort = settings.SmtpPort > 0 ? settings.SmtpPort : (int.TryParse(configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587);
    }

    public async Task SendEmailAsync(string to, string subject, string text, string html = "")
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("Email sending skipped: Recipient email is empty");
            return;
        }

        if (string.IsNullOrWhiteSpace(_smtpUser) || string.IsNullOrWhiteSpace(_smtpPass) || string.IsNullOrWhiteSpace(_senderEmail))
        {
            _logger.LogWarning("Email sending skipped: SMTP credentials not configured");
            return;
        }

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
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
            _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Recipient} with subject '{Subject}'", to, subject);
        }
    }
}
