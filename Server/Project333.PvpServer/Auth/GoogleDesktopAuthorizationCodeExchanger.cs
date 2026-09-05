using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Project333.PvpServer.Auth;

public sealed class GoogleDesktopAuthorizationCodeExchanger
{
    public const string ClientIdConfigurationKey = "PROJECT333_GOOGLE_DESKTOP_CLIENT_ID";
    public const string ClientSecretConfigurationKey = "PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET";

    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const int MaximumAuthorizationCodeLength = 4096;
    private const int MaximumClientIdLength = 512;
    private const int MinimumCodeVerifierLength = 43;
    private const int MaximumCodeVerifierLength = 128;
    private const int MaximumGoogleErrorDescriptionLength = 400;

    private readonly HttpClient _httpClient;
    private readonly GoogleIdentityTokenValidator _tokenValidator;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GoogleDesktopAuthorizationCodeExchanger(
        HttpClient httpClient,
        IConfiguration configuration,
        GoogleIdentityTokenValidator tokenValidator)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(tokenValidator);

        _httpClient = httpClient;
        _tokenValidator = tokenValidator;
        _clientId = configuration[ClientIdConfigurationKey]?.Trim() ?? string.Empty;
        _clientSecret = configuration[ClientSecretConfigurationKey]?.Trim() ?? string.Empty;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_clientId) &&
        !string.IsNullOrWhiteSpace(_clientSecret) &&
        _tokenValidator.AcceptsClientId(_clientId);

    public async Task<GoogleDesktopCodeExchangeResponse> ExchangeAsync(
        GoogleDesktopCodeExchangeRequest? request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var validated = ValidateRequest(request);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = validated.AuthorizationCode,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = validated.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = validated.CodeVerifier
        });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(TokenEndpoint, content, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AuthServiceException(
                "google_token_exchange_unavailable",
                "Google token exchange timed out. Please try again.");
        }
        catch (HttpRequestException)
        {
            throw new AuthServiceException(
                "google_token_exchange_unavailable",
                "Google token exchange is temporarily unavailable.");
        }

        using (response)
        {
            string json;
            try
            {
                json = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AuthServiceException(
                    "google_token_exchange_unavailable",
                    "Google token exchange timed out while reading the response.");
            }
            catch (HttpRequestException)
            {
                throw new AuthServiceException(
                    "google_token_exchange_unavailable",
                    "Google token exchange returned an unreadable response.");
            }

            GoogleTokenEndpointResponse? tokenResponse;
            try
            {
                tokenResponse = JsonSerializer.Deserialize<GoogleTokenEndpointResponse>(json);
            }
            catch (JsonException)
            {
                throw new AuthServiceException(
                    "google_token_exchange_unavailable",
                    "Google token exchange returned an invalid response.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AuthServiceException(
                    "google_token_exchange_failed",
                    BuildGoogleFailureMessage(response.StatusCode, tokenResponse));
            }

            var idToken = tokenResponse?.IdToken?.Trim();
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new AuthServiceException(
                    "google_token_exchange_unavailable",
                    "Google token exchange did not return an ID token.");
            }

            // Reject a wrong audience or invalid signature before relaying the token to Unity.
            await _tokenValidator.ValidateAsync(idToken, cancellationToken);
            return new GoogleDesktopCodeExchangeResponse(idToken);
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
        {
            throw new AuthServiceException(
                "google_desktop_exchange_not_configured",
                $"{ClientIdConfigurationKey} and {ClientSecretConfigurationKey} must both be set for Windows Google login.");
        }

        if (!_tokenValidator.AcceptsClientId(_clientId))
        {
            throw new AuthServiceException(
                "google_desktop_exchange_not_configured",
                $"The desktop client ID must also be included in {GoogleIdentityTokenValidator.ClientIdsConfigurationKey}.");
        }
    }

    private ValidatedRequest ValidateRequest(GoogleDesktopCodeExchangeRequest? request)
    {
        if (request == null)
        {
            throw InvalidRequest("A Google desktop authorization request is required.");
        }

        var requestClientId = request.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(requestClientId) || requestClientId.Length > MaximumClientIdLength)
        {
            throw InvalidRequest("A valid Google desktop client ID is required.");
        }

        if (!string.Equals(requestClientId, _clientId, StringComparison.Ordinal))
        {
            throw new AuthServiceException(
                "google_desktop_client_mismatch",
                "The Unity Google Desktop Client ID does not match the server configuration.");
        }

        var authorizationCode = request.AuthorizationCode?.Trim();
        if (string.IsNullOrWhiteSpace(authorizationCode) ||
            authorizationCode.Length > MaximumAuthorizationCodeLength)
        {
            throw InvalidRequest("A valid Google authorization code is required.");
        }

        var codeVerifier = request.CodeVerifier?.Trim();
        if (string.IsNullOrWhiteSpace(codeVerifier) ||
            codeVerifier.Length < MinimumCodeVerifierLength ||
            codeVerifier.Length > MaximumCodeVerifierLength ||
            codeVerifier.Any(character => !IsCodeVerifierCharacter(character)))
        {
            throw InvalidRequest("A valid PKCE code verifier is required.");
        }

        var redirectUriText = request.RedirectUri?.Trim();
        if (!Uri.TryCreate(redirectUriText, UriKind.Absolute, out var redirectUri) ||
            !string.Equals(redirectUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(redirectUri.Host, "127.0.0.1", StringComparison.Ordinal) ||
            redirectUri.IsDefaultPort ||
            redirectUri.Port <= 0 ||
            !string.Equals(redirectUri.AbsolutePath, "/", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(redirectUri.Query) ||
            !string.IsNullOrEmpty(redirectUri.Fragment) ||
            !string.IsNullOrEmpty(redirectUri.UserInfo))
        {
            throw InvalidRequest("The Google desktop redirect URI must use a random 127.0.0.1 loopback port.");
        }

        return new ValidatedRequest(
            authorizationCode,
            redirectUriText!,
            codeVerifier);
    }

    private static bool IsCodeVerifierCharacter(char value)
    {
        return char.IsAsciiLetterOrDigit(value) || value is '-' or '.' or '_' or '~';
    }

    private static AuthServiceException InvalidRequest(string message)
    {
        return new AuthServiceException("invalid_google_desktop_code_request", message);
    }

    private static string BuildGoogleFailureMessage(
        HttpStatusCode statusCode,
        GoogleTokenEndpointResponse? response)
    {
        var error = string.IsNullOrWhiteSpace(response?.Error)
            ? statusCode.ToString()
            : response.Error.Trim();
        var description = response?.ErrorDescription?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            return $"Google rejected the authorization code exchange ({error}). Please try again.";
        }

        if (description.Length > MaximumGoogleErrorDescriptionLength)
        {
            description = description[..MaximumGoogleErrorDescriptionLength];
        }

        return $"Google rejected the authorization code exchange ({error}): {description}";
    }

    private sealed record ValidatedRequest(
        string AuthorizationCode,
        string RedirectUri,
        string CodeVerifier);

    private sealed class GoogleTokenEndpointResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }
    }
}
