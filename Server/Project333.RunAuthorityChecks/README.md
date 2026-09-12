# After333 run authority regression checks

This package is intended to live at `Server/Project333.RunAuthorityChecks`.
It tests the retired client result upload endpoint, battle participant binding, durable result delivery, connection stability, connection guards, and the normal server result/reward path.
It has no test-framework or new NuGet package dependencies; the harness references a freshly built server's assemblies.

## Run on Windows

Prerequisites: .NET 8 SDK, PostgreSQL command-line tools (`initdb`, `pg_ctl`, `createdb`), and the server's already restored NuGet packages. PostgreSQL 18 at `C:\Program Files\PostgreSQL\18\bin` is the default; pass `-PostgresBin` for another installed tool directory.

From the repository root, build current server source into an isolated output directory:

```powershell
$artifactRoot = Join-Path $env:TEMP ('after333-authority-build-' + [guid]::NewGuid().ToString('N'))
dotnet build .\Server\Project333.PvpServer\Project333.PvpServer.csproj `
  --configuration Release --artifacts-path $artifactRoot `
  --configfile .\Server\Project333.RunAuthorityChecks\offline.NuGet.Config `
  -p:NuGetAudit=false --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { throw 'Server build failed' }

$serverOutput = Join-Path $artifactRoot 'bin\Project333.PvpServer\release'
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\Server\Project333.RunAuthorityChecks\RunIsolatedRunAuthorityChecks.ps1 `
  -ServerOutput $serverOutput
if ($LASTEXITCODE -ne 0) { throw 'Run authority checks failed' }
```

The cleared package-source configuration keeps this check offline. If a clean machine does not yet have the server's dependencies, restore those using your normal approved package sources first; do not replace missing packages with arbitrary binaries. `-ExecutionPolicy Bypass` applies only to this script process and does not change machine policy.

## Isolation and cleanup

The runner requires unused loopback ports **15439** (PostgreSQL) and **17339** (HTTP). It refuses to take over either port. Each invocation initializes a new PostgreSQL cluster under a unique canonical OS temporary directory. The harness confirms the running database's `data_directory` matches that new directory and that there are zero pre-existing accounts before doing any work.

There is no live database connection-string argument or remote URL argument. The fresh test cluster uses trust authentication bound to `127.0.0.1`, contains only synthetic data, and is stopped in `finally`. Only HTTP processes launched by the runner are stopped. Existing PostgreSQL services, deployment servers, and live accounts are never used. Logs and the stopped test cluster are retained at the directory printed as `Preserved test artifacts`.

All environment overrides are restored when the runner exits. Test-only settings grant two initial tickets, charge one draft ticket, disable the development ticket top-up, and explicitly use the existing reward formula of three Resource Gold and three random cards per earned win. Audit logs are redirected into the new temporary directory. These are process-local fixtures, not deployment configuration changes.

## Assertions

- Twelve synthetic accounts are registered over HTTP: ten complete 33 authoritative draft selections, and two exercise run-start races and failure handling.
- Eight participant regression groups cover metadata tampering, live takeover, PVE/PVP reconnect, JSON recovery after both sockets disconnect, legacy recovery, practice battles and pre-start edits.
- Direct DB checks reject cross-account run results and started-match account/run/deck/seat rewrites. Rejected writes roll back player, connection, queue and match rows.
- Three actual WebSocket battles through the PVP matchmaking queue verify same-socket account-switch rejection, rejoin, live takeover and abrupt disconnect/reconnect with forged run/deck/card fields. Reconnect retains both player views, including hand runtime IDs, board and resources. The initial participants receive the correct wins/losses through the server's actual result persistence path.
- Twenty-four concurrent envelopes use one socket send at a time; state factories execute inside that gate. A close response waits for an in-flight send.
- Injected send failures and timeouts in PVE/PVP abort once, suppress queued state creation, and preserve the disconnected seat and battle state. Duplicate cleanup cannot extend grace or remove a replacement connection/session.
- DB checks cover late and duplicate disconnects, legitimate grace entry, and eight concurrent reconnect/old-disconnect races. Replaced connections cannot mark the current seat disconnected.
- Reader checks accept fragmented Korean UTF-8, exactly 64 KiB, and a normal 33-card Join; reject 64 KiB plus one byte across fragments, binary input and malformed/truncated UTF-8; and cover remote close and cancellation.
- The third real WebSocket battle also sends nine fragments totaling 65,537 bytes, observes close `1009`, then reconnects and verifies the original seat and both player views before surrender. Existing DB result assertions ensure rejected input does not change progression.
- Matchmaking checks cover the previously reproduced three-account assignment gap, four simultaneous reservations, reverse socket arrival, waiting for DB confirmation, duplicate account requests, same-connection retry, wrong-owner/seat rejection, downstream SQL failure rollback, reservation expiry and late cleanup. Pending in-memory join leases prevent a waiting-room disconnect from removing the shared session.
- Four additional authenticated clients join concurrently and form two actual WebSocket battles. Both battles complete and record exactly two wins and two losses, with one result per original run. There are five real WebSocket battles total.
- Client uploads of 33 wins, three losses, another account's deck ID, or partial results return HTTP 410 and `client_authoritative_run_result_removed`.
- Unauthenticated, malformed and empty uploads also return that tombstone.
- Full protected row snapshots prove rejected uploads do not change runs, decks, wallets, card collections, pack inventories, reward grants or account transactions.
- Premature reward claims are rejected, including claims with forged wins, losses, status and reward fields.
- Actual `BattleSession` surrender resolution produces immutable result batches; the harness passes those through `BattleResultStore.ApplyAsync`, the service used by the server. It never sets run counters using test SQL.
- A synthetic trigger fails the second participant's UPDATE. Both run updates, result receipt and match completion roll back, then succeed together on retry.
- Twelve simultaneous first deliveries and twelve replays prove a result increments its runs once. Reusing an ID with a conflicting payload is rejected.
- Draw receipts are replayable and complete both PvP seats without changing run counters.
- A wrapper drops the response after a real DB commit; a separate process replays the retained file without another increment.
- A genuinely unreachable loopback DB retains the journal. A separate process with the test DB restored applies it.
- The actual battle engine journals surrender and reconnect-timeout forfeiture before any socket send. Snapshot restoration preserves the result UUID; legacy active recovery is stable and unsafe legacy ended snapshots are rejected.
- The hosted worker delivers without client activity and keeps malformed journals available for repair while continuing valid deliveries.
- Thirty-four synthetic battles produce 38 authoritative run-result records: one run reaches 33 wins/one loss and the other one win/three losses. The result is consumed once per battle.
- Reward claims use persisted results even when the request includes forged reward fields. The two completed runs receive exactly 99 gold/99 cards and three gold/three cards respectively.
- Repeated claims return HTTP 409 `reward_already_claimed` and leave protected rows unchanged. This verifies no double payment; the API does not promise repeated HTTP 200 responses.
- Completed runs reject further result increments.
- A second isolated HTTP process with no DB configuration still returns the same tombstone for valid, malformed and empty uploads.

## Limits

This is bounded integration coverage of HTTP/WebSocket endpoints, participant binding, result delivery, connection stability, matchmaking reservations and PostgreSQL reward transactions. The 34 progression battles and participant regressions run in-process; five additional battles use real WebSockets and matchmaking. Snapshot recovery uses JSON serialization and a new BattleSession instance. Durable outbox replay runs in four independent child processes. SQL failure and commit-response loss are injected; DB connection failure uses an unreachable local port. Reservation expiry is injected by moving only the synthetic reservation's deadline into the past. Transport timeout/send/close tests use a controllable WebSocket, while abrupt client disconnect uses a real ClientWebSocket. This does not simulate physical disk failure, a killed complete HTTP server during an arbitrary instruction, sustained load, all event ordering, Unity presentation, or every socket failure path.

The isolated runner raises the account HTTP fixture limit to 600 per window so ten accounts can complete their 33-card drafts in one run. The fixture also allows 100 WebSocket upgrades per window and sets the authentication deadline to 5 seconds for wire tests. Deterministic clock tests verify the 30-second production default; message and join budgets and the account socket cap use production defaults. These fixture overrides do not change the live server. The fresh test database applies migrations through `0015_account_operation_receipts`.

The runner sets `PROJECT333_BATTLE_RESULT_OUTBOX_DIR` inside its fresh temporary artifact directory. It never reads the deployed server's outbox. Child replay verifies the same loopback DB identity/data directory and is restricted to that run's artifact tree.

The runner compiles the harness and saves `harness-build.log`, `checks.log`, `no-db-checks.log`, isolated HTTP stdout/stderr, PostgreSQL lifecycle logs and test audit files. It does not rebuild or deploy the server itself; always supply a fresh server build from the source revision being checked.


## Connection guard regressions

The 2026-09-09 guard run passes 51 groups plus four no-DB tombstone checks. It covers message/join burst boundaries and monotonic refill, capped idle credit, invalid configuration, unrenewable authentication expiry, late DB authentication, immutable unseated identity, 20 concurrent account admissions, idempotent release and replacement ownership. Real sockets exercise KeepAlive/invalid JSON/unsupported command floods, join-rate rejection, authenticated unseated connection capacity, released capacity reuse, and authentication timeout during heartbeat and partial-message traffic. Admission-only tests compare match/reservation/run/reward aggregates before and after.

The existing three-battle binding test additionally rejects an authenticated socket for its message rate, reconnects its original account, compares both player views and completes the battle. The existing result assertions still require exactly three losses and three wins on the original accounts. There are still ten synthetic accounts and five actual WebSocket battles; no extra live accounts or deployment are involved.
## Run-start concurrency regressions

The run-start change passed 58 groups plus four no-DB tombstone checks before account-operation coverage was added. A held wallet row forces eight independent service calls to reach the same contention point before either can proceed. On the pre-fix binary this deterministically created two runs and spent two tickets. The fixed service creates one run/debit and returns seven `active_run_exists` errors. Eight actual authenticated HTTP requests also produce one success and seven HTTP 409 responses.

The checks compare full wallet/run/offer/ledger snapshots across retries, canceled waits, insufficient balance and a synthetic failure at the final ticket-ledger insert. They verify another account can proceed while one account is locked, all three active states and older unclaimed rewards remain blockers behind newer abandoned history, claimed completion allows a new run, and the DB index rejects duplicate inserts and reactivation. Only the two new synthetic accounts receive SQL lifecycle fixtures and a wallet top-up; these fixtures do not represent actual earned game results. Existing battle-result/reward regressions remain engine driven. Totals are twelve accounts, 34 progression battles and five real WebSocket battles.

This covers concurrent/repeated starts while a blocking run exists, not request-ID replay after all prior runs have ended and rewards are claimed, Upgrade/ticket-purchase retry semantics are covered in the section below.
## Account-operation retry regressions

The current full suite passes 71 groups, two operation replays and durable-cancellation verification after a real isolated HTTP server restart, and four no-DB tombstone checks. It reuses the two run-start synthetic accounts: totals remain twelve accounts, 34 progression battles and five real WebSocket battles. The added checks cover concurrent purchase/upgrade retries, different request IDs at one expected level, changed payloads, account separation, current-state responses for old receipts, missing/invalid request IDs and levels, final receipt-insert failure and atomic rollback, canceled lock waits, and replay with zero balance.

Only the synthetic fixtures receive SQL wallet/card top-ups. The runner stores their request bodies and synthetic tokens in its own temporary `operation-replays.json`, restarts only its own server process and verifies that both committed operations remain replayable with unchanged wallet/collection/ledger/receipt snapshots. It never reads real account credentials. This restart occurs after commits; it does not simulate killing the process at every instruction.

Client pending-request tests live in `Server/Project333.AccountOperationClientChecks`. See `docs/account_operation_retries.md` for the API contract, client behavior, compatibility and rollout order.