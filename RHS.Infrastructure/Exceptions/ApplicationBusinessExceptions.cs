namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Ném ra khi không tìm thấy hồ sơ theo ID hoặc người dùng không có quyền truy cập.
/// Controller nên bắt exception này và trả về HTTP 404 (Not Found).
/// </summary>
public sealed class ApplicationNotFoundException : HousingApplicationException
{
    public const string CodeNotFound = "APP_NOT_FOUND";

    public Guid ApplicationId { get; }

    public ApplicationNotFoundException(Guid applicationId)
        : base(CodeNotFound, $"Không tìm thấy hồ sơ với ID '{applicationId}'.")
    {
        ApplicationId = applicationId;
    }
}

/// <summary>
/// Ném ra khi Applicant cố tạo hồ sơ thứ hai cho cùng một dự án.
/// Quy định: mỗi Applicant chỉ được nộp 1 hồ sơ cho mỗi dự án.
/// Controller nên trả về HTTP 409 (Conflict).
/// </summary>
public sealed class DuplicateApplicationException : HousingApplicationException
{
    public const string CodeDuplicate = "APP_DUPLICATE";

    public Guid ApplicantId { get; }
    public Guid ProjectId { get; }

    public DuplicateApplicationException(Guid applicantId, Guid projectId)
        : base(CodeDuplicate,
            $"Bạn đã có hồ sơ đăng ký cho dự án này. " +
            $"Mỗi người chỉ được nộp một hồ sơ cho mỗi dự án.")
    {
        ApplicantId = applicantId;
        ProjectId = projectId;
    }
}

/// <summary>
/// Ném ra khi Applicant cố nộp hồ sơ (SUBMIT) nhưng chưa upload đủ giấy tờ bắt buộc.
/// Yêu cầu: phải có ít nhất 1 trong 2 loại giấy tờ trước khi nộp.
/// Controller nên trả về HTTP 422 (Unprocessable Entity).
/// </summary>
public sealed class ApplicationNotReadyToSubmitException : HousingApplicationException
{
    public const string CodeNotReady = "APP_NOT_READY";

    public Guid ApplicationId { get; }
    public string Reason { get; }

    public ApplicationNotReadyToSubmitException(Guid applicationId, string reason)
        : base(CodeNotReady,
            $"Hồ sơ chưa đủ điều kiện để nộp: {reason}")
    {
        ApplicationId = applicationId;
        Reason = reason;
    }
}
