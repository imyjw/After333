using UnityEngine;
using UnityEngine.Scripting;

namespace Project333.Runtime.Application.Accounts
{
    [Preserve]
    public sealed class GoogleAndroidCredentialCallbackReceiver : MonoBehaviour
    {
        [Preserve]
        public void OnGoogleIdTokenReceived(string payload)
        {
            GoogleAndroidCredentialBridge.HandleSuccess(payload);
        }

        [Preserve]
        public void OnGoogleSignInError(string payload)
        {
            GoogleAndroidCredentialBridge.HandleError(payload);
        }
    }
}
