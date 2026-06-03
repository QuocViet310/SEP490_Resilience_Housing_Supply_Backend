using RHS.Application.DTOs.EKyc;

namespace RHS.Application.Interfaces;

/// <summary>
/// Định nghĩa contract cho dịch vụ eKYC tích hợp FPT AI.
/// Triển khai cụ thể nằm ở <c>RHS.Infrastructure.Services.FptEKycService</c>.
/// </summary>
public interface IEKycService
{
    /// <summary>
    /// Gọi FPT AI OCR API để trích xuất thông tin từ ảnh Căn cước công dân.
    /// </summary>
    /// <param name="request">
    /// Yêu cầu chứa file ảnh CCCD (mặt trước hoặc mặt sau).
    /// File phải là JPEG/PNG và dung lượng ≤ 5 MB.
    /// </param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>
    /// <see cref="OcrIdCardResponse"/> chứa thông tin được trích xuất.
    /// </returns>
    /// <exception cref="RHS.Infrastructure.Exceptions.EKycValidationException">
    /// Ném ra khi file ảnh không hợp lệ (sai định dạng, quá dung lượng...).
    /// </exception>
    /// <exception cref="RHS.Infrastructure.Exceptions.EKycIntegrationException">
    /// Ném ra khi FPT AI API trả về lỗi hoặc không thể kết nối.
    /// </exception>
    Task<OcrIdCardResponse> ExtractIdCardAsync(
        OcrIdCardRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi FPT AI Face Match API để so sánh ảnh selfie với ảnh trên CCCD.
    /// </summary>
    /// <param name="request">
    /// Yêu cầu chứa ảnh selfie và ảnh chân dung trên thẻ CCCD.
    /// Cả hai file phải là JPEG/PNG và dung lượng ≤ 5 MB.
    /// </param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ.</param>
    /// <returns>
    /// <see cref="FaceMatchResponse"/> chứa kết quả so khớp và độ tương đồng.
    /// </returns>
    /// <exception cref="RHS.Infrastructure.Exceptions.EKycValidationException">
    /// Ném ra khi một trong hai file ảnh không hợp lệ.
    /// </exception>
    /// <exception cref="RHS.Infrastructure.Exceptions.EKycIntegrationException">
    /// Ném ra khi FPT AI API trả về lỗi hoặc không thể kết nối.
    /// </exception>
    Task<FaceMatchResponse> MatchFaceAsync(
        FaceMatchRequest request,
        CancellationToken cancellationToken = default);
}
