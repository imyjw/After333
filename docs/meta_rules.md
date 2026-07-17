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
- A future shop may also use Resource Gold, but shop rules are not implemented.

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
- Card level stat bonuses are cumulative:
  - Most level gains grant `HP +1`.
  - Reaching `Lv.3`, `Lv.6`, `Lv.9`, and `Lv.13` grants `ATK +1` instead of `HP +1`.
- Therefore, a `Lv.13` card has total bonuses of `ATK +4` and `HP +9`.
- Unit and Building cards are upgradeable.
- Firebolt (`firebolt`, 파이어볼) is the currently upgradeable Spell card.
- Microreactor (`Microreactor`, 초소형 발전기), Cheonra Jimang (`CheonraJimang`, 천라지망), and Daehwandan (`Daehwandan`, 대환단) are not upgradeable.
- Cards marked non-upgradeable are excluded from random run card rewards.

### Ticket

Current temporary rules:

- Starting a new draft run costs `3` tickets.
- Tickets are consumed once when the server creates the new run, before draft begins.
- A player cannot start another run while a resumable run exists or completed-run rewards remain unclaimed.
- One ticket can be purchased for `3` Resource Gold.
- Ticket balance and ticket purchases are server-authoritative and persisted to the account wallet.
- A completed, server-verified rewarded video grants `1` ticket.
- Rewarded video tickets are temporarily limited to `3` per account per UTC day with a `60`-second reward cooldown.
- Unity cannot grant an ad ticket directly; the wallet changes only after a valid provider server-to-server callback.
- Provider event IDs are idempotent, so callback retries cannot grant duplicate tickets.
- See [rewarded_ads.md](./rewarded_ads.md) for the integration and security contract.

## Persistence

- Account wallet, card collection, upgrades, draft picks, current offer, completed deck, run record, and reward claim are server-owned data.
- Unity may cache session and display data but is not authoritative for progression.
- Closing and reopening the game restores the latest resumable run from the authenticated account.
- PvE and PvP results use the same persisted run win/loss counters.

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
