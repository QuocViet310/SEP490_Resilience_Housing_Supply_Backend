# RHS Housing Application Dashboard APIs

# 🏠 Resilience Housing Supply - Backend API

Dashboard APIs for Verification Officer and Ward Manager

---

# 📖 Project Context

The system already supports:

* Authentication & Authorization
* Housing Project Management
* Housing Application Management
* Application Status Workflow

Current architecture:

```plaintext
RHS.API
RHS.Application
RHS.Infrastructure
RHS.Domain
```

Technology stack:

* .NET 8
* EF Core 8
* SQL Server
* JWT Authentication
* Clean Architecture

---

# 🎯 Goal

Implement dashboard APIs for:

1. Verification Officer (VO)
2. Ward Manager (WM)

The purpose is to provide a single endpoint for each role to retrieve all housing applications that require processing.

The APIs must support:

* Pagination
* Searching
* Filtering
* Sorting
* Project filtering

The implementation must be optimized for dashboard usage.

---

# ⚠️ Implementation Rules

Must:

* Follow Clean Architecture
* Use DTOs
* Use Services
* Use Repositories
* Use IQueryable
* Use AsNoTracking()
* Use Pagination
* Use Role-Based Authorization

Do NOT:

* Return EF entities directly
* Put business logic inside controllers
* Duplicate query logic

---

# Existing Entity Assumptions

The system already contains:

```csharp
HousingApplication
```

with fields similar to:

```csharp
ApplicationId
ApplicantId
ProjectId
OfficerId
ApplicationStatus
SubmittedAt
PriorityScore
EstimatedMonthlyIncome
FinalDecisionDate
```

Navigation:

```csharp
Applicant
Officer
HousingProject
```

---

# Shared Dashboard DTOs

Create folder:

```plaintext
RHS.Application/DTOs/HousingApplications/Dashboard
```

---

# HousingApplicationDashboardItemDto

```csharp
public class HousingApplicationDashboardItemDto
{
    public Guid ApplicationId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string ApplicantEmail { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ApplicationStatus { get; set; } = string.Empty;

    public decimal PriorityScore { get; set; }

    public decimal EstimatedMonthlyIncome { get; set; }

    public DateTime SubmittedAt { get; set; }
}
```

---

# HousingApplicationDashboardQueryDto

```csharp
public class HousingApplicationDashboardQueryDto
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }

    public Guid? ProjectId { get; set; }

    public string? Status { get; set; }
}
```

---

# Paged Dashboard Response

Use existing PagedResult<T> if available.

Otherwise create:

```csharp
public class PagedResult<T>
{
    public IReadOnlyCollection<T> Items { get; set; }

    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }
}
```

---

# TASK #3

# Verification Officer Dashboard

---

# Endpoint

```http
GET /api/housing-applications/dashboard/vo
```

Authorization:

```csharp
[Authorize(Roles = "VerificationOfficer")]
```

---

# Business Rule

Verification Officer (Maker) must see applications with statuses:

```plaintext
SUBMITTED
UNDER_REVIEW
NEED_MORE_DOCUMENTS
```

VO pipeline: applications waiting for VO to pick up (SUBMITTED), being reviewed (UNDER_REVIEW), or needing more documents (NEED_MORE_DOCUMENTS). Once VO proposes approval (PROPOSED), the application moves to WM pipeline and is no longer visible here.

---

# Supported Filters

Query parameters:

```http
?pageIndex=
&pageSize=
&search=
&projectId=
&status=
```

---

# Search Behavior

Search against:

```plaintext
Applicant Name
Applicant Email
Project Name
```

Use:

```csharp
Contains()
```

or equivalent SQL-translatable logic.

---

# Sorting

Default:

```plaintext
SubmittedAt DESC
```

Newest applications first.

---

# Repository Method

Create:

```csharp
Task<PagedResult<HousingApplicationDashboardItemDto>>
GetVerificationOfficerDashboardAsync(
    HousingApplicationDashboardQueryDto query);
```

Requirements:

```csharp
AsNoTracking()

Include(Applicant)

Include(HousingProject)

OrderByDescending(SubmittedAt)
```

---

# Service Method

Create:

```csharp
Task<PagedResult<HousingApplicationDashboardItemDto>>
GetVerificationOfficerDashboardAsync(
    HousingApplicationDashboardQueryDto query);
```

Responsibilities:

* Validate paging
* Apply filters
* Map DTOs
* Return paged result

---

# Expected Response

```json
{
  "items": [
    {
      "applicationId": "guid",
      "applicantName": "Nguyen Van A",
      "applicantEmail": "a@email.com",
      "projectName": "Sunrise Housing",
      "applicationStatus": "SUBMITTED",
      "priorityScore": 85,
      "estimatedMonthlyIncome": 7000000,
      "submittedAt": "2026-07-01T08:00:00"
    }
  ],
  "pageIndex": 1,
  "pageSize": 10,
  "totalCount": 120
}
```

---

# TASK #4

# Ward Manager Dashboard

---

# Endpoint

```http
GET /api/housing-applications/dashboard/wm
```

Authorization:

```csharp
[Authorize(Roles = "WardManager")]
```

---

# Business Rule

Ward Manager (Checker) sees applications with statuses:

```plaintext
PROPOSED
UNDER_REVIEW
```

PROPOSED = VO đã đề xuất phê duyệt, chờ WM chốt quyết định cuối cùng.
UNDER_REVIEW = hồ sơ chưa qua VO, WM có thể trực tiếp xét duyệt.

---

# Supported Filters

```http
?pageIndex=
&pageSize=
&search=
&projectId=
```

Status filter is optional because all records are already PROPOSED or UNDER_REVIEW.

---

# Search Behavior

Search against:

```plaintext
Applicant Name
Applicant Email
Project Name
```

---

# Sorting

Default:

```plaintext
SubmittedAt DESC
```

---

# Repository Method

Create:

```csharp
Task<PagedResult<HousingApplicationDashboardItemDto>>
GetWardManagerDashboardAsync(
    HousingApplicationDashboardQueryDto query);
```

Requirements:

```csharp
AsNoTracking()

Include(Applicant)

Include(HousingProject)

Where(ApplicationStatus == "UNDER_REVIEW")
```

---

# Service Method

Create:

```csharp
Task<PagedResult<HousingApplicationDashboardItemDto>>
GetWardManagerDashboardAsync(
    HousingApplicationDashboardQueryDto query);
```

Responsibilities:

* Filtering
* Pagination
* Mapping
* Validation

---

# Controller Requirements

Controller:

```plaintext
HousingApplicationsController
```

Add endpoints:

```csharp
[HttpGet("dashboard/vo")]
```

```csharp
[HttpGet("dashboard/wm")]
```

Controllers must:

* Only call service layer
* Not contain business logic

---

# Query Optimization Requirements

Must use:

```csharp
AsNoTracking()
```

Projection before pagination:

```csharp
Select(...)
```

Avoid:

```csharp
ToList()
```

before filtering or paging.

---

# Future Compatibility

Implementation should support future dashboards:

* Regional Officer Dashboard
* Admin Dashboard
* Analytics Dashboard
* Review Board Dashboard

The dashboard query logic should be reusable and extensible.

---

# Final Goal

Build two production-ready dashboard APIs:

## Verification Officer Dashboard

```http
GET /api/housing-applications/dashboard/vo
```

Displays:

```plaintext
SUBMITTED
UNDER_REVIEW
NEED_MORE_DOCUMENTS
```

applications.

---

## Ward Manager Dashboard

```http
GET /api/housing-applications/dashboard/wm
```

Displays:

```plaintext
UNDER_REVIEW
```

applications.

Both APIs must:

* Follow Clean Architecture
* Support pagination
* Support searching
* Support project filtering
* Be optimized for large datasets
* Be ready for future dashboard expansion
