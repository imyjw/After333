# Meta Rules

## Document Purpose

This document defines the full-game run structure, win/loss tracking, and out-of-battle reward economy.

- High-level overview: [game_design.md](./game_design.md)
- Battle details: [battle_rules.md](./battle_rules.md)
- Draft details: [draft_rules.md](./draft_rules.md)
- Future account/database persistence: [account_persistence_db_design.md](./account_persistence_db_design.md)
- PostgreSQL implementation plan: [postgresql_db_implementation_plan.md](./postgresql_db_implementation_plan.md)

## Scope Separation

### Full Game Design Scope

The full meta loop is:

1. Start a run
2. Build a 33-card deck through draft
3. Fight repeated battles with that deck
4. End the run at 33 cumulative wins or 3 cumulative losses
5. Receive rewards after run end
6. Spend Resource Gold in meta systems

### Current Account-Backed Implementation Scope

Implemented through the server and PostgreSQL:

- Ticket spending when a new run starts
- Persistent draft picks, current draft offer, completed deck, and run resume
- PvE and PvP battle results applied to the current run
- `33` cumulative win and `3` cumulative loss termination
- One-time run-end reward claim
- Persistent Resource Gold, card-copy collection, and upgrade levels
- Card upgrade spending and battle-time upgraded stat application
- Resource Gold ticket purchase
- Server-verified rewarded video ticket grants

Still deferred:

- Shop inventory and purchases
- Pack opening
- Crafting
- Paid purchase flows

## Run Definition

- A run is a multi-battle progression sequence.
- A run uses one drafted `33`-card deck.
- That deck is reused across repeated battles within the run.

## Run End Conditions

- A run ends immediately when cumulative wins reach `33`.
- A run ends immediately when cumulative losses reach `3`.

## Rewards

- Rewards are paid after the run ends.
- A completed run's reward can be claimed only once.
- Temporary reward formula:
  - `Resource Gold = final win count * 3`
  - `random card copies = final win count * 3`
- Each random card reward first rolls a rarity using these weights:
  - Common: `40`
  - Uncommon: `30`
  - Rare: `18`
  - Unique: `9`
  - Legendary: `3`
- Only upgradeable cards can appear in the random card reward pool.
- The Master card is never included in random card rewards.
- If an eligible rarity has no rewardable card, the roll is normalized across the remaining eligible rarities.

> TBD: The final reward formula is not fixed yet.

## Meta Currency

### Gold

- Gold is an in-battle resource.
- Gold is used inside battle rules.
- Gold is not the same as Resource Gold.

See [battle_rules.md](./battle_rules.md).

### Resource Gold

- Resource Gold is an out-of-battle currency.
- Resource Gold is gained through run-end rewards.
- Resource Gold is used to upgrade cards.
- Resource Gold is used to buy tickets to start the game.
- The first shop product exchanges `3` Resource Gold for `1` game ticket.
- Ticket purchases are made from the dedicated `Shop_VSlice` scene opened from the Game Start scene.

## Meta Systems

### Shop

Confirmed purpose:

- Buy cards

> TBD: Shop inventory generation, pricing, and refresh rules are not fixed yet.

### Card Upgrade

Confirmed purpose:

- Upgrade cards

Confirmed temporary rules:

- Each card has an independent upgrade level.
- Maximum card level is `13`.
- Upgrading requires Resource Gold and copies of that exact card.
- The temporary upgrade cost uses the target level:
  - `required Resource Gold = target level * 3`
  - `required card copies = target level * 3`
- Example costs:
  - `Lv.0 -> Lv.1`: Resource Gold `3`, card copies `3`
  - `Lv.1 -> Lv.2`: Resource Gold `6`, card copies `6`
  - `Lv.12 -> Lv.13`: Resource Gold `39`, card copies `39`
- Cards at `Lv.13` cannot be upgraded further.
- Upgrade stat gains are card-specific. These are the currently implemented schedules, not defaults to assign to a new card:
  - Standard schedule: ATK +1 at Lv.3/6/9/13; HP +1 at every other level (Lv.13 total ATK +4 / HP +9).
  - A-212 and Cerberus: HP +1 at Lv.1 through Lv.12; ATK +1 only at Lv.13 (total ATK +1 / HP +12).
  - Mana Pond, Robot Factory, Gaebang Branch, Merchant Caravan, Inn, Power Plant and Nuclear Power Plant: HP +1 at every level Lv.1 through Lv.13 (total HP +13; no ATK gain).
  - Demon King and Hero: ATK +1 at Lv.3/6/9, ATK +3 at Lv.13, and HP +1 at every other level (total ATK +6 / HP +9).
- Confirmed 2026-09-12: Cerberus reaches ATK 10 / HP 78 at Lv.13; Mana Pond reaches ATK 0 / HP 33. Card costs, production and other effects do not change under these schedules.
- For every new card, ask the user for upgrade eligibility and the exact per-level gains before implementing upgrade behavior. Do not infer a schedule from its card type, rarity, attack capability, similar cards or a shared-code fallback. An explicitly supplied schedule may group levels if every level 1 through 13 is covered; do not ask again when that card's schedule has already been supplied.
- All currently registered Unit and Building cards are upgradeable; new cards still require the user-supplied eligibility and schedule above.
- Firebolt (`firebolt`), Firewall (`Firewall`), Timed Bomb (`TimedBomb`), and Biochemical Bomb (`BiochemicalBomb`) are upgradeable Spell cards; each level adds `+1` to their direct or stored damage.
- The other ten current spells are not upgradeable: CheonraJimang, Daehwandan, TenThousandYearSnowGinseng, Gu, HuanShu, RobotFusion, Microreactor, PowerBank, ManaStone and ManaStoneBundle.
- Cards marked non-upgradeable are excluded from random run card rewards.

### Ticket

Current temporary rules:

- Starting a new draft run costs `3` tickets.
- Tickets are consumed once when the server creates the new run, before draft begins.
- A player cannot start another run while a resumable run exists or completed-run rewards remain unclaimed.
- One ticket can be purchased for `3` Resource Gold.
- Ticket balance and ticket purchases are server-authoritative and persisted to the account wallet.
- Ticket purchases and card upgrades require a non-empty UUID RequestId. A repeated account/request pair returns its committed result without another charge. Reusing that pair for different input or a different operation is rejected.
- Upgrade requests also include the displayed ExpectedUpgradeLevel. A different current level rejects the request before spending, even with a new request ID.
- Success receipts, wallet/collection changes and the account transaction ledger commit together. Receipts persist across server restarts and are not automatically expired.
- Replayed results preserve the original operation cost while returning current wallet and collection state. Unity retains uncertain requests across scene changes and restarts until the matching response or a definitive rejection is received.
- A completed, server-verified rewarded video grants `1` ticket.
- Rewarded video tickets are temporarily limited to `10` per account per UTC day with a `30`-second reward cooldown.
- Unity cannot grant an ad ticket directly; the wallet changes only after a valid provider server-to-server callback.
- Provider event IDs are idempotent, so callback retries cannot grant duplicate tickets.
- See [rewarded_ads.md](./rewarded_ads.md) for the integration and security contract.

## Persistence

- Account wallet, card collection, upgrades, draft picks, current offer, completed deck, run record, and reward claim are server-owned data.
- Unity may cache session and display data but is not authoritative for progression.
- Closing and reopening the game restores the latest resumable run from the authenticated account.
- PvE and PvP results use the same persisted run win/loss counters.
- Only results resolved by a server battle may advance account run wins/losses or unlock run rewards. Client-reported or offline results never update account progression.
- At battle start, the server fixes each participant's account, seat, run, deck contents and card upgrade levels. Rejoining or reconnecting restores this binding and cannot change the run receiving the result.
- Run-result persistence must match the bound account and the run's completed deck. A battle started without a run cannot attach one after it starts.
- Every battle has a server-generated result ID, retained by recovery snapshots. A committed result ID can never increment a run again.
- Result receipts, both participants' run counters, and any persisted PvP match completion commit in one DB transaction. Draws complete the match without incrementing wins or losses.
- The server durably queues terminal results before sending battle-end responses. Failed DB delivery remains pending for background retries and process-restart replay; client result uploads remain prohibited.
- Unity refreshes server run data through /me while awaiting result persistence; it must not upload local win/loss totals as a fallback.

## Relationship to Other Documents

- [game_design.md](./game_design.md) separates full-game design from the current slice scope.
- [draft_rules.md](./draft_rules.md) defines how the run deck is created before the run begins.
- [battle_rules.md](./battle_rules.md) defines what happens inside each individual battle during a run.
- [account_persistence_db_design.md](./account_persistence_db_design.md) defines the implemented account, collection, deck, run, reward, and PvP persistence model plus deferred extensions.
- [postgresql_db_implementation_plan.md](./postgresql_db_implementation_plan.md) records the PostgreSQL migrations and verification direction.

## Deferred Meta Systems

These items are intentionally reserved for later implementation:

- Shop inventory, refresh, pricing, and purchase flow
- Pack inventory and pack opening
- Crafting
- Paid purchases
- Ranked, season, and ladder rewards
- Final balancing of run rewards, upgrades, and ticket economy

## Open Issues / TBD

> TBD: Final reward formula details

> TBD: Shop pricing and inventory rules

> TBD: Pack contents and probability rules

> TBD: Crafting rules

> TBD: Future PvE opponent and encounter sourcing beyond the current demonstration AI

> TBD: Whether any rewards should exist before run end
