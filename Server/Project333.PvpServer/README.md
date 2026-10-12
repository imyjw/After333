# After333 authoritative battle and account server

Actual release status (2026-10-02): separately authorized live server and DB0020/0021/0022 cutover plus Android APK build passed. See the dated actual release section below; earlier source-only statements describe their original work. Physical-device installation/runtime remains untested.

This ASP.NET/PostgreSQL server owns After333 accounts, economy, draft/run results and online human/AI battle state. Internal `Project333` names remain for compatibility. Source documentation reviewed 2026-10-01; historical deployment records below retain their dates and do not identify the currently running release.

Normal battle uses the current 33-card run deck, human-first matchmaking and a 20-second default server AI fallback. Gold Dungeon is separate: up to three admissions per run started with three tickets, win Resource Gold10, no change to normal run wins/losses. The complete current run deck is required, and midnight does not reset its quota. Running-server reconnect is 70 seconds to the original authenticated seat; server interruption cancels unfinished battles without result and refunds challenge admissions to their original run. Durable finalized results replay idempotently before startup cancellation. See [battle modes](../../docs/battle_modes.md) and [AI policies](../../docs/pve_ai.md).

Card-pack purchase/receipt source was added on 2026-10-02: 33 Resource Gold grants 10 independent reward-eligible cards immediately in one DB transaction; opening is client presentation. Migration `0022_card_pack_purchase_receipts.sql` must follow the existing ordered migrations on the target server. This is a source change, not a deployment record. See [shop card packs](../../docs/shop_card_packs.md).

Gold Dungeon per-run source update (2026-10-09): production DB migration `0023_ai_challenge_run_usage.sql` was applied after `0022` at 12:57 KST on 2026-10-09; 23 migrations and a valid/ready index were verified, with all 33 existing data tables unchanged. Server deployment and client build for the per-run revision are still pending. The migration adds an index for non-refunded `(account_id, run_id)` usage; it does not erase or reassign existing attempts. Earlier-day attempts still count against their original run. This source update is not a live deployment or client-build record.

ElfElder ID source update (2026-10-09): all authored card IDs and native resource paths use `ElfElder`. Migration `0024_elf_elder_card_id.sql` converts the old saved identifier in collections, decks, offers, picks, rewards, packs, snapshots and receipts atomically, preserving copies/levels and economic results. Upgrade receipt fingerprints are converted together with their response so canonical retries replay. Mixed JSON keys abort the migration without partial changes for inspection. This migration has only been tested in an isolated database; production remains at 0023. Deploy the server revision, migration and rebuilt client together with old writers stopped.

Current public authentication is Game ID/Google login and saved-session refresh. Guest creation/resume and account-link routes are unavailable. Start-scene settings provide confirmed logout. The canonical card database is `Assets/Project333/Resources/Project333/Data/cards.json`, linked into server output; [card catalog](../../docs/card_catalog.md) describes current registered cards. Newly authored cards do not automatically enter the ten fixed AI decks.

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

`RunServer_Production.ps1` requires PostgreSQL, disables development ticket helpers and requires a client-version hint. Its retained recovery switch/environment setting is legacy compatibility only: current `Program.cs` always cancels unfinished battles on startup and reports `StartupRecoveryEnabled=false`; it cannot revive snapshots.
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

- Public auth register/login/Google/refresh/logout: the existing auth limiter defaults to `12` requests per `60` seconds; guest/link routes are not exposed
- Account, wallet, card, shop purchase/receipt, run, and reconnect-status APIs: `120` requests per `60` seconds. Both card-pack POST and receipt GET use the account API category.
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

Authentication expiry closes with `1008` / `authentication_timeout`. Policy close uses the serialized send gate and waits at most one second for the peer close (within the existing send deadline). A receive-loop rejection discards at most 32 reads of 2 KiB without processing commands. The authentication timer waits for the existing reader's close notification and never starts a concurrent receive. Completed close handshakes are not aborted again. Transport failure can still prevent a peer from receiving any close frame. Started battles use the current 70-second reconnect grace. The preceding 2026-09-09 guard deployment evidence retains its original date; the grace change is a later source update.

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
POST /auth/google/desktop-token
POST /auth/register
POST /auth/login
POST /auth/google
POST /auth/refresh
POST /auth/logout
GET  /me
POST /wallet/purchase-ticket
POST /shop/purchase-card-pack
GET  /shop/card-pack-purchases/{requestId}
POST /cards/upgrade
POST /account/operations/resolve
GET  /gifts
POST /gifts/{giftId}/claim
POST /ads/rewarded-ticket/attempt
GET  /ads/rewarded-ticket/attempt/{attemptId}
GET  /battle/reconnect-status
GET  /pvp/reconnect-status
GET  /ai-challenge/status
POST /ai-challenge/start
GET  /ai-challenge/attempts/{id}
POST /runs/start
POST /runs/draft-state
POST /runs/select-draft-card
POST /runs/claim-rewards
```

Gold Dungeon quota contract (2026-10-09):

- `GET /ai-challenge/status` returns `RunLimit` (3), `RemainingAttempts`, nullable `RunId`, and `PendingAttempt`. There is no daily reset timestamp. A current `ready`/`in_progress` run must have its owned, non-archived, completed 33-card deck; otherwise the status has no usable run and zero remaining attempts.
- Admission is charged at `POST /ai-challenge/start`. Usage is the count of that account and run's attempts whose `refunded_at` is null, across all dates and statuses. Remaining attempts are `max(0, 3 - usage)`; exhausted admission returns `ai_challenge_run_limit`. `challenge_day` remains audit data only.
- A new paid run receives its own three attempts after draft completion. A reused request ID remains bound to its original attempt/run/deck and reports that original run's remaining quota, even when a newer run exists.
- A server-cancellation refund restores capacity only to its original run. Losses, draws and non-refunded join failures still consume attempts. Concurrent requests, double-click aliases and reward delivery remain atomic/idempotent.

Draft offers and final deck creation are server-authoritative. The former
`/runs/save-draft-picks` and `/runs/complete-draft` routes return `410 Gone`.

Run the full draft authority smoke test against a local server with:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerAuthoritativeDraft.ps1 -BaseUrl 'http://127.0.0.1:7333'
```

The script verifies the Legendary opening offer, unique three-card offers,
copy limits, invalid and stale-pick rejection, all 33 authoritative picks,
server deck creation, and mid-draft resume through a newly issued session token
for the same registered account without rerolling the current offer. Older guest-based harness instructions are not a current authentication path.

`POST /auth/register`, `/auth/login` and `/auth/google` return a registered account with `sessionToken` (API bearer credential) and `refreshToken` (rotating device credential). `/auth/google/desktop-token` is the Windows OAuth exchange step for immediate Google login; it does not create a guest account. `/auth/guest` and public identity-link endpoints are unavailable.

`POST /auth/refresh` replaces both login tokens. The previous token pair is revoked immediately:

```json
{
  "refreshToken": "saved-refresh-token",
  "clientVersion": "0.1.0-dev"
}
```

`POST /auth/logout` accepts the current bearer token and refresh token and revokes the current device session. The start-scene settings menu asks for confirmation, then clears local credentials and displays the login gate even if remote logout fails; it does not erase account data.

`GET /me` requires:

```http
Authorization: Bearer {sessionToken}
```

`POST /wallet/purchase-ticket` requires the same bearer token and buys server tickets from Resource Gold:

```json
{
  "ticketCount": 1,
  "requestId": "7c9e5a01-d64d-4e2c-a31c-cb61e42f0521"
}
```

The current development exchange rate is:

```text
3 Resource Gold = 1 Ticket
```

`POST /shop/purchase-card-pack` requires the same bearer token and a fresh D-format UUID `requestId`; cost, card IDs and rarity are selected by the server. It deducts 33 Resource Gold and grants 10 independent, possibly duplicate cards from the shared reward-eligible pool at 40/30/18/9/3% rarity probabilities. Wallet debit, collection increases, transaction ledger and original ordered receipt commit together. Same-ID replay never charges, grants or draws again.

`GET /shop/card-pack-purchases/{requestId}` requires the same bearer token and reads only that account's original receipt plus current wallet. It does not buy or open a pack. Saved uncertain purchases use the common `/account/operations/resolve` completion/cancellation protocol, and a committed result remains granted even when the client exits before opening. Migration `0022` extends the existing receipt operation constraint to `ticket_purchase`, `card_upgrade`, and `card_pack_purchase`; it does not create unopened pack inventory. See [API, recovery and presentation policy](../../docs/shop_card_packs.md).

`POST /cards/upgrade` requires the same bearer token and upgrades one owned card by one level:

```json
{
  "cardId": "Goblin",
  "expectedUpgradeLevel": 0,
  "requestId": "c0947ab7-f34c-4a56-9774-95e53149d782"
}
```

Create a fresh D-format UUID for each new explicit operation; a same-request retry keeps that ID and payload. `expectedUpgradeLevel` is the displayed current level. Saved uncertain purchases/upgrades use `/account/operations/resolve`, never automatic charging resubmission; see [completion recovery](../../docs/account_operation_retries.md).

The current upgrade costs use the target level (schedule confirmed 2026-10-01; target Lv.2 revised 2026-10-02). Maximum level remains 13; eligible card types and rarities share the same schedule:

| Target level | Same-card copies consumed | Resource Gold consumed |
| --- | --- | --- |
| 1 | 3 | 3 |
| 2 | 6 | 6 |
| 3 | 10 | 10 |
| 4 | 20 | 20 |
| 5 | 30 | 30 |
| 6 | 33 | 33 |
| 7 | 33 | 33 |
| 8 | 33 | 33 |
| 9 | 33 | 33 |
| 10 | 33 | 33 |
| 11 | 33 | 33 |
| 12 | 33 | 33 |
| 13 | 33 | 33 |

The actual `/cards/upgrade` charge remains server-authoritative through the `card_upgrade_costs` database table. Apply pending source migrations through the existing runner in order: `0020_card_upgrade_costs_revision.sql` establishes the 13 transitions, then `0021_card_upgrade_level2_cost_revision.sql` changes only Lv.1 -> Lv.2 to 6/6. Original seed `0003` and migration `0020` remain historical and unchanged. A running database without the pending revision retains its previous costs, even when a newer client displays the revised requirements. This source change does not itself connect to or update the live database/server.

Existing atomic mutation, RequestId/ExpectedUpgradeLevel checks and cancellation/receipt behavior are unchanged. A committed request replay keeps its original UpgradeCost while returning current wallet/collection state; no retrospective refunds, level changes or receipt edits are performed. Card-specific stat/effect gains and upgrade eligibility remain unchanged. See [complete upgrade rules](../../docs/meta_rules.md#card-upgrade-costs).

Guest-disabled rejection check after the server is running (it verifies that creation is unavailable):

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

Preflight policy updated 2026-10-02: `TestDeploymentReadiness.ps1`, `TestPublicPvpSmokeReadiness.ps1` and generated staging verification require 70-second reconnect, `StartupRecoveryEnabled=false` and `InterruptedBattlePolicy=cancel_without_result`. Missing or conflicting policy fields fail the check. The retired `-RequireStartupRecovery` flag remains accepted only for old callers: it prints a retirement warning and checks the current cancellation policy, never requires snapshot revival. These source/tool updates do not establish a running-server deployment.

Deployment-readiness diagnostic after the server is running:

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

## Current battle scope

- Authenticated reservations bind match, account, original `PlayerA`/`PlayerB` seat, runtime `Player`/`AI`, run/deck and captured upgrade levels. The internal runtime name `AI` may represent a human second seat.
- Starts the server battle after required connections finish joining; normal matchmaking may replace its waiting human opponent with server AI. It rejects unauthorized actors, stale expected-turn commands and extra occupied seats.
- Uses common services for mulligan, unit/building play, damage/persistent/scripted spells, `InstallTrap`, movement, attack, `ActivateEffect` (including robot fusion), end turn and surrender.
- Sends viewer-filtered `StateView` and public battle events after accepted commands. Opponent hand identities, deck order and armed trap identity/rules remain private. Installation/log/ACK uses generic trap identity; successful trigger reveals the actual card.
- TrapReturn and TrapMirrorField cancel matching attacks before combat. Ordered one-shot activation, duplicate suppression, insufficient-resource retention, mana/Gold substitution and returned/reflected outcomes are shared local/server rules.
- Current card stats, upgrade schedules, status restrictions (including Stun/Freeze), persistent effects, aura buffs and direct defense buffs come from canonical card definitions and shared rules, not the early smoke-test card subset.
- Run counters/rewards, challenge receipts, purchase/upgrade resolution, gifts and ad rewards remain server-owned and idempotent. Unity presentation does not grant results or economy mutations.

The ten fixed AI decks keep their authored 36-card pool, Lv.0 and `1 Legendary / 5 Unique / 9 Rare / 11 Uncommon / 7 Common` counts per 33-card deck. `Program.cs` still prints an older L1/U4/R9/UC11/C8 startup summary; `PveAiDeckCatalog` and the JSON data contain the current counts. This stale console label is not a change to the actual deck rules. See [fixed deck list](../../docs/pve_ai_decks.md).

## Atomic draft run start

Migration `0014_single_active_draft_run` and the corresponding start fix were deployed and verified on 2026-09-09 at 22:09 KST. See `TempBuild/ServerRunStartAtomicityDeploy_20260909/README.md`. `StartDraftRunAsync` uses an explicit READ COMMITTED transaction and locks the account wallet before checking for existing resumable runs or completed runs with unclaimed rewards. The check searches all blocking runs, so a newer abandoned/claimed historical row cannot hide an older blocker. Ticket debit, run creation, opening offer and ticket ledger commit together. Other accounts use their own wallet locks.

The partial unique index `draft_runs_one_active_per_account_idx` permits only one `drafting`, `ready` or `in_progress` run per account across PvE/PvP. It also rejects direct inserts or status changes that bypass the service. Known index conflicts roll back the debit and return the existing `active_run_exists` error; `/runs/start` keeps its HTTP 409 contract. Retrying while that run remains active cannot spend another ticket. This does not add request-ID replay after the run has already ended and all its rewards have been claimed.

The migration does not delete, abandon, select or refund existing runs. It fails transactionally if active duplicates exist. Check for active duplicates before deployment. Historical unclaimed rewards are preserved and continue to block new starts according to `docs/meta_rules.md`. See `TempBuild/ServerRunStartAtomicity_20260909/README.md` for the reproduced two-creation bug and 58-group isolated validation.
## Actual card-pack server / DB / Android release — 2026-10-02

- User-authorized live cutover: release [C:/Project_333/Builds/Server/Releases/20261002-shop-card-pack](C:/Project_333/Builds/Server/Releases/20261002-shop-card-pack), started **2026-10-02 02:22:12 KST**, verified PID2916. Before cutover sessions, active normal/challenge matches and queued matches were all0. Published cards60; local/public HTTP10 checks and actual public WSS passed. Both shop routes rejected unauthenticated requests with401. Public strict readiness passed reconnect70, StartupRecoveryEnabled=false and cancel_without_result; compatibility remains0.1.0-dev.
- Actual ordered migrations0020/0021/0022 are applied,22total/latest0022. The13 upgrade charges are3/6/10/20/30 followed by eight33s; card_pack_purchase receipts are allowed. Final pre-cutover backup was archive-verified and separately restored with34/34 table row/hash comparisons. Production was not restored.
- Preservation:31protected table row counts/hashes are identical. The133-row pvp_match_queue retains131identical rows; only2historical matched rows referencing completed matches changed to canceled under existing startup SQL, affecting queue_status/canceled_at/updated_at only. No inserted/deleted rows; IDs, account/run/deck references and all other columns remain identical. See [migration verification](C:/Project_333/TempBuild/CardPackRelease_20261002/server/MigrationVerification.json), [queue delta](C:/Project_333/TempBuild/CardPackRelease_20261002/server/QueueStartupDelta.json) and [frozen-backup restore](C:/Project_333/TempBuild/CardPackRelease_20261002/server/FinalRestoreDrill.json).
- Actual Android APK: [After333.apk](C:/Project_333/Builds/Android/20261002-shop-card-pack/After333.apk), versionName1.0/versionCode5, IL2CPP ARM64, 266518036bytes(254.2 MiB), SHA256 EA8C151E16E08B429EE25E8CD81E86EB9E8FFCF5134583E06FCB8D1FD982D2E8. Signature verified and matches the prior APK certificate. Real SDK/LevelPlay and Unity6000.3.12f1 were used; build errors0, original9,123inputs and user Editor/scenes preserved. TMP build-cache reset was verified separately with authored font/material/source/meta preserved.
- Exact final evidence: [DeploymentResult](C:/Project_333/TempBuild/CardPackRelease_20261002/server/DeploymentResult.json), [client build proof](C:/Project_333/TempBuild/CardPackRelease_20261002/client/client-build-proof.json). No real account purchase, physical-device installation/runtime, or full authenticated device match was performed. Startup automation, scheduled tasks and OS services remain unchanged. Earlier dated source-only and deployment records retain their original scope.
