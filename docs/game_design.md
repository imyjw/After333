# Game Design

## Document Purpose

This document defines the high-level game design, shared terminology, and the boundary between the full game design and the current vertical slice.

- Detailed battle rules live in [battle_rules.md](./battle_rules.md).
- Draft rules live in [draft_rules.md](./draft_rules.md).
- Meta progression rules live in [meta_rules.md](./meta_rules.md).

## Game Snapshot

- Genre: 2.5D mobile portrait turn-based card game
- Current play mode: offline single-player versus AI
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

### Current Vertical Slice Implementation Scope

The current vertical slice is intentionally narrower:

1. Use fixed decks instead of draft
2. Complete exactly one battle
3. Resolve that battle as victory or defeat

The current vertical slice does not implement:

- Draft flow
- Multi-battle run progression
- 33-win / 3-loss run termination
- Reward payout
- Shop
- Card upgrades
- Ticket purchase

## Core Concepts

### Match Unit

- A battle is the atomic gameplay unit for the current vertical slice.
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
| Battle | One match between the player and an AI opponent |
| Run | A multi-battle progression sequence that ends at 33 wins or 3 losses |
| Master Unit | The protected leader unit whose HP reaching 0 or below causes defeat |
| Front Row | Row `0`, the row farther from the owning player |
| Back Row | Row `1`, the row nearer to the owning player |
| Melee | Attack type that follows front-row blocking rules and only counterattacks against other melee attackers |
| Ranged | Attack type that can target enemy occupied tiles regardless of front-row blockers and never counterattacks |
| Gold | In-battle resource used to play cards and gained by battle rules and card effects |
| Resource Gold | Out-of-battle currency used for shop, upgrades, and tickets |
| Occupied Tile | A tile currently taken by a unit or building |
| Occupancy Size | The number of tiles a card requires when summoned |
| Disabled | A temporary science-civilization state that prevents attack, movement, counterattack, effect use, and effect text while making the unit take double damage |
| Persistent Spell | A spell whose effect remains in a separate persistent zone after the card itself is cast and sent to the discard pile |

## Core Systems Overview

### Battle System

- Random first player / second player
- Opening hands of `3` for first player and `4` for second player
- One mulligan per player, using Hearthstone-style partial replacement
- Starting resources of `0 mana`, `0 qi`, `0 power`, and `3 gold`
- Turn structure with draw/resource calculation, gain, science power payment, main phase, and turn end
- Summon, move, attack, and spell usage in the main phase
- Melee and ranged targeting rules
- Melee-versus-melee counterattack only
- Draw failure damage to the Master Unit when the deck is empty

See [battle_rules.md](./battle_rules.md).

### Draft System

- Present 3 cards
- Choose 1 card
- Repeat 33 times
- Lock the 33-card deck after draft

See [draft_rules.md](./draft_rules.md).

### Meta Progression System

- A single drafted deck is used across repeated battles in a run
- The run ends at 33 wins or 3 losses
- Resource Gold is awarded after the run ends
- Resource Gold is spent in the shop, upgrades, and tickets

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
