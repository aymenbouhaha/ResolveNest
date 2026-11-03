namespace ResolveNest.Api.Features.AiInvestigations;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxOutputTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.1;
    public string? ThinkingLevel { get; set; } = "low";

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
