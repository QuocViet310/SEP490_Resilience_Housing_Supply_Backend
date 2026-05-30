# Admin Staff Management - Quick Testing Guide

## Before Running the API

1. **Apply Database Migrations**
   ```bash
   cd RHS.Infrastructure
   dotnet ef database update
   ```
   This will add the new Ward Manager and Verification Officer roles to the database.

2. **Start the API**
   ```bash
   cd RHS.API
   dotnet run
   ```
   API will run on `https://localhost:5001` (or as configured)

## Testing the API

### 1. Get JWT Token (Admin Login)
First, you need to login as System Administrator to get a JWT token.

**Request:**
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@example.com",
  "password": "AdminPassword123"
}
```

**Response:** (Copy the `accessToken` value for subsequent requests)
```json
{
  "success": true,
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "userId": "44444444-4444-4444-4444-444444444444",
  "email": "admin@example.com",
  "roleName": "System Administrator"
}
```

### 2. Create Ward Manager Account

**Request:**
```
POST /api/admin/create-staff
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "email": "ward.manager.01@example.com",
  "fullName": "Nguyễn Văn Manager",
  "phoneNumber": "0912345670",
  "citizenId": "0123456789",
  "dateOfBirth": "1995-01-15",
  "address": "123 Nguyễn Huệ, Quận 1, TP HCM",
  "role": "Ward Manager",
  "temporaryPassword": "TempPassword@123456"
}
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "email": "ward.manager.01@example.com",
  "fullName": "Nguyễn Văn Manager",
  "phoneNumber": "0912345670",
  "citizenId": "0123456789",
  "dateOfBirth": "1995-01-15T00:00:00Z",
  "address": "123 Nguyễn Huệ, Quận 1, TP HCM",
  "roleName": "Ward Manager",
  "status": "Active",
  "createdAt": "2026-05-30T10:30:00Z",
  "message": "Tài khoản cán bộ Ward Manager đã được tạo thành công. Email: ward.manager.01@example.com"
}
```

### 3. Create Verification Officer Account

**Request:**
```
POST /api/admin/create-staff
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "email": "officer.verify.01@example.com",
  "fullName": "Trần Thị Officer",
  "phoneNumber": "0912345671",
  "citizenId": "9876543210",
  "dateOfBirth": "1992-06-20",
  "address": "456 Lê Lợi, Quận 2, TP HCM",
  "role": "Verification Officer",
  "temporaryPassword": "VerifyPassword@654321"
}
```

### 4. Get All Staff Members

**Request:**
```
GET /api/admin/staff-list?pageNumber=1&pageSize=10
Authorization: Bearer {accessToken}
```

**Response:**
```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "email": "ward.manager.01@example.com",
      "fullName": "Nguyễn Văn Manager",
      "phoneNumber": "0912345670",
      "citizenId": "0123456789",
      "dateOfBirth": "1995-01-15T00:00:00Z",
      "address": "123 Nguyễn Huệ, Quận 1, TP HCM",
      "roleName": "Ward Manager",
      "status": "Active",
      "createdAt": "2026-05-30T10:30:00Z"
    },
    {
      "id": "550e8400-e29b-41d4-a716-446655440002",
      "email": "officer.verify.01@example.com",
      "fullName": "Trần Thị Officer",
      "phoneNumber": "0912345671",
      "citizenId": "9876543210",
      "dateOfBirth": "1992-06-20T00:00:00Z",
      "address": "456 Lê Lợi, Quận 2, TP HCM",
      "roleName": "Verification Officer",
      "status": "Active",
      "createdAt": "2026-05-30T10:30:30Z"
    }
  ],
  "totalCount": 2,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

### 5. Filter Staff by Role

**Request:**
```
GET /api/admin/staff-list?pageNumber=1&pageSize=10&role=Ward Manager
Authorization: Bearer {accessToken}
```

### 6. Search Staff by Name

**Request:**
```
GET /api/admin/staff-list?pageNumber=1&pageSize=10&searchTerm=Nguyễn
Authorization: Bearer {accessToken}
```

### 7. Get Staff Details

**Request:**
```
GET /api/admin/staff/550e8400-e29b-41d4-a716-446655440001
Authorization: Bearer {accessToken}
```

### 8. Update Staff Information

**Request:**
```
PUT /api/admin/staff/550e8400-e29b-41d4-a716-446655440001
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "fullName": "Nguyễn Văn Manager Updated",
  "phoneNumber": "0987654321"
}
```

### 9. Assign Permission (Change Role)

**Request:**
```
POST /api/admin/assign-permission
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "staffId": "550e8400-e29b-41d4-a716-446655440001",
  "role": "Verification Officer",
  "status": "Active",
  "reason": "Chuyển vị trí theo yêu cầu"
}
```

### 10. Deactivate Staff Account

**Request:**
```
POST /api/admin/staff/550e8400-e29b-41d4-a716-446655440001/deactivate
Authorization: Bearer {accessToken}
Content-Type: application/json

"Kỳ hạn hết"
```

**Response:**
```json
{
  "success": true,
  "message": "Tài khoản cán bộ đã được khóa"
}
```

### 11. Activate Staff Account

**Request:**
```
POST /api/admin/staff/550e8400-e29b-41d4-a716-446655440001/activate
Authorization: Bearer {accessToken}
```

**Response:**
```json
{
  "success": true,
  "message": "Tài khoản cán bộ đã được kích hoạt"
}
```

### 12. Reset Staff Password

**Request:**
```
POST /api/admin/staff/550e8400-e29b-41d4-a716-446655440001/reset-password
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "newPassword": "NewSecurePassword@12345"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Mật khẩu đã được đặt lại thành công"
}
```

## Testing in Postman or VS Code REST Client

### Option 1: Using VS Code REST Client Extension

Create a file `admin-test.http`:

```http
### Define Variables
@baseUrl = https://localhost:5001/api
@token = (paste_your_jwt_token_here)

### Login as Admin
POST {{baseUrl}}/auth/login
Content-Type: application/json

{
  "email": "admin@example.com",
  "password": "AdminPassword123"
}

### Create Ward Manager
POST {{baseUrl}}/admin/create-staff
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "email": "ward.manager.01@example.com",
  "fullName": "Nguyễn Văn Manager",
  "phoneNumber": "0912345670",
  "citizenId": "0123456789",
  "dateOfBirth": "1995-01-15",
  "address": "123 Nguyễn Huệ, Quận 1, TP HCM",
  "role": "Ward Manager",
  "temporaryPassword": "TempPassword@123456"
}

### Get Staff List
GET {{baseUrl}}/admin/staff-list?pageNumber=1&pageSize=10
Authorization: Bearer {{token}}
```

## Important Notes

1. **Authentication Required**: All endpoints require a valid JWT token in the Authorization header
2. **Role Authorization**: Only System Administrator role should have access (add authorization policy as needed)
3. **Password Requirements**: 
   - Minimum 8 characters
   - Should contain letters, numbers, and special characters
   - Temporary password must be changed on first login (implement in frontend)
4. **Email Uniqueness**: Email addresses must be unique in the system
5. **Status Options**: "Active", "Inactive", or "Suspended"

## Error Handling

Common error responses:

```json
// Email already exists
{
  "success": false,
  "message": "Email user@example.com đã được sử dụng"
}

// Invalid role
{
  "success": false,
  "message": "Vai trò InvalidRole không hợp lệ"
}

// Staff not found
{
  "success": false,
  "message": "Không tìm thấy cán bộ với ID 550e8400-e29b-41d4-a716-446655440999"
}

// Password too short
{
  "success": false,
  "message": "Mật khẩu phải có ít nhất 8 ký tự"
}
```

## Next Steps

1. Test all endpoints with valid data
2. Test error scenarios (invalid roles, duplicate emails, etc.)
3. Verify JWT token is required for all endpoints
4. Add authorization policies to restrict access to System Administrators
5. Implement audit logging for staff management operations
6. Add email notifications when staff accounts are created/modified
