namespace TripHaeven.Api.Configuration;

public class CloudinarySettings
{
    public const string SectionName = "Cloudinary";
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}

public class StripeSettings
{
    public const string SectionName = "Stripe";
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

public class ClerkSettings
{
    public const string SectionName = "Clerk";
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string? Authority { get; set; }
}

public class EmailSettings
{
    public const string SectionName = "EmailSettings";
    public string SenderEmail { get; set; } = string.Empty;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
}

public class MongoDbSettings
{
    public const string SectionName = "ConnectionStrings";
    public string MongoDB { get; set; } = string.Empty;
}
