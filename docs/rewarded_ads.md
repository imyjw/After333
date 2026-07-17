# Rewarded Ads

## Goal

The Game Start scene can show an opt-in rewarded video. A successfully verified completion grants exactly `1` account ticket.

The Unity client never grants the ticket directly. PostgreSQL wallet data changes only after the server verifies a Unity LevelPlay server-to-server callback.

## Temporary Product Rules

- Placement: `start_ticket_reward`
- Reward: `1` ticket
- Daily limit: `3` rewarded tickets per account, reset at `00:00 UTC`
- Reward cooldown: `60` seconds per account
- Guests and registered accounts are both eligible because both are server accounts.
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
4. Copy the Android App Key and Rewarded Ad Unit ID into the `RewardedTicketController` component in `GameStart_VSlice`.
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
- `PROJECT333_REWARDED_AD_DAILY_LIMIT=3`
- `PROJECT333_REWARDED_AD_COOLDOWN_SECONDS=60`
- `PROJECT333_REWARDED_AD_ATTEMPT_TTL_MINUTES=30`

Local launcher example:

```powershell
powershell -ExecutionPolicy Bypass `
  -File C:\Project_333\Server\Project333.PvpServer\Tools\RunServer_Local.ps1 `
  -DatabaseConnection 'Host=127.0.0.1;Port=5432;Database=project333;Username=postgres;Password=dev_password' `
  -LevelPlayAppKey '<ANDROID_APP_KEY>' `
  -LevelPlayPrivateKey '<S2S_PRIVATE_KEY>'
```

Cloudflare Tunnel launcher example for `https://api.after333.com`:

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
