using Google.Apis.Auth;

namespace Project333.PvpServer.Auth;

public sealed record VerifiedGoogleIdentity(
    string Subject,
    string? Email,
    bool EmailVerified,
    string DisplayName,
    string? PictureUrl,
    string? Locale,
    string Audience);

public sealed class GoogleIdentityTokenValidator
{
    public const string ClientIdsConfigurationKey = "PROJECT333_GOOGLE_CLIENT_IDS";
    private const int MaximumIdTokenLength = 16_384;
    private const int MaximumDisplayNameLength = 32;

    private readonly IReadOnlyList<string> _acceptedClientIds;

    public GoogleIdentityTokenValidator(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _acceptedClientIds = ParseClientIds(configuration[ClientIdsConfigurationKey]);
    }

    public bool IsConfigured => _acceptedClientIds.Count > 0;
    public int AcceptedClientIdCount => _acceptedClientIds.Count;

    public bool AcceptsClientId(string? clientId)
    {
        var normalizedClientId = clientId?.Trim();
        return !string.IsNullOrWhiteSpace(normalizedClientId) &&
               _acceptedClientIds.Contains(normalizedClientId, StringComparer.Ordinal);
    }

    public async Task<VerifiedGoogleIdentity> ValidateAsync(
        string? idToken,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new AuthServiceException(
                "google_auth_not_configured",
                $"{ClientIdsConfigurationKey} is not set. Google login is unavailable.");
        }

        var normalizedToken = idToken?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedToken) || normalizedToken.Length > MaximumIdTokenLength)
        {
            throw new AuthServiceException(
                "invalid_google_id_token",
                "A valid Google ID token is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                normalizedToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _acceptedClientIds
                });
        }
        catch (InvalidJwtException)
        {
            throw new AuthServiceException(
                "invalid_google_id_token",
                "The Google ID token is invalid, expired, or was issued for another application.");
        }
        catch (HttpRequestException)
        {
            throw new AuthServiceException(
                "google_token_verification_unavailable",
                "Google token verification is temporarily unavailable.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (payload == null || string.IsNullOrWhiteSpace(payload.Subject))
        {
            throw new AuthServiceException(
                "invalid_google_id_token",
                "The Google ID token does not contain a stable account subject.");
        }

        var audience = payload.Audience?.ToString();
        if (string.IsNullOrWhiteSpace(audience) ||
            !_acceptedClientIds.Contains(audience, StringComparer.Ordinal))
        {
            throw new AuthServiceException(
                "invalid_google_id_token",
                "The Google ID token audience is not accepted by this server.");
        }

        return new VerifiedGoogleIdentity(
            payload.Subject.Trim(),
            NormalizeNullable(payload.Email),
            payload.EmailVerified,
            ResolveDisplayName(payload),
            NormalizeNullable(payload.Picture),
            NormalizeNullable(payload.Locale?.ToString()),
            audience!);
    }

    private static IReadOnlyList<string> ParseClientIds(string? configuredClientIds)
    {
        if (string.IsNullOrWhiteSpace(configuredClientIds))
        {
            return Array.Empty<string>();
        }

        return configuredClientIds
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveDisplayName(GoogleJsonWebSignature.Payload payload)
    {
        var displayName = NormalizeNullable(payload.Name);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            var email = NormalizeNullable(payload.Email);
            var separatorIndex = email?.IndexOf('@') ?? -1;
            displayName = separatorIndex > 0 ? email![..separatorIndex] : null;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            var subjectSuffix = payload.Subject?.Trim();
            if (!string.IsNullOrWhiteSpace(subjectSuffix) && subjectSuffix.Length > 8)
            {
                subjectSuffix = subjectSuffix[^8..];
            }

            displayName = $"Google{subjectSuffix}";
        }

        return displayName.Length <= MaximumDisplayNameLength
            ? displayName
            : displayName[..MaximumDisplayNameLength];
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
