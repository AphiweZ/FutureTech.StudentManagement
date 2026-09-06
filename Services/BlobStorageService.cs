using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace FutureTech.StudentManagement.Services;

public class BlobStorageService
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    private readonly BlobContainerClient _containerClient;
    private readonly string _accountName;
    private readonly string _accountKey;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        var connectionString = configuration["BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("BlobStorage:ConnectionString is missing.");
        var containerName = configuration["BlobStorage:ContainerName"] ?? "student-images";
        _containerClient = new BlobContainerClient(connectionString, containerName);
        _containerClient.CreateIfNotExists(PublicAccessType.None);

        _accountName = GetConnectionStringValue(connectionString, "AccountName")
            ?? throw new InvalidOperationException("Blob storage account name is missing.");

        _accountKey = GetConnectionStringValue(connectionString, "AccountKey")
            ?? throw new InvalidOperationException("Blob storage account key is missing.");

        _logger = logger;

    }
    private static string? GetConnectionStringValue(string connectionString, string key)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 2 && pieces[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return pieces[1].Trim();
            }
        }

        return null;
    }
    public async Task<string> UploadProfileImageAsync(IFormFile imageFile, string studentId)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            throw new InvalidOperationException("A profile image file is required.");
        }

        if (!AllowedContentTypes.Contains(imageFile.ContentType))
        {
            throw new InvalidOperationException("Only JPEG and PNG profile images are allowed.");
        }

        using var inputStream = imageFile.OpenReadStream();
        using var image = await ImageSharpImage.LoadAsync(inputStream);

        if (image.Width > 800 || image.Height > 800)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(800, 800)
            }));
        }

        using var outputStream = new MemoryStream();
        await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 80 });
        outputStream.Position = 0;

        var blobName = $"students/{studentId}/{Guid.NewGuid():N}.jpg";
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(outputStream, new BlobHttpHeaders
        {
            ContentType = "image/jpeg",
            CacheControl = "public, max-age=86400"
        });

        _logger.LogInformation("Profile image uploaded for student {StudentId}: {BlobName}", studentId, blobName);
        return blobName;
    }

    public async Task DeleteImageAsync(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
        _logger.LogInformation("Image deleted: {BlobName}", blobName);
    }

    public string GenerateSasToken(string? blobName, int expirationMinutes = 60)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return string.Empty;
        }

        var blobClient = _containerClient.GetBlobClient(blobName);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        var credential = new StorageSharedKeyCredential(_accountName, _accountKey);
        var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
        return $"{blobClient.Uri}?{sasToken}";
    }

    public string GetBlobUrl(string? blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return string.Empty;
        }

        return _containerClient.GetBlobClient(blobName).Uri.ToString();
    }
}
