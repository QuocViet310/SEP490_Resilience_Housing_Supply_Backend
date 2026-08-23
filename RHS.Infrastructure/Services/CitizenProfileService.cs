using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.CitizenProfile;
using RHS.Application.DTOs.HouseholdMember;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class CitizenProfileService : ICitizenProfileService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<CitizenProfileService> _logger;

    public CitizenProfileService(
        AppDbContext db,
        IFileStorageService fileStorageService,
        ILogger<CitizenProfileService> logger)
    {
        _db = db;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // 1. Full Profile & Prefill
    // ─────────────────────────────────────────────────────────────

    public async Task<CitizenFullProfileDto?> GetFullProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.UserHouseholdMembers)
            .Include(u => u.UserDocuments)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return null;

        var householdMembers = user.UserHouseholdMembers
            .OrderBy(m => m.CreatedAt)
            .Select(m => MapToHouseholdMemberResponse(m))
            .ToList();

        var documents = user.UserDocuments
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => MapToUserDocumentResponse(d))
            .ToList();

        return new CitizenFullProfileDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            CitizenId = user.CitizenId,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            Role = user.Role?.RoleName ?? "Applicant",
            ProfileImageUrl = user.ProfileImageUrl,

            // eKYC
            IsEkycVerified = user.IsEkycVerified,
            EkycVerifiedAt = user.EkycVerifiedAt,
            Gender = user.Gender,
            Nationality = user.Nationality,
            PlaceOfOrigin = user.PlaceOfOrigin,
            IdIssueDate = user.IdIssueDate,
            IdIssuePlace = user.IdIssuePlace,

            // Hôn nhân & Vợ chồng
            MaritalStatus = user.MaritalStatus,
            MaritalStatusLabel = MaritalStatusConstants.GetLabel(user.MaritalStatus),
            SpouseFullName = user.SpouseFullName,
            SpouseCitizenId = user.SpouseCitizenId,
            SpouseDateOfBirth = user.SpouseDateOfBirth,
            SpouseMonthlyIncome = user.SpouseMonthlyIncome,

            // Việc làm, Nơi ở & Thu nhập
            Occupation = user.Occupation,
            WorkPlace = user.WorkPlace,
            CurrentResidence = user.CurrentResidence ?? user.Address,
            PermanentAddress = user.PermanentAddress,
            MonthlyIncome = user.MonthlyIncome,

            // Thực trạng nhà ở & Ưu tiên
            HousingStatus = user.HousingStatus,
            AverageHousingAreaPerPerson = user.AverageHousingAreaPerPerson,
            PriorityGroup = user.PriorityGroup,
            PriorityGroupLabel = !string.IsNullOrWhiteSpace(user.PriorityGroup) && PriorityGroupConstants.Labels.TryGetValue(user.PriorityGroup, out var pLabel)
                ? pLabel
                : user.PriorityGroup,

            // Collections
            HouseholdMembersCount = 1 + householdMembers.Count,
            HouseholdMembers = householdMembers,
            Documents = documents,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<CitizenFullProfileDto> UpdateCitizenProfileAsync(
        Guid userId,
        UpdateCitizenProfileDto dto,
        CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.UserHouseholdMembers)
            .Include(u => u.UserDocuments)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");

        // Kiểm tra eKYC: Nếu đã eKYC, không cho phép sửa đổi tùy tiện Họ tên, CCCD, Ngày sinh
        if (user.IsEkycVerified)
        {
            if (!string.IsNullOrWhiteSpace(dto.CitizenId) && dto.CitizenId.Trim() != user.CitizenId)
            {
                throw new InvalidOperationException("Tài khoản đã hoàn tất xác thực eKYC. Không thể thay đổi số CCCD.");
            }
        }
        else
        {
            // Chưa eKYC thì có thể điền thông tin ban đầu
            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (dto.DateOfBirth.HasValue)
                user.DateOfBirth = dto.DateOfBirth.Value;

            if (!string.IsNullOrWhiteSpace(dto.CitizenId))
            {
                var trimmedCid = dto.CitizenId.Trim();
                var exists = await _db.Users.AnyAsync(u => u.CitizenId == trimmedCid && u.Id != userId && u.Status != "Deleted", ct);
                if (exists)
                    throw new InvalidOperationException("Số CCCD này đã được sử dụng bởi tài khoản khác trong hệ thống.");
                user.CitizenId = trimmedCid;
            }

            if (!string.IsNullOrWhiteSpace(dto.Gender))
                user.Gender = dto.Gender.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Nationality))
                user.Nationality = dto.Nationality.Trim();

            if (!string.IsNullOrWhiteSpace(dto.PlaceOfOrigin))
                user.PlaceOfOrigin = dto.PlaceOfOrigin.Trim();
        }

        // Cập nhật số điện thoại & liên hệ
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            user.PhoneNumber = dto.PhoneNumber.Trim();

        // Cập nhật Hôn nhân & Vợ chồng
        if (!string.IsNullOrWhiteSpace(dto.MaritalStatus))
        {
            user.MaritalStatus = dto.MaritalStatus.Trim().ToUpperInvariant();
        }

        user.SpouseFullName = dto.SpouseFullName?.Trim();
        user.SpouseCitizenId = dto.SpouseCitizenId?.Trim();
        user.SpouseDateOfBirth = dto.SpouseDateOfBirth;
        user.SpouseMonthlyIncome = dto.SpouseMonthlyIncome;

        // Cập nhật Việc làm, Nơi ở & Thu nhập
        if (dto.Occupation != null) user.Occupation = dto.Occupation.Trim();
        if (dto.WorkPlace != null) user.WorkPlace = dto.WorkPlace.Trim();
        if (dto.CurrentResidence != null)
        {
            user.CurrentResidence = dto.CurrentResidence.Trim();
            user.Address = dto.CurrentResidence.Trim();
        }
        if (dto.PermanentAddress != null) user.PermanentAddress = dto.PermanentAddress.Trim();
        if (dto.MonthlyIncome.HasValue) user.MonthlyIncome = dto.MonthlyIncome.Value;

        // Cập nhật Thực trạng nhà ở & Đối tượng ưu tiên
        if (!string.IsNullOrWhiteSpace(dto.HousingStatus))
            user.HousingStatus = dto.HousingStatus.Trim().ToUpperInvariant();

        if (dto.AverageHousingAreaPerPerson.HasValue)
            user.AverageHousingAreaPerPerson = dto.AverageHousingAreaPerPerson.Value;

        if (!string.IsNullOrWhiteSpace(dto.PriorityGroup))
            user.PriorityGroup = dto.PriorityGroup.Trim().ToUpperInvariant();

        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated citizen profile for user {UserId}", userId);

        return (await GetFullProfileAsync(userId, ct))!;
    }

    public async Task<ApplicationPrefillResponseDto> GetApplicationPrefillAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserHouseholdMembers)
            .Include(u => u.UserDocuments)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản.");

        var householdMembers = user.UserHouseholdMembers
            .OrderBy(m => m.CreatedAt)
            .Select(m => new HouseholdMemberRequestDto
            {
                FullName = m.FullName,
                CitizenId = m.CitizenId,
                DateOfBirth = m.DateOfBirth,
                Relationship = m.Relationship,
                Occupation = m.Occupation,
                MonthlyIncome = m.MonthlyIncome,
                IsDependent = m.IsDependent,
                DependentReason = m.DependentReason,
                HasMeritService = m.HasMeritService,
                MeritDetails = m.MeritDetails,
                Note = m.Note
            })
            .ToList();

        var availableDocs = user.UserDocuments
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new PrefillDocumentItemDto
            {
                DocumentId = d.DocumentId,
                DocumentType = d.DocumentType,
                DocumentTypeLabel = DocumentTypeConstants.GetLabel(d.DocumentType),
                FileName = d.FileName,
                FileUrl = d.FileUrl,
                FileSizeBytes = d.FileSizeBytes,
                VerificationStatus = d.VerificationStatus
            })
            .ToList();

        return new ApplicationPrefillResponseDto
        {
            ApplicantId = user.Id,
            FullName = user.FullName,
            CitizenId = user.CitizenId ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            IsEkycVerified = user.IsEkycVerified,

            Occupation = user.Occupation,
            WorkPlace = user.WorkPlace,
            CurrentResidence = user.CurrentResidence ?? user.Address ?? string.Empty,
            PermanentAddress = user.PermanentAddress ?? user.Address ?? string.Empty,

            HousingStatus = user.HousingStatus ?? HousingStatusConstants.NoHouse,
            MaritalStatus = user.MaritalStatus ?? MaritalStatusConstants.Single,
            PriorityGroup = user.PriorityGroup ?? string.Empty,

            MonthlyIncome = user.MonthlyIncome,
            SpouseMonthlyIncome = user.SpouseMonthlyIncome,
            AverageHousingAreaPerPerson = user.AverageHousingAreaPerPerson,

            HouseholdMembersCount = 1 + householdMembers.Count,
            HouseholdMembers = householdMembers,
            AvailableVaultDocuments = availableDocs
        };
    }

    // ─────────────────────────────────────────────────────────────
    // 2. User Household Members
    // ─────────────────────────────────────────────────────────────

    public async Task<List<UserHouseholdMemberResponseDto>> GetHouseholdMembersAsync(Guid userId, CancellationToken ct = default)
    {
        var members = await _db.UserHouseholdMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return members.Select(MapToHouseholdMemberResponse).ToList();
    }

    public async Task<UserHouseholdMemberResponseDto> AddHouseholdMemberAsync(
        Guid userId,
        UserHouseholdMemberRequestDto dto,
        CancellationToken ct = default)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists)
            throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");

        // Kiểm tra CCCD trùng trong cùng sổ hộ khẩu của user
        if (!string.IsNullOrWhiteSpace(dto.CitizenId))
        {
            var trimmedCid = dto.CitizenId.Trim();
            var duplicate = await _db.UserHouseholdMembers
                .AnyAsync(m => m.UserId == userId && m.CitizenId == trimmedCid, ct);

            if (duplicate)
                throw new InvalidOperationException($"Số CCCD {trimmedCid} đã tồn tại trong danh sách hộ gia đình của bạn.");
        }

        var member = new UserHouseholdMember
        {
            MemberId = Guid.NewGuid(),
            UserId = userId,
            FullName = dto.FullName.Trim(),
            CitizenId = dto.CitizenId?.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Relationship = dto.Relationship.Trim().ToUpperInvariant(),
            Occupation = dto.Occupation?.Trim(),
            MonthlyIncome = dto.MonthlyIncome,
            IsDependent = dto.IsDependent,
            DependentReason = dto.DependentReason?.Trim().ToUpperInvariant(),
            HasMeritService = dto.HasMeritService,
            MeritDetails = dto.MeritDetails?.Trim(),
            Note = dto.Note?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.UserHouseholdMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Added household member {MemberId} for user {UserId}", member.MemberId, userId);
        return MapToHouseholdMemberResponse(member);
    }

    public async Task<UserHouseholdMemberResponseDto> UpdateHouseholdMemberAsync(
        Guid userId,
        Guid memberId,
        UserHouseholdMemberRequestDto dto,
        CancellationToken ct = default)
    {
        var member = await _db.UserHouseholdMembers
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.UserId == userId, ct);

        if (member == null)
            throw new KeyNotFoundException("Không tìm thấy thành viên trong sổ hộ khẩu.");

        if (!string.IsNullOrWhiteSpace(dto.CitizenId))
        {
            var trimmedCid = dto.CitizenId.Trim();
            var duplicate = await _db.UserHouseholdMembers
                .AnyAsync(m => m.UserId == userId && m.MemberId != memberId && m.CitizenId == trimmedCid, ct);

            if (duplicate)
                throw new InvalidOperationException($"Số CCCD {trimmedCid} đã được sử dụng bởi thành viên khác trong hộ gia đình.");
            member.CitizenId = trimmedCid;
        }
        else
        {
            member.CitizenId = null;
        }

        member.FullName = dto.FullName.Trim();
        member.DateOfBirth = dto.DateOfBirth;
        member.Relationship = dto.Relationship.Trim().ToUpperInvariant();
        member.Occupation = dto.Occupation?.Trim();
        member.MonthlyIncome = dto.MonthlyIncome;
        member.IsDependent = dto.IsDependent;
        member.DependentReason = dto.DependentReason?.Trim().ToUpperInvariant();
        member.HasMeritService = dto.HasMeritService;
        member.MeritDetails = dto.MeritDetails?.Trim();
        member.Note = dto.Note?.Trim();
        member.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated household member {MemberId} for user {UserId}", memberId, userId);

        return MapToHouseholdMemberResponse(member);
    }

    public async Task<bool> DeleteHouseholdMemberAsync(Guid userId, Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.UserHouseholdMembers
            .FirstOrDefaultAsync(m => m.MemberId == memberId && m.UserId == userId, ct);

        if (member == null)
            return false;

        _db.UserHouseholdMembers.Remove(member);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted household member {MemberId} for user {UserId}", memberId, userId);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // 3. User Document Vault
    // ─────────────────────────────────────────────────────────────

    public async Task<List<UserDocumentResponseDto>> GetDocumentsAsync(Guid userId, CancellationToken ct = default)
    {
        var docs = await _db.UserDocuments
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);

        return docs.Select(MapToUserDocumentResponse).ToList();
    }

    public async Task<UserDocumentResponseDto> UploadDocumentAsync(
        Guid userId,
        UploadUserDocumentRequestDto dto,
        CancellationToken ct = default)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists)
            throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");

        var docType = dto.DocumentType.Trim().ToUpperInvariant();
        if (!DocumentTypeConstants.IsAllowedProfileDocumentType(docType))
        {
            throw new ArgumentException($"Loại giấy tờ '{dto.DocumentType}' không hợp lệ hoặc không được hỗ trợ.");
        }

        if (dto.File == null || dto.File.Length == 0)
        {
            throw new ArgumentException("File tài liệu không được để trống.");
        }

        if (dto.File.Length > 10 * 1024 * 1024)
        {
            throw new ArgumentException("Dung lượng file tối đa là 10MB.");
        }

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        string fileUrl;

        // Hỗ trợ cả file PDF và file ảnh (JPG/PNG) cho hồ sơ cá nhân
        if (ext == ".pdf")
        {
            fileUrl = await _fileStorageService.UploadPdfAsync(dto.File, "citizen-vault");
        }
        else if (new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
        {
            fileUrl = await _fileStorageService.UploadImageAsync(dto.File, "citizen-vault");
        }
        else
        {
            throw new ArgumentException("Hệ thống chỉ chấp nhận file định dạng PDF hoặc hình ảnh (JPG, PNG, WEBP).");
        }

        // Kiểm tra xem đã có document cùng loại trong kho chưa -> nếu có thì cập nhật URL mới
        var existingDoc = await _db.UserDocuments
            .FirstOrDefaultAsync(d => d.UserId == userId && d.DocumentType == docType, ct);

        if (existingDoc != null)
        {
            existingDoc.FileName = dto.File.FileName;
            existingDoc.FileUrl = fileUrl;
            existingDoc.FileSizeBytes = dto.File.Length;
            existingDoc.Description = dto.Description?.Trim();
            existingDoc.VerificationStatus = "PENDING";
            existingDoc.UploadedAt = DateTime.UtcNow;
            existingDoc.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Updated existing document {DocId} ({Type}) in vault for user {UserId}", existingDoc.DocumentId, docType, userId);
            return MapToUserDocumentResponse(existingDoc);
        }

        var newDoc = new UserDocument
        {
            DocumentId = Guid.NewGuid(),
            UserId = userId,
            DocumentType = docType,
            FileName = dto.File.FileName,
            FileUrl = fileUrl,
            FileSizeBytes = dto.File.Length,
            Description = dto.Description?.Trim(),
            VerificationStatus = "PENDING",
            UploadedAt = DateTime.UtcNow
        };

        _db.UserDocuments.Add(newDoc);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Uploaded new document {DocId} ({Type}) to vault for user {UserId}", newDoc.DocumentId, docType, userId);
        return MapToUserDocumentResponse(newDoc);
    }

    public async Task<bool> DeleteDocumentAsync(Guid userId, Guid documentId, CancellationToken ct = default)
    {
        var doc = await _db.UserDocuments
            .FirstOrDefaultAsync(d => d.DocumentId == documentId && d.UserId == userId, ct);

        if (doc == null)
            return false;

        // Thử xóa file vật lý trên Cloudinary nếu là ảnh
        try
        {
            if (!doc.FileUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                await _fileStorageService.DeleteImageAsync(doc.FileUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete physical file from storage: {Url}", doc.FileUrl);
        }

        _db.UserDocuments.Remove(doc);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted document {DocId} from vault for user {UserId}", documentId, userId);
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // Helper mapping methods
    // ─────────────────────────────────────────────────────────────

    private static UserHouseholdMemberResponseDto MapToHouseholdMemberResponse(UserHouseholdMember m)
    {
        return new UserHouseholdMemberResponseDto
        {
            MemberId = m.MemberId,
            UserId = m.UserId,
            FullName = m.FullName,
            CitizenId = m.CitizenId,
            DateOfBirth = m.DateOfBirth,
            Relationship = m.Relationship,
            Occupation = m.Occupation,
            MonthlyIncome = m.MonthlyIncome,
            IsDependent = m.IsDependent,
            DependentReason = m.DependentReason,
            DependentReasonLabel = DependentReasonConstants.GetLabel(m.DependentReason),
            HasMeritService = m.HasMeritService,
            MeritDetails = m.MeritDetails,
            Note = m.Note,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt
        };
    }

    private static UserDocumentResponseDto MapToUserDocumentResponse(UserDocument d)
    {
        return new UserDocumentResponseDto
        {
            DocumentId = d.DocumentId,
            UserId = d.UserId,
            DocumentType = d.DocumentType,
            DocumentTypeLabel = DocumentTypeConstants.GetLabel(d.DocumentType),
            FileName = d.FileName,
            FileUrl = d.FileUrl,
            FileSizeBytes = d.FileSizeBytes,
            Description = d.Description,
            VerificationStatus = d.VerificationStatus,
            UploadedAt = d.UploadedAt
        };
    }
}
