using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Services;
using RHS.Infrastructure.Validators;

namespace RHS.Infrastructure.Extensions;

/// <summary>
/// Extension methods cho <see cref="IServiceCollection"/> để đăng ký toàn bộ
/// dịch vụ eKYC (FPT AI) vào DI container.
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
    ///   <item>Bind <see cref="FptAiOptions"/> từ section "FptAi" trong appsettings.</item>
    ///   <item>Đăng ký named <see cref="System.Net.Http.HttpClient"/> "FptAiHttpClient"
    ///         với default header <c>api-key</c> và timeout.</item>
    ///   <item>Đăng ký <see cref="EKycFileValidator"/> là Singleton (stateless, thread-safe).</item>
    ///   <item>Đăng ký <see cref="IEKycService"/> → <see cref="FptEKycService"/> là Scoped.</item>
    /// </list>
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configuration">Configuration root để đọc section "FptAi".</param>
    /// <returns><paramref name="services"/> để hỗ trợ fluent chaining.</returns>
    public static IServiceCollection AddEKycServices(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        // ── Bước 1: Bind strongly-typed options ────────────────────────────
        var fptAiSection = configuration.GetSection(FptAiOptions.SectionName);
        services.Configure<FptAiOptions>(fptAiSection);

        // Đọc options ngay để cấu hình HttpClient bên dưới
        var options = fptAiSection.Get<FptAiOptions>() ?? new FptAiOptions();

        // ── Bước 2: Đăng ký named HttpClient ──────────────────────────────
        services
            .AddHttpClient(FptEKycService.HttpClientName, client =>
            {
                // Đặt timeout cho toàn bộ request (bao gồm connection + send + receive)
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

                // Thêm API key vào mọi request mà không cần nhắc lại trong service
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
                }
            });

        // ── Bước 3: Đăng ký Validator ─────────────────────────────────────
        // Singleton vì class không giữ state mutable, tái sử dụng an toàn giữa các request.
        services.AddSingleton<EKycFileValidator>();

        // ── Bước 4: Đăng ký Service ───────────────────────────────────────
        // Scoped để theo vòng đời của HTTP request, tránh rò rỉ HttpClient context.
        services.AddScoped<IEKycService, FptEKycService>();

        return services;
    }
}
