namespace RHS.Infrastructure.Configurations;

public sealed class GeminiAiOptions
{
    public const string SectionName = "GeminiAi";

    /// <summary>API Key lấy từ Google AI Studio</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Model sử dụng. Mặc định: gemini-2.5-flash</summary>
    public string ModelName { get; init; } = "gemini-2.5-flash";

    /// <summary>Endpoint API của Gemini</summary>
    public string ApiUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Timeout (giây) cho mỗi request. Mặc định: 30s</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
