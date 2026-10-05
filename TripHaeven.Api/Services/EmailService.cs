using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
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
    private readonly string _apiKey;
    private readonly ILogger<EmailService> _logger;
    private static readonly HttpClient _httpClient = new HttpClient();

    public EmailService(IConfiguration configuration, IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _logger = logger;
        var settings = options.Value;

        _senderEmail = !string.IsNullOrWhiteSpace(configuration["SENDER_EMAIL"] ?? Environment.GetEnvironmentVariable("SENDER_EMAIL"))
            ? (configuration["SENDER_EMAIL"] ?? Environment.GetEnvironmentVariable("SENDER_EMAIL")!)
            : (!string.IsNullOrWhiteSpace(settings.SenderEmail) ? settings.SenderEmail : string.Empty);

        _smtpUser = !string.IsNullOrWhiteSpace(configuration["SMTP_USER"] ?? Environment.GetEnvironmentVariable("SMTP_USER"))
            ? (configuration["SMTP_USER"] ?? Environment.GetEnvironmentVariable("SMTP_USER")!)
            : (!string.IsNullOrWhiteSpace(settings.SmtpUser) ? settings.SmtpUser : string.Empty);

        _smtpPass = !string.IsNullOrWhiteSpace(configuration["SMTP_PASS"] ?? Environment.GetEnvironmentVariable("SMTP_PASS"))
            ? (configuration["SMTP_PASS"] ?? Environment.GetEnvironmentVariable("SMTP_PASS")!)
            : (!string.IsNullOrWhiteSpace(settings.SmtpPass) ? settings.SmtpPass : string.Empty);

        _smtpHost = !string.IsNullOrWhiteSpace(configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST"))
            ? (configuration["SMTP_HOST"] ?? Environment.GetEnvironmentVariable("SMTP_HOST")!)
            : (!string.IsNullOrWhiteSpace(settings.SmtpHost) ? settings.SmtpHost : "smtp-relay.brevo.com");

        var envPortStr = configuration["SMTP_PORT"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
        if (int.TryParse(envPortStr, out var envPort) && envPort > 0)
        {
            _smtpPort = envPort;
        }
        else if (settings.SmtpPort > 0)
        {
            _smtpPort = settings.SmtpPort;
        }
        else
        {
            _smtpPort = 2525; // Default to port 2525 to prevent firewall blocking on Render
        }

        _apiKey = !string.IsNullOrWhiteSpace(configuration["BREVO_API_KEY"] ?? Environment.GetEnvironmentVariable("BREVO_API_KEY"))
            ? (configuration["BREVO_API_KEY"] ?? Environment.GetEnvironmentVariable("BREVO_API_KEY")!)
            : (!string.IsNullOrWhiteSpace(settings.ApiKey) ? settings.ApiKey : string.Empty);
    }

    public async Task SendEmailAsync(string to, string subject, string text, string html = "")
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("Email sending skipped: Recipient email is empty");
            return;
        }

        // Option A: If Brevo REST API Key is configured, send via HTTPS port 443 (never blocked by cloud hosts)
        if (!string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_senderEmail))
        {
            try
            {
                var payload = new
                {
                    sender = new { name = "TripHaeven QuickStay", email = _senderEmail },
                    to = new[] { new { email = to } },
                    subject = subject,
                    htmlContent = !string.IsNullOrEmpty(html) ? html : $"<p>{text}</p>",
                    textContent = text
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", _apiKey);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email sent successfully via Brevo REST API to {Recipient}", to);
                    return;
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Brevo REST API returned {StatusCode}: {ResponseBody}. Falling back to SMTP relay.", response.StatusCode, responseBody);
            }
            catch (Exception apiEx)
            {
                _logger.LogWarning(apiEx, "Brevo REST API call failed. Falling back to SMTP relay.");
            }
        }

        // Option B: SMTP Relay (supports port 2525 and 587)
        if (string.IsNullOrWhiteSpace(_smtpUser) || string.IsNullOrWhiteSpace(_smtpPass) || string.IsNullOrWhiteSpace(_senderEmail))
        {
            _logger.LogWarning("Email sending skipped: SMTP credentials not configured");
            return;
        }

        // Try primary port (2525 by default)
        var primarySent = await SendViaSmtpAsync(to, subject, text, html, _smtpPort);
        if (!primarySent && _smtpPort != 2525)
        {
            _logger.LogInformation("Retrying email delivery to {Recipient} via fallback port 2525...", to);
            await SendViaSmtpAsync(to, subject, text, html, 2525);
        }
    }

    private async Task<bool> SendViaSmtpAsync(string to, string subject, string text, string html, int port)
    {
        try
        {
            using var client = new SmtpClient(_smtpHost, port)
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
            _logger.LogInformation("Email sent successfully to {Recipient} via SMTP on port {Port} with subject '{Subject}'", to, port, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Recipient} via SMTP on port {Port}: {ErrorMessage}", to, port, ex.Message);
            return false;
        }
    }
}
