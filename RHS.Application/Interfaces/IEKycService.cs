using RHS.Application.DTOs.EKyc;

namespace RHS.Application.Interfaces;

/// <summary>
/// Định nghĩa contract cho dịch vụ eKYC (Electronic Know Your Customer).
/// Triển khai cụ thể: <c>RHS.Infrastructure.Services.VnptEKycService</c> (VNPT Cloud).
/// </summary>
public interface IEKycService
{
    /// <summary>
    /// Gọi eKYC OCR API để trích xuất thông tin từ ảnh Căn cước công dân.
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
    /// Ném ra khi eKYC API trả về lỗi hoặc không thể kết nối.
    /// </exception>
    Task<OcrIdCardResponse> ExtractIdCardAsync(
        OcrIdCardRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gọi eKYC Face Match API để so sánh ảnh selfie với ảnh trên CCCD.
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
    /// Ném ra khi eKYC API trả về lỗi hoặc không thể kết nối.
    /// </exception>
    Task<FaceMatchResponse> MatchFaceAsync(
        FaceMatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra liveness detection — xác minh ảnh selfie có phải người thật không.
    /// </summary>
    /// <remarks>
    /// ⚠️ VNPT eKYC không hỗ trợ Liveness Detection qua REST API.
    /// Tính năng này yêu cầu tích hợp VNPT eKYC SDK phía client (Mobile/Web).
    /// Gọi method này sẽ throw <see cref="System.NotSupportedException"/>.
    /// </remarks>
    /// <exception cref="System.NotSupportedException">
    /// Luôn ném ra vì VNPT Liveness yêu cầu SDK integration.
    /// </exception>
    Task<LivenessDetectionResponse> DetectLivenessAsync(
        LivenessDetectionRequest request,
        CancellationToken cancellationToken = default);
}
