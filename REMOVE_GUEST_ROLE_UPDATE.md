# Remove Guest Role - Update Summary

## 📋 Tổng Quan

Guest role đã được loại bỏ khỏi hệ thống vì Guest không cần đăng nhập và không cần được lưu trong database.

---

## ✅ Thay Đổi Đã Thực Hiện

### 1. **RoleConstants.cs - Updated**
**File:** `RHS.Domain/Constants/RoleConstants.cs`

**Thay đổi:**
- ❌ Removed: `Guest` constant
- ❌ Removed: `GuestId` GUID
- ✅ Updated: `GetAllRoles()` - Chỉ trả về 3 roles
- ✅ Updated: `GetRoleId()` - Loại bỏ Guest case

**Roles còn lại:**
1. **Applicant** - Người nộp đơn xin nhà ở xã hội (default role)
2. **Housing Authority Officer** - Cán bộ quản lý nhà ở
3. **System Administrator** - Quản trị viên hệ thống

---

### 2. **AppDbContext.cs - Updated**
**File:** `RHS.Infrastructure/Data/AppDbContext.cs`

**Thay đổi:**
- ✅ Updated: `SeedRoles()` method
- ❌ Removed: Guest role seed data
- ✅ Kept: 3 roles (Applicant, Officer, Administrator)

**Seed Data:**
```csharp
new Role { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), RoleName = "Applicant" }
new Role { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), RoleName = "Housing Authority Officer" }
new Role { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), RoleName = "System Administrator" }
```

---

### 3. **Database Migration - Created**
**Migration:** `20260522061635_RemoveGuestRole`

**SQL Operation:**
```sql
DELETE FROM [Roles]
WHERE [Id] = '11111111-1111-1111-1111-111111111111';
```

**Status:** ✅ Applied successfully

---

## 🔄 Impact Analysis

### ✅ No Breaking Changes
- Guest users không bao giờ được lưu trong database
- Không có users nào có RoleId = Guest
- Không ảnh hưởng đến existing users
- Không ảnh hưởng đến APIs

### 📊 Database State
**Before:**
```
Roles Table:
- Guest (11111111-1111-1111-1111-111111111111)
- Applicant (22222222-2222-2222-2222-222222222222)
- Housing Authority Officer (33333333-3333-3333-3333-333333333333)
- System Administrator (44444444-4444-4444-4444-444444444444)
```

**After:**
```
Roles Table:
- Applicant (22222222-2222-2222-2222-222222222222)
- Housing Authority Officer (33333333-3333-3333-3333-333333333333)
- System Administrator (44444444-4444-4444-4444-444444444444)
```

---

## 🎯 Rationale (Lý Do)

### Tại Sao Loại Bỏ Guest Role?

1. **Guest không cần authentication**
   - Guest users không đăng nhập
   - Không có JWT token
   - Không có user record trong database

2. **Guest không cần authorization**
   - Guest chỉ xem public content
   - Không cần role-based access control
   - Không cần được track trong database

3. **Simplified architecture**
   - Giảm complexity
   - Rõ ràng hơn: Có account = có role, không có account = guest
   - Dễ maintain hơn

4. **Best practice**
   - Guest thường được handle ở application level, không phải database level
   - Chỉ authenticated users mới cần roles

---

## 📝 User Roles Explained

### 1. Applicant (Default Role)
**Mô tả:** Người nộp đơn xin nhà ở xã hội

**Permissions:**
- ✅ Xem danh sách dự án nhà ở
- ✅ Nộp đơn đăng ký
- ✅ Upload tài liệu hỗ trợ
- ✅ Theo dõi trạng thái đơn
- ✅ Quản lý profile cá nhân
- ❌ Không thể duyệt đơn của người khác
- ❌ Không thể quản lý hệ thống

**Assigned to:** Tất cả users đăng ký mới

---

### 2. Housing Authority Officer
**Mô tả:** Cán bộ quản lý nhà ở

**Permissions:**
- ✅ Tất cả permissions của Applicant
- ✅ Xem danh sách đơn đăng ký
- ✅ Duyệt/từ chối đơn
- ✅ Xác minh tài liệu
- ✅ Phân bổ nhà ở
- ✅ Quản lý dự án nhà ở
- ❌ Không thể quản lý users
- ❌ Không thể thay đổi system settings

**Assigned to:** Cán bộ chính quyền

---

### 3. System Administrator
**Mô tả:** Quản trị viên hệ thống

**Permissions:**
- ✅ Full access to all features
- ✅ Quản lý users và roles
- ✅ Xem audit logs
- ✅ Cấu hình hệ thống
- ✅ Quản lý dữ liệu
- ✅ Backup/restore

**Assigned to:** IT administrators

---

## 🔐 Authorization Examples

### Public Endpoints (No Authentication)
```csharp
// Không cần [Authorize] attribute
[HttpGet("public/projects")]
public IActionResult GetPublicProjects()
{
    // Guest users có thể access
    return Ok(projects);
}
```

### Authenticated Endpoints (Any Role)
```csharp
[Authorize] // Bất kỳ authenticated user nào
[HttpGet("profile")]
public IActionResult GetProfile()
{
    // Applicant, Officer, Admin đều access được
    return Ok(profile);
}
```

### Role-Specific Endpoints
```csharp
[Authorize(Roles = "Housing Authority Officer")]
[HttpPost("applications/{id}/approve")]
public IActionResult ApproveApplication(Guid id)
{
    // Chỉ Officer mới access được
    return Ok();
}

[Authorize(Roles = "System Administrator")]
[HttpGet("admin/users")]
public IActionResult GetAllUsers()
{
    // Chỉ Admin mới access được
    return Ok(users);
}
```

---

## 🧪 Testing

### Verify Roles in Database
```sql
-- Check roles table
SELECT * FROM Roles;

-- Expected result: 3 roles
-- Applicant
-- Housing Authority Officer
-- System Administrator

-- Check users and their roles
SELECT u.Email, r.RoleName 
FROM Users u
INNER JOIN Roles r ON u.RoleId = r.Id;
```

### Test Registration (Default Role)
```bash
POST /api/auth/register
{
  "email": "test@example.com",
  "password": "Test123!",
  "fullName": "Test User"
}

# Expected: User được assign role "Applicant" (default)
```

### Test Role-Based Authorization
```bash
# Test as Applicant
GET /api/users/profile
Authorization: Bearer {applicant_token}
# Expected: 200 OK

GET /api/users/officer-only
Authorization: Bearer {applicant_token}
# Expected: 403 Forbidden

# Test as Officer
GET /api/users/officer-only
Authorization: Bearer {officer_token}
# Expected: 200 OK
```

---

## 📁 Files Modified

1. `RHS.Domain/Constants/RoleConstants.cs` - Removed Guest constants
2. `RHS.Infrastructure/Data/AppDbContext.cs` - Updated seed data
3. `RHS.Infrastructure/Migrations/20260522061635_RemoveGuestRole.cs` - New migration

---

## ✅ Checklist

- [x] Remove Guest from RoleConstants
- [x] Update seed data in AppDbContext
- [x] Create migration
- [x] Apply migration to database
- [x] Verify Guest role removed from database
- [x] Build successful
- [x] No breaking changes
- [x] Documentation updated

---

## 🚀 Next Steps

1. ✅ Migration applied - No action needed
2. ⚠️ Update documentation if Guest is mentioned elsewhere
3. ⚠️ Inform Frontend team about role changes
4. ⚠️ Update API documentation

---

## 📞 Summary

**Change:** Removed Guest role from database  
**Reason:** Guest users don't need authentication/authorization  
**Impact:** None (no breaking changes)  
**Status:** ✅ Complete  
**Migration:** ✅ Applied  

**Current Roles:** 3 (Applicant, Housing Authority Officer, System Administrator)  
**Default Role:** Applicant (for new registrations)

---

*Last Updated: May 22, 2026*

