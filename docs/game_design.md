# After333 Game Design

## Document Purpose

This document defines the high-level game design, shared terminology, and the boundary between the implemented account-backed prototype and later full-game systems.

- Detailed battle rules live in [battle_rules.md](./battle_rules.md).
- Draft rules live in [draft_rules.md](./draft_rules.md).
- Meta progression rules live in [meta_rules.md](./meta_rules.md).

## Game Snapshot

- Public game name: `After333`
- Genre: 2.5D mobile landscape turn-based card game
- Current play modes: server-backed PvE versus AI and account-matched 1:1 PvP
- Field size: each player owns a 5x2 tile field
- Core deck concept: build a 33-card deck through a 3-choose-1 draft
- Full game long-term goal: reach 33 cumulative wins before 3 cumulative losses

## Scope Separation

### Full Game Design Scope

The full game design loop is:

1. Build a 33-card deck through draft
2. Fight repeated battles using that deck
3. End the run at 33 cumulative wins or 3 cumulative losses
4. Receive rewards after run end

### Current Account-Backed Implementation Scope

The current implemented loop is:

1. Login with a server-backed guest or Game ID account
2. Spend `3` tickets to start a run
3. Build and persist a `33`-card deck through draft
4. Play server-backed PvE or account-matched 1:1 PvP battles
5. Persist run wins and losses until `33` wins or `3` losses
6. Claim run-end Resource Gold and random card-copy rewards
7. Spend Resource Gold and matching card copies to upgrade eligible cards

The current prototype does not yet implement:

- Production Google, Kakao, or Naver OAuth login
- Ranked matchmaking, MMR, seasons, or ladders
- Shop inventory or purchases
- Pack opening
- Crafting
- Paid purchase flows

## Core Concepts

### Match Unit

- A battle is one PvE or PvP match and is the atomic gameplay unit inside a run.
- A run is the atomic progression unit for the full game design.

### Protected Leader

- Each player has a Master Unit.
- A battle ends when a Master Unit's HP reaches 0 or below.
- The Master Unit is a 1x1 unit placed on the player's own field.
- The Master Unit starts at `(2,1)`.
- The Master Unit is a movable melee attacker.
- The Master Unit has `333 HP` and `3 ATK`.
- The Master Unit follows the same targeting and melee counterattack rules as other melee units.

### Card Types

- Unit card
- Spell card
- Building card

### Combat Roles

- Melee attacker
- Ranged attacker

### Field Model

- Each player has 2 rows and 5 columns.
- Coordinates use `(column, row)` notation.
- Column values run from `0` to `4`, left to right.
- Row `0` is the front row.
- Row `1` is the back row.
- One tile can hold at most one occupant.
- Units and buildings occupy field tiles.
- Persistent spells do not occupy field tiles.

Some cards may occupy more than one tile.

> TBD: Multi-tile footprint shape, orientation, and placement rules are not fixed yet.

## Glossary

| Term | Meaning |
| --- | --- |
| Battle | One server-resolved match against an AI or another authenticated player |
| Run | A multi-battle progression sequence that ends at 33 wins or 3 losses |
| Master Unit | The protected leader unit whose HP reaching 0 or below causes defeat |
| Front Row | Row `0`, the row farther from the owning player |
| Back Row | Row `1`, the row nearer to the owning player |
| Melee | Attack type that follows front-row blocking rules and only counterattacks against other melee attackers |
| Ranged | Attack type that can target enemy occupied tiles regardless of front-row blockers and never counterattacks |
| Physical Damage | Damage reduced only by Physical Defense |
| Magic Damage | Damage reduced only by Magic Defense |
| Fixed Damage | Damage that ignores Physical Defense and Magic Defense |
| Physical Defense | The value subtracted from each incoming Physical damage instance |
| Magic Defense | The value subtracted from each incoming Magic damage instance |
| Gold | In-battle resource used to play cards and gained by battle rules and card effects |
| Resource Gold | Out-of-battle currency used for shop, upgrades, and tickets |
| Occupied Tile | A tile currently taken by a unit or building |
| Occupancy Size | The number of tiles a card requires when summoned |
| Drained | A power-upkeep failure state for units and buildings that prevents attack, movement, counterattack, and effect use while making the occupant take triple damage |
| Erasure | `망각`; suppresses card effects and removes existing combat-time modifiers while preserving ordinary attack, movement, baseline defenses, damage taken, and front-row blocking |
| Persistent Spell | A spell whose effect remains in a separate persistent zone after the card itself is cast and sent to the discard pile |

## Core Systems Overview

### Battle System

- Random first player / second player
- Opening hands of `3` for both players
- One mulligan per player, using Hearthstone-style partial replacement
- Starting resources of `0 mana`, `0 qi`, `0 power`, and `3 gold`; the first player skips base gold gain on battle turn `1`, while the second player gains `gold +1` on battle turn `2`
- Turn structure with draw/resource calculation, gain, science power payment, main phase, and turn end
- Summon, move, attack, and spell usage in the main phase
- Melee and ranged targeting rules
- Melee-versus-melee counterattack only
- Physical, Magic, and Fixed damage with separate Physical/Magic defenses
- Per-hit defense calculation for multi-hit attacks
- Draw failure damage to the Master Unit when the deck is empty

See [battle_rules.md](./battle_rules.md).

### Draft System

- First present `3` Legendary cards and choose `1`
- Then present `3` non-Legendary cards and choose `1`, repeated for the remaining `32` picks
- Persist selected picks and the current offer so an interrupted draft can resume
- Lock the completed `33`-card deck for the run

See [draft_rules.md](./draft_rules.md).

### Meta Progression System

- A single drafted deck is used across repeated battles in a run
- The run ends at 33 wins or 3 losses
- Resource Gold and eligible card copies are awarded after the run ends
- Resource Gold is currently spent on card upgrades and ticket purchases
- Shop, pack, and crafting uses are planned but not implemented

See [meta_rules.md](./meta_rules.md).

## Document Reference Map

- This document is the overview and glossary.
- [battle_rules.md](./battle_rules.md) is the source of truth for battle state, coordinates, actions, targeting, and combat resolution.
- [draft_rules.md](./draft_rules.md) is the source of truth for 33-pick deck construction.
- [meta_rules.md](./meta_rules.md) is the source of truth for run progression, rewards, and meta spending.

## Open Issues / TBD

> TBD: Multi-tile unit and building footprint rules

> TBD: Full civilization roster and any civilization rules beyond the confirmed science power-upkeep rule

> TBD: Any cross-battle carryover rules for battle resources in the full multi-battle run


