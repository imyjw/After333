# Purchase and upgrade completion recovery

Updated 2026-09-10. Ticket prices and card upgrade costs are unchanged.

## User interaction

Buttons retain their normal purchase/upgrade labels. There is no previous-result confirmation button and no reconnect/recovery instruction. Duplicate purchase/upgrade actions are disabled while an unresolved account operation exists. A transient failure clears the in-progress message without claiming cancellation. After connectivity returns, the app silently resolves the saved request and refreshes the account, then shows only the final completed/cancelled result.

A scene-independent recovery component starts with the app and checks pending work every five seconds while authenticated and online. It skips requests still being sent. Pending metadata survives scene changes and app restarts; older per-card pending keys are discovered from the account collection. It never automatically resends a purchase/upgrade POST. A new charge requires a new explicit action after resolution.

## Transaction protocol

Authenticated purchase and upgrade requests still require a nonempty D-format UUID RequestId. Upgrades require the displayed ExpectedUpgradeLevel. Their existing wallet lock, economy mutation, ledger entry and success receipt remain one READ COMMITTED transaction. Original same-ID retries remain idempotent for older clients; matching a cancelled ID never charges.

POST /account/operations/resolve with { "RequestId": "UUID" } uses the authenticated account identity, validates the UUID, takes the SAME wallet row lock as purchase/upgrade, and:
- If a committed success receipt exists, returns Status "completed".
- Otherwise inserts an idempotent permanent cancellation tombstone and returns Status "cancelled".

The response contains AccountId, RequestId and Status. Resolution itself never changes gold, tickets or cards. It is a POST because cancellation is durable, not a read-only lookup. Wallet locking prevents a "missing" lookup from racing with an in-flight purchase: completion or cancellation wins, never both. Delayed original requests check the cancellation table under that lock and return operation_cancelled before charging.

Migration 0016_account_operation_cancellations adds tombstones. Keep both success receipts and cancellation tombstones for the account lifetime; deleting either would make delayed requests unsafe. The endpoint is covered by account API rate limiting.

## Client confirmation

Saved metadata is scoped by server/account/operation/card and contains no credentials. Recovery validates the returned account, request ID and terminal status, then fetches /me. It applies the wallet and collection and clears pending metadata only after that refresh succeeds and the account, token and endpoint still match. Invalid replies, unavailable network/server, failed refresh and account switches retain pending work for a later attempt. A new worker can recover that state after app restart.

## Rollout

Deploy the server with migration 0016 before installing the new client. The current running 0015 server does not have the resolution endpoint: new pending work would remain disabled until the server is updated. Do not roll back to a server which ignores cancellation tombstones after any cancellations exist.

Source validation does not itself redeploy the server or install a client APK.

## Validation

The isolated server harness covers completed purchase/upgrade lookup, idempotent absent-request cancellation, late arrivals, account isolation/authentication, purchase-versus-cancel races, and the existing economy/result tests. Client boundary tests link the actual recovery code and cover both outcomes for both operations, no purchase resubmission, failed refresh, account change, worker recreation and skipping active sends. Unity runtime compilation is separate from native Android screen testing.
