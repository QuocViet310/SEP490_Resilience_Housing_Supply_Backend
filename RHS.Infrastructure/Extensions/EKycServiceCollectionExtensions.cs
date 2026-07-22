using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Services;
using RHS.Infrastructure.Validators;

namespace RHS.Infrastructure.Extensions;

/// <summary>
/// Extension methods cho <see cref="IServiceCollection"/> để đăng ký toàn bộ
/// dịch vụ eKYC (VNPT eKYC) vào DI container.
/// </summary>
/// <remarks>
/// Cách dùng trong <c>Program.cs</c>:
/// <code>
/// builder.Services.AddEKycServices(builder.Configuration);
/// </code>
/// </remarks>
public static class EKycServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký tất cả dependency cần thiết cho tính năng eKYC:
    /// <list type="number">
    ///   <item>Bind <see cref="VnptEKycOptions"/> từ section "VnptEKyc" trong appsettings.</item>
    ///   <item>Đăng ký named <see cref="System.Net.Http.HttpClient"/> "VnptEKycHttpClient"
    ///         với BaseAddress, Authorization header, Token-id, Token-key, và timeout.</item>
    ///   <item>Đăng ký <see cref="EKycFileValidator"/> là Singleton (stateless, thread-safe).</item>
    ///   <item>Đăng ký <see cref="IEKycService"/> → <see cref="VnptEKycService"/> là Scoped.</item>
    /// </list>
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configuration">Configuration root để đọc section "VnptEKyc".</param>
    /// <returns><paramref name="services"/> để hỗ trợ fluent chaining.</returns>
    public static IServiceCollection AddEKycServices(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── Bước 1: Bind strongly-typed options ────────────────────────────
        var vnptSection = configuration.GetSection(VnptEKycOptions.SectionName);
        services.Configure<VnptEKycOptions>(vnptSection);

        // Đọc options ngay để cấu hình HttpClient bên dưới
        var options = vnptSection.Get<VnptEKycOptions>() ?? new VnptEKycOptions();

        // ── Bước 2: Đăng ký named HttpClient ──────────────────────────────
        services
            .AddHttpClient(VnptEKycService.HttpClientName, client =>
            {
                // Đặt timeout cho toàn bộ request (bao gồm connection + send + receive)
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

                // Base URL cho VNPT eKYC API
                client.BaseAddress = new Uri(options.BaseUrl);

                // VNPT eKYC authentication headers
                if (!string.IsNullOrWhiteSpace(options.AccessToken))
                {
                    // Cho phép dán cả "bearer xxx" từ Dashboard — strip prefix trước khi set header
                    var rawToken = options.AccessToken.Trim();
                    if (rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        rawToken = rawToken["Bearer ".Length..].Trim();

                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rawToken);
                }

                if (!string.IsNullOrWhiteSpace(options.TokenId))
                    client.DefaultRequestHeaders.Add("Token-id", options.TokenId);

                if (!string.IsNullOrWhiteSpace(options.TokenKey))
                    client.DefaultRequestHeaders.Add("Token-key", options.TokenKey);
            });

        // ── Bước 3: Đăng ký Validator ─────────────────────────────────────
        // Singleton vì class không giữ state mutable, tái sử dụng an toàn giữa các request.
        services.AddSingleton<EKycFileValidator>();

        // ── Bước 4: Đăng ký Service ───────────────────────────────────────
        // Scoped để theo vòng đời của HTTP request, tránh rò rỉ HttpClient context.
        services.AddScoped<IEKycService, VnptEKycService>();

        return services;
    }
}
