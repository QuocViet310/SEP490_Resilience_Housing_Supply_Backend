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

        return MapFullProfile(user);
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

        // Khóa định danh khi đã có CCCD (đồng bộ rule với UpdateProfile — không phụ thuộc cờ IsEkycVerified)
        var identityLocked = !string.IsNullOrWhiteSpace(user.CitizenId);
        if (identityLocked)
        {
            if (!string.IsNullOrWhiteSpace(dto.FullName) && dto.FullName.Trim() != user.FullName)
                throw new InvalidOperationException("Tài khoản đã xác thực CCCD. Không thể thay đổi họ tên.");

            if (dto.DateOfBirth.HasValue
                && user.DateOfBirth.HasValue
                && dto.DateOfBirth.Value.Date != user.DateOfBirth.Value.Date)
                throw new InvalidOperationException("Tài khoản đã xác thực CCCD. Không thể thay đổi ngày sinh.");

            if (!string.IsNullOrWhiteSpace(dto.CitizenId) && dto.CitizenId.Trim() != user.CitizenId)
                throw new InvalidOperationException("Tài khoản đã xác thực CCCD. Không thể thay đổi số CCCD.");

            if (!string.IsNullOrWhiteSpace(dto.Gender)
                && !string.IsNullOrWhiteSpace(user.Gender)
                && !string.Equals(dto.Gender.Trim(), user.Gender, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tài khoản đã xác thực CCCD. Không thể thay đổi giới tính.");

            if (!string.IsNullOrWhiteSpace(dto.Nationality)
                && !string.IsNullOrWhiteSpace(user.Nationality)
                && !string.Equals(dto.Nationality.Trim(), user.Nationality, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tài khoản đã xác thực CCCD. Không thể thay đổi quốc tịch.");

            if (!string.IsNullOrWhiteSpace(dto.PlaceOfOrigin)
                && !string.IsNullOrWhiteSpace(user.PlaceOfOrigin)
                && !string.Equals(dto.PlaceOfOrigin.Trim(), user.PlaceOfOrigin, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Tài khoản đã xác thực CCCD. Không thể thay đổi quê quán.");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName.Trim();

            if (dto.DateOfBirth.HasValue)
                user.DateOfBirth = dto.DateOfBirth.Value;

            if (!string.IsNullOrWhiteSpace(dto.CitizenId))
            {
                var trimmedCid = dto.CitizenId.Trim();
                var exists = await _db.Users.AnyAsync(
                    u => u.CitizenId == trimmedCid && u.Id != userId && u.Status == "Active", ct);
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

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            user.PhoneNumber = dto.PhoneNumber.Trim();

        // Hôn nhân
        if (!string.IsNullOrWhiteSpace(dto.MaritalStatus))
        {
            var marital = dto.MaritalStatus.Trim().ToUpperInvariant();
            if (!MaritalStatusConstants.IsValid(marital))
                throw new InvalidOperationException(
                    $"Tình trạng hôn nhân '{dto.MaritalStatus}' không hợp lệ. " +
                    $"Giá trị cho phép: {string.Join(", ", MaritalStatusConstants.AllValues)}");
            user.MaritalStatus = marital;
        }

        ApplySpouseFields(user, dto);

        if (dto.Occupation != null) user.Occupation = dto.Occupation.Trim();
        if (dto.WorkPlace != null) user.WorkPlace = dto.WorkPlace.Trim();
        if (dto.CurrentResidence != null)
        {
            user.CurrentResidence = dto.CurrentResidence.Trim();
            user.Address = dto.CurrentResidence.Trim();
        }
        if (dto.PermanentAddress != null) user.PermanentAddress = dto.PermanentAddress.Trim();
        if (dto.MonthlyIncome.HasValue) user.MonthlyIncome = dto.MonthlyIncome.Value;

        if (!string.IsNullOrWhiteSpace(dto.HousingStatus))
        {
            var housing = dto.HousingStatus.Trim().ToUpperInvariant();
            if (!HousingStatusConstants.IsValid(housing))
                throw new InvalidOperationException(
                    $"Thực trạng nhà ở '{dto.HousingStatus}' không hợp lệ. " +
                    $"Giá trị cho phép: {string.Join(", ", HousingStatusConstants.AllValues)}");
            user.HousingStatus = housing;
        }

        if (dto.AverageHousingAreaPerPerson.HasValue)
            user.AverageHousingAreaPerPerson = dto.AverageHousingAreaPerPerson.Value;

        if (user.HousingStatus == HousingStatusConstants.SmallHouse
            && !user.AverageHousingAreaPerPerson.HasValue)
        {
            throw new InvalidOperationException(
                "Khi khai nhà chật hẹp (SMALL_HOUSE) bắt buộc nhập diện tích bình quân đầu người (m²).");
        }

        if (!string.IsNullOrWhiteSpace(dto.PriorityGroup))
        {
            var pg = dto.PriorityGroup.Trim().ToUpperInvariant();
            if (!PriorityGroupConstants.IsValid(pg))
                throw new InvalidOperationException($"Nhóm đối tượng '{dto.PriorityGroup}' không hợp lệ.");
            user.PriorityGroup = pg;
        }

        await SyncSpouseHouseholdMemberAsync(user, ct);

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
            IsEkycVerified = IsEffectivelyEkycVerified(user),

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
        var user = await _db.Users
            .Include(u => u.UserHouseholdMembers)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");

        NormalizeAndValidateMember(dto, user);

        if (dto.Relationship.Equals(HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase)
            && user.UserHouseholdMembers.Any(m =>
                m.Relationship.Equals(HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Hộ gia đình đã có vợ/chồng. Không thể thêm thêm thành viên SPOUSE.");
        }

        if (!string.IsNullOrWhiteSpace(dto.CitizenId))
        {
            var trimmedCid = dto.CitizenId.Trim();
            if (user.UserHouseholdMembers.Any(m => m.CitizenId == trimmedCid))
                throw new InvalidOperationException($"Số CCCD {trimmedCid} đã tồn tại trong danh sách hộ gia đình của bạn.");
        }

        ApplyDependentRules(dto);

        var member = new UserHouseholdMember
        {
            MemberId = Guid.NewGuid(),
            UserId = userId,
            FullName = dto.FullName.Trim(),
            CitizenId = string.IsNullOrWhiteSpace(dto.CitizenId) ? null : dto.CitizenId.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Relationship = dto.Relationship.Trim().ToUpperInvariant(),
            Occupation = dto.Occupation?.Trim(),
            MonthlyIncome = dto.IsDependent ? null : dto.MonthlyIncome,
            IsDependent = dto.IsDependent,
            DependentReason = dto.IsDependent ? dto.DependentReason?.Trim().ToUpperInvariant() : null,
            HasMeritService = dto.HasMeritService,
            MeritDetails = dto.MeritDetails?.Trim(),
            Note = dto.Note?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.UserHouseholdMembers.Add(member);

        if (member.Relationship == HouseholdRelationshipConstants.Spouse)
        {
            user.MaritalStatus = MaritalStatusConstants.Married;
            user.SpouseFullName = member.FullName;
            user.SpouseCitizenId = member.CitizenId;
            user.SpouseDateOfBirth = member.DateOfBirth;
            user.SpouseMonthlyIncome = member.MonthlyIncome;
            user.UpdatedAt = DateTime.UtcNow;
        }

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
        var user = await _db.Users
            .Include(u => u.UserHouseholdMembers)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin tài khoản người dùng.");

        var member = user.UserHouseholdMembers.FirstOrDefault(m => m.MemberId == memberId)
            ?? throw new KeyNotFoundException("Không tìm thấy thành viên trong sổ hộ khẩu.");

        NormalizeAndValidateMember(dto, user);

        if (dto.Relationship.Equals(HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase)
            && user.UserHouseholdMembers.Any(m =>
                m.MemberId != memberId
                && m.Relationship.Equals(HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Hộ gia đình đã có vợ/chồng. Không thể chuyển thành viên khác thành SPOUSE.");
        }

        if (!string.IsNullOrWhiteSpace(dto.CitizenId))
        {
            var trimmedCid = dto.CitizenId.Trim();
            if (user.UserHouseholdMembers.Any(m => m.MemberId != memberId && m.CitizenId == trimmedCid))
                throw new InvalidOperationException($"Số CCCD {trimmedCid} đã được sử dụng bởi thành viên khác trong hộ gia đình.");
            member.CitizenId = trimmedCid;
        }
        else
        {
            member.CitizenId = null;
        }

        ApplyDependentRules(dto);

        member.FullName = dto.FullName.Trim();
        member.DateOfBirth = dto.DateOfBirth;
        member.Relationship = dto.Relationship.Trim().ToUpperInvariant();
        member.Occupation = dto.Occupation?.Trim();
        member.MonthlyIncome = dto.IsDependent ? null : dto.MonthlyIncome;
        member.IsDependent = dto.IsDependent;
        member.DependentReason = dto.IsDependent ? dto.DependentReason?.Trim().ToUpperInvariant() : null;
        member.HasMeritService = dto.HasMeritService;
        member.MeritDetails = dto.MeritDetails?.Trim();
        member.Note = dto.Note?.Trim();
        member.UpdatedAt = DateTime.UtcNow;

        if (member.Relationship == HouseholdRelationshipConstants.Spouse)
        {
            user.MaritalStatus = MaritalStatusConstants.Married;
            user.SpouseFullName = member.FullName;
            user.SpouseCitizenId = member.CitizenId;
            user.SpouseDateOfBirth = member.DateOfBirth;
            user.SpouseMonthlyIncome = member.MonthlyIncome;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated household member {MemberId} for user {UserId}", memberId, userId);

        return MapToHouseholdMemberResponse(member);
    }

    public async Task<bool> DeleteHouseholdMemberAsync(Guid userId, Guid memberId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.UserHouseholdMembers)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return false;

        var member = user.UserHouseholdMembers.FirstOrDefault(m => m.MemberId == memberId);
        if (member == null)
            return false;

        var wasSpouse = member.Relationship.Equals(
            HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase);

        _db.UserHouseholdMembers.Remove(member);

        if (wasSpouse)
        {
            user.SpouseFullName = null;
            user.SpouseCitizenId = null;
            user.SpouseDateOfBirth = null;
            user.SpouseMonthlyIncome = null;
            user.UpdatedAt = DateTime.UtcNow;
        }

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
            throw new ArgumentException($"Loại giấy tờ '{dto.DocumentType}' không hợp lệ hoặc không được hỗ trợ.");

        if (dto.File == null || dto.File.Length == 0)
            throw new ArgumentException("File tài liệu không được để trống.");

        if (dto.File.Length > 10 * 1024 * 1024)
            throw new ArgumentException("Dung lượng file tối đa là 10MB.");

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        string fileUrl;

        if (ext == ".pdf")
            fileUrl = await _fileStorageService.UploadPdfAsync(dto.File, "citizen-vault");
        else if (new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(ext))
            fileUrl = await _fileStorageService.UploadImageAsync(dto.File, "citizen-vault");
        else
            throw new ArgumentException("Hệ thống chỉ chấp nhận file định dạng PDF hoặc hình ảnh (JPG, PNG, WEBP).");

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

        try
        {
            if (!doc.FileUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                await _fileStorageService.DeleteImageAsync(doc.FileUrl);
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
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private static bool IsEffectivelyEkycVerified(User user) =>
        user.IsEkycVerified || !string.IsNullOrWhiteSpace(user.CitizenId);

    private static CitizenFullProfileDto MapFullProfile(User user)
    {
        var householdMembers = user.UserHouseholdMembers
            .OrderBy(m => m.CreatedAt)
            .Select(MapToHouseholdMemberResponse)
            .ToList();

        var documents = user.UserDocuments
            .OrderByDescending(d => d.UploadedAt)
            .Select(MapToUserDocumentResponse)
            .ToList();

        var dependentCount = householdMembers.Count(m => m.IsDependent);
        var countableIncome = ComputeCountableIncome(user, user.UserHouseholdMembers);
        var requiredDocs = DocumentTypeConstants.GetRequiredTypesForCitizenProfile(
            user.MaritalStatus,
            user.HousingStatus,
            dependentCount > 0);
        var uploadedTypes = documents.Select(d => d.DocumentType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingDocs = requiredDocs.Where(t => !uploadedTypes.Contains(t)).ToList();

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

            IsEkycVerified = IsEffectivelyEkycVerified(user),
            EkycVerifiedAt = user.EkycVerifiedAt,
            Gender = user.Gender,
            Nationality = user.Nationality,
            PlaceOfOrigin = user.PlaceOfOrigin,
            IdIssueDate = user.IdIssueDate,
            IdIssuePlace = user.IdIssuePlace,

            MaritalStatus = user.MaritalStatus,
            MaritalStatusLabel = MaritalStatusConstants.GetLabel(user.MaritalStatus),
            SpouseFullName = user.SpouseFullName,
            SpouseCitizenId = user.SpouseCitizenId,
            SpouseDateOfBirth = user.SpouseDateOfBirth,
            SpouseMonthlyIncome = user.SpouseMonthlyIncome,

            Occupation = user.Occupation,
            WorkPlace = user.WorkPlace,
            CurrentResidence = user.CurrentResidence ?? user.Address,
            PermanentAddress = user.PermanentAddress,
            MonthlyIncome = user.MonthlyIncome,

            HousingStatus = user.HousingStatus,
            AverageHousingAreaPerPerson = user.AverageHousingAreaPerPerson,
            PriorityGroup = user.PriorityGroup,
            PriorityGroupLabel = !string.IsNullOrWhiteSpace(user.PriorityGroup)
                && PriorityGroupConstants.Labels.TryGetValue(user.PriorityGroup, out var pLabel)
                    ? pLabel
                    : user.PriorityGroup,

            HouseholdMembersCount = 1 + householdMembers.Count,
            DependentMembersCount = dependentCount,
            CountableHouseholdIncome = countableIncome,
            HouseholdMembers = householdMembers,
            Documents = documents,
            RequiredDocumentTypes = requiredDocs,
            MissingDocumentTypes = missingDocs,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static decimal ComputeCountableIncome(User user, IEnumerable<UserHouseholdMember> members)
    {
        decimal total = user.MonthlyIncome ?? 0m;

        var hasSpouseMember = members.Any(m =>
            m.Relationship.Equals(HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase));

        if (user.MaritalStatus == MaritalStatusConstants.Married && !hasSpouseMember)
            total += user.SpouseMonthlyIncome ?? 0m;

        foreach (var m in members)
        {
            if (m.IsDependent) continue;
            total += m.MonthlyIncome ?? 0m;
        }

        return total;
    }

    private static void ApplySpouseFields(User user, UpdateCitizenProfileDto dto)
    {
        var marital = user.MaritalStatus?.ToUpperInvariant();

        if (marital == MaritalStatusConstants.Married)
        {
            if (dto.SpouseFullName != null) user.SpouseFullName = dto.SpouseFullName.Trim();
            if (dto.SpouseCitizenId != null) user.SpouseCitizenId = dto.SpouseCitizenId.Trim();
            if (dto.SpouseDateOfBirth.HasValue) user.SpouseDateOfBirth = dto.SpouseDateOfBirth;
            if (dto.SpouseMonthlyIncome.HasValue) user.SpouseMonthlyIncome = dto.SpouseMonthlyIncome;

            if (string.IsNullOrWhiteSpace(user.SpouseFullName))
                throw new InvalidOperationException("Khi đã kết hôn bắt buộc khai họ tên vợ/chồng.");
        }
        else if (marital is MaritalStatusConstants.Single or MaritalStatusConstants.Divorced)
        {
            user.SpouseFullName = null;
            user.SpouseCitizenId = null;
            user.SpouseDateOfBirth = null;
            user.SpouseMonthlyIncome = null;
        }
        else
        {
            // Chưa đổi marital — vẫn cho cập nhật spouse nếu gửi
            if (dto.SpouseFullName != null) user.SpouseFullName = dto.SpouseFullName.Trim();
            if (dto.SpouseCitizenId != null) user.SpouseCitizenId = dto.SpouseCitizenId.Trim();
            if (dto.SpouseDateOfBirth.HasValue) user.SpouseDateOfBirth = dto.SpouseDateOfBirth;
            if (dto.SpouseMonthlyIncome.HasValue) user.SpouseMonthlyIncome = dto.SpouseMonthlyIncome;
        }
    }

    private async Task SyncSpouseHouseholdMemberAsync(User user, CancellationToken ct)
    {
        var spouses = user.UserHouseholdMembers
            .Where(m => m.Relationship.Equals(HouseholdRelationshipConstants.Spouse, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (user.MaritalStatus == MaritalStatusConstants.Married)
        {
            if (string.IsNullOrWhiteSpace(user.SpouseFullName))
                return;

            var spouse = spouses.FirstOrDefault();
            if (spouse == null)
            {
                spouse = new UserHouseholdMember
                {
                    MemberId = Guid.NewGuid(),
                    UserId = user.Id,
                    Relationship = HouseholdRelationshipConstants.Spouse,
                    CreatedAt = DateTime.UtcNow
                };
                user.UserHouseholdMembers.Add(spouse);
                _db.UserHouseholdMembers.Add(spouse);
            }
            else
            {
                // Chỉ giữ 1 SPOUSE
                foreach (var extra in spouses.Skip(1))
                    _db.UserHouseholdMembers.Remove(extra);
            }

            spouse.FullName = user.SpouseFullName!;
            spouse.CitizenId = user.SpouseCitizenId;
            spouse.DateOfBirth = user.SpouseDateOfBirth;
            spouse.MonthlyIncome = user.SpouseMonthlyIncome;
            spouse.IsDependent = false;
            spouse.DependentReason = null;
            spouse.UpdatedAt = DateTime.UtcNow;
        }
        else if (user.MaritalStatus is MaritalStatusConstants.Single or MaritalStatusConstants.Divorced)
        {
            foreach (var spouse in spouses)
                _db.UserHouseholdMembers.Remove(spouse);
        }

        await Task.CompletedTask;
    }

    private static void NormalizeAndValidateMember(UserHouseholdMemberRequestDto dto, User user)
    {
        if (!HouseholdRelationshipConstants.IsValid(dto.Relationship))
        {
            throw new InvalidOperationException(
                $"Quan hệ '{dto.Relationship}' không hợp lệ. " +
                $"Giá trị cho phép: {string.Join(", ", HouseholdRelationshipConstants.AllValues)}");
        }

        if (!string.IsNullOrWhiteSpace(dto.CitizenId)
            && !string.IsNullOrWhiteSpace(user.CitizenId)
            && dto.CitizenId.Trim() == user.CitizenId.Trim())
        {
            throw new InvalidOperationException("Số CCCD thành viên không được trùng với CCCD chủ tài khoản.");
        }

        if (dto.DateOfBirth.HasValue)
        {
            var age = GetAge(dto.DateOfBirth.Value);
            if (age >= 14 && string.IsNullOrWhiteSpace(dto.CitizenId))
            {
                throw new InvalidOperationException(
                    $"Thành viên '{dto.FullName}' từ 14 tuổi trở lên bắt buộc phải có số CCCD.");
            }
        }
    }

    private static void ApplyDependentRules(UserHouseholdMemberRequestDto dto)
    {
        if (dto.DateOfBirth.HasValue)
        {
            var age = GetAge(dto.DateOfBirth.Value);
            if (age < 18)
            {
                dto.IsDependent = true;
                dto.DependentReason = DependentReasonConstants.Under18;
                dto.MonthlyIncome = null;
            }
        }

        if (dto.IsDependent)
        {
            if (string.IsNullOrWhiteSpace(dto.DependentReason)
                || !DependentReasonConstants.IsValid(dto.DependentReason))
            {
                throw new InvalidOperationException(
                    "Người phụ thuộc bắt buộc có lý do hợp lệ: UNDER_18, STUDENT, DISABLED, ELDERLY, OTHER.");
            }

            dto.DependentReason = dto.DependentReason.Trim().ToUpperInvariant();
            dto.MonthlyIncome = null;
        }
        else
        {
            dto.DependentReason = null;
        }
    }

    private static int GetAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dateOfBirth.Date.Year;
        if (dateOfBirth.Date > today.AddYears(-age))
            age--;
        return age;
    }

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
