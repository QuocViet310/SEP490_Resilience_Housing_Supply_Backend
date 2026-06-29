using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

            // Sử dụng ImageUploadParams thay vì RawUploadParams để PDF được coi là ResourceType.Image.
            // Điều này cho phép tải file công khai mà không bị chặn 401 bởi Cloudinary Restricted Raw.
            var uploadParams = new ImageUploadParams
            {
                File       = new FileDescription(file.FileName, stream),
                PublicId   = publicId,
                Folder     = folder,
                Overwrite  = false,
                Type       = "upload"
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
    public async Task<string> UploadPdfFromBytesAsync(byte[] pdfBytes, string fileName, string folder)
    {
        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            throw new InvalidOperationException("PDF bytes is null or empty — cannot upload.");
        }

        _logger.LogInformation(
            "UploadPdfFromBytesAsync: {ByteCount} bytes, fileName={FileName}, folder={Folder}.",
            pdfBytes.Length, fileName, folder);

        try
        {
            var publicId = $"{folder}/{Guid.NewGuid()}";

            using var stream = new MemoryStream(pdfBytes);
            stream.Position = 0; // Đảm bảo đọc từ đầu

            // Sử dụng ImageUploadParams thay vì RawUploadParams để PDF được coi là ResourceType.Image
            var uploadParams = new ImageUploadParams
            {
                File       = new FileDescription(fileName, stream),
                PublicId   = publicId,
                Folder     = folder,
                Overwrite  = false,
                Type       = "upload"
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _logger.LogError(
                    "Cloudinary PDF bytes upload failed: {Error}, StatusCode={StatusCode}",
                    uploadResult.Error?.Message, uploadResult.StatusCode);
                throw new InvalidOperationException(
                    $"Upload PDF thất bại: {uploadResult.Error?.Message}");
            }

            _logger.LogInformation(
                "PDF bytes uploaded successfully to Cloudinary: {Url}, Bytes={Bytes}",
                uploadResult.SecureUrl, uploadResult.Bytes);

            return uploadResult.SecureUrl.ToString();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading PDF bytes to Cloudinary.");
            throw new InvalidOperationException("Không thể upload file PDF.", ex);
        }
    }

    public async Task<byte[]> DownloadFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
        {
            throw new ArgumentException("URL không được trống", nameof(fileUrl));
        }

        // Nếu không phải link Cloudinary thì tải trực tiếp bằng HttpClient
        if (!fileUrl.Contains("res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            return await client.GetByteArrayAsync(fileUrl);
        }

        try
        {
            var uri = new Uri(fileUrl);
            var segments = uri.AbsolutePath.Split('/');

            // Tìm index của segment "upload"
            int uploadIndex = Array.IndexOf(segments, "upload");
            if (uploadIndex == -1 || uploadIndex < 2)
            {
                // Fallback tải trực tiếp
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                return await client.GetByteArrayAsync(fileUrl);
            }

            string resourceType = segments[uploadIndex - 1]; // "image", "raw", etc.
            
            // Xác định vị trí của publicId (bỏ qua version nếu có)
            int startOfPublicIdIndex = uploadIndex + 1;
            if (startOfPublicIdIndex < segments.Length && segments[startOfPublicIdIndex].StartsWith("v"))
            {
                startOfPublicIdIndex++;
            }

            var publicIdSegments = segments.Skip(startOfPublicIdIndex).ToArray();
            var publicId = string.Join("/", publicIdSegments);

            // Cloudinary: Đối với raw resource, public ID chứa phần mở rộng (ví dụ .pdf).
            // Đối với image, public ID thường không bao gồm phần mở rộng (phần mở rộng được coi là format).
            // Ta sẽ bóc tách format nếu là image.
            string format = "";
            if (resourceType.Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                var lastDotIndex = publicId.LastIndexOf('.');
                if (lastDotIndex > 0)
                {
                    format = publicId.Substring(lastDotIndex + 1);
                    publicId = publicId.Substring(0, lastDotIndex);
                }
            }

            // Tạo signed URL sử dụng Cloudinary SDK
            var urlBuilder = _cloudinary.Api.UrlImgUp
                .ResourceType(resourceType)
                .Action("upload")
                .Signed(true);

            // Thiết lập định dạng nếu có
            if (!string.IsNullOrEmpty(format))
            {
                urlBuilder.Format(format);
            }

            var signedUrl = urlBuilder.BuildUrl(publicId);

            _logger.LogInformation("Generated signed download URL for {ResourceType}: {SignedUrl}", resourceType, signedUrl);

            using (var downloadClient = new HttpClient())
            {
                downloadClient.Timeout = TimeSpan.FromSeconds(30);
                downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                return await downloadClient.GetByteArrayAsync(signedUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file sử dụng URL ký tên từ Cloudinary: {Url}", fileUrl);
            // Fallback tải trực tiếp
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            return await client.GetByteArrayAsync(fileUrl);
        }
    }
}

