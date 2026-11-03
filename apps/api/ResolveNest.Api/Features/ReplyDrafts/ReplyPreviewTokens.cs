using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ResolveNest.Api.Features.ReplyDrafts;

public sealed record ReplyPreviewTokenPayload(
    int Version,
    string PreviewId,
    string TicketId,
    string GeneratedText,
    IReadOnlyList<string> CitationIds,
    IReadOnlyList<string> LimitationCodes,
    string Model,
    string? ModelVersion,
    string EvidenceContractVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ExpiresAt);

public enum PreviewTokenFailure
{
    Invalid,
    Expired,
}

public sealed class PreviewTokenException(PreviewTokenFailure failure) : Exception($"Preview token rejected: {failure}.")
{
    public PreviewTokenFailure Failure { get; } = failure;
}

public sealed class ReplyPreviewTokens(IDataProtectionProvider dataProtection, TimeProvider timeProvider)
{
    public const int CurrentVersion = 1;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private const string Purpose = "ResolveNest.ReplyDrafts.PreviewToken.v1";

    private readonly IDataProtector protector = dataProtection.CreateProtector(Purpose);

    public DateTimeOffset Now => timeProvider.GetUtcNow();

    public string Protect(ReplyPreviewTokenPayload payload) =>
        protector.Protect(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web));

    public ReplyPreviewTokenPayload Unprotect(string token)
    {
        ReplyPreviewTokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ReplyPreviewTokenPayload>(protector.Unprotect(token), JsonSerializerOptions.Web);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            throw new PreviewTokenException(PreviewTokenFailure.Invalid);
        }

        if (payload is null || payload.Version != CurrentVersion || string.IsNullOrWhiteSpace(payload.PreviewId) || string.IsNullOrWhiteSpace(payload.GeneratedText))
        {
            throw new PreviewTokenException(PreviewTokenFailure.Invalid);
        }

        if (payload.ExpiresAt <= Now)
        {
            throw new PreviewTokenException(PreviewTokenFailure.Expired);
        }

        return payload;
    }
}
