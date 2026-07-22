using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Extensions;
using RHS.Infrastructure.Repositories;
using RHS.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Load .env file (nếu tồn tại) ───────────────────────────────────────
// File .env chứa secrets (API keys, connection strings) cho môi trường local.
// Copy .env.example → .env và điền giá trị thật. File .env đã được gitignore.
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
    // Nạp các biến từ .env vào Configuration để IOptions<T> đọc được
    builder.Configuration.AddEnvironmentVariables();
}

// Add services to the container.

// Database Configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Dependency Injection - Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IHousingProjectRepository, HousingProjectRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IHousingApplicationRepository, HousingApplicationRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IReviewHistoryRepository, ReviewHistoryRepository>();
builder.Services.AddScoped<IIssueReportRepository, IssueReportRepository>();
builder.Services.AddScoped<IWishlistRepository, WishlistRepository>();

// Dependency Injection - Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHousingProjectService, HousingProjectService>();
builder.Services.AddScoped<IHousingProjectStatusService, HousingProjectStatusService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IHousingApplicationService, HousingApplicationService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IIssueReportService, IssueReportService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IPolicyService, PolicyService>();
builder.Services.AddScoped<IEligibilityRuleEngine, EligibilityRuleEngine>();
builder.Services.AddScoped<ILotteryService, LotteryService>();
builder.Services.AddScoped<IBeneficiaryPublishService, BeneficiaryPublishService>();
builder.Services.AddScoped<IPublicPostCheckService, PublicPostCheckService>();
builder.Services.AddMemoryCache();

// Dependency Injection - VNPay Payment
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IInstallmentService, InstallmentService>();

// Configure EPPlus NonCommercial License
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

// Dependency Injection - PDF Contract & PrincipleAgreement
builder.Services.AddScoped<IPdfContractService, PdfContractService>();
builder.Services.AddScoped<IPdfReceiptService, PdfReceiptService>();
builder.Services.AddScoped<IReportExportService, ReportExportService>();
builder.Services.AddScoped<IPrincipleAgreementRepository, PrincipleAgreementRepository>();

// Dependency Injection - Contract Sign
builder.Services.AddScoped<IContractSignService, ContractSignService>();

// Dependency Injection - Notification
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();

// Dependency Injection - Announcement
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();

// Dependency Injection - VNPT eKYC
builder.Services.AddEKycServices(builder.Configuration);

// Dependency Injection - Gemini Document Verification
builder.Services.AddDocumentVerificationServices(builder.Configuration);

// Background Worker
builder.Services.AddHostedService<RHS.API.BackgroundServices.PaymentTimeoutWorker>();
builder.Services.AddHostedService<RHS.API.BackgroundServices.ProjectAutomationWorker>();
builder.Services.AddHostedService<RHS.API.BackgroundServices.OverduePaymentWorker>();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

// Cấu hình giới hạn upload file (dành cho PDF tài liệu hồ sơ, tối đa 10MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});


// Swagger Configuration with JWT Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Resilience Housing Supply API",
        Version = "v1",
        Description = "API for Intelligent Social Housing Coordination & Vetting Platform"
    });

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT token với format: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// QuestPDF Community License (miễn phí cho doanh thu < $1M/năm)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        Console.WriteLine("🔄 Applying migrations...");
        dbContext.Database.Migrate();
        Console.WriteLine("✅ Migrations applied successfully!");

        // Seed default project statuses if they don't exist
        if (!dbContext.HousingProjectStatuses.Any())
        {
            var statuses = new[]
            {
                new RHS.Domain.Entities.HousingProjectStatus
                {
                    Id = Guid.NewGuid(),
                    StatusName = "Upcoming",
                    StatusCode = "UPCOMING",
                    CreatedAt = DateTime.UtcNow
                },
                new RHS.Domain.Entities.HousingProjectStatus
                {
                    Id = Guid.NewGuid(),
                    StatusName = "Open",
                    StatusCode = "OPEN",
                    CreatedAt = DateTime.UtcNow
                },
                new RHS.Domain.Entities.HousingProjectStatus
                {
                    Id = Guid.NewGuid(),
                    StatusName = "Closed",
                    StatusCode = "CLOSED",
                    CreatedAt = DateTime.UtcNow
                },
                new RHS.Domain.Entities.HousingProjectStatus
                {
                    Id = Guid.NewGuid(),
                    StatusName = "Full",
                    StatusCode = "FULL",
                    CreatedAt = DateTime.UtcNow
                }
            };

            dbContext.HousingProjectStatuses.AddRange(statuses);
            dbContext.SaveChanges();
            Console.WriteLine("✅ Seeded project statuses!");
        }

        // Seed PolicyConfig defaults (NOXH decree parameters)
        try
        {
            var policyService = scope.ServiceProvider.GetRequiredService<IPolicyService>();
            policyService.EnsureDefaultsSeededAsync(RHS.Domain.Constants.RoleConstants.SystemAdministratorId)
                .GetAwaiter().GetResult();
            Console.WriteLine("✅ PolicyConfig defaults ensured!");
        }
        catch (Exception seedEx)
        {
            Console.WriteLine($"⚠️ PolicyConfig seed skipped: {seedEx.Message}");
        }

        // Seed demo staff + housing projects (idempotent)
        try
        {
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var demoLogger = loggerFactory.CreateLogger("DemoDataSeeder");
            RHS.Infrastructure.Seed.DemoDataSeeder
                .EnsureSeededAsync(dbContext, demoLogger)
                .GetAwaiter().GetResult();
            Console.WriteLine("✅ Demo data (CĐT/SXD + dự án) ensured!");
            Console.WriteLine($"   CĐT: {RHS.Infrastructure.Seed.DemoDataSeeder.DemoDeveloperEmail} / {RHS.Infrastructure.Seed.DemoDataSeeder.DemoPassword}");
            Console.WriteLine($"   SXD: {RHS.Infrastructure.Seed.DemoDataSeeder.DemoSxdEmail} / {RHS.Infrastructure.Seed.DemoDataSeeder.DemoPassword}");
        }
        catch (Exception demoEx)
        {
            Console.WriteLine($"⚠️ Demo data seed skipped: {demoEx.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database setup error: {ex.Message}");
    }
}

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
