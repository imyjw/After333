using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Project333.Runtime.Presentation.Tutorial;

namespace Project333.Runtime.Application.Accounts
{
    internal static class AccountHttpTransport
    {
        internal const int TimeoutSeconds = 20;

        // Return HTTP responses unchanged so callers retain their API error handling.
        // Never retry here: a timed-out mutation may already have committed on the server.
        internal static async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TutorialRuntimeIsolation.ThrowIfActive();
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