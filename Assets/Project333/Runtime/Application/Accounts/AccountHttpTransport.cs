using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Project333.Runtime.Presentation.Tutorial;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class AccountSessionReplacedException : InvalidOperationException
    {
        public AccountSessionReplacedException() : base(AccountSessionState.SessionReplacedMessage) { }
    }

    internal static class AccountHttpTransport
    {
        internal const int TimeoutSeconds = 20;

        internal static bool IsSessionReplacedResponse(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return false;
            try
            {
                var envelope = JsonUtility.FromJson<ApiErrorEnvelope>(body);
                // Unity may materialize an empty object for an absent serialized
                // class field. Test both wire spellings instead of a null fallback.
                return string.Equals(envelope?.Error?.ResolvedCode, AccountSessionState.SessionReplacedCode, StringComparison.Ordinal) ||
                       string.Equals(envelope?.error?.ResolvedCode, AccountSessionState.SessionReplacedCode, StringComparison.Ordinal);
            }
            catch (ArgumentException) { return false; }
        }

        // Return HTTP responses unchanged so callers retain their API error handling.
        // Never retry here: a timed-out mutation may already have committed on the server.
        internal static async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TutorialRuntimeIsolation.ThrowIfActive();
            var authorization = request.GetRequestHeader("Authorization");
            var sessionToken = authorization != null && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authorization.Substring(7) : string.Empty;
            string refreshToken = null;
            if (Uri.TryCreate(request.url, UriKind.Absolute, out var requestUri) &&
                requestUri.AbsolutePath == "/auth/refresh" && request.uploadHandler?.data != null)
            {
                var payload = JsonUtility.FromJson<RefreshAuthRequest>(Encoding.UTF8.GetString(request.uploadHandler.data));
                refreshToken = payload?.RefreshToken;
            }
            var wasCurrentCredential = AccountSessionState.IsCurrentCredential(request.url, sessionToken, refreshToken);
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Action<bool> tutorialChanged = active => { if (active) attempt.Cancel(); };
            TutorialRuntimeIsolation.Changed += tutorialChanged;
            request.timeout = TimeoutSeconds;
            var elapsed = Stopwatch.StartNew();
            try
            {
                TutorialRuntimeIsolation.ThrowIfActive();
                attempt.Token.ThrowIfCancellationRequested();
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    attempt.Token.ThrowIfCancellationRequested();
                    TutorialRuntimeIsolation.ThrowIfActive();
                    // Bound the whole request even if native redirect/platform timeout behavior differs.
                    if (elapsed.Elapsed.TotalSeconds >= TimeoutSeconds)
                        throw new TimeoutException("서버 응답 시간이 초과되었습니다.");
                    await Task.Yield();
                }
                attempt.Token.ThrowIfCancellationRequested();
                TutorialRuntimeIsolation.ThrowIfActive();
                if (request.responseCode == 401 && IsSessionReplacedResponse(request.downloadHandler?.text))
                {
                    if (AccountSessionState.TryHandleSessionReplacement(request.url, sessionToken, refreshToken))
                        throw new AccountSessionReplacedException();
                    throw new OperationCanceledException("A superseded account request was discarded.");
                }
                // Never let an old successful /me, refresh or purchase response rehydrate a replaced login.
                if (wasCurrentCredential && !AccountSessionState.IsCurrentCredential(request.url, sessionToken, refreshToken))
                    throw new OperationCanceledException("The account or server changed while the request was in progress.");
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            catch (TimeoutException)
            {
                request.Abort();
                throw;
            }
            finally { TutorialRuntimeIsolation.Changed -= tutorialChanged; }
        }
    }
}