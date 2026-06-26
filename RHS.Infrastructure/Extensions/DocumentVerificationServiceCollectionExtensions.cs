using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Services;

namespace RHS.Infrastructure.Extensions;

public static class DocumentVerificationServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentVerificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind GeminiAiOptions
        services.Configure<GeminiAiOptions>(
            configuration.GetSection(GeminiAiOptions.SectionName));

        // Đăng ký HttpClient cho Gemini Service
        services.AddHttpClient<IDocumentVerificationService, GeminiDocumentVerificationService>();

        return services;
    }
}
