using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Repositories;
using RHS.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

// Dependency Injection - Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IHousingProjectRepository, HousingProjectRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();

// Dependency Injection - Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHousingProjectService, HousingProjectService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// Dependency Injection - VNPay Payment
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

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
