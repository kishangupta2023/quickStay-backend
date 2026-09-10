using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using TripHaeven.Api.Configuration;

namespace TripHaeven.Api.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration configuration, IOptions<CloudinarySettings> options, ILogger<CloudinaryService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        var cloudName = !string.IsNullOrWhiteSpace(settings.CloudName) ? settings.CloudName : (configuration["CLOUDINARY_CLOUD_NAME"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"));
        var apiKey = !string.IsNullOrWhiteSpace(settings.ApiKey) ? settings.ApiKey : (configuration["CLOUDINARY_API_KEY"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"));
        var apiSecret = !string.IsNullOrWhiteSpace(settings.ApiSecret) ? settings.ApiSecret : (configuration["CLOUDINARY_API_SECRET"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET"));

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file.Length == 0) return string.Empty;

        try
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream)
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image {FileName} to Cloudinary", file.FileName);
            return string.Empty;
        }
    }
}
