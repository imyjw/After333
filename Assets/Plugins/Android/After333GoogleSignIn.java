package com.after333.auth;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executor;

public final class After333GoogleSignIn {
    private static final Map<String, CancellationSignal> PENDING_REQUESTS =
        new ConcurrentHashMap<>();

    private After333GoogleSignIn() {
    }

    public static void signIn(
        String serverClientId,
        String nonce,
        String requestId,
        String callbackObjectName) {
        startRequest(
            serverClientId,
            nonce,
            requestId,
            callbackObjectName,
            false);
    }

    public static void signInAutomatic(
        String serverClientId,
        String nonce,
        String requestId,
        String callbackObjectName) {
        startRequest(
            serverClientId,
            nonce,
            requestId,
            callbackObjectName,
            true);
    }

    private static void startRequest(
        String serverClientId,
        String nonce,
        String requestId,
        String callbackObjectName,
        boolean automatic) {
        if (isBlank(serverClientId) || isBlank(nonce) ||
            isBlank(requestId) || isBlank(callbackObjectName)) {
            sendError(callbackObjectName, requestId, "configuration", "Missing Google login configuration.");
            return;
        }

        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            sendError(callbackObjectName, requestId, "configuration", "Unity activity is unavailable.");
            return;
        }

        activity.runOnUiThread(() -> startSignIn(
            activity,
            serverClientId,
            nonce,
            requestId,
            callbackObjectName,
            automatic));
    }

    public static void cancel(String requestId) {
        if (isBlank(requestId)) {
            return;
        }

        CancellationSignal signal = PENDING_REQUESTS.remove(requestId);
        if (signal != null) {
            signal.cancel();
        }
    }

    private static void startSignIn(
        Activity activity,
        String serverClientId,
        String nonce,
        String requestId,
        String callbackObjectName,
        boolean automatic) {
        try {
            GetCredentialRequest.Builder requestBuilder = new GetCredentialRequest.Builder();
            if (automatic) {
                GetGoogleIdOption googleOption = new GetGoogleIdOption.Builder()
                    .setFilterByAuthorizedAccounts(true)
                    .setAutoSelectEnabled(true)
                    .setServerClientId(serverClientId)
                    .setNonce(nonce)
                    .build();
                requestBuilder.addCredentialOption(googleOption);
            } else {
                GetSignInWithGoogleOption googleOption =
                    new GetSignInWithGoogleOption.Builder(serverClientId)
                        .setNonce(nonce)
                        .build();
                requestBuilder.addCredentialOption(googleOption);
            }

            GetCredentialRequest request = requestBuilder.build();
            CredentialManager credentialManager = CredentialManager.create(activity);
            CancellationSignal cancellationSignal = new CancellationSignal();
            PENDING_REQUESTS.put(requestId, cancellationSignal);
            Executor mainExecutor = command -> activity.runOnUiThread(command);

            credentialManager.getCredentialAsync(
                activity,
                request,
                cancellationSignal,
                mainExecutor,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse result) {
                        PENDING_REQUESTS.remove(requestId);
                        handleCredential(result, requestId, callbackObjectName);
                    }

                    @Override
                    public void onError(GetCredentialException exception) {
                        PENDING_REQUESTS.remove(requestId);
                        sendCredentialError(callbackObjectName, requestId, exception);
                    }
                });
        } catch (Throwable throwable) {
            PENDING_REQUESTS.remove(requestId);
            sendError(
                callbackObjectName,
                requestId,
                "configuration",
                safeMessage(throwable));
        }
    }

    private static void handleCredential(
        GetCredentialResponse response,
        String requestId,
        String callbackObjectName) {
        try {
            Credential credential = response.getCredential();
            if (!(credential instanceof CustomCredential) ||
                !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                sendError(
                    callbackObjectName,
                    requestId,
                    "unsupported_credential",
                    "Credential Manager returned an unsupported credential type.");
                return;
            }

            GoogleIdTokenCredential googleCredential =
                GoogleIdTokenCredential.createFrom(credential.getData());
            String idToken = googleCredential.getIdToken();
            if (isBlank(idToken)) {
                sendError(
                    callbackObjectName,
                    requestId,
                    "empty_token",
                    "Google Credential Manager returned an empty ID token.");
                return;
            }

            UnityPlayer.UnitySendMessage(
                callbackObjectName,
                "OnGoogleIdTokenReceived",
                requestId + "\n" + idToken);
        } catch (Throwable throwable) {
            sendError(
                callbackObjectName,
                requestId,
                "token_parse",
                safeMessage(throwable));
        }
    }

    private static void sendCredentialError(
        String callbackObjectName,
        String requestId,
        GetCredentialException exception) {
        String className = exception.getClass().getSimpleName();
        String code;
        if (className.contains("Cancellation")) {
            code = "cancelled";
        } else if (className.contains("NoCredential")) {
            code = "no_credential";
        } else if (className.contains("ProviderConfiguration")) {
            code = "provider_configuration";
        } else if (className.contains("Unsupported")) {
            code = "unsupported";
        } else {
            code = "credential_error";
        }

        sendError(callbackObjectName, requestId, code, safeMessage(exception));
    }

    private static void sendError(
        String callbackObjectName,
        String requestId,
        String code,
        String message) {
        if (isBlank(callbackObjectName)) {
            return;
        }

        UnityPlayer.UnitySendMessage(
            callbackObjectName,
            "OnGoogleSignInError",
            safeValue(requestId) + "\n" + safeValue(code) + "\n" + safeValue(message));
    }

    private static String safeMessage(Throwable throwable) {
        if (throwable == null) {
            return "Unknown Android Google login error.";
        }

        String message = throwable.getMessage();
        return isBlank(message) ? throwable.getClass().getSimpleName() : message;
    }

    private static String safeValue(String value) {
        return value == null ? "" : value;
    }

    private static boolean isBlank(String value) {
        return value == null || value.trim().isEmpty();
    }
}
