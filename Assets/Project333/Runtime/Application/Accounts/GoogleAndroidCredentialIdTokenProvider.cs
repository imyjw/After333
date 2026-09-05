using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Project333.Runtime.Application.Accounts
{
    public sealed class GoogleAndroidCredentialIdTokenProvider :
        IGoogleIdTokenProvider,
        IAutomaticGoogleIdTokenProvider
    {
        private static readonly TimeSpan SignInTimeout = TimeSpan.FromMinutes(3);
        private readonly string _serverClientId;

        public GoogleAndroidCredentialIdTokenProvider(string serverClientId)
        {
            _serverClientId = serverClientId?.Trim() ?? string.Empty;
        }

        public static bool IsCurrentPlatformSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public bool IsSupported => IsCurrentPlatformSupported;

        public string UnavailableReason => IsSupported
            ? string.Empty
            : "Google Credential Manager is available only in an Android player build.";

        public async Task<string> AcquireIdTokenAsync(CancellationToken cancellationToken)
        {
            return await AcquireIdTokenAsync(false, cancellationToken);
        }

        public async Task<string> AcquireAutomaticIdTokenAsync(CancellationToken cancellationToken)
        {
            return await AcquireIdTokenAsync(true, cancellationToken);
        }

        private async Task<string> AcquireIdTokenAsync(
            bool automatic,
            CancellationToken cancellationToken)
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException(UnavailableReason);
            }

            if (string.IsNullOrWhiteSpace(_serverClientId))
            {
                throw new InvalidOperationException("Google Android server client ID is not configured.");
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(SignInTimeout);
            var nonce = CreateRandomBase64Url(32);

            try
            {
                var idToken = await GoogleAndroidCredentialBridge.AcquireIdTokenAsync(
                    _serverClientId,
                    nonce,
                    automatic,
                    timeoutCancellation.Token);
                ValidateNonce(idToken, nonce);
                return idToken;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Google login timed out. Please try again.");
            }
        }

        private static string CreateRandomBase64Url(int byteLength)
        {
            var bytes = new byte[byteLength];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static void ValidateNonce(string idToken, string expectedNonce)
        {
            var segments = idToken?.Split('.');
            if (segments == null || segments.Length != 3)
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

    internal static class GoogleAndroidCredentialBridge
    {
        private const string PluginClassName = "com.after333.auth.After333GoogleSignIn";
        private const string CallbackObjectName = "__After333GoogleAuthCallback";
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, PendingRequest> PendingRequests =
            new Dictionary<string, PendingRequest>(StringComparer.Ordinal);
        private static GoogleAndroidCredentialCallbackReceiver _receiver;

        public static Task<string> AcquireIdTokenAsync(
            string serverClientId,
            string nonce,
            bool automatic,
            CancellationToken cancellationToken)
        {
#if !UNITY_ANDROID || UNITY_EDITOR
            throw new PlatformNotSupportedException(
                "Google Credential Manager is available only in an Android player build.");
#else
            cancellationToken.ThrowIfCancellationRequested();
            EnsureReceiver();

            var requestId = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = new PendingRequest(completion);
            lock (Sync)
            {
                PendingRequests.Add(requestId, pending);
            }

            pending.CancellationRegistration = cancellationToken.Register(
                () => CancelRequest(requestId));

            if (cancellationToken.IsCancellationRequested || !IsPending(requestId))
            {
                return completion.Task;
            }

            try
            {
                using var plugin = new AndroidJavaClass(PluginClassName);
                plugin.CallStatic(
                    automatic ? "signInAutomatic" : "signIn",
                    serverClientId,
                    nonce,
                    requestId,
                    CallbackObjectName);
            }
            catch (Exception exception)
            {
                CompleteWithException(
                    requestId,
                    new InvalidOperationException(
                        "Android Google login plugin could not be started. Rebuild the Android player with the Credential Manager dependencies.",
                        exception));
            }

            return completion.Task;
#endif
        }

        public static void HandleSuccess(string payload)
        {
            var parts = SplitPayload(payload, 2);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                if (parts.Length > 0)
                {
                    CompleteWithException(
                        parts[0],
                        new InvalidOperationException("Google Credential Manager returned an empty ID token."));
                }

                return;
            }

            var pending = RemovePending(parts[0]);
            if (pending == null)
            {
                return;
            }

            pending.CancellationRegistration.Dispose();
            pending.Completion.TrySetResult(parts[1]);
        }

        public static void HandleError(string payload)
        {
            var parts = SplitPayload(payload, 3);
            if (parts.Length == 0)
            {
                return;
            }

            var code = parts.Length > 1 ? parts[1] : "unknown";
            var nativeMessage = parts.Length > 2 ? parts[2] : string.Empty;
            CompleteWithException(parts[0], CreateException(code, nativeMessage));
        }

        private static Exception CreateException(string code, string nativeMessage)
        {
            switch (code)
            {
                case "cancelled":
                    return new InvalidOperationException("Google 로그인이 취소되었습니다.");
                case "no_credential":
                    return new InvalidOperationException(
                        "기기에서 사용할 수 있는 Google 계정을 찾지 못했습니다.");
                case "provider_configuration":
                    return new InvalidOperationException(
                        "Google 로그인 설정을 확인해주세요. Android OAuth 클라이언트의 패키지 이름과 SHA 인증서가 현재 앱과 일치해야 합니다.");
                case "unsupported":
                    return new PlatformNotSupportedException(
                        "이 Android 기기에서는 Google Credential Manager를 사용할 수 없습니다.");
                default:
                    return new InvalidOperationException(
                        string.IsNullOrWhiteSpace(nativeMessage)
                            ? "Google 로그인 중 알 수 없는 오류가 발생했습니다."
                            : $"Google 로그인 오류: {nativeMessage}");
            }
        }

        private static string[] SplitPayload(string payload, int count)
        {
            return (payload ?? string.Empty).Split(
                new[] { '\n' },
                count,
                StringSplitOptions.None);
        }

        private static void EnsureReceiver()
        {
            if (_receiver != null)
            {
                return;
            }

            var receiverObject = GameObject.Find(CallbackObjectName);
            if (receiverObject == null)
            {
                receiverObject = new GameObject(CallbackObjectName);
                UnityEngine.Object.DontDestroyOnLoad(receiverObject);
            }

            _receiver = receiverObject.GetComponent<GoogleAndroidCredentialCallbackReceiver>();
            if (_receiver == null)
            {
                _receiver = receiverObject.AddComponent<GoogleAndroidCredentialCallbackReceiver>();
            }
        }

        private static void CancelRequest(string requestId)
        {
            var pending = RemovePending(requestId);
            if (pending == null)
            {
                return;
            }

            try
            {
                using var plugin = new AndroidJavaClass(PluginClassName);
                plugin.CallStatic("cancel", requestId);
            }
            catch
            {
                // Cancellation must still complete even if the Android activity is shutting down.
            }

            pending.Completion.TrySetCanceled();
        }

        private static void CompleteWithException(string requestId, Exception exception)
        {
            var pending = RemovePending(requestId);
            if (pending == null)
            {
                return;
            }

            pending.CancellationRegistration.Dispose();
            pending.Completion.TrySetException(exception);
        }

        private static PendingRequest RemovePending(string requestId)
        {
            lock (Sync)
            {
                if (string.IsNullOrWhiteSpace(requestId) ||
                    !PendingRequests.TryGetValue(requestId, out var pending))
                {
                    return null;
                }

                PendingRequests.Remove(requestId);
                return pending;
            }
        }

        private static bool IsPending(string requestId)
        {
            lock (Sync)
            {
                return !string.IsNullOrWhiteSpace(requestId) &&
                    PendingRequests.ContainsKey(requestId);
            }
        }

        private sealed class PendingRequest
        {
            public PendingRequest(TaskCompletionSource<string> completion)
            {
                Completion = completion;
            }

            public TaskCompletionSource<string> Completion { get; }
            public CancellationTokenRegistration CancellationRegistration { get; set; }
        }
    }

}
