using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    public interface IGoogleIdTokenProvider
    {
        bool IsSupported { get; }
        string UnavailableReason { get; }
        Task<string> AcquireIdTokenAsync(CancellationToken cancellationToken);
    }

    public interface IAutomaticGoogleIdTokenProvider
    {
        Task<string> AcquireAutomaticIdTokenAsync(CancellationToken cancellationToken);
    }

    public sealed class GoogleDesktopOAuthIdTokenProvider : IGoogleIdTokenProvider
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const int MaximumCallbackHeaderBytes = 16 * 1024;
        private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(3);

        private readonly string _clientId;
        private readonly string _accountServerUrl;

        public GoogleDesktopOAuthIdTokenProvider(string clientId, string accountServerUrl)
        {
            _clientId = clientId?.Trim() ?? string.Empty;
            _accountServerUrl = accountServerUrl?.Trim() ?? string.Empty;
        }

        public static bool IsCurrentPlatformSupported
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsSupported => IsCurrentPlatformSupported;

        public string UnavailableReason => IsSupported
            ? string.Empty
            : "Google Desktop OAuth is available only in the Windows Editor or a Windows standalone build.";

        public async Task<string> AcquireIdTokenAsync(CancellationToken cancellationToken)
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException(UnavailableReason);
            }

            if (string.IsNullOrWhiteSpace(_clientId))
            {
                throw new InvalidOperationException("Google Desktop OAuth client ID is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_accountServerUrl))
            {
                throw new InvalidOperationException("After333 account server URL is not configured.");
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(SignInTimeout);

            try
            {
                return await AcquireIdTokenCoreAsync(timeoutCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Google login timed out. Please try again.");
            }
        }

        private async Task<string> AcquireIdTokenCoreAsync(CancellationToken cancellationToken)
        {
            var state = CreateRandomBase64Url(32);
            var nonce = CreateRandomBase64Url(32);
            var codeVerifier = CreateRandomBase64Url(64);
            var codeChallenge = CreateCodeChallenge(codeVerifier);

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var redirectUri = $"http://127.0.0.1:{port}/";
            var authorizationUrl = BuildAuthorizationUrl(
                redirectUri,
                state,
                nonce,
                codeChallenge);

            UnityEngine.Application.OpenURL(authorizationUrl);

            try
            {
                TcpClient callbackClient;
                using (cancellationToken.Register(listener.Stop))
                {
                    try
                    {
                        callbackClient = await listener.AcceptTcpClientAsync();
                    }
                    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }
                    catch (SocketException) when (cancellationToken.IsCancellationRequested)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }
                }

                using (callbackClient)
                using (var stream = callbackClient.GetStream())
                {
                    var requestTarget = await ReadRequestTargetAsync(stream, cancellationToken);
                    var query = ParseQuery(requestTarget);

                    if (!query.TryGetValue("state", out var returnedState) ||
                        !string.Equals(state, returnedState, StringComparison.Ordinal))
                    {
                        await TryWriteBrowserResponseAsync(stream, false, "로그인 요청을 확인할 수 없습니다.");
                        throw new InvalidOperationException("Google OAuth state validation failed.");
                    }

                    if (query.TryGetValue("error", out var oauthError))
                    {
                        await TryWriteBrowserResponseAsync(stream, false, "Google 로그인이 취소되었습니다.");
                        throw new InvalidOperationException($"Google login failed: {oauthError}");
                    }

                    if (!query.TryGetValue("code", out var authorizationCode) ||
                        string.IsNullOrWhiteSpace(authorizationCode))
                    {
                        await TryWriteBrowserResponseAsync(stream, false, "Google 인증 코드가 없습니다.");
                        throw new InvalidOperationException("Google OAuth callback did not include an authorization code.");
                    }

                    try
                    {
                        var idToken = await ExchangeCodeForIdTokenAsync(
                            authorizationCode,
                            redirectUri,
                            codeVerifier,
                            cancellationToken);
                        ValidateNonce(idToken, nonce);
                        await TryWriteBrowserResponseAsync(
                            stream,
                            true,
                            "Google 인증이 완료되었습니다. After333으로 돌아가세요.");
                        return idToken;
                    }
                    catch
                    {
                        await TryWriteBrowserResponseAsync(
                            stream,
                            false,
                            "Google 인증을 완료하지 못했습니다. After333에서 다시 시도해주세요.");
                        throw;
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private string BuildAuthorizationUrl(
            string redirectUri,
            string state,
            string nonce,
            string codeChallenge)
        {
            var query = new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["prompt"] = "select_account"
            };

            var builder = new StringBuilder(AuthorizationEndpoint);
            builder.Append('?');
            var isFirst = true;
            foreach (var pair in query)
            {
                if (!isFirst)
                {
                    builder.Append('&');
                }

                isFirst = false;
                builder.Append(Uri.EscapeDataString(pair.Key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(pair.Value));
            }

            return builder.ToString();
        }

        private async Task<string> ExchangeCodeForIdTokenAsync(
            string authorizationCode,
            string redirectUri,
            string codeVerifier,
            CancellationToken cancellationToken)
        {
            var client = new GuestAuthClient(_accountServerUrl);
            return await client.ExchangeGoogleDesktopCodeAsync(
                _clientId,
                authorizationCode,
                redirectUri,
                codeVerifier,
                cancellationToken);
        }

        private static async Task<string> ReadRequestTargetAsync(
            NetworkStream stream,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[MaximumCallbackHeaderBytes];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer,
                    length,
                    buffer.Length - length,
                    cancellationToken);
                if (read <= 0)
                {
                    break;
                }

                length += read;
                if (ContainsHeaderTerminator(buffer, length))
                {
                    break;
                }
            }

            var header = Encoding.ASCII.GetString(buffer, 0, length);
            var firstLineEnd = header.IndexOf("\r\n", StringComparison.Ordinal);
            var requestLine = firstLineEnd >= 0 ? header.Substring(0, firstLineEnd) : header;
            var parts = requestLine.Split(' ');
            if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Google OAuth callback request was invalid.");
            }

            return parts[1];
        }

        private static bool ContainsHeaderTerminator(byte[] buffer, int length)
        {
            for (var i = 3; i < length; i++)
            {
                if (buffer[i - 3] == '\r' &&
                    buffer[i - 2] == '\n' &&
                    buffer[i - 1] == '\r' &&
                    buffer[i] == '\n')
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, string> ParseQuery(string requestTarget)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var callbackUri = new Uri($"http://127.0.0.1{requestTarget}");
            var query = callbackUri.Query.TrimStart('?');
            if (string.IsNullOrWhiteSpace(query))
            {
                return result;
            }

            var pairs = query.Split('&');
            for (var i = 0; i < pairs.Length; i++)
            {
                var separatorIndex = pairs[i].IndexOf('=');
                var rawKey = separatorIndex >= 0 ? pairs[i].Substring(0, separatorIndex) : pairs[i];
                var rawValue = separatorIndex >= 0 ? pairs[i].Substring(separatorIndex + 1) : string.Empty;
                var key = Uri.UnescapeDataString(rawKey.Replace("+", " "));
                var value = Uri.UnescapeDataString(rawValue.Replace("+", " "));
                result[key] = value;
            }

            return result;
        }

        private static async Task TryWriteBrowserResponseAsync(
            NetworkStream stream,
            bool success,
            string message)
        {
            try
            {
                var title = success ? "After333 Google Login" : "After333 Google Login Error";
                var color = success ? "#22c55e" : "#ef4444";
                var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>{title}</title></head><body style=\"margin:0;background:#07111f;color:#eef6ff;font-family:sans-serif;display:grid;place-items:center;min-height:100vh\"><main style=\"text-align:center;padding:40px\"><h1 style=\"color:{color}\">{title}</h1><p>{message}</p></main></body></html>";
                var body = Encoding.UTF8.GetBytes(html);
                var header = Encoding.ASCII.GetBytes(
                    "HTTP/1.1 200 OK\r\n" +
                    "Content-Type: text/html; charset=utf-8\r\n" +
                    $"Content-Length: {body.Length}\r\n" +
                    "Connection: close\r\n\r\n");
                await stream.WriteAsync(header, 0, header.Length);
                await stream.WriteAsync(body, 0, body.Length);
                await stream.FlushAsync();
            }
            catch
            {
                // The browser may close early; authentication still continues safely.
            }
        }

        private static void ValidateNonce(string idToken, string expectedNonce)
        {
            var segments = idToken.Split('.');
            if (segments.Length != 3)
            {
                throw new InvalidOperationException("Google ID token format was invalid.");
            }

            var payloadJson = Encoding.UTF8.GetString(DecodeBase64Url(segments[1]));
            var payload = JsonUtility.FromJson<GoogleIdTokenPayload>(payloadJson);
            if (payload == null ||
                string.IsNullOrWhiteSpace(payload.nonce) ||
                !string.Equals(payload.nonce, expectedNonce, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Google ID token nonce validation failed.");
            }
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            using var sha256 = SHA256.Create();
            return EncodeBase64Url(sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        private static string CreateRandomBase64Url(int byteLength)
        {
            var bytes = new byte[byteLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return EncodeBase64Url(bytes);
        }

        private static string EncodeBase64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static byte[] DecodeBase64Url(string value)
        {
            var normalized = value.Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2:
                    normalized += "==";
                    break;
                case 3:
                    normalized += "=";
                    break;
            }

            return Convert.FromBase64String(normalized);
        }

        [Serializable]
        private sealed class GoogleIdTokenPayload
        {
            public string nonce;
        }
    }
}
