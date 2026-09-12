# Project333 PvP Test Server

This is the first local WebSocket test server for the Project333 online PvP prototype.

For the verified LAN PvP demo checklist, see:

```text
C:\Project_333\docs\lan_pvp_demo_checklist.md
```

For production-like deployment preparation, see:

```text
C:\Project_333\docs\deployment_readiness_checklist.md
```

For repeatable small public PvP smoke tests, see:

```text
C:\Project_333\docs\public_pvp_smoke_test_checklist.md
```

For a short Korean guide that can be sent directly to external testers, see:

```text
C:\Project_333\docs\public_pvp_tester_quickstart_ko.md
```

For HTTPS/WSS reverse proxy setup, see:

```text
C:\Project_333\docs\https_wss_reverse_proxy_setup.md
```

## Run

```powershell
dotnet run --project C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj
```

Recommended local-development launcher:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Local.ps1
```

If `PROJECT333_DB_CONNECTION` is not already set, pass it explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Local.ps1 -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

For a LAN or external test server, use:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Test.ps1 -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

`RunServer_Test.ps1` prints LAN client URL candidates such as `http://192.168.x.x:7333`.
Use that URL in the Unity start scene `ServerSettingsPanel`, or pass it to a built client with `-project333ServerUrl`.
If another PC cannot connect, allow TCP port `7333` through Windows Defender Firewall on the server PC.

For a production-like test server, use:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Production.ps1 -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

`RunServer_Production.ps1` requires PostgreSQL, disables development ticket helpers, enables PvP startup recovery by default, and requires a client-version hint.
Use it for production-like testing, not casual local development.

For the current laptop-hosted Cloudflare Tunnel at `api.after333.com`, use the dedicated launcher. It binds only to loopback while reporting the public HTTPS URL separately:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_CloudflareTunnel.ps1 -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

For a cleaner deployment-style run, publish the server first:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\PublishServer_Release.ps1 -CleanOutput
```

Then run the published server DLL:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Production.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

For longer tests, use the watchdog wrapper so the published server restarts after a crash and writes logs under `C:\Project_333\Logs\Server`:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Watchdog.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

For a remote host with a real HTTPS domain, package the published server, Caddy configuration, and relative-path operations scripts into one staging bundle:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\NewStagingDeploymentBundle.ps1 -Domain 'staging.project333.example.com' -AdminEmail 'admin@example.com'
```

Copy `C:\Project_333\Builds\Staging\Project333.PvpServer` to the staging host and follow the generated `README.md`. The generator requires a current publish manifest and matching client version. The bundle never contains the database password; pass the connection string only when starting the server.

The watchdog keeps its redirected console logs for `30` days by default.
Use `-LogRetentionDays 0` to keep them forever, or pass another day count:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Watchdog.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev' -LogRetentionDays 14
```

Use `-PrintOnly` with these launcher scripts to preview the environment settings without starting the server.

Before larger tests or schema changes, create a PostgreSQL backup:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\BackupPostgres_Project333.ps1 -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password'
```

To preview a restore without changing the database:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RestorePostgres_Project333.ps1 -BackupPath 'C:\Project_333\Backups\PostgreSQL\project333_YYYYMMDD_HHMMSS.dump' -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password' -PrintOnly
```

The default URL is:

```text
http://127.0.0.1:7333
```

The battle WebSocket endpoint is:

```text
ws://127.0.0.1:7333/battle
```

The active session list endpoint is:

```text
http://127.0.0.1:7333/sessions
```

The full development status endpoint is:

```text
http://127.0.0.1:7333/server/status
```

For public HTTPS/WSS tests, run Project333 behind a reverse proxy such as Caddy.
The sample Caddy template is:

```text
C:\Project_333\Server\Project333.PvpServer\Deploy\Caddyfile.example
```

`/server/status` reports DB health, loaded card count, PvP runtime settings, client-version hints, and deployment-readiness warnings. In local development it is normal for `Deployment.Status` to be `Check` because the default server URL is `127.0.0.1`.

## Request Rate Limits

The server applies fixed-window limits by client IP to public mutation and connection surfaces:

- Auth guest/register/login/link: `12` requests per `60` seconds
- Account, wallet, card, run, and reconnect-status APIs: `120` requests per `60` seconds
- New `/battle` WebSocket connections: `20` requests per `60` seconds

HTTP `429` responses use error code `rate_limited` and include a `Retry-After` header. Health and status endpoints are not limited. Caddy's local forwarded client address is processed before rate limiting. Production launcher scripts explicitly enable these defaults, and `/server/status` exposes the active `RateLimits` block.

Battle WebSocket input has a **65,536-byte limit per complete client text message**, accumulated across all continuation frames. The reader checks the limit before appending to its message buffer. Oversized input closes with `1009`, binary input with `1003`, and invalid UTF-8 with `1007` (the WebSocket implementation may also reject invalid wire encoding). Valid UTF-8 characters split across frames are supported. Normal JSON validation remains in the command handler. Outbound server StateViews are not subject to this input limit.

Protocol rejection uses the connection's serialized close path and the existing disconnect/reconnect grace cleanup. The later connection guards below add command rate limits, an authentication deadline and an account connection cap; their deployment status is recorded separately. Source verification for this change: `TempBuild/ServerInputLimits_20260909/README.md`. Deployed at 20:15 KST on 2026-09-09; public HTTP/WSS, protocol rejection and DB aggregate preservation checks are recorded in `TempBuild/ServerInputLimitsDeploy_20260909/README.md`.

Matchmaking now requires migration `0013_pvp_match_reservations` (deployed and verified on 2026-09-09). Match selection and reservation of `PlayerA`/`PlayerB` commit together. A server-generated reservation binds the account, connection, run and deck, and counts toward capacity before the socket joins the in-memory session. Its 60-second internal join lease is distinct from the battle reconnect grace. Successful player/connection persistence consumes it in the same transaction; failed handlers release only their own reservation. Abandoned leases expire lazily when another matchmaking request runs. Waiting disconnects release their seat; an in-progress join holds a session lease to prevent deletion of the shared room.

Short PostgreSQL advisory transaction locking serializes matchmaking capacity changes in the current single-region deployment. It covers DB work only, not socket I/O or battle simulation. A battle waits until all its registered connections have completed DB joining. This does not add multiple-server battle ownership, ranked matchmaking, or a queue throughput guarantee. See `TempBuild/ServerMatchReservations_20260909/README.md` for the reproduction and 44-group isolated regression run.


Battle connection guards (deployed and verified on 2026-09-09; see `TempBuild/ServerConnectionGuardsDeploy_20260909/README.md`) add the following server-only defaults:

| Environment variable | Default | Meaning |
| --- | ---: | --- |
| `PROJECT333_BATTLE_AUTH_TIMEOUT_SECONDS` | 30 | Time from WebSocket acceptance to successful server account authentication |
| `PROJECT333_BATTLE_ACCOUNT_CONNECTION_LIMIT` | 2 | Authenticated sockets per account in this server process, including sockets without a battle seat |
| `PROJECT333_BATTLE_MESSAGE_BURST` | 30 | Maximum accumulated message permits per socket |
| `PROJECT333_BATTLE_MESSAGES_PER_SECOND` | 10 | Message permits replenished per second |
| `PROJECT333_BATTLE_JOIN_BURST` | 4 | Maximum accumulated JoinMatch permits per socket |
| `PROJECT333_BATTLE_JOIN_REFILL_SECONDS` | 5 | Seconds to replenish one JoinMatch permit |

Every complete text message consumes a permit before JSON parsing/logging, including KeepAlive, malformed JSON and rejected commands. JoinMatch also consumes a separate permit before token lookup or matchmaking. Budgets use monotonic elapsed time and do not queue commands. Exceeding a budget closes with `1008` and reason `message_rate_limited` or `join_rate_limited`.

KeepAlive, invalid authentication and partial frames do not extend the authentication deadline. Late authentication responses cannot acquire account capacity. Only a server-verified account ID can acquire a connection slot; repeated authentication of the same identity reuses the slot and switching identity is rejected even before joining a battle. A third socket closes with `1008` / `account_connection_limit`. The default two sockets leave room for an existing connection and its reconnect replacement; existing one-battle/seat ownership checks still apply. Slots are released before DB disconnect cleanup, and empty account registry entries are removed.

Authentication expiry closes with `1008` / `authentication_timeout`. Policy close uses the serialized send gate and waits at most one second for the peer close (within the existing send deadline). A receive-loop rejection discards at most 32 reads of 2 KiB without processing commands. The authentication timer waits for the existing reader's close notification and never starts a concurrent receive. Completed close handshakes are not aborted again. Transport failure can still prevent a peer from receiving any close frame. Started battles keep the existing 60-second reconnect grace.

These guards are independent of the HTTP limiter's enable flag. Invalid explicit settings fail startup; the account cap cannot be configured below two. Budgets are per socket, account capacity is per server process, and this is not a distributed rate limiter. No DB migration or Unity client change is required. Verification: `TempBuild/ServerConnectionGuards_20260909/README.md`.
Override the defaults only when a load test demonstrates a legitimate need:

```text
PROJECT333_RATE_LIMIT_WINDOW_SECONDS
PROJECT333_AUTH_RATE_LIMIT_PER_WINDOW
PROJECT333_ACCOUNT_RATE_LIMIT_PER_WINDOW
PROJECT333_BATTLE_CONNECT_RATE_LIMIT_PER_WINDOW
```

## Audit Logs

The server writes lightweight JSONL audit logs for important account, reward, run, and PvP result events.
These logs are intended for diagnosing production-like tests without storing passwords, guest tokens, or session tokens.

Default path:

```text
C:\Project_333\Logs\Server\Audit
```

Each day is written to a separate file:

```text
audit_YYYYMMDD.jsonl
```

Useful settings:

```powershell
$env:PROJECT333_AUDIT_LOG_ENABLED='1'
$env:PROJECT333_AUDIT_LOG_DIR='C:\Project_333\Logs\Server\Audit'
$env:PROJECT333_AUDIT_LOG_RETENTION_DAYS='30'
```

Set `PROJECT333_AUDIT_LOG_ENABLED` to `0`, `false`, `off`, or `no` to disable audit logging.
Set `PROJECT333_AUDIT_LOG_RETENTION_DAYS` to `0` or lower to keep audit logs forever.
`/server/status` includes an `Audit` block showing whether audit logging is enabled, which directory is active, and how many days are retained.

## Account DB Development

Account APIs require PostgreSQL and the `PROJECT333_DB_CONNECTION` environment variable.

If `PROJECT333_DB_CONNECTION` is not set, the battle server can still run, but account endpoints return `503`.

Development connection string example:

```powershell
$env:PROJECT333_DB_CONNECTION='Host=127.0.0.1;Port=5432;Database=project333;Username=project333;Password=dev_password'
$env:PROJECT333_DEV_MIN_TICKETS='9'
dotnet run --project C:\Project_333\Server\Project333.PvpServer\Project333.PvpServer.csproj
```

`PROJECT333_DEV_MIN_TICKETS` is a development-only helper. When it is set, newly created guest accounts start with at least that many server tickets. Existing accounts are not topped up on login, so ticket spending remains persistent after the reward pipeline is enabled.

When the server starts with a valid PostgreSQL connection, it runs SQL migrations from:

```text
C:\Project_333\Server\Project333.PvpServer\Persistence\Migrations
```

Current account endpoints:

```text
GET  /health/db
POST /auth/guest
POST /auth/register
POST /auth/login
POST /auth/google
POST /auth/refresh
POST /auth/logout
GET  /me
POST /wallet/purchase-ticket
POST /cards/upgrade
POST /runs/start
POST /runs/draft-state
POST /runs/select-draft-card
POST /runs/claim-rewards
```

Draft offers and final deck creation are server-authoritative. The former
`/runs/save-draft-picks` and `/runs/complete-draft` routes return `410 Gone`.

Run the full draft authority smoke test against a local server with:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerAuthoritativeDraft.ps1 -BaseUrl 'http://127.0.0.1:7333'
```

The script verifies the Legendary opening offer, unique three-card offers,
copy limits, invalid and stale-pick rejection, all 33 authoritative picks,
server deck creation, and mid-draft resume through a newly issued session token
for the same guest account without rerolling the current offer.

`POST /auth/guest` creates or resumes a server-side guest account and returns:

- `guestToken`: guest account resume token
- `sessionToken`: API bearer token
- `refreshToken`: rotating device auto-login token

`POST /auth/refresh` replaces both login tokens. The previous token pair is revoked immediately:

```json
{
  "refreshToken": "saved-refresh-token",
  "clientVersion": "0.1.0-dev"
}
```

`POST /auth/logout` accepts the current bearer token and refresh token, revokes the current device session, and then Unity clears its local credentials.

`GET /me` requires:

```http
Authorization: Bearer {sessionToken}
```

`POST /wallet/purchase-ticket` requires the same bearer token and buys server tickets from Resource Gold:

```json
{
  "ticketCount": 1
}
```

The current development exchange rate is:

```text
3 Resource Gold = 1 Ticket
```

`POST /cards/upgrade` requires the same bearer token and upgrades one owned card by one level:

```json
{
  "cardId": "Goblin"
}
```

The temporary upgrade rule is:

```text
Max Level = 13
Required Resource Gold = target level * 3
Required card copies = target level * 3
```

Smoke test after the server is running:

```powershell
C:\Project_333\Server\Project333.PvpServer\Tools\TestGuestAuth.ps1
```

Automatic-login rotation and logout smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestRefreshAuth.ps1 -BaseUrl 'http://127.0.0.1:7333'
```

Server endpoint smoke test after the server is running:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -ExpectedRequiredClientVersion '0.1.0-dev'
```

Strict deployment-readiness preflight after the server is running:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestDeploymentReadiness.ps1 -BaseUrl 'http://127.0.0.1:7333' -ExpectedRequiredClientVersion '0.1.0-dev' -AllowDeploymentWarnings
```

Public PvP smoke-test readiness check after the server is running:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestPublicPvpSmokeReadiness.ps1 -BaseUrl 'http://127.0.0.1:7333' -ExpectedRequiredClientVersion '0.1.0-dev' -AllowDeploymentWarnings
```

Collect a public PvP smoke-test evidence bundle after a failed or suspicious test:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\CollectPublicPvpSmokeEvidence.ps1 -BaseUrl 'http://127.0.0.1:7333' -TestLabel 'pvp-test-01' -Notes 'Write what failed here'
```

For a LAN or external server:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -BaseUrl 'http://SERVER_PC_IP:7333' -ExpectedRequiredClientVersion '0.1.0-dev'
```

This checks `/health`, `/server/status`, and the `/battle` WebSocket endpoint.

## Two Built Client PvP Test

When testing two built clients on the same PC, launch them with different client profiles so account sessions and server endpoint overrides do not share the same PlayerPrefs keys.

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\LaunchTwoClients_Local.ps1 -ClientExePath 'C:\Path\To\Project333.exe'
```

The launcher passes:

```text
-project333Profile PlayerA
-project333Profile PlayerB
-project333ServerUrl http://127.0.0.1:7333
```

The game start screen shows the active profile as `Client: PlayerA` or `Client: PlayerB` in the server status line.

Run-end reward claim uses `POST /runs/claim-rewards` with:

```json
{
  "runId": "completed-draft-run-guid"
}
```

The temporary reward formula is:

```text
Resource Gold = final win count * 3
```

The final reward formula is still a game-design TBD, so the development server still allows provisional reward values to be overridden from environment variables.

```powershell
$env:PROJECT333_RUN_REWARD_RESOURCE_GOLD_BASE='0'
$env:PROJECT333_RUN_REWARD_RESOURCE_GOLD_PER_WIN='3'
$env:PROJECT333_RUN_REWARD_RESOURCE_GOLD_PER_LOSS='0'
$env:PROJECT333_RUN_REWARD_TICKETS_BASE='0'
$env:PROJECT333_RUN_REWARD_TICKETS_PER_WIN='0'
$env:PROJECT333_RUN_REWARD_TICKETS_PER_LOSS='0'
$env:PROJECT333_RUN_REWARD_CARD_REWARDS='Goblin:3;firebolt:1'
$env:PROJECT333_RUN_REWARD_PACK_REWARDS='starter_pack:1'
```

## Current Scope

- Accepts WebSocket connections at `/battle`.
- Receives `JoinMatch` JSON envelopes and groups WebSocket connections by `matchId`.
- Assigns the first two active PvP connections to external `PlayerA` and `PlayerB` seats.
- Internally maps `PlayerA -> Player` and `PlayerB -> AI` until the shared battle model gets neutral player slot names.
- Rejects additional active connections in the same match with `match_full`.
- Rejects commands whose `ActorId` does not match the sender's assigned seat.
- Starts a prototype in-memory battle automatically when the second player joins.
- Sends a per-viewer `StateView` to each connected client when battle starts.
- Resolves `PlayUnitCard`, `PlayBuildingCard`, `CastDamageSpell`, `CastPersistentResourceSpell`, `CastScriptedSpell`, `MoveOccupant`, `Attack`, and `EndTurn` through the shared battle rules model.
- Sends updated `StateView` messages after accepted commands.
- Receives `ClientCommand` JSON envelopes from the Unity client.
- Logs command type, match id, online seat label, internal runtime actor id, and sequence.
- Broadcasts a simple `BattleEvents` ACK response to every connection in the same match after accepted commands.

The prototype card pool includes basic unit cards plus `firebolt`, `starter_power_contract`, `CheonraJimang`, and `Daehwandan` so spell commands can be smoke-tested through the same server-authored `StateView` loop.

## Atomic draft run start

Migration `0014_single_active_draft_run` and the corresponding start fix were deployed and verified on 2026-09-09 at 22:09 KST. See `TempBuild/ServerRunStartAtomicityDeploy_20260909/README.md`. `StartDraftRunAsync` uses an explicit READ COMMITTED transaction and locks the account wallet before checking for existing resumable runs or completed runs with unclaimed rewards. The check searches all blocking runs, so a newer abandoned/claimed historical row cannot hide an older blocker. Ticket debit, run creation, opening offer and ticket ledger commit together. Other accounts use their own wallet locks.

The partial unique index `draft_runs_one_active_per_account_idx` permits only one `drafting`, `ready` or `in_progress` run per account across PvE/PvP. It also rejects direct inserts or status changes that bypass the service. Known index conflicts roll back the debit and return the existing `active_run_exists` error; `/runs/start` keeps its HTTP 409 contract. Retrying while that run remains active cannot spend another ticket. This does not add request-ID replay after the run has already ended and all its rewards have been claimed.

The migration does not delete, abandon, select or refund existing runs. It fails transactionally if active duplicates exist. Check for active duplicates before deployment. Historical unclaimed rewards are preserved and continue to block new starts according to `docs/meta_rules.md`. See `TempBuild/ServerRunStartAtomicity_20260909/README.md` for the reproduced two-creation bug and 58-group isolated validation.