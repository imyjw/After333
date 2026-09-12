# After333 stability check - 2026-09-06

## Scope and environment

- Starting revision: `8933d0a` on `main`.
- Unity: 6000.3.12f1, Windows Standalone target, EditMode batch runner.
- Server: .NET 8 Release build.
- PostgreSQL: separate temporary PostgreSQL 18 cluster on 127.0.0.1:15433.
- Test server: 127.0.0.1:17333, with synthetic accounts and no real credentials.
- The user's existing database and running Unity project were not used for tests.
- Unity tests ran in the existing detached worktree under
  `Backups/main-merge-20260905`, with the changed asset and test source copied in.
- Temporary servers and the PostgreSQL cluster were stopped after testing.
- Changes from this check are not committed or pushed automatically.

## Confirmed defects and fixes

### Unauthenticated battle entry

Before the fix, both an anonymous JoinMatch request and a ClientCommand sent
before joining could receive an assigned seat. This was reproduced against the
isolated database-backed server, not just inferred from source inspection.

Program.cs now rejects JoinMatch without a session token. The existing auth
service validates supplied tokens. A ClientCommand may only use the match already
joined by its connection; it cannot create a session or switch matches implicitly.

Retest: anonymous JoinMatch returns `missing_session_token`; a command before
joining returns `join_required`. No battle session is created by either request.
The normal authenticated two-client match and reconnect flow also passed.

Reusable regression check:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Project_333\Server\Project333.PvpServer\Tools\TestBattleAuthentication.ps1 -BaseUrl http://127.0.0.1:7333
```

### Golem asset mismatch

The JSON definition had physical defense 1, but Golem.asset still had 0.
The Unity asset now matches the confirmed card rule and server definition: 1.

### Stale ManaStone validator test

The first EditMode run passed 639/640 tests. The failing assertion still expected
the old `immediate mana +6` message, while ManaStone now grants mana +3.

The expectation was updated to +3. The shared fixture now supplies 13 independent
non-legendary draft cards, so a tested card excluded from drafting does not create
an unrelated pool-size error. The ManaStone test asserts exactly its intended error.

## Verification results

| Check | Result |
| --- | --- |
| Final Unity EditMode run | 640 passed, 0 failed, 0 skipped |
| Server Release build after server fix | 0 errors, 92 nullable-analysis warnings |
| Card database validator | All 41 definitions passed |
| Starter card asset cost/stat comparison | Golem defense mismatch corrected; no other compared cost/stat mismatches |
| Asset GUID scan | 3,194 GUID entries; no duplicate groups |
| Six enabled build scenes | All files exist; referenced script GUIDs resolve in Assets/package cache |
| HTTP /health and /server/status | Passed |
| WebSocket connection | Passed |
| Fresh PostgreSQL migrations 0001 through 0011 | Passed |
| Restart with migrations already applied | Passed |
| Game ID registration/login | Same account restored |
| Refresh/logout | Tokens rotated; revoked sessions rejected |
| Server-authoritative draft | Offer persistence, re-login restore and completed 33-card deck passed |
| Simulated signed ad callback | Ticket +1; duplicate callback grants no additional ticket |
| Card upgrade API | Golem Lv.0 to Lv.1 consumes 3 copies and 3 gold |
| Ticket purchase API | 3 gold consumed; 1 ticket granted |
| Run start | 3 tickets consumed |
| Two-win completed-run reward | 6 gold and 6 cards granted; duplicate claim rejected |
| Account refresh after reward | Wallet preserved; no automatic ticket refill |
| New run after claim | New run created; tickets charged once |
| Anonymous battle access | Both entry paths rejected after fix |
| Two authenticated PvP clients | Same match assigned |
| Hidden hand information | Opponent card identities omitted for both viewers |
| Mulligan and EndTurn | Main phase reached; next turn synchronized |
| Disconnect/reconnect within grace | Same match, seat and turn restored |
| PvP surrender | Same winner on both clients; win/loss persisted to respective runs |

The reward API test seeded a completed 2-win/3-loss run in the temporary database.
It verifies claiming, wallet/collection updates and duplicate rejection, not a full
sequence of five played PvE battles. Test resources were seeded only in this cluster.

## Remaining checks and limitations

- 92 existing nullable-analysis warnings remain: CS8625 (42), CS8765 (1),
  CS8618 (13), CS8603 (13), CS8600 (3), CS8601 (20).
- Android build, real-device layout, audio playback and actual network switching
  were not exercised by the Windows EditMode runner.
- Google browser login and actual LevelPlay ad delivery were not tested.
  The ad check uses a synthetic signing key and callback against the local server.
- Public HTTPS/WSS routing, sustained concurrent-user load, 60-second grace expiry
  and server-crash snapshot recovery were not exercised in this run.
- No exhaustive proof of every card interaction or security boundary is implied.
- Restart the normal server to activate the Program.cs authentication fixes.

## Local evidence

Evidence is under `TempBuild/StabilityCheck20260906` (ignored by Git):

- `EditMode.xml`: original 639/640 result.
- `EditMode-final.xml`: final 640/640 result.
- `UnityEditMode.log`, `UnityEditMode-final.log`: Unity execution logs.
- `server-build-final.log`: server build and warning details.
- `TestGameIdAuth.log`, `TestRefreshAuth.log`, `TestServerAuthoritativeDraft.log`,
  `TestRewardedTicketAd.log`: integration script output.
- `integration-server.out.log`, `meta-server.out.log`, `pvp-server.out.log`:
  server-side integration evidence.
- `CheckBattleAuth.ps1`, `CheckPvp.ps1`: isolated local check drivers.

Logs and temporary account tokens are local test evidence, not public artifacts.
