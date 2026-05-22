# FE-20 Housing Project Lifecycle Administration - Testing Guide

## 📋 Mục lục
1. [Setup](#setup)
2. [API Endpoints](#api-endpoints)
3. [Test Cases](#test-cases)
4. [Status ID Reference](#status-id-reference)

---

## Setup

### Prerequisites
- API chạy trên: `https://localhost:7085` (hoặc port của bạn)
- Database đã init và có dữ liệu Status
- Dùng **REST Client** hoặc **Postman** để test

### Authorization
- Hiện tại: **Authorization bị tắt** để dễ test
- Sau khi test xong: **Bật lại** `[Authorize(Roles = "Admin,Officer")]` trên POST/PUT/DELETE

---

## API Endpoints

### 1. GET - Lấy danh sách dự án
```
GET /api/housing-projects
```

**Query Parameters (Optional):**
```
pageIndex=1
pageSize=12
search=Sunrise
province=Ho Chi Minh
district=District 1
minPrice=1000000000
maxPrice=5000000000
minArea=30
maxArea=100
statusId=6B0E79C0-B662-4CEE-949F-9693B9FEFF38
```

**Example URL:**
```
GET /api/housing-projects?pageIndex=1&pageSize=12&search=Modern
```

**Response (200 OK):**
```json
{
  "pageIndex": 1,
  "pageSize": 12,
  "totalCount": 5,
  "items": [
    {
      "id": "47d0b216-b33d-442f-8573-a52faa54ec78",
      "projectName": "Modern City Tower",
      "description": "Tòa tháp hiện đại tại trung tâm thành phố",
      "province": "Ho Chi Minh",
      "district": "District 1",
      "address": "100 Landmark Street, District 1",
      "minPrice": 4000000000,
      "maxPrice": 10000000000,
      "minArea": 80,
      "maxArea": 180,
      "availableUnits": 120,
      "thumbnailUrl": "https://example.com/modern-city-tower.jpg",
      "createdAt": "2026-05-22T10:04:46.6392462",
      "updatedAt": null,
      "status": "Open"
    }
  ]
}
```

---

### 2. GET - Lấy chi tiết dự án
```
GET /api/housing-projects/{id}
```

**Example:**
```
GET /api/housing-projects/47d0b216-b33d-442f-8573-a52faa54ec78
```

**Response (200 OK):**
```json
{
  "id": "47d0b216-b33d-442f-8573-a52faa54ec78",
  "projectName": "Modern City Tower",
  "description": "Tòa tháp hiện đại tại trung tâm thành phố",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "100 Landmark Street, District 1",
  "minPrice": 4000000000,
  "maxPrice": 10000000000,
  "minArea": 80,
  "maxArea": 180,
  "availableUnits": 120,
  "thumbnailUrl": "https://example.com/modern-city-tower.jpg",
  "createdAt": "2026-05-22T10:04:46.6392462",
  "updatedAt": null,
  "status": "Open"
}
```

**Response (404 Not Found):**
```json
{
  "message": "Housing project with ID 12345678-1234-1234-1234-123456789012 not found."
}
```

---

### 3. POST - Tạo dự án mới
```
POST /api/housing-projects
Content-Type: application/json
```

**Request Body:**
```json
{
  "projectName": "Modern City Tower",
  "description": "Tòa tháp hiện đại tại trung tâm thành phố",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "100 Landmark Street, District 1",
  "minPrice": 4000000000,
  "maxPrice": 10000000000,
  "minArea": 80,
  "maxArea": 180,
  "availableUnits": 120,
  "thumbnailUrl": "https://example.com/modern-city-tower.jpg",
  "housingProjectStatusId": "6B0E79C0-B662-4CEE-949F-9693B9FEFF38"
}
```

**Response (201 Created):**
```json
{
  "id": "47d0b216-b33d-442f-8573-a52faa54ec78",
  "projectName": "Modern City Tower",
  "description": "Tòa tháp hiện đại tại trung tâm thành phố",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "100 Landmark Street, District 1",
  "minPrice": 4000000000,
  "maxPrice": 10000000000,
  "minArea": 80,
  "maxArea": 180,
  "availableUnits": 120,
  "thumbnailUrl": "https://example.com/modern-city-tower.jpg",
  "createdAt": "2026-05-22T10:04:46.6392462",
  "updatedAt": null,
  "status": "Open"
}
```

**Response (400 Bad Request):**
```json
{
  "message": "ProjectName is required."
}
```

---

### 4. PUT - Cập nhật dự án
```
PUT /api/housing-projects/{id}
Content-Type: application/json
```

**Example:**
```
PUT /api/housing-projects/47d0b216-b33d-442f-8573-a52faa54ec78
```

**Request Body:**
```json
{
  "projectName": "Modern City Tower Premium",
  "description": "Tòa tháp hiện đại tại trung tâm thành phố - bản cao cấp",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "100 Landmark Street Premium, District 1",
  "minPrice": 5000000000,
  "maxPrice": 15000000000,
  "minArea": 100,
  "maxArea": 200,
  "availableUnits": 150,
  "thumbnailUrl": "https://example.com/modern-city-tower-premium.jpg",
  "housingProjectStatusId": "93112F18-902F-406D-AE1F-0305407D2C27"
}
```

**Response (200 OK):**
```json
{
  "id": "47d0b216-b33d-442f-8573-a52faa54ec78",
  "projectName": "Modern City Tower Premium",
  "description": "Tòa tháp hiện đại tại trung tâm thành phố - bản cao cấp",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "100 Landmark Street Premium, District 1",
  "minPrice": 5000000000,
  "maxPrice": 15000000000,
  "minArea": 100,
  "maxArea": 200,
  "availableUnits": 150,
  "thumbnailUrl": "https://example.com/modern-city-tower-premium.jpg",
  "createdAt": "2026-05-22T10:04:46.6392462",
  "updatedAt": "2026-05-22T10:15:30.1234567",
  "status": "Upcoming"
}
```

**Response (404 Not Found):**
```json
{
  "message": "Housing project with ID 12345678-1234-1234-1234-123456789012 not found."
}
```

---

### 5. DELETE - Xóa dự án (Soft Delete)
```
DELETE /api/housing-projects/{id}
```

**Example:**
```
DELETE /api/housing-projects/47d0b216-b33d-442f-8573-a52faa54ec78
```

**Response (204 No Content):**
- Không có response body
- Project bị soft delete (IsDeleted = true)

**Response (404 Not Found):**
```json
{
  "message": "Housing project with ID 12345678-1234-1234-1234-123456789012 not found."
}
```

---

## Test Cases

### Test 1️⃣: Create Project (POST)
**Mục tiêu:** Tạo 1 dự án mới

**JSON Request:**
```json
{
  "projectName": "Eco Smart Living",
  "description": "Dự án nhà ở sinh thái thông minh với công nghệ xanh",
  "province": "Hanoi",
  "district": "Long Bien",
  "address": "555 Eco Park Street, Long Bien, Hanoi",
  "minPrice": 2500000000,
  "maxPrice": 5000000000,
  "minArea": 60,
  "maxArea": 100,
  "availableUnits": 80,
  "thumbnailUrl": "https://example.com/eco-smart.jpg",
  "housingProjectStatusId": "6B0E79C0-B662-4CEE-949F-9693B9FEFF38"
}
```

**Kỳ vọng:**
- ✅ Response: 201 Created
- ✅ Trả về project object với ID, createdAt, status
- ✅ Có thể query lại từ GET /api/housing-projects

---

### Test 2️⃣: Get All Projects (GET)
**Mục tiêu:** Lấy danh sách dự án

**Request:**
```
GET /api/housing-projects?pageIndex=1&pageSize=10
```

**Kỳ vọng:**
- ✅ Response: 200 OK
- ✅ Trả về mảng projects với pagination info
- ✅ Không bao gồm soft deleted projects

---

### Test 3️⃣: Get Project By ID (GET)
**Mục tiêu:** Lấy chi tiết 1 dự án

**Request:**
```
GET /api/housing-projects/{id-from-test-1}
```

**Kỳ vọng:**
- ✅ Response: 200 OK
- ✅ Trả về đúng project từ Test 1
- ✅ Có status name

---

### Test 4️⃣: Update Project (PUT)
**Mục tiêu:** Cập nhật thông tin dự án

**Request:**
```
PUT /api/housing-projects/{id-from-test-1}
```

**JSON Request:**
```json
{
  "projectName": "Eco Smart Living Premium",
  "description": "Dự án nhà ở sinh thái thông minh - bản nâng cấp",
  "province": "Hanoi",
  "district": "Cau Giay",
  "address": "888 Eco Park Premium, Cau Giay, Hanoi",
  "minPrice": 3000000000,
  "maxPrice": 6000000000,
  "minArea": 70,
  "maxArea": 120,
  "availableUnits": 100,
  "thumbnailUrl": "https://example.com/eco-smart-premium.jpg",
  "housingProjectStatusId": "93112F18-902F-406D-AE1F-0305407D2C27"
}
```

**Kỳ vọng:**
- ✅ Response: 200 OK
- ✅ updatedAt được cập nhật
- ✅ Status thay đổi thành "Upcoming"
- ✅ GET lại để verify changes

---

### Test 5️⃣: Delete Project (DELETE - Soft Delete)
**Mục tiêu:** Soft delete 1 dự án

**Request:**
```
DELETE /api/housing-projects/{id-from-test-1}
```

**Kỳ vọng:**
- ✅ Response: 204 No Content
- ✅ Khi GET lại ID này: 404 Not Found
- ✅ Trong database: IsDeleted = 1

**Kiểm tra database:**
```sql
SELECT Id, ProjectName, IsDeleted FROM HousingProjects 
WHERE Id = '{id-from-test-1}'
```
- IsDeleted phải = 1

---

### Test 6️⃣: Validation - Missing Required Field (POST)
**Mục tiêu:** Test validation khi thiếu field bắt buộc

**JSON Request (thiếu ProjectName):**
```json
{
  "description": "Test",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "123 Street",
  "minPrice": 1000000000,
  "maxPrice": 5000000000,
  "minArea": 30,
  "maxArea": 80,
  "availableUnits": 50,
  "housingProjectStatusId": "6B0E79C0-B662-4CEE-949F-9693B9FEFF38"
}
```

**Kỳ vọng:**
- ✅ Response: 400 Bad Request
- ✅ Message: "ProjectName is required."

---

### Test 7️⃣: Validation - Price Validation (POST)
**Mục tiêu:** Test validation giá

**JSON Request (MaxPrice < MinPrice):**
```json
{
  "projectName": "Test Project",
  "description": "Test",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "123 Street",
  "minPrice": 5000000000,
  "maxPrice": 1000000000,
  "minArea": 30,
  "maxArea": 80,
  "availableUnits": 50,
  "housingProjectStatusId": "6B0E79C0-B662-4CEE-949F-9693B9FEFF38"
}
```

**Kỳ vọng:**
- ✅ Response: 400 Bad Request
- ✅ Message: "MaxPrice must be greater than or equal to MinPrice."

---

### Test 8️⃣: Validation - Area Validation (POST)
**Mục thiêu:** Test validation diện tích

**JSON Request (MinArea <= 0):**
```json
{
  "projectName": "Test Project",
  "description": "Test",
  "province": "Ho Chi Minh",
  "district": "District 1",
  "address": "123 Street",
  "minPrice": 1000000000,
  "maxPrice": 5000000000,
  "minArea": 0,
  "maxArea": 80,
  "availableUnits": 50,
  "housingProjectStatusId": "6B0E79C0-B662-4CEE-949F-9693B9FEFF38"
}
```

**Kỳ vọng:**
- ✅ Response: 400 Bad Request
- ✅ Message: "MinArea must be greater than 0."

---

### Test 9️⃣: Search by Project Name (GET)
**Mục tiêu:** Test search functionality

**Request:**
```
GET /api/housing-projects?search=Eco
```

**Kỳ vọng:**
- ✅ Response: 200 OK
- ✅ Chỉ return projects có "Eco" trong tên

---

### Test 🔟: Filter by Status (GET)
**Mục tiêu:** Test filter by status

**Request:**
```
GET /api/housing-projects?statusId=6B0E79C0-B662-4CEE-949F-9693B9FEFF38
```

**Kỳ vọng:**
- ✅ Response: 200 OK
- ✅ Chỉ return projects có status "Open"

---

## Status ID Reference

Dùng các Status ID này để test:

| ID | Status Name | Status Code |
|---|---|---|
| 93112F18-902F-406D-AE1F-0305407D2C27 | Upcoming | UPCOMING |
| 65808C84-5894-4077-A011-63C1729568A0 | Full | FULL |
| 6B0E79C0-B662-4CEE-949F-9693B9FEFF38 | Open | OPEN |
| EEF758D5-EEE2-4557-B2A3-D797EE333431 | Closed | CLOSED |

---

## Validation Rules Summary

| Field | Rules |
|---|---|
| ProjectName | Required, không empty |
| Province | Required, không empty |
| District | Required, không empty |
| Address | Required, không empty |
| MinPrice | >= 0 |
| MaxPrice | >= MinPrice |
| MinArea | > 0 |
| MaxArea | >= MinArea |
| AvailableUnits | >= 0 |
| HousingProjectStatusId | Phải tồn tại trong DB |

---

## Notes

- ✅ Soft delete: Record không bị xóa vật lý, chỉ set `IsDeleted = true`
- ✅ Query filter: Tự động loại bỏ soft deleted records
- ✅ Status: Trả về dạng string (ví dụ: "Open")
- ✅ Timestamp: `createdAt`, `updatedAt` ở định dạng ISO 8601
- ⚠️ Authorization: Hiện tại bị tắt, cần bật lại cho production

---

**Happy Testing!** 🚀
