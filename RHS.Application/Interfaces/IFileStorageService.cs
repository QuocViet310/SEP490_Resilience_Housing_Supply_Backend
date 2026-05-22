using Microsoft.AspNetCore.Http;

namespace RHS.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadImageAsync(IFormFile file, string folder);
    Task<bool> DeleteImageAsync(string imageUrl);
    bool IsValidImageFile(IFormFile file);
}
