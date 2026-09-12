# After333 Deployment Readiness Checklist

This checklist separates the current development/LAN setup from a production-like server setup.

The current goal is not a final commercial deployment yet.
The goal is to make the server settings explicit enough that remote testing does not depend on hidden local defaults.

For HTTPS/WSS reverse proxy setup, see:

```text
C:\Project_333\docs\https_wss_reverse_proxy_setup.md
```

For the repeatable two-client PvP smoke-test flow, see:

```text
C:\Project_333\docs\public_pvp_smoke_test_checklist.md
```

## Server Modes

### Local Development

Use this when testing alone on the development PC.

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Local.ps1 -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

Expected characteristics:

- Listens on `http://127.0.0.1:7333`.
- May use development ticket helpers.
- Startup PvP recovery is off unless explicitly enabled.

### LAN Test

Use this when another PC on the same network needs to connect.

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Test.ps1 -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

Expected characteristics:

- Prints client URL candidates such as `http://192.168.x.x:7333`.
- Development ticket helpers are disabled.
- Use Windows Defender Firewall to allow inbound TCP `7333`.

### Production-Like Test

Use this before any broader external test.
For source-folder testing, this still runs the project with `dotnet run`:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Production.ps1 -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

For a cleaner deployment-style flow, publish the server first and run the published DLL:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\PublishServer_Release.ps1 -CleanOutput
```

After a domain is assigned, generate a portable staging bundle:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\NewStagingDeploymentBundle.ps1 -Domain 'api.after333.com' -AdminEmail 'admin@example.com'
```

The generated bundle is written to:

```text
C:\Project_333\Builds\Staging\Project333.PvpServer
```

It contains the published server, a domain-specific `Caddyfile`, relative-path start and verification scripts, selected operations tools, and a bundle manifest. It requires the publish manifest produced by `PublishServer_Release.ps1` and rejects a client-version mismatch. It intentionally does not contain the PostgreSQL connection string or password. Copy the entire bundle to the staging host and follow its `README.md`.

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Production.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

For longer tests, use the watchdog wrapper so the published server restarts after a crash:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Watchdog.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

Expected characteristics:

- PostgreSQL connection is required.
- `PROJECT333_DEV_MIN_TICKETS` is disabled.
- Request rate limiting is enabled for auth, account APIs, and new battle WebSocket connections.
- `PROJECT333_ENABLE_PVP_STARTUP_RECOVERY` is enabled by default.
- `PROJECT333_REQUIRED_CLIENT_VERSION` is required.
- Verbose transport logs are off unless explicitly enabled.
- The published output includes the server DLL, `cards.json`, and SQL migration files.
- Watchdog logs are written under `C:\Project_333\Logs\Server` by default and kept for `30` days unless `-LogRetentionDays` is changed.

## Required Server Settings

| Setting | Required | Notes |
| --- | --- | --- |
| `PROJECT333_PVP_SERVER_URL` | Yes | Internal ASP.NET listen URL. Use `http://127.0.0.1:7333` behind Cloudflare Tunnel or a local reverse proxy. |
| `PROJECT333_PUBLIC_SERVER_URL` | Required behind a tunnel/proxy | Public client URL such as `https://api.after333.com`. Kept separate from the internal listen URL. |
| `PROJECT333_DB_CONNECTION` | Yes | PostgreSQL connection string. Do not commit passwords. |
| `PROJECT333_REQUIRED_CLIENT_VERSION` | Recommended for testing, required by production script | Exposed through `/server/status` and enforced during online battle `JoinMatch` through the `X-Project333-Client-Version` WebSocket header. |
| `PROJECT333_RECOMMENDED_CLIENT_VERSION` | Optional | Non-blocking version hint. |
| `PROJECT333_CARD_DEFINITION_VERSION` | Optional | Useful when card DB and client builds must be matched manually. |
| `PROJECT333_ENABLE_PVP_STARTUP_RECOVERY` | Recommended for production-like tests | Restores reconnectable PvP matches after server restart when snapshots exist. |
| `PROJECT333_VERBOSE_TRANSPORT_LOGS` | No | Keep off unless diagnosing WebSocket payload issues. |
| `PROJECT333_AUDIT_LOG_ENABLED` | Recommended | Writes JSONL audit events for auth, reward, run result, and PvP match result diagnostics. Enabled by default. |
| `PROJECT333_AUDIT_LOG_DIR` | Optional | Overrides the audit log directory. Defaults to `C:\Project_333\Logs\Server\Audit` when available. |
| `PROJECT333_AUDIT_LOG_RETENTION_DAYS` | Optional | Defaults to `30`. Set to `0` or lower to keep audit logs forever. |
| `PROJECT333_RATE_LIMIT_ENABLED` | Required for public tests | Defaults to enabled. Production launchers explicitly set it to `1`. |
| `PROJECT333_RATE_LIMIT_WINDOW_SECONDS` | Optional | Fixed-window duration. Default: `60`. |
| `PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW` | Optional | Guest/register/login/link requests per client IP per window. Default: `12`. |
| `PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW` | Optional | Authenticated account API requests per client IP per window. Default: `120`. |
| `PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW` | Optional | New `/battle` WebSocket requests per client IP per window. Default: `20`. |
| `PROJECT333_GOOGLE_CLIENT_IDS` | Required for Google login | Comma-separated Google OAuth client IDs accepted by the server. These IDs are not secrets. Never store a Google ID token or client secret in source control. |
| `PROJECT333_GOOGLE_DESKTOP_CLIENT_ID` | Required for Windows Google login | Desktop OAuth client ID used for the server-side authorization-code exchange. It must also appear in `PROJECT333_GOOGLE_CLIENT_IDS`. |
| `PROJECT333_GOOGLE_DESKTOP_CLIENT_SECRET` | Required for Windows Google login | Desktop OAuth client secret. Keep it only in the server environment and never commit or place it in Unity. |
| `PROJECT333_DEV_MIN_TICKETS` | No | Development helper only. Must stay unset for production-like tests. |

## Public Testing Checklist

Before asking another player outside the development PC to test:

1. Start PostgreSQL and confirm the target database exists.
2. Back up the current database if it contains useful accounts or test progress.
3. Publish the server with `PublishServer_Release.ps1`.
4. Generate and copy the staging bundle when using a remote host and real domain.
5. Start the published server with `RunPublishedServer_Production.ps1`, or use `RunPublishedServer_Watchdog.ps1` for longer tests.
6. Start Caddy with the generated `Caddyfile` when HTTPS/WSS is required.
7. Open `https://YOUR_DOMAIN/health` and confirm `Status` is `Ok`.
8. Open `https://YOUR_DOMAIN/server/status`.
9. Confirm `Database.Status` is `Ok`.
10. Confirm `Cards.Status` is `Ok`.
11. Confirm `Deployment.Warnings` has no unexpected warnings.
12. Confirm `PvP.ReconnectGraceSeconds` is `60`.
13. Confirm `RateLimits.Enabled` is `true` and the reported limits are positive.
14. Confirm `Audit.Enabled` is `true` unless audit logging was intentionally disabled.
15. Confirm the Unity client points at the same server URL in `ServerSettingsPanel`.
16. Run `TestServerEndpoint.ps1` against the target URL.
17. Run `TestDeploymentReadiness.ps1` for the stricter preflight check.

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -BaseUrl 'http://SERVER_HOST:7333'
```

Strict preflight check:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestDeploymentReadiness.ps1 -BaseUrl 'https://api.after333.com' -ExpectedRequiredClientVersion '0.1.0-dev' -RequireStartupRecovery
```

For local or LAN-only tests, deployment warnings such as `127.0.0.1` are expected.
Use:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestDeploymentReadiness.ps1 -BaseUrl 'http://SERVER_HOST:7333' -ExpectedRequiredClientVersion '0.1.0-dev' -AllowDeploymentWarnings
```

## Database Backup And Restore

Before schema changes or deployment, preserve a custom-format PostgreSQL backup, its SHA256, the exact server release and backup-time validation records. Current deployment backups are kept outside the repository under `%LOCALAPPDATA%\After333\Server\Backups`; the older backup tool defaults to `C:\Project_333\Backups\PostgreSQL` unless explicitly overridden. Use the private location for account data and supply credentials through the existing protected configuration, not command-line examples or repository files.

An actual restore drill passed on 2026-09-12: 30 public tables, backup-time account/economy/run/receipt comparisons, synthetic API operations, and isolated PostgreSQL plus HTTP server restart with idempotent replay. Production was not restored or restarted. Follow the [tested restore runbook](./postgres_restore_runbook.md) for the exact isolated command, locale requirements, verification evidence and production recovery sequence.

`RestorePostgres_Project333.ps1` is the older in-place helper: it uses `--clean --if-exists`, so its confirmation flag does not make it an isolated drill. Prefer restoration to a new, explicitly verified recovery DB and preserve the current DB. Do not target the live `project333` DB for rehearsal. Database roles, server secrets and uncommitted result journal files require separate recovery planning.

## Current Limitations

- Direct LAN tests still use HTTP, while the current laptop-hosted public test path uses Cloudflare Tunnel at `https://api.after333.com` and `wss://api.after333.com/battle`.
- Windows Desktop OAuth and Android Credential Manager Google login/link flows are implemented, but each remains disabled until its matching client ID is configured in Unity and accepted by the server.
- Android Google login still needs a real-device smoke test with the `com.after333.game` OAuth client and the signing-certificate SHA. Kakao and Naver login are not implemented yet.
- Account, game ID login, cards, rewards, deck building, PvP matchmaking, reconnect, audit logs, server-side battle resolution, and basic IP-partitioned request limiting are implemented for the current prototype flow, but still need load testing and broader abuse hardening.
- Database backups are scripted, but automatic scheduled backups and migration rollback are not automated yet.
- Crash restart is available through `RunPublishedServer_Watchdog.ps1`, but it is still a simple development wrapper rather than a full OS service.

## Recommended Next Deployment Work

1. Run the public PvP smoke-test checklist through `https://api.after333.com` with two external clients and record the result.
2. The isolated restore drill is complete (2026-09-12); scheduled backups and separate-location retention remain future work and were not configured by the drill.
3. Choose a VPS and managed PostgreSQL target when moving beyond the graduation-project demonstration stage.
4. Replace the PowerShell watchdog with a real process manager on that hosted environment.
5. Generate a staging deployment bundle for the hosted environment and repeat the public smoke test before wider testing.

## Safe server publication (2026-09-12)

PublishServer_Release.ps1 always publishes into a fresh staging directory and checks the native exit code, required artifacts and migrations before promoting it. OutputPath must be strictly below this checkout's Builds/Server directory; links/junctions and unrelated nonempty folders are rejected. Prefer a new release path for each build.

If the target exists, explicitly use -CleanOutput or choose a new path. -CleanOutput now retains the previous output in a sibling backup directory instead of deleting it before building. A caught promotion failure restores that backup. A hard process termination between renames may require manual restoration; failed staging directories, backups and empty lock files remain available for diagnosis. The publisher does not start or restart servers or apply database changes.

The schema-2 publish manifest retains legacy summary fields and adds BuildId, SDK version, exit code and hashes for all published files. Git dirty status includes untracked files; this is artifact traceability, not a substitute for a clean source checkout and CI.

Run Tools/TestPublishServerSafety.ps1 in a separate PowerShell process with a valid server ReferenceOutput to run isolated filesystem/compiler-boundary regression checks. Full validation evidence: [publish safety report](C:/Project_333/TempBuild/PublishSafety_20260912/README.md).