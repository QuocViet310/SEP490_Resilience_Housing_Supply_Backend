namespace RHS.Domain.Constants;

public static class RoleConstants
{
    // Guest không cần role vì không cần đăng nhập
    public const string Applicant = "Applicant";
    public const string HousingAuthorityOfficer = "Housing Authority Officer";
    public const string SystemAdministrator = "System Administrator";
    public const string DepartmentOfConstruction = "Department Of Construction";
    public const string HousingDeveloper = "Housing Developer";

    // Role IDs (matching seed data)
    public static readonly Guid ApplicantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid HousingAuthorityOfficerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid SystemAdministratorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid DepartmentOfConstructionId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid HousingDeveloperId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public static List<string> GetAllRoles()
    {
        return new List<string>
        {
            Applicant,
            HousingAuthorityOfficer,
            SystemAdministrator,
            DepartmentOfConstruction,
            HousingDeveloper
        };
    }

    public static List<string> GetStaffRoles()
    {
        return new List<string>
        {
            DepartmentOfConstruction,
            HousingDeveloper
        };
    }

    public static Guid GetRoleId(string roleName)
    {
        return roleName switch
        {
            Applicant => ApplicantId,
            HousingAuthorityOfficer => HousingAuthorityOfficerId,
            SystemAdministrator => SystemAdministratorId,
            DepartmentOfConstruction => DepartmentOfConstructionId,
            HousingDeveloper => HousingDeveloperId,
            _ => ApplicantId // Default to Applicant
        };
    }
}
