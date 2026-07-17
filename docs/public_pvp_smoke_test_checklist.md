# Public PvP Smoke Test Checklist

For a short Korean guide that can be sent directly to external testers, see:

```text
C:\Project_333\docs\public_pvp_tester_quickstart_ko.md
```

To generate a per-test Korean tester brief with the exact server URL, client version, and server publish manifest, run:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\NewPublicPvpTesterBrief.ps1 -ServerUrl 'http://SERVER_HOST:7333' -ClientVersion '0.1.0-dev' -TestLabel 'pvp-test-01'
```

This checklist is for small public or semi-public After333 PvP tests.
Use it after the LAN checklist passes and before inviting more testers.

The goal is not load testing yet.
The goal is to confirm that two real clients can use the account, deck, matchmaking, battle, reconnect, result, reward, and log flow on the same server.

## Test Scope

Minimum test size:

- 1 server PC or hosted server
- 2 different player accounts
- 2 Unity clients
- 1 completed 33-card draft deck per account
- 1 full PvP match result
- 1 reconnect-grace scenario

Optional stretch:

- 4 clients using 4 different accounts
- 2 simultaneous PvP matches

## Before The Test

### 1. Confirm Server Mode

Use the published production-like server for external tests when possible.

Recommended:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\RunPublishedServer_Watchdog.ps1 -PublishedServerPath 'C:\Project_333\Builds\Server\Project333.PvpServer' -ListenUrl 'http://0.0.0.0:7333' -DatabaseConnection 'Host=DB_HOST;Port=5432;Database=project333;Username=project333;Password=CHANGE_ME' -RequiredClientVersion '0.1.0-dev'
```

If the published server folder was not created yet, create it first:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\PublishServer_Release.ps1 -ClientVersion '0.1.0-dev'
```

The publish script writes `project333_server_publish_manifest.json` into the published server folder. Keep this file with test evidence so later logs can be matched to the exact server build.

For LAN-only smoke tests, `RunServer_Test.ps1` is acceptable.

### 2. Confirm Server Health

Open:

```text
http://SERVER_HOST:7333/health
```

Expected:

```text
Status: Ok
```

Open:

```text
http://SERVER_HOST:7333/server/status
```

Expected:

- `Status` is `Ok`
- `Database.Status` is `Ok`
- `Cards.Status` is `Ok`
- `PvP.ReconnectGraceSeconds` is `60`
- `Audit.Enabled` is `true`

### 3. Run Endpoint Preflight

Recommended public PvP smoke preflight:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestPublicPvpSmokeReadiness.ps1 -BaseUrl 'http://SERVER_HOST:7333' -ExpectedRequiredClientVersion '0.1.0-dev' -AllowDeploymentWarnings
```

If this is running from a remote client PC that cannot access the server PC's local log folders, add:

```powershell
-SkipLocalLogCheck
```

Basic endpoint check:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestServerEndpoint.ps1 -BaseUrl 'http://SERVER_HOST:7333' -ExpectedRequiredClientVersion '0.1.0-dev'
```

For stricter production-like checks:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestDeploymentReadiness.ps1 -BaseUrl 'http://SERVER_HOST:7333' -ExpectedRequiredClientVersion '0.1.0-dev' -AllowDeploymentWarnings
```

### 4. Prepare Accounts

Use two different accounts.

Do not test PvP with the same account on both clients unless you are specifically testing the duplicate-login warning.

Each account should have:

- login success
- enough server tickets if starting a new run
- one active or completed draft deck ready for PvP
- no stale PvP reconnect button from an already-ended match

### 5. Prepare Client Settings

On each Unity client or built client:

1. Open the game start scene.
2. Enter the server URL in `ServerSettingsPanel`.
3. Press `Apply`.
4. Confirm:

```text
Server: OK  DB: OK  Cards: OK
```

If the server line shows `OFF`, stop the test and fix the server URL, firewall, HTTPS/HTTP setting, or network path first.

## Main Test Flow

### Test A. Account And Deck Resume

For each client:

1. Start the game.
2. Confirm the expected account name is shown.
3. Confirm server tickets and gold are shown.
4. Enter Draft scene.
5. Confirm the current draft deck or current run state is restored correctly.

Pass if both accounts show the expected account/run/deck state.

### Test B. PvP Matchmaking

1. Client A presses PvP.
2. Confirm matchmaking overlay appears.
3. Client B presses PvP.
4. Confirm both clients enter the same battle.

Pass if both clients leave the matchmaking overlay and enter battle.

Server log should include:

```text
[matchmaking] account=... assigned match=...
Battle started in match ...
```

### Test C. Battle Command Sync

During the battle, verify at least one of each:

- play a unit card
- move a unit
- cast a targeted spell if available
- attack with a unit or Master
- end turn

Pass if the action is visible on both clients and the server accepts the command without command rejection.

Server log should include:

```text
command #... PlayUnitCard
command #... MoveOccupant
command #... Attack
command #... EndTurn
StateView -> 2 client(s)
```

### Test D. Reconnect Grace

Use two clients already inside the same PvP battle.

1. Close Client B or disconnect its network.
2. Client A should see:

```text
PVP 전투 재접속 (60초)
```

or the current reconnect waiting message.

3. Before 60 seconds pass, reopen Client B.
4. Log in with the same account.
5. Press the PvP reconnect button if it appears.
6. Confirm Client B returns to the same battle.

Pass if battle state is restored and both clients can continue.

Server log should include:

```text
disconnected during PvP battle. Waiting 60 seconds for reconnect.
reconnect account=...
ReconnectGraceCancelled
```

### Test E. Forfeit After Reconnect Timeout

Use a separate match or repeat after Test D.

1. Close Client B.
2. Do not reconnect Client B.
3. Wait until the 60-second reconnect grace expires.
4. Confirm Client A receives a win result.
5. Confirm the ended-match reconnect button does not remain.

Pass if the remaining player wins and can return to Draft/Start normally.

Server log should include:

```text
reconnect grace expired
recorded PvP match result winner=...
```

### Test F. Normal Battle End

Finish a match through normal lethal damage or surrender.

Verify:

- winner sees victory flow
- loser sees defeat flow
- both clients return to Draft/Start cleanly
- no stale PvP reconnect button remains for the ended match
- run win/loss count updates on the correct accounts

Server log should include:

```text
recorded draft run result seat=...
recorded PvP match result winner=...
```

### Test G. Reward Claim If Run Completed

If a run reaches 33 wins or 3 losses:

1. Return to Draft scene.
2. Confirm `Claim Rewards` is available.
3. Click it once.
4. Confirm reward popup appears.
5. Confirm gold and card counts update after returning to the start/card scenes.

Pass if rewards are claimed once and cannot be claimed again for the same run.

Audit log should include:

```text
runs.claim_rewards.success
```

## After The Test

### 1. Check Server Logs

Watchdog console logs:

```text
C:\Project_333\Logs\Server
```

Audit logs:

```text
C:\Project_333\Logs\Server\Audit
```

Look for:

- unexpected `command_rejected`
- unexpected `websocket error`
- failed reward claims
- duplicate same-account connection warnings
- stuck reconnect grace
- stale PvP reconnect state after match end

### 2. Record Test Result

Use this template:

```text
Date:
Server URL:
Server build/version:
Client build/version:
Tester A account:
Tester B account:
Match ID:
Result:
Reconnect tested: Yes/No
Reward claim tested: Yes/No
Pass/Fail:
Notes:
```

### 3. Keep Useful Evidence

If something fails, keep:

- server PowerShell output
- latest `server_*.out.log`
- latest `server_*.err.log`
- latest `audit_*.jsonl`
- Unity Console logs from both clients
- the exact server URL shown in `ServerSettingsPanel`
- account names used by each client
- match id if visible in the server log

If the test is running on the server PC, collect the common evidence bundle with:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\CollectPublicPvpSmokeEvidence.ps1 -BaseUrl 'http://SERVER_HOST:7333' -TestLabel 'pvp-test-01' -Notes 'Write what failed here'
```

The report also copies `project333_server_publish_manifest.json` from the default published server folder when it exists. If your published server folder is elsewhere, add:

```powershell
-PublishedServerPath 'C:\Path\To\Project333.PvpServer'
```

To include `/health`, `/server/status`, WebSocket, deployment readiness, and public-smoke preflight outputs in the same report folder, add:

```powershell
-RunReadinessChecks -ExpectedRequiredClientVersion '0.1.0-dev' -AllowDeploymentWarnings
```

If running from another PC that cannot access the server PC log folders, add:

```powershell
-SkipLocalLogCopy
```

## Quick Failure Map

- `/health` fails: server process, server URL, firewall, reverse proxy, or hosting issue.
- `/server/status` says DB is not OK: PostgreSQL connection or migration issue.
- Unity shows `Server: OFF`: wrong server URL, blocked HTTP, firewall, or client cannot reach the server.
- PvP button unavailable: account has no playable draft run/deck or run state did not refresh.
- Matchmaking never completes: only one player queued, same account conflict, or queue state issue.
- Commands reject during battle: actor/seat mismatch, stale match, invalid card, or server-side rule rejection.
- Reconnect button stays after match end: active-match cleanup or client session refresh issue.
- Reward claim fails: run is not completed, already claimed, wrong account, or stale local run id.

## Pass Criteria For A Small Public Test

The test passes when:

- both clients can log in through the same server
- both clients can enter PvP matchmaking
- both clients enter the same battle
- at least one unit play, move, attack, spell, and end turn sync correctly
- reconnect within 60 seconds restores the battle
- reconnect timeout grants the correct forfeit result
- normal battle end records the correct win/loss
- completed run rewards can be claimed once
- server logs and audit logs contain enough information to diagnose the session
