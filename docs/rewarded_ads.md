# Rewarded Ads

**2026-10-20 운영 기록 갱신:** 서울 Lightsail + Caddy + PostgreSQL18로 이전했고 공개 주소는 `https://api.after333.com`, 운영DB는 `project333_live`다. 사용자 전환 결과에서 migration24개/latest`0024_elf_elder_card_id`를 확인했다. 아래 과거 날짜의 노트북/Tunnel·DB버전·미배포 문구는 당시 기록이다. 현재 실행·백업 절차는 [Lightsail 운영](lightsail_operations.md), 확인 범위와 남은 시험은 [현재 상태](current_project_status.md)를 우선한다. 오늘 원격 재확인을 수행하지 않았다.

## Goal

The Shop scene can show an opt-in rewarded video. A successfully verified completion grants exactly `1` account ticket.

The Unity client never grants the ticket directly. PostgreSQL wallet data changes only after the server verifies a Unity LevelPlay server-to-server callback.

## Temporary Product Rules

- Placement: `start_ticket_reward`
- Reward: `1` ticket
- Daily limit: `10` rewarded tickets per account, reset at `00:00 UTC`
- Reward cooldown: `30` seconds per account
- Authenticated registered accounts are eligible.
- Closing or skipping an ad does not grant a ticket.
- A LevelPlay event ID can grant a reward only once.

## End-to-End Flow

1. Unity logs in and initializes LevelPlay with a stable installation user ID.
2. The player presses `광고 보고 티켓 +1`.
3. Unity calls `POST /ads/rewarded-ticket/attempt` with that LevelPlay user ID.
4. The server checks the daily limit and cooldown, then returns an opaque `dynamicUserId`.
5. Unity calls `LevelPlay.SetDynamicUserId` immediately before showing the rewarded ad.
6. LevelPlay calls `GET /ads/levelplay/rewarded-callback` after a completed ad.
7. The server verifies the private-key signature, event ID, provider user ID, opaque attempt token, reward amount, placement, and account limits in one database transaction.
8. The server increments the wallet ticket balance and records an `ad_reward` account transaction.
9. Unity polls `GET /ads/rewarded-ticket/attempt/{attemptId}` and updates the visible wallet only after status becomes `granted`.

## Database

Migration `0009_rewarded_ads.sql` adds:

- `rewarded_ad_attempts`: account-owned, short-lived attempts and final grant status
- `rewarded_ad_events`: unique provider event IDs and callback processing results
- `ad_reward`: an allowed `account_transactions.transaction_type`

The unique `(provider, provider_event_id)` constraint and a transaction-level wallet update make callback retries idempotent.

## LevelPlay Dashboard Setup

1. Create the Android app for package `com.after333.game` in the LevelPlay dashboard.
2. Create a Rewarded Ad Unit and a placement named `start_ticket_reward`.
3. Set the placement reward amount to `1`.
4. Copy the Android App Key and Rewarded Ad Unit ID into the `RewardedTicketController` component in `Shop_VSlice`.
5. Open the app's `Set S2S callback` page.
6. Use this callback endpoint:

   `https://api.after333.com/ads/levelplay/rewarded-callback`

7. Add mandatory callback query parameters with these exact parameter names:

   `userId=[USER_ID]&rewards=[REWARDS]&eventId=[EVENT_ID]`

8. Add optional parameters using these exact names:

   `placementName=[PLACEMENT_NAME]&appKey=[APP_KEY]`

   LevelPlay labels these as optional, but After333 requires `appKey` whenever `PROJECT333_LEVELPLAY_APP_KEY` is configured.

9. Configure a private key. Use the same value as `PROJECT333_LEVELPLAY_PRIVATE_KEY` on the After333 server.
10. LevelPlay supplies `timestamp`, `signature`, and the SDK dynamic user ID (`dynamicUserId`) to the callback. Confirm those exact names in the callback preview before saving.

The final URL should therefore reach the server with `eventId`, `userId`, `dynamicUserId`, `rewards`, `timestamp`, `signature`, `placementName`, and `appKey` query values.

## Server Configuration

Required:

- `PROJECT333_LEVELPLAY_PRIVATE_KEY`
- `PROJECT333_LEVELPLAY_APP_KEY`

Optional defaults:

- `PROJECT333_LEVELPLAY_REWARDED_PLACEMENT=start_ticket_reward`
- `PROJECT333_REWARDED_AD_REWARD_TICKETS=1`
- `PROJECT333_REWARDED_AD_DAILY_LIMIT=10`
- `PROJECT333_REWARDED_AD_COOLDOWN_SECONDS=30`
- `PROJECT333_REWARDED_AD_ATTEMPT_TTL_MINUTES=30`

Local launcher example:

```powershell
powershell -ExecutionPolicy Bypass `
  -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Local.ps1 `
  -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password' `
  -LevelPlayAppKey '<ANDROID_APP_KEY>' `
  -LevelPlayPrivateKey '<S2S_PRIVATE_KEY>'
```

Legacy Windows/Cloudflare Tunnel launcher example (before the Lightsail cutover):

```powershell
powershell -ExecutionPolicy Bypass `
  -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_CloudflareTunnel.ps1 `
  -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password' `
  -LevelPlayAppKey '<ANDROID_APP_KEY>' `
  -LevelPlayPrivateKey '<S2S_PRIVATE_KEY>'
```

Never put the LevelPlay private key in Unity, Git, a scene, or a client build.

## Verification

After restarting the configured server:

1. Open `/server/status` and confirm `RewardedAds.CallbackConfigured=true` and `RewardedAds.AppKeyConfigured=true`.
2. Build and test on a physical Android device. Editor ad behavior is not the production proof.
3. Complete one rewarded ad and confirm the visible ticket balance increases by exactly one.
4. Confirm PostgreSQL has one `rewarded_ad_attempts` row with `attempt_status='granted'` and one `account_transactions` row with `transaction_type='ad_reward'`.
5. Retry the same callback event and confirm no second ticket is granted.
6. Confirm the fourth completed ad in the same UTC day is rejected by the daily limit.

The signed callback and duplicate-event protection can be checked without consuming a real ad:

```powershell
powershell -ExecutionPolicy Bypass `
  -File C:\Project_333\Server\Project333.PvpServer\Tools\TestRewardedTicketAd.ps1 `
  -BaseUrl 'http://127.0.0.1:7333' `
  -PrivateKey '<S2S_PRIVATE_KEY>' `
  -AppKey '<ANDROID_APP_KEY>'
```

This test creates a temporary guest account, grants one signed test reward, then replays the same callback and verifies that the wallet does not increase twice.

## Troubleshooting `invalid_callback_signature`

This error means the callback reached After333, but the S2S private key used by LevelPlay does not exactly match `PROJECT333_LEVELPLAY_PRIVATE_KEY` on the running server. It is not a Unity client, App Key, Ad Unit ID, database, or tunnel failure.

1. Open the Android app's `Set S2S callback` page in LevelPlay.
2. Copy or replace the value in that page's **Private key** field.
3. Stop the After333 server and restart it with that exact value in `-LevelPlayPrivateKey`.
4. Do not use the Android App Key, Rewarded Ad Unit ID, placement name, LevelPlay API secret, or the literal `<S2S_PRIVATE_KEY>` placeholder as this value.
5. Keep capitalization and every character identical. Do not add quotes as part of the key value or add leading/trailing spaces.
6. Save the S2S callback and retry. A successful server log contains `result=granted` instead of `code=invalid_callback_signature`.

LevelPlay retries callbacks until it receives HTTP 200 with `[EVENT_ID]:OK`, so repeated rejection lines for the same event are expected while the key is mismatched. After correcting and restarting the server, a later retry can grant the still-pending attempt without another ad view.

## Troubleshooting `509 Mediation No fill`

`509 Mediation No fill` means that LevelPlay initialized and accepted the rewarded-ad request, but none of the active ad sources returned an ad. It is not a ticket-server error and no ticket should be granted because no ad was watched.

For an unpublished development build:

1. Open the LevelPlay dashboard and go to `Unity LevelPlay > Settings > Test devices`.
2. Select the After333 Android app.
3. Add the physical Android device with its Advertising ID.
4. Enable test ads for an active ad source and the After333 rewarded ad unit.
5. Confirm that the App Key and Rewarded Ad Unit ID belong to the same Android app and that the ad unit format is Rewarded.
6. Restart the Android build after the dashboard setting has propagated.

Use the LevelPlay Integration Test Suite when the ad source, adapter, or ad-unit assignment is unclear. Enable `is_test_suite` before SDK initialization, launch the suite after initialization succeeds, and remove the test-suite launch from release builds.

The After333 client retries failed rewarded-ad loads with backoff delays of `5`, `15`, `30`, and then `60` seconds. A retry can recover from temporary inventory shortages, but it cannot replace missing dashboard test-mode, ad-source, or mediation-group configuration.

## Deferred Before Store Release

- GDPR/CCPA/COPPA consent flow and regional privacy review
- Production ad test-device configuration and removal of test mode
- Store privacy disclosures and Google Play Data safety answers
- Final daily limit, cooldown, and economy balancing
- Monitoring for callback latency, rejection rate, and suspicious completion patterns

## Automatic verification recovery (2026-09-12)

The client saves the server attempt ID per account/server before entering the ad SDK. A persistent recovery worker queries that same attempt automatically; a 30-second polling window, network failure or scene exit no longer discards it. App relaunch resumes verification after login. Each HTTP request uses the common 20-second deadline; subsequent verification is retried approximately every five seconds while online. SDK completion never grants currency.

Only server `granted` with a wallet updates the displayed balance. Server `expired`/`rejected` releases the pending record without a reward. Unknown, malformed or failed responses retain it. Account/session/endpoint changes and mismatched attempt IDs prevent stale response application.

A completion-marked attempt blocks another viewing while unresolved. An ad closed without a completion signal retains its ID but can be retried explicitly after checking server status; a terminal server result is resolved instead of replaying it. A late completion signal blocks replay. After a process exit without either SDK callback, a matching server pending response now permits an explicit retry when no ad is showing and no completion was recorded. The saved attempt is retained for late rewards, and the normal show path checks the server again before reusing it. An ad that starts during the status request or a completion callback racing with that request must remain blocked. No local timer pretends to cancel a server attempt.

Verification is automatic, with no previous-result button or reconnect instruction. This source change has passed 11 isolated client recovery groups and Unity runtime compilation; actual provider callbacks and physical-device checks of the new client remain separate. [Implementation and evidence](C:/Project_333/TempBuild/RewardedAdRecovery_20260912/README.md)
## Interrupted ad relaunch fix (2026-09-12)

A physical-device report confirmed one completed ad paid one ticket, but force-closing the app during an ad left the button disabled on relaunch. The saved non-replayable state required a Closed callback that process death cannot deliver. Recovery now restores retry eligibility only after a matching pending server response and while no SDK display is active; it retains the same attempt ID and never grants a local reward. Completed attempts still wait for server resolution. No new button or recovery message was added.

The new process-death regression failed against the previous code. The fixed code passes14 client recovery groups (including late reward/display races) and1043 Unity EditMode tests. Device relaunch verification of this fix requires a new client build; no server change or redeployment is needed. [Evidence](../TempBuild/AdInterruptedRecovery_20260912/README.md).

## Device follow-up and previous-process completion marker (2026-09-12)

The user reproduced the disabled button with the first fix installed. Read-only device/DB checks observed that the button enabled when the unpaid attempt expired at23:23:24 KST, rather than on initial recovery. Installed private metadata could not be read, so its exact flags are not claimed as confirmed.

A further source regression was reproduced: a completion signal persisted by a previous app process continued to block retry after a matching server pending response. Recovery now releases that previous-process presentation marker only after verified pending status and with no current-process SDK observer. The original attempt ID remains for late S2S payment; live-process completion and callback races remain protected. Server-only grants and attempt idempotency are unchanged.

The new test failed before this change;15 isolated recovery groups pass afterward. Added application diagnostics distinguish pending lock, SDK readiness, HTTP failures and recovery outcomes without logging credentials or account/attempt IDs. The revised client still requires user build/install and real-device interruption verification. No server redeployment is required. Evidence: TempBuild/AdRecoveryDeviceDiagnosis_20260912/README.md.
Unity verification of this follow-up:1043 EditMode tests passed (0 failures/skips) in the isolated project copy; this does not replace Android ad/kill/relaunch verification.

Device follow-up23:49 KST: user selected ironSource Bidding + Rewarded and Test ads for the registered phone (network/format had been empty). The same installed app/process then logged ready=True and enabled=True, pending=False/blocked=False. No code or build change was needed to resolve this no-fill episode. The original ad interruption/relaunch verification remains pending.

## Single test-ad interruption retest passed (2026-09-12,23:54 KST)

The user repeated ad start → app close before completion → relaunch and confirmed the button re-enabled. New-process logs show pending=True/blocked=True at shop entry, a matching pending recovery response with replay=True, then SDK ready=True and button enabled=True at23:54:01.791 (about6 seconds after recovery-worker startup). Read-only DB verification confirms the interrupted attempt was still pending, expired only at00:23:20, and had no reward timestamp or callback. Thus this reproduction recovers without waiting for30-minute expiry, retains the original server attempt, and records no unverified reward. The original reported interruption/relaunch button defect is verified resolved for this tested device scenario. Evidence: TempBuild/AdRecoveryDeviceDiagnosis_20260912/device-interruption-retest.log and RecentAdStates-retest.json.
## Repeated real-ad interruption and hidden no-fill status (2026-09-13)

The user reset Test devices targeting and repeated interruption/relaunch with real ads. The earlier single test-ad retest does not establish that real ads remain available on repeated attempts.

Device logs separate two states: saved-attempt verification and SDK readiness. At00:07:52 KST, pending recovery set replay=True and the button reported blocked=False; repeated509/no-fill loads kept ready=False. At00:11:49 the SDK loaded an ad and the button immediately enabled while the pending attempt remained saved. After the next interruption, recovery again unlocked at00:14:26, but SDK loads continued failing through00:28:19. The automatic load retries were running. This observed disabled state was SDK unavailability rather than an unresolved client lock.

A client UI bug hid the explanation: BuildStatusText returned empty for every saved pending attempt, including attempts already eligible for replay. The guard now checks IsBlocking instead of HasPending. Thus replayable attempts show SDK loading/no-fill/retry/ready status while retaining the attempt for late S2S rewards. Current server eligibility/request errors remain visible. Playback text is supplied by SDK state instead of cached after TryShow, so a closed ad cannot leave a stale playback notice. Blocking verification remains automatic and silent; no recovery button or reconnect instruction is introduced.

This source fix corrects missing status text; it cannot guarantee real ad inventory. The exact cause of repeated real-ad no-fill still requires provider/network configuration and serving diagnostics. Do not infer a punishment for force-closing, a fixed waiting time, or a changed reward threshold from these logs. Official guidance: https://docs.unity.com/en-us/grow/levelplay/sdk/unity/troubleshoot-no-ads-displaying

Evidence and focused regression results: TempBuild/AdRealInterruption_20260913/README.md. Actual APK display requires the user's next build/install; test-ad success alone is not a production-fill acceptance test.
Validation: new status regressions reproduced7 failures before the fix;13 focused Unity tests and15 existing recovery groups pass after the fix. No APK build/install was performed.
