using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace TripHaeven.Api.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"] ?? configuration["CLOUDINARY_CLOUD_NAME"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
        var apiKey = configuration["Cloudinary:ApiKey"] ?? configuration["CLOUDINARY_API_KEY"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
        var apiSecret = configuration["Cloudinary:ApiSecret"] ?? configuration["CLOUDINARY_API_SECRET"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file.Length == 0) return string.Empty;

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            // Transformation = new Transformation().Height(500).Width(500).Crop("fill") // Optional, could match Node.js
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        return uploadResult.SecureUrl.ToString();
    }
}
