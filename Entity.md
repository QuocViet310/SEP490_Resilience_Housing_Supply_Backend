# RHS Backend Domain Expansion Request

# 🏠 Resilience Housing Supply - Backend API

Intelligent Social Housing Coordination & Vetting Platform

---

# 📖 Project Context

The project currently contains:

* User
* Role
* RefreshToken
* OtpVerification
* Payment
* HousingProject
* HousingProjectStatus

Implemented modules:

* Authentication & Authorization
* FE-01 Project Discovery & Search
* FE-02 Advanced Project Filtering
* FE-20 Project Lifecycle Administration

The next task is to expand the domain model according to the official RHS ERD.

---

# 🏗️ Existing Architecture

The project follows Clean Architecture.

```plaintext
RHS.API              → Presentation Layer
RHS.Application      → Business Logic Layer
RHS.Infrastructure   → Data Access Layer
RHS.Domain           → Domain Layer
```

---

# 🛠️ Existing Tech Stack

Backend:

* .NET 8 Web API
* Entity Framework Core 8
* SQL Server
* JWT Authentication
* Clean Architecture
* Code First

---

# ⚠️ IMPORTANT RULES

The implementation must:

* Follow Clean Architecture
* Follow existing coding style
* Use EF Core Code First
* Use Fluent API Configuration
* Use GUID primary keys
* Use SQL Server
* Use Navigation Properties
* Configure all relationships

DO NOT:

* Remove existing entities
* Remove HousingProjectStatus
* Convert statuses to enums
* Break existing FE-01, FE-02 or FE-20 functionality

---

# Existing HousingProject Rule

HousingProject must continue using:

```csharp
Guid HousingProjectStatusId

HousingProjectStatus HousingProjectStatus
```

HousingProjectStatus remains the official project status management table.

Do not replace it with string Status.

---

# 🎯 Required New Entities

---

# 1. HousingApplication

```csharp
public class HousingApplication
{
    public Guid ApplicationId { get; set; }

    public Guid ApplicantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? OfficerId { get; set; }

    public string ApplicationStatus { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }

    public decimal PriorityScore { get; set; }

    public decimal EstimatedMonthlyIncome { get; set; }

    public DateTime? FinalDecisionDate { get; set; }

    public User Applicant { get; set; } = null!;

    public User? Officer { get; set; }

    public HousingProject HousingProject { get; set; } = null!;

    public ICollection<ApplicationDocument> Documents { get; set; }
        = new List<ApplicationDocument>();

    public ICollection<ApplicationStatusHistory> StatusHistories { get; set; }
        = new List<ApplicationStatusHistory>();

    public ICollection<Appointment> Appointments { get; set; }
        = new List<Appointment>();
}
```

---

# 2. ApplicationStatusHistory

```csharp
public class ApplicationStatusHistory
{
    public Guid HistoryId { get; set; }

    public Guid ApplicationId { get; set; }

    public Guid ChangedBy { get; set; }

    public string OldStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }

    public HousingApplication Application { get; set; } = null!;

    public User ChangedByUser { get; set; } = null!;
}
```

---

# 3. ApplicationDocument

```csharp
public class ApplicationDocument
{
    public Guid DocumentId { get; set; }

    public Guid ApplicationId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;

    public HousingApplication HousingApplication { get; set; } = null!;

    public AIVerificationResult? VerificationResult { get; set; }
}
```

---

# 4. AIVerificationResult

```csharp
public class AIVerificationResult
{
    public Guid VerificationId { get; set; }

    public Guid DocumentId { get; set; }

    public string ExtractedText { get; set; } = string.Empty;

    public decimal FaceMatchScore { get; set; }

    public decimal RiskScore { get; set; }

    public string ValidationResult { get; set; } = string.Empty;

    public DateTime VerifiedAt { get; set; }

    public ApplicationDocument Document { get; set; } = null!;
}
```

---

# 5. Appointment

```csharp
public class Appointment
{
    public Guid AppointmentId { get; set; }

    public Guid ApplicationId { get; set; }

    public DateTime AppointmentDate { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public HousingApplication HousingApplication { get; set; } = null!;
}
```

---

# 6. ProjectImage

```csharp
public class ProjectImage
{
    public Guid ImageId { get; set; }

    public Guid ProjectId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public HousingProject HousingProject { get; set; } = null!;
}
```

---

# 7. HousingQuota

```csharp
public class HousingQuota
{
    public Guid QuotaId { get; set; }

    public Guid ProjectId { get; set; }

    public string PriorityGroup { get; set; } = string.Empty;

    public int AllocatedSlots { get; set; }

    public int RemainingSlots { get; set; }

    public HousingProject HousingProject { get; set; } = null!;
}
```

---

# 8. EligibilityAssessment

```csharp
public class EligibilityAssessment
{
    public Guid AssessmentId { get; set; }

    public Guid UserId { get; set; }

    public decimal EstimatedScore { get; set; }

    public bool Eligible { get; set; }

    public DateTime AssessmentDate { get; set; }

    public User User { get; set; } = null!;
}
```

---

# 9. Notification

```csharp
public class Notification
{
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string NotificationType { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
```

---

# 10. Message

```csharp
public class Message
{
    public Guid MessageId { get; set; }

    public Guid SenderId { get; set; }

    public Guid ReceiverId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public User Sender { get; set; } = null!;

    public User Receiver { get; set; } = null!;
}
```

---

# 11. AuditLog

```csharp
public class AuditLog
{
    public Guid AuditId { get; set; }

    public Guid UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public DateTime ActionTime { get; set; }

    public User User { get; set; } = null!;
}
```

---

# 12. PolicyConfig

```csharp
public class PolicyConfig
{
    public Guid PolicyId { get; set; }

    public Guid UpdatedBy { get; set; }

    public string PolicyName { get; set; } = string.Empty;

    public string PolicyValue { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; }

    public User UpdatedByUser { get; set; } = null!;
}
```

---

# 📏 Decimal Precision Requirements

Configure:

```csharp
PriorityScore          -> decimal(18,2)

EstimatedMonthlyIncome -> decimal(18,2)

FaceMatchScore         -> decimal(5,2)

RiskScore              -> decimal(5,2)

EstimatedScore         -> decimal(18,2)
```

---

# 📏 String Length Requirements

Configure:

```csharp
ApplicationStatus      -> 50

DocumentType           -> 100

VerificationStatus     -> 50

ValidationResult       -> 100

Location               -> 500

PriorityGroup          -> 100

NotificationType       -> 100

Title                  -> 255

Action                 -> 100

EntityName             -> 100

PolicyName             -> 200

IpAddress              -> 50
```

---

# 🔗 Relationship Requirements

Configure all navigation properties.

Required relationships:

```plaintext
User
 ├── HousingApplications (Applicant)
 ├── AssignedApplications (Officer)
 ├── Notifications
 ├── AuditLogs
 ├── EligibilityAssessments
 ├── SentMessages
 └── ReceivedMessages

HousingProject
 ├── HousingApplications
 ├── ProjectImages
 └── HousingQuotas

HousingApplication
 ├── Documents
 ├── StatusHistories
 └── Appointments
```

---

# ⚙️ Delete Behavior Requirements

Configure:

```csharp
User -> HousingApplication(Applicant)
    Restrict

User -> HousingApplication(Officer)
    Restrict

HousingProject -> HousingApplication
    Restrict

HousingApplication -> ApplicationDocument
    Cascade

ApplicationDocument -> AIVerificationResult
    Cascade

HousingApplication -> Appointment
    Cascade

HousingApplication -> ApplicationStatusHistory
    Cascade

HousingProject -> ProjectImage
    Cascade

HousingProject -> HousingQuota
    Cascade
```

---

# 📚 DbContext Requirements

Register all DbSets.

```csharp
DbSet<HousingApplication>
DbSet<ApplicationStatusHistory>
DbSet<ApplicationDocument>
DbSet<AIVerificationResult>
DbSet<Appointment>
DbSet<ProjectImage>
DbSet<HousingQuota>
DbSet<EligibilityAssessment>
DbSet<Notification>
DbSet<Message>
DbSet<AuditLog>
DbSet<PolicyConfig>
```

---

# ⚙️ Fluent API Requirements

Create separate configuration classes for each entity.

Configure:

* Table Names
* Primary Keys
* Foreign Keys
* Navigation Properties
* Required Fields
* Max Lengths
* Decimal Precision
* Delete Behaviors
* Indexes where appropriate

Location:

```plaintext
RHS.Infrastructure/Persistence/Configurations
```

---

# 🔄 Migration Requirements

After implementation:

```bash
dotnet ef migrations add ExpandDomainEntities

dotnet ef database update
```

Migration must remain compatible with:

* FE-01
* FE-02
* FE-20

---

# ✅ Final Goal

Expand the RHS domain model according to the official ERD while preserving the existing HousingProjectStatus implementation.

The implementation must be:

* Production-ready
* Clean Architecture compliant
* EF Core Code First compliant
* Scalable
* Maintainable
* Compatible with future RHS modules such as:

  * Housing Applications
  * OCR & AI Verification
  * Eligibility Assessment
  * Appointment Scheduling
  * Messaging
  * Notifications
  * Audit Logging
  * Administrative Review Workflow
  * Analytics & Reporting
