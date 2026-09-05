# Mobile Secure Credential Storage

## Purpose

After333 supports automatic login by retaining account credentials between launches. Raw login credentials must not be treated like ordinary UI settings on mobile devices.

This document defines the implemented client-side storage boundary and the remaining real-device checks.

## Stored Credentials

The following values pass through `AccountCredentialStore`:

- `sessionToken`: short-lived API access credential
- `refreshToken`: longer-lived rotating login credential

Passwords and Google ID tokens are not persisted by this store.

Client-profile scoping remains part of every key. This preserves the existing Player A/Player B development profiles without changing server account identity.

## Platform Behavior

The locked Android application identifier is `com.after333.game`. Android app updates, Keystore data, and future Google OAuth registration must continue to use this exact identifier.

| Runtime | Storage | Security behavior |
| --- | --- | --- |
| Android player | Android Keystore plus app-private SharedPreferences | A 256-bit AES key is generated inside Android Keystore. Each credential is encrypted independently with AES-GCM, a random 12-byte IV, and the scoped credential key as authenticated additional data. SharedPreferences contains only the versioned encrypted payload. |
| iOS player | iOS Keychain generic-password item | Each credential is stored under the After333 service using `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`. |
| Unity Editor | Scoped PlayerPrefs | Development fallback so local dual-client testing continues to work. Not a mobile-release security guarantee. |
| Windows development player | Scoped PlayerPrefs | Current development fallback. A production Windows credential vault can be added later if required. |

## Legacy Migration

Older builds stored account credentials directly in scoped PlayerPrefs.

The obsolete `guestToken` is deleted during credential loading. If the saved account kind is `guest`, its saved session and refresh credentials are also cleared so the mandatory login gate is shown.

On Android or iOS:

1. Secure storage is checked first.
2. If no secure value exists, the matching legacy PlayerPrefs key is checked.
3. The legacy value is written to Keystore-backed storage or Keychain.
4. The PlayerPrefs copy is deleted only when that secure write succeeds.
5. If the secure write fails, the legacy value remains available so the user is not unexpectedly logged out, and an error is logged without printing the credential.

New mobile credentials are never intentionally written back to PlayerPrefs.

## Login Gate

- A saved session is restored first.
- If needed, Unity rotates the saved refresh token and restores the same account.
- Android may then attempt automatic Google sign-in when the existing safety rules allow it.
- If no valid registered account can be restored, a non-dismissible login gate blocks all game features until Game ID or Google login succeeds.
- The current UI intentionally exposes no logout or account-switch action.

## Source Files

- `Assets/Project333/Runtime/Application/Accounts/AccountCredentialStore.cs`
- `Assets/Project333/Runtime/Application/Accounts/AccountSessionState.cs`
- `Assets/Plugins/Android/After333SecureCredentialStore.java`
- `Assets/Plugins/iOS/After333SecureCredentialStore.mm`
- `Assets/Project333/Editor/After333SecureStorageBuildProcessor.cs`
- `Assets/Project333/Tests/EditMode/AccountCredentialStoreTests.cs`

The iOS post-build processor adds `Security.framework` automatically. Android uses the Java source plugin under `Assets/Plugins/Android` and requires no scene reference.

## Android Real-Device Smoke Test

1. Install an older or temporary development build that stores a valid login in PlayerPrefs.
2. Install the new APK over it without clearing app data.
3. Launch After333 and verify that the same account restores automatically.
4. Close the app completely and launch it again; the same account should still restore.
5. Log out, close the app, and launch it again; the login gate should appear.
6. Log in again, let the access token refresh, and verify `/me` and PvP matchmaking still work.
7. Confirm that logs never print raw guest, session, refresh, password, or provider tokens.

## iOS Real-Device Smoke Test

The same login, relaunch, refresh, and logout flow must be tested on a signed iOS device build produced on macOS/Xcode. Windows can compile the C# bridge but cannot perform the final Objective-C++ link or device Keychain test.

## Current Verification

- Unity Editor runtime assembly compilation: passed
- Windows player runtime assembly compilation: passed
- EditMode test assembly compilation: passed
- Android C# bridge compilation: passed
- Android Java plugin compilation against Unity's installed Android SDK: passed
- iOS C# bridge compilation: passed
- Android/iOS real-device storage test: pending
