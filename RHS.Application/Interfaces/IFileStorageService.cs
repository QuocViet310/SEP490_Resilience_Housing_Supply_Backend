using Microsoft.AspNetCore.Http;

namespace RHS.Application.Interfaces;

public interface IFileStorageService
{
    // ── Image (existing) ─────────────────────────────────────────
    Task<string> UploadImageAsync(IFormFile file, string folder);
    Task<bool> DeleteImageAsync(string imageUrl);
    bool IsValidImageFile(IFormFile file);

    // ── PDF Document (new) ───────────────────────────────────────

    /// <summary>
    /// Upload file PDF lên Cloudinary dưới dạng raw file.
    /// Trả về secure URL của file đã upload.
    /// </summary>
    /// <param name="file">File PDF từ multipart/form-data</param>
    /// <param name="folder">Thư mục lưu trữ trên Cloudinary (ví dụ: "housing-docs")</param>
    Task<string> UploadPdfAsync(IFormFile file, string folder);

    /// <summary>
    /// Kiểm tra file có phải PDF hợp lệ không.
    /// Kiểm tra: extension .pdf, content-type application/pdf, kích thước ≤ maxSizeMb.
    /// </summary>
    bool IsValidPdfFile(IFormFile file, long maxSizeBytes);
}

