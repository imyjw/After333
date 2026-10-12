# Purchase and upgrade completion recovery

Actual release status (2026-10-02): a separate approved deployment applied DB0020/0021/0022 and the card-pack purchase/receipt server, and built Android APK(versionCode5). [Actual release evidence](shop_card_packs.md) records backup, verified preservation exceptions and endpoint checks. No real-account purchase or physical-device runtime test was performed; the earlier source-review statements retain their dated scope.

Source documentation reviewed 2026-10-02 for shop card-pack purchases. Ticket prices are unchanged. Card upgrade costs follow the [current target-level schedule](meta_rules.md#card-upgrade-costs), including Lv.2 at 6/6. Card packs cost 33 Resource Gold and immediately grant ten copies. The common completion/cancellation protocol retains its original implementation and validation dated 2026-09-10.

## User interaction

Buttons retain their normal purchase/upgrade labels. There is no previous-result confirmation button and no reconnect/recovery instruction. Duplicate purchase/upgrade actions are disabled while an unresolved account operation exists. A transient failure clears the in-progress message without claiming cancellation. After connectivity returns, the app silently resolves the saved request and refreshes the account, then shows only the final completed/cancelled result.

A scene-independent recovery component starts with the app and checks pending work every five seconds while authenticated and online. It skips requests still being sent. Pending metadata survives scene changes and app restarts; older per-card pending keys are discovered from the account collection. It never automatically resends a purchase/upgrade POST. A new charge requires a new explicit action after resolution.

## Transaction protocol

Authenticated ticket/card-pack purchase and upgrade requests require a nonempty D-format UUID RequestId. Upgrades require the displayed ExpectedUpgradeLevel. Their existing wallet lock, economy mutation, ledger entry and success receipt remain one READ COMMITTED transaction. Original same-ID retries remain idempotent for older clients; matching a cancelled ID never charges. Replaying a committed upgrade after the cost revision preserves its original paid UpgradeCost and returns current wallet/collection state; it does not charge the new table again. The current authoritative costs require the ordered database migrations `0020_card_upgrade_costs_revision.sql` and `0021_card_upgrade_level2_cost_revision.sql`; 0021 changes only the Lv.1 -> Lv.2 transition to 6 copies/6 Resource Gold. Existing receipts keep their original paid cost, including 5/5 receipts from before 0021.

POST /account/operations/resolve with { "RequestId": "UUID" } uses the authenticated account identity, validates the UUID, takes the SAME wallet row lock as purchase/upgrade, and:

- If a committed success receipt exists, returns Status "completed".
- Otherwise inserts an idempotent permanent cancellation tombstone and returns Status "cancelled".

The response contains AccountId, RequestId and Status. Resolution itself never changes gold, tickets or cards. It is a POST because cancellation is durable, not a read-only lookup. Wallet locking prevents a "missing" lookup from racing with an in-flight purchase: completion or cancellation wins, never both. Delayed original requests check the cancellation table under that lock and return operation_cancelled before charging.

Migration 0016_account_operation_cancellations adds tombstones. Keep both success receipts and cancellation tombstones for the account lifetime; deleting either would make delayed requests unsafe. The endpoint is covered by account API rate limiting.

## Card-pack receipts and presentation

`card_pack_purchase` uses the same wallet lock, permanent cancellation tombstones and success receipts as ticket purchases/upgrades. `POST /shop/purchase-card-pack` spends 33 Resource Gold and grants the original ordered ten card copies in one transaction. The receipt retains duplicate cards and draw order. `GET /shop/card-pack-purchases/{requestId}` reads the authenticated account's original successful receipt and current wallet without spending, granting or rerolling. A committed replay keeps the original ten even if the later reward pool changes. Invalid or missing receipts never authorize speculative grants.

The saved card-pack request participates in scene-independent automatic resolution and duplicate-operation blocking. Before clearing a confirmed request, recovery retains the pack's local account/server/request presentation marker so leaving during receipt refresh or opening can restore the same cards on shop reentry. It does not automatically send a new purchase POST. Cancelling an absent original request clears its presentation intent without showing a pack.

Opening is presentation only: the cards are already in the server collection at purchase commit, and `/me` supplies current ownership. Quitting before clicking, during animation or before dismissing results never removes the paid cards. The explicit result dismissal clears only the corresponding local presentation marker. This marker does not provide cross-device read synchronization or survive app-data deletion; committed collection/receipt data remains server-owned. See [card packs](shop_card_packs.md) for the full odds, API and failure policy.

## Client confirmation

Saved metadata is scoped by server/account/operation/card and contains no credentials. Recovery validates the returned account, request ID and terminal status, then fetches /me. It applies the wallet and collection and clears pending metadata only after that refresh succeeds and the account, token and endpoint still match. Invalid replies, unavailable network/server, failed refresh and account switches retain pending work for a later attempt. A new worker can recover that state after app restart.

## Rollout

The server release used by the client must contain `/account/operations/resolve` and migration `0016_account_operation_cancellations`. Card-pack clients additionally require `/shop/purchase-card-pack`, `/shop/card-pack-purchases/{requestId}` and migration `0022_card_pack_purchase_receipts.sql` to extend the existing receipt operation-type constraint. A release without the required endpoint/schema cannot complete that feature; unresolved work stays pending and duplicate actions stay disabled. Current source contains the endpoint; this documentation review does not identify or update the running server release. Do not roll back to a server which ignores cancellation tombstones after any cancellations exist.

Source validation does not itself redeploy the server or install a client APK.

## Validation

The original isolated server harness covers completed ticket purchase/upgrade lookup, idempotent absent-request cancellation, late arrivals, account isolation/authentication, purchase-versus-cancel races, and the existing economy/result tests. New card-pack source must additionally verify exact ten-card receipt replay, atomic 33-gold/collection commit, duplicate draws, odds boundaries, operation cancellation and failed-final-write rollback; final performed checks are recorded in [the handoff](codex_handoff.md). Client boundary tests link the actual recovery code and cover both outcomes for both operations, no purchase resubmission, failed refresh, account change, worker recreation and skipping active sends. Unity runtime compilation is separate from native Android screen testing.

## Local tutorial isolation (2026-10-02)

`TutorialRuntimeIsolation` closes the account request boundary before tutorial navigation and also recognizes a directly opened `Tutorial_VSlice`. Automatic purchase/upgrade/card-pack and rewarded-ad recovery pause for the tutorial. Entering cancels only the current recovery attempt, leaving its durable pending request and the real wallet, collection and run intact. HTTP sends and late nested responses cannot apply or clear those records while the gate is active. Leaving allows the same workers to resume normally.

Real ad SDK completion signals from a show begun earlier are retained in memory during the lesson and processed after exit. They do not grant tutorial rewards. Existing online battle sessions retain their pre-entry transport and queued messages; gameplay sends, heartbeat, reconnect and projection are deferred during the tutorial. The server's existing real-match clock and reconnect grace are not extended. The tutorial creates no new online battle session. This isolates the lesson without resetting the user's real account or run.
