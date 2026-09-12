# Android, public endpoint and concurrent PvP checks

Date: 2026-09-06 (KST)

## Scope and isolation

- Current server source includes the preceding stability-check authentication fixes.
- Load test origin: http://127.0.0.1:17333, PostgreSQL 18 on 127.0.0.1:15433.
- Separate temporary database: after333check. No production wallet, collection,
  run or match records were changed by the load test.
- Synthetic accounts were registered through the real API; every account built
  a 33-card deck through 33 server-authoritative selections before connecting.
- Load generator, server and database ran on the same laptop: AMD Ryzen 5 PRO
  5650U, 12 logical processors, approximately 30.8 GiB RAM.
- Normal game server remained on port 7333. Both temporary processes were stopped
  after the load test. Some audit entries from synthetic accounts use the shared
  local audit directory; these do not represent production account activity.

## Concurrent PvP results

| Concurrent clients | Concurrent matches | Passed matches | EndTurn samples | Median ms | P95 ms | Maximum ms | Peak server working set MiB |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2 | 1 | 1 | 12 | 7.74 | 11.43 | 11.43 | 89.0 |
| 10 | 5 | 5 | 60 | 6.17 | 14.93 | 169.49 | 127.5 |
| 30 | 15 | 15 | 180 | 5.80 | 14.07 | 121.61 | 206.1 |
| 50 | 25 | 25 | 300 | 11.38 | 22.52 | 24.82 | 251.1 |

All 46 matches passed, with 552 successful EndTurn measurements. No test-stage
errors, server exceptions or persistence failures were found in the load logs.
Each measurement covers sending EndTurn until both clients receive the next
StateView, not just an HTTP acknowledgement. The test verifies:

- Authenticated matchmaking and distinct pairs.
- Opponent hand identities hidden from both viewers.
- Both mulligans completed and Main phase entered.
- Exactly one turn advance and matching active player on both clients.
- Application keepalive traffic every three seconds, including 15 seconds idle.
- Surrender winner synchronized to both clients.
- Winner's run gains one win; loser's run gains one loss, read through /me.

### Important limits

This is a short concurrent integration/burst test, NOT a maximum-capacity claim
or a sustained production soak test. Each measured stage lasted approximately
18 seconds, excluding account/deck provisioning and connection setup.

All synthetic clients used one source IP. Only the isolated server's per-minute
limits were raised: auth 2000, account 10000, battle connect 2000. Production
rate limits were not changed. The results do not test the default same-IP limits.

The workload used initial master boards and turn changes, not many simultaneous
summons, animations, persistent-area effects, reconnect storms or database-scale
history. CPU averages include idle time; working set covers only the game-server
process, not PostgreSQL, Unity or the load generator. Do not extrapolate this to
50 real mobile clients over the public tunnel or an unlimited player count.

## Public HTTPS/WSS checks

- https://api.after333.com/server/status: server and database Ok.
- Google readiness: Configured=true, AcceptedClientIdCount=2,
  DesktopCodeExchangeConfigured=true.
- LevelPlay readiness: callback and app key configured; reward one ticket,
  daily limit three. This is configuration evidence, not an ad payout test.
- Anonymous WSS JoinMatch rejected with missing_session_token.
- WSS ClientCommand before authenticated joining rejected with join_required.

Ten sequential public /health response times, milliseconds:
1088.8, 627.0, 318.4, 287.3, 297.2, 286.7, 331.3, 281.0, 383.1, 395.9.
These include HTTP/network/client overhead and are not WebSocket command timings.
The first sample may include connection setup. They cannot identify a specific
network hop as the cause. Public latency must not be confused with local load
test latency.

## Android and actual authentication/ad checks

USB debugging connected successfully to an SM-S936N physical phone. The installed
package com.after333.game reported versionName 1.0, versionCode 1, target SDK 36,
last updated 2026-08-06 11:43:12. It was NOT rebuilt or replaced during this check;
therefore device results do not validate the latest September source build.

After starting the app, Android logs show:

1. Credentials loaded from Android Keystore with session and refresh credentials.
2. Saved session restoration attempted through https://api.after333.com.
3. Expired/invalid saved session rejected with HTTP 401.
4. Refresh credential restored the registered account successfully.
5. Corresponding auth.session.refresh.success server audit event at
   2026-09-06 00:52:41 KST.

The initial 401 was recovered by the refresh flow and was not a final login failure.
The process remained active. Platform logs also contained a dex-finalizer assertion
and Google configuration warnings; no app-fatal AndroidRuntime crash was observed
in the inspected interval. This does not establish that those warnings are harmless
under every condition.

The user was asked to log out and sign in with Google on the phone, then watch ONE
rewarded ad and report ticket counts before/after. As of report creation, no new
completion was confirmed. Fresh Google OAuth and real ad reward delivery remain
PENDING, not passed. No live callback was forged and no repeated real ad views or
advertisement-link clicks were automated.

Other untested items: current-build Android installation, network switching during
battle, UI/audio quality, long-running load, mass reconnect, production DB scale.

## Reproduction evidence

Ignored local directory: TempBuild/StabilityCheck20260906.

- LoadHarness/Program.cs and LoadHarness.csproj: concurrent test client.
- RunLoad.ps1: starts/stops isolated server and PostgreSQL.
- load-results.json: measured stage results.
- load-server.out.log and load-server.err.log: server logs.
- postgres.log: temporary cluster lifecycle.
- Server/Project333.PvpServer/Tools/TestGoogleAuthReadiness.ps1: readiness probe.
- Server/Project333.PvpServer/Tools/TestBattleAuthentication.ps1: unauthorized access probe.

Synthetic credentials/log identifiers are local test artifacts and should not be
published. Existing source changes from the preceding stability check were kept;
no commit or push was performed for this follow-up.
