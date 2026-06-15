using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RHS.Application.Interfaces;

namespace RHS.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private readonly long _maxFileSize = 5 * 1024 * 1024; // 5MB

    public FileStorageService(IConfiguration configuration, ILogger<FileStorageService> logger)
    {
        _logger = logger;

        // Initialize Cloudinary
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is missing. Please check appsettings.json");
        }

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true; // Use HTTPS
    }

    public bool IsValidImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return false;
        }

        // Check file size
        if (file.Length > _maxFileSize)
        {
            _logger.LogWarning("File size {Size} exceeds maximum allowed size {MaxSize}", file.Length, _maxFileSize);
            return false;
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
        {
            _logger.LogWarning("File extension {Extension} is not allowed", extension);
            return false;
        }

        // Check content type
        if (!file.ContentType.StartsWith("image/"))
        {
            _logger.LogWarning("File content type {ContentType} is not an image", file.ContentType);
            return false;
        }

        return true;
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder)
    {
        if (!IsValidImageFile(file))
        {
            throw new InvalidOperationException("File không hợp lệ");
        }

        try
        {
            // Generate unique public ID
            var publicId = $"{folder}/{Guid.NewGuid()}";

            // Upload to Cloudinary
            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Folder = folder,
                    Transformation = new Transformation()
                        .Width(800)
                        .Height(800)
                        .Crop("limit")
                        .Quality("auto:good")
                        .FetchFormat("auto"),
                    Overwrite = false
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.LogError("Cloudinary upload failed: {Error}", uploadResult.Error?.Message);
                    throw new InvalidOperationException($"Upload thất bại: {uploadResult.Error?.Message}");
                }

                _logger.LogInformation("File uploaded successfully to Cloudinary: {Url}", uploadResult.SecureUrl);
                return uploadResult.SecureUrl.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Cloudinary");
            throw new InvalidOperationException("Không thể upload file", ex);
        }
    }

    public async Task<bool> DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return false;
        }

        try
        {
            // Extract public ID from Cloudinary URL
            // URL format: https://res.cloudinary.com/{cloud_name}/image/upload/v{version}/{public_id}.{format}
            var publicId = ExtractPublicIdFromUrl(imageUrl);

            if (string.IsNullOrEmpty(publicId))
            {
                _logger.LogWarning("Could not extract public ID from URL: {Url}", imageUrl);
                return false;
            }

            // Delete from Cloudinary
            var deletionParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Image
            };

            var deletionResult = await _cloudinary.DestroyAsync(deletionParams);

            if (deletionResult.Result == "ok" || deletionResult.Result == "not found")
            {
                _logger.LogInformation("File deleted successfully from Cloudinary: {PublicId}", publicId);
                return true;
            }

            _logger.LogWarning("Cloudinary deletion failed: {Result}", deletionResult.Result);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from Cloudinary: {Url}", imageUrl);
            return false;
        }
    }

    /// <summary>
    /// Extract public ID from Cloudinary URL
    /// </summary>
    private string? ExtractPublicIdFromUrl(string imageUrl)
    {
        try
        {
            // Example URL: https://res.cloudinary.com/demo/image/upload/v1234567890/profiles/abc-def-ghi.jpg
            var uri = new Uri(imageUrl);
            var segments = uri.AbsolutePath.Split('/');

            // Find "upload" segment and get everything after version
            var uploadIndex = Array.IndexOf(segments, "upload");
            if (uploadIndex == -1 || uploadIndex + 2 >= segments.Length)
            {
                return null;
            }

            // Skip "upload" and version (v1234567890), get the rest
            var pathSegments = segments.Skip(uploadIndex + 2).ToArray();
            var pathWithExtension = string.Join("/", pathSegments);

            // Remove file extension
            var lastDotIndex = pathWithExtension.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                return pathWithExtension.Substring(0, lastDotIndex);
            }

            return pathWithExtension;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting public ID from URL: {Url}", imageUrl);
            return null;
        }
    }

    // ── PDF Methods ───────────────────────────────────────────────

    public bool IsValidPdfFile(IFormFile file, long maxSizeBytes)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("PDF validation failed: file is null or empty.");
            return false;
        }

        // Kiểm tra kích thước
        if (file.Length > maxSizeBytes)
        {
            _logger.LogWarning(
                "PDF validation failed: file size {Size} exceeds max {Max} bytes.",
                file.Length, maxSizeBytes);
            return false;
        }

        // Kiểm tra extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            _logger.LogWarning("PDF validation failed: extension '{Extension}' is not .pdf.", extension);
            return false;
        }

        // Kiểm tra content-type
        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "PDF validation failed: content-type '{ContentType}' is not application/pdf.",
                file.ContentType);
            return false;
        }

        return true;
    }

    public async Task<string> UploadPdfAsync(IFormFile file, string folder)
    {
        try
        {
            // Đặt tên file duy nhất để tránh trùng lặp
            var publicId = $"{folder}/{Guid.NewGuid()}";

            using var stream = file.OpenReadStream();

            // Dùng RawUploadParams thay vì ImageUploadParams
            // để Cloudinary lưu file PDF dưới dạng raw binary
            var uploadParams = new RawUploadParams
            {
                File       = new FileDescription(file.FileName, stream),
                PublicId   = publicId,
                Folder     = folder,
                Overwrite  = false
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogError(
                    "Cloudinary PDF upload failed: {Error}", uploadResult.Error?.Message);
                throw new InvalidOperationException(
                    $"Upload PDF thất bại: {uploadResult.Error?.Message}");
            }

            _logger.LogInformation(
                "PDF uploaded successfully to Cloudinary: {Url}", uploadResult.SecureUrl);

            return uploadResult.SecureUrl.ToString();
        }
        catch (InvalidOperationException)
        {
            throw; // re-throw lỗi đã có message cụ thể
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading PDF to Cloudinary.");
            throw new InvalidOperationException("Không thể upload file PDF.", ex);
        }
    }
}

