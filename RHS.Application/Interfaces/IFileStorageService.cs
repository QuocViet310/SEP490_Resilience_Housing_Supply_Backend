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

    /// <summary>
    /// Upload file PDF từ byte array (sinh từ QuestPDF) lên Cloudinary.
    /// </summary>
    /// <param name="pdfBytes">Nội dung file PDF dạng byte array</param>
    /// <param name="fileName">Tên file (ví dụ: "HopDong_NOXH-BT1-001.pdf")</param>
    /// <param name="folder">Thư mục trên Cloudinary (ví dụ: "principle-agreements")</param>
    /// <returns>Secure URL của file đã upload</returns>
    Task<string> UploadPdfFromBytesAsync(byte[] pdfBytes, string fileName, string folder);

    /// <summary>
    /// Tải file từ URL, tự động sinh chữ ký bảo mật (signed URL) nếu tài nguyên nằm trên Cloudinary.
    /// </summary>
    /// <param name="fileUrl">URL của file (từ DB/Cloudinary hoặc ngoài hệ thống)</param>
    /// <returns>Mảng byte dữ liệu của file</returns>
    Task<byte[]> DownloadFileAsync(string fileUrl);
}


