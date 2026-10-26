# After333 Google Login Setup

**2026-10-20 운영 기록 갱신:** 서울 Lightsail + Caddy + PostgreSQL18로 이전했고 공개 주소는 `https://api.after333.com`, 운영DB는 `project333_live`다. 사용자 전환 결과에서 migration24개/latest`0024_elf_elder_card_id`를 확인했다. 아래 과거 날짜의 노트북/Tunnel·DB버전·미배포 문구는 당시 기록이다. 현재 실행·백업 절차는 [Lightsail 운영](lightsail_operations.md), 확인 범위와 남은 시험은 [현재 상태](current_project_status.md)를 우선한다. 오늘 원격 재확인을 수행하지 않았다.

## Current public authentication boundary (2026-10-01)

Current routes support Game ID registration/login, Google login, saved-session refresh and device logout. Guest creation/resume and public account-link endpoints are not exposed. Legacy link helper methods and serialized button fields are implementation history, not a supported setup path. The start-scene settings menu provides confirmed logout and the mandatory login gate.

This review compares source only. It does not verify the running server's OAuth client IDs/secrets, Google Console registration, token exchange or a device login. The platform configuration and remaining live checks below stay release prerequisites.

## What Is Implemented

The server, Windows Unity client, and Android Unity client now share one account flow.

Windows Editor and Windows player builds:

1. Unity opens the system browser, never an embedded WebView.
2. The client listens only on a random `127.0.0.1` port and validates OAuth `state`, PKCE, and the OpenID Connect nonce.
3. Unity sends the one-time authorization code, loopback redirect URI, and PKCE verifier to the After333 server over HTTPS.
4. The After333 server exchanges the code with Google using the Desktop OAuth client credentials.
5. Unity validates the nonce in the returned ID token before continuing.

Android player builds:

1. Unity opens Android Credential Manager from the explicit Google login button.
2. The native bridge uses `GetSignInWithGoogleOption` and a cryptographically random nonce.
3. Unity validates the nonce in the returned ID token before sending it to After333.

Both platforms then:

1. Send the ID token to `POST /auth/google` over HTTPS.
2. Let the After333 server verify the token signature, issuer, expiration, and audience.
3. Use Google's stable `sub` value as the login identity.
4. Log an existing identity into its After333 account or create a registered account for a new identity.
5. Apply only the account returned by verified login. Existing account identities are not automatically merged, and public identity linking is not exposed.

The raw Google authorization code, PKCE verifier, client secret, and ID token are never written to PostgreSQL or audit logs. Email is profile data only and is never used as the account key.

## OAuth Prerequisites for a New Environment (existing production setup completed)

A new environment requires the following OAuth setup. Production setup was transferred during the Lightsail migration; Android production login and Windows Unity Editor Play login were subsequently confirmed. Do not recreate existing clients merely because these setup instructions remain here:

1. Create or select a Google Cloud project for After333.
2. Configure the OAuth consent screen and test users.
3. Create a `Desktop app` OAuth client for Windows testing and obtain its client ID and client secret.
4. Create a `Web application` OAuth client that represents the After333 server. Android uses this value as the ID-token audience.
5. Create an `Android` OAuth client with package name `com.after333.game` and the SHA fingerprint of the signing certificate.
6. Configure the matching client IDs in Unity and `PROJECT333_GOOGLE_CLIENT_IDS`.
7. Configure the Desktop client ID and secret only on the After333 server.

Google currently requires the generated Desktop client secret during this project's authorization-code exchange. Never paste that secret into Unity, a scene, source code, documentation, or a committed script. Keep it only in the server process environment. Desktop-app client secrets are not a strong confidentiality boundary in a general OAuth installed-app model, but After333 still avoids shipping this value in game builds.

## Google Cloud Console Setup

1. Open [Google Auth Platform](https://console.cloud.google.com/auth/overview) and create or select the After333 project.
2. Open **Branding** and set the app name to `After333`, then set the support email and developer contact email.
3. Open **Audience**, choose `External`, keep the app in `Testing`, and add every Google account that will test the game.
4. Request only `openid`, email, and basic profile access. Do not add Drive, Calendar, or unrelated scopes.
5. In **Clients**, create a **Desktop app** client named `After333 Windows Demo`.
6. Create a **Web application** client named `After333 Server`. No browser redirect URI is required for the Android ID-token flow.
7. Create an **Android** client named `After333 Android`, set package name to `com.after333.game`, and enter the SHA-1 fingerprint for the keystore used to sign the build.
8. Add a second Android client when debug and release builds use different signing certificates.

Testing mode allows only accounts listed as test users.

The Android OAuth client identifies the installed APK by package name and certificate. Do not paste that Android client ID into Unity. The Android Credential Manager request must use the **Web application client ID** so the server can verify the ID-token audience.

## Unity Scene Configuration

1. Let Unity finish recompiling.
2. Open `GameStart_VSlice`.
3. Select the object containing `GameStartSceneController`.
4. In **Google Auth**, paste the Desktop app client ID into **Google Desktop Client Id**.
5. Paste the Web application client ID into **Google Android Server Client Id**.
6. Configure `GoogleAuthPanel` and `LoginButton` before Play mode. A retained `LinkButton` field does not enable a public account-link route.

The custom Android Gradle template includes:

- `androidx.credentials:credentials:1.6.0`
- `androidx.credentials:credentials-play-services-auth:1.6.0`
- `com.google.android.libraries.identity.googleid:googleid:1.2.0`

Do not disable **Custom Main Gradle Template** unless those dependencies are moved to another dependency-management system.

## Server Configuration

One accepted client ID:

```powershell
$env:PROJECT333_GOOGLE_CLIENT_IDS='YOUR_CLIENT_ID.apps.googleusercontent.com'
```

Multiple accepted audiences, for example the Desktop app client ID and the Android flow's Web application client ID:

```powershell
$env:PROJECT333_GOOGLE_CLIENT_IDS='DESKTOP_CLIENT_ID.apps.googleusercontent.com,WEB_SERVER_CLIENT_ID.apps.googleusercontent.com'
```

Windows authorization-code exchange settings:

```powershell
$env:PROJECT333_GOOGLE_DESKTOP_CLIENT_ID='DESKTOP_CLIENT_ID.apps.googleusercontent.com'
$env:PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET='DESKTOP_CLIENT_SECRET'
```

`PROJECT333_GOOGLE_DESKTOP_CLIENT_ID` must be the same Desktop client ID entered in the Unity scene and must also be present in `PROJECT333_GOOGLE_CLIENT_IDS`. The secret must match that exact Desktop client. Do not print or commit it.

The local launcher accepts the setting directly:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Local.ps1 `
  -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password' `
  -GoogleClientIds 'DESKTOP_CLIENT_ID.apps.googleusercontent.com,WEB_SERVER_CLIENT_ID.apps.googleusercontent.com'
```

The launcher reads the Desktop ID and secret from the two environment variables above. Restart the server after changing any Google setting.

## Readiness Check

Open:

```text
http://127.0.0.1:7333/server/status
```

The expected section after configuration is:

```json
"Authentication": {
  "Google": {
    "Configured": true,
    "AcceptedClientIdCount": 2,
    "DesktopCodeExchangeConfigured": true
  }
}
```

The server intentionally reports only the count, not the configured client ID strings.

The same readiness check is available as a script:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestGoogleAuthReadiness.ps1 -RequireDesktopCodeExchange
```

For server-only diagnostics, a real ID token can be passed through a temporary PowerShell variable rather than written into a file:

```powershell
$googleIdToken='TEMPORARY_REAL_ID_TOKEN'
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestGoogleAuthReadiness.ps1 -IdToken $googleIdToken
Remove-Variable googleIdToken
```

## API Contracts

Windows only, exchange the browser authorization code through the After333 server:

```http
POST /auth/google/desktop-token
Content-Type: application/json

{
  "clientId": "DESKTOP_CLIENT_ID.apps.googleusercontent.com",
  "authorizationCode": "ONE_TIME_CODE",
  "redirectUri": "http://127.0.0.1:RANDOM_PORT/",
  "codeVerifier": "PKCE_VERIFIER"
}
```

The response contains an ID token only for the immediate login flow. The endpoint accepts only the exact configured Desktop client ID and a `127.0.0.1` loopback redirect URI.

Google login or first-time account creation:

```http
POST /auth/google
Content-Type: application/json

{
  "idToken": "GOOGLE_ID_TOKEN",
  "clientVersion": "0.1.0-dev"
}
```

Guest authentication and `POST /auth/link-google` / `POST /auth/link-game-id` are unavailable in the current public server. Do not use the older link-readiness tool as a required passing login check. Provider identity verification uses Google's stable `sub`; email alone never merges accounts. A separate identity-link product flow would require a new confirmed decision and implementation.

## Android Build And Smoke Test

1. Confirm the Android package name is `com.after333.game`.
2. Confirm the Google Cloud Android client contains the SHA fingerprint for the exact keystore that signs this build.
3. Confirm **Google Android Server Client Id** contains the Web application client ID.
4. Start the server with that Web client ID included in `PROJECT333_GOOGLE_CLIENT_IDS`.
5. Build and install on a device with Google Play services.
6. Press **Google login**, choose a test-user account, and confirm the same After333 account is restored after restarting the app.
7. From the logged-in start scene, confirm logout in the settings menu, restart, and verify the login gate appears. Log in again and confirm server cards, gold, tickets, deck and run remain intact.
8. Verify the same Google identity resumes its original After333 account; confirm a different provider identity does not silently merge through email or local cached profile data.

If Android reports a provider-configuration error, first check package name, signing SHA, OAuth project, Web client ID, and whether the Google account is listed as a test user.

## Current Test Boundary

Server verification, Windows token acquisition, Android Credential Manager integration, Unity platform selection, the no-configuration path, and the invalid-token path are implemented. A successful Android live login still requires real Google Cloud clients and a physical-device smoke test.

## Windows 버튼이 ‘Google 로그인 준비 중’인 경우 — 2026-10-19 해결 기록

Unity Editor Play에서 Inspector의 기본 서버URL과 실제 저장된 접속URL이 달랐다. 실제 `ServerHttpUrlInput`에는 `https://api-staging.after333.com`이 남아 있었다. 기존 Server Endpoint UI에서 `https://api.after333.com`을 입력·Apply한 뒤 Google 버튼이 활성화되고 실제 로그인에 성공했다. 운영 readiness의 acceptedClientIds2개/DesktopCodeExchange=true를 먼저 확인했으며 서버 비밀키·DB·OAuth 클라이언트를 재생성하지 않았다. Standalone Windows exe의 성공으로 확대하지 않는다.


## Forced-logout Google readiness recovery (2026-10-22 source fix)

The user reported that another-device login correctly logged out an Android PvP participant, but the returned login gate left Google disabled as `Google 로그인 준비 중`. A delayed loopback reproduction confirms the lifecycle race: GameStart OnEnable begins `/server/status`, then the session-replacement callback cancels credential restore and that same public status request before it can populate the per-scene Google configuration cache.

GameStart now performs an independent cancellable public availability GET when forced logout leaves that cache empty. It preserves the replacement message and visible login gate, and enables `Google로 로그인` only after platform, client ID and server configuration checks succeed. It never refreshes credentials or starts automatic Google sign-in. The request is deduplicated, canceled on disable/server-address change, and cannot apply a canceled or different-endpoint response. Existing known readiness needs no extra request; tutorial mode sends nothing.

Isolated Unity6000.3.12f1: the pre-fix race fails1/1; final EditMode76/76 and PlayMode1/1 pass. Actual Awake/OnEnable, the installed session guard, native generated login gate, exact label/interactability, replacement notice and confirmation input release were checked using only memory credentials and synthetic loopback HTTP. No real Google provider, phone build/install, live server or DB was used. This client-only fix requires the user's updated client build/install; it adds no server change or DB migration. Evidence: session visualization `GoogleLoginAfterReplacement_20261022`.
