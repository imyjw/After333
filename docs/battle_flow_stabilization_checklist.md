# Current Battle Flow Verification Checklist

**2026-10-20 운영 기록 갱신:** 서울 Lightsail + Caddy + PostgreSQL18로 이전했고 공개 주소는 `https://api.after333.com`, 운영DB는 `project333_live`다. 사용자 전환 결과에서 migration24개/latest`0024_elf_elder_card_id`를 확인했다. 아래 과거 날짜의 노트북/Tunnel·DB버전·미배포 문구는 당시 기록이다. 현재 실행·백업 절차는 [Lightsail 운영](lightsail_operations.md), 확인 범위와 남은 시험은 [현재 상태](current_project_status.md)를 우선한다. 오늘 원격 재확인을 수행하지 않았다.

Updated 2026-10-09. This is a manual verification guide for the implemented server-backed loop, not a report that these steps were rerun today. The earlier prerequisite to defer accounts/DB until local PVE/PVP was stable is superseded: authentication, PostgreSQL, run persistence, rewards and upgrades are already implemented.

## Rules and execution boundary

- [battle_modes.md](battle_modes.md): normal matchmaking, per-run Gold Dungeon, reconnect and canceled-battle handling.
- [battle_rules.md](battle_rules.md): actions, damage, statuses and traps.
- [draft_rules.md](draft_rules.md), [meta_rules.md](meta_rules.md): persisted deck and server-owned run/economy.
- [online_pvp_prototype.md](online_pvp_prototype.md): filtered views, event sequencing and transport.
- [account_operation_retries.md](account_operation_retries.md): silent recovery and idempotent operations.
- [public_pvp_smoke_test_checklist.md](public_pvp_smoke_test_checklist.md): separate authorized multi-client checks.

Use existing approved test environments/accounts. This document does not authorize a production restart, deployment, account/currency change or client build/install. LocalBattleGateway remains a test/local compatibility path; the ordinary account-backed AI battle is server-resolved.

## Account, draft and normal battle

1. Verify the existing account session loads its wallet, current run and card levels without opening a card detail or duplicating an operation.
2. Start a new run with 3 tickets. Confirm exactly one spend and a persisted 3-card offer.
3. Complete 33 picks: the first offer is Legendary, the remaining offers non-Legendary. Interrupt/reopen during a pick and verify the same offer/picks remain.
4. Queue normal matchmaking with the completed deck. Confirm human priority and fallback AI after 20 seconds when no human joins.
5. Check original account/seat/deck and upgrade levels, 333HP/3ATK Master, opening hands, mulligan and starting resources.
6. Summon, cast, move/swap, attack and activate abilities. Confirm each accepted command pays once, each rejection preserves state, and both viewers converge after event presentation.
7. End a turn repeatedly. Only the current turn ends; further actions stay locked until the next own turn while hand expansion and previews remain usable.
8. Finish through lethal damage or surrender. The server records one result; the draft lobby retains its deck and updates run wins/losses once.
9. At 33 wins or 3 losses, verify one run-end claim using final wins ×3 Resource Gold and the actual reward pool. Retry/reopen must not double-grant.

<a id="separate-daily-ai-challenge"></a>
## Separate per-run Gold Dungeon

1. Start a run with exactly 3 tickets and complete its 33-card deck. Verify `RunId`, `RunLimit=3`, `RemainingAttempts=3` and any `PendingAttempt` come from the server; incomplete or ended runs cannot start a new challenge.
2. Start three challenges. Each accepted start consumes one attempt; retries and reconnect resume the same reservation without another debit. A fourth distinct start is rejected.
3. Cross KST midnight, restart and log in again. The same run keeps the same usage. Existing pre-change attempts for that run count across all dates; three or more leave zero. A new paid run has its own three attempts.
4. Win: award Resource Gold10 once. Lose: no victory reward. Neither result changes the normal run's wins/losses or ends it.
5. For server interruption/cancellation, refund only the original attempt's `RunId` once. Canceling an old-run attempt must not add an attempt to the new run; ordinary disconnect loss does not refund.
6. Ensure the status has no next-midnight reset timestamp and the UI never replenishes attempts locally. `challenge_day` remains audit data only.

## Reconnect and interruption

1. In an authorized normal human/AI battle and Gold Dungeon, interrupt the client connection. Observe the deadline starting at server detection, with a 70-second grace.
2. Rejoin within that deadline. Restore the authenticated account's original seat, run/deck, remaining resources/statuses, hand and recent structured combat log.
3. Restore ongoing persistent effects and private traps without replaying installation/central card previews or leaking enemy trap identities.
4. After deadline/finished result, show final status rather than entering an invalid battle or charging a fresh challenge attempt.
5. Server outage/maintenance cancellation must produce no win/loss; the mode-specific challenge refund follows battle_modes.md. Current startup policy cancels unfinished persisted matches rather than resuming old live sessions.

## Current presentation and rule regression checks

- Hidden hand/trap identities remain filtered per viewer; only successful trap activation reveals its real card.
- Return and Mirror Field activate in install order before attack damage; unaffordable traps remain armed; canceled accepted attacks still spend their attack use and any already paid ManaCannon fee.
- Stun blocks attack/counter/active; Freeze also blocks movement/front-row blocking and pauses the occupant Animator. Status VFX continues until expiry; animation resumes on thaw.
- Meteor, Earthquake, Nuclear, TimedBomb and FireSpear use visible tile-background Canvas anchors in the actual battle scene. FireSpear traverses the selected owner's two-tile column at 1.5× speed. Verify effect impact and the subsequent StateView stay ordered.
- Damage/kill/revival/popups/audio are emitted once. The common UnitHit sound plays for actual hurt, while character attack/cast audio remains separate.
- Compact hand exceptions cover both fields, End Turn and gear; affordable tile hints follow actual legality and use the reduced 0.65 maximum white overlay.
- Long-press trait/effect panels and current owned-card stats/races/flip table remain readable. Notices use the shared dark theme; Your Turn retains its original gold banner.

Prior task-specific automated proof is indexed in the relevant feature documents and codex_handoff.md. Documentation edits alone do not replace device or multi-client testing.