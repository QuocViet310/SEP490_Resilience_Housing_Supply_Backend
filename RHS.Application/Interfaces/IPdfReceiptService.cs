using RHS.Domain.Entities;
using System.Threading.Tasks;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service chịu trách nhiệm sinh biên nhận nộp hồ sơ nhà ở xã hội dưới dạng PDF.
/// </summary>
public interface IPdfReceiptService
{
    /// <summary>
    /// Sinh "Giấy biên nhận nộp hồ sơ", upload lên Cloudinary, và trả về URL file đã ký/bảo mật.
    /// </summary>
    /// <param name="application">Hồ sơ đã nộp thành công</param>
    /// <param name="project">Dự án nhà ở xã hội đăng ký</param>
    /// <returns>URL lưu trên Cloudinary</returns>
    Task<string> GenerateAndUploadReceiptAsync(HousingApplication application, HousingProject project);
}
