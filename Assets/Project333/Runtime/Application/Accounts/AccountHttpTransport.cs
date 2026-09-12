using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

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
            request.timeout = TimeoutSeconds;
            var elapsed = Stopwatch.StartNew();
            try
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Bound the whole request even if native redirect/platform timeout behavior differs.
                    if (elapsed.Elapsed.TotalSeconds >= TimeoutSeconds)
                        throw new TimeoutException("서버 응답 시간이 초과되었습니다.");
                    await Task.Yield();
                }
                cancellationToken.ThrowIfCancellationRequested();
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
        }
    }
}