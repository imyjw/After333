# Game Design

## Document Purpose

This document defines the high-level game design and separates the full game design from the current vertical slice implementation scope.

- Full battle details live in [battle_rules.md](./battle_rules.md).
- Full draft details live in [draft_rules.md](./draft_rules.md).
- Full meta progression details live in [meta_rules.md](./meta_rules.md).

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

The current vertical slice implementation scope is intentionally narrower:

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
- A battle ends when a Master Unit's HP reaches 0.

> TBD: Master Unit base HP, attack value, board placement, attackability rules, and unique abilities are not fixed yet.

### Card Types

- Unit card
- Spell card
- Building card

### Combat Roles

- Melee attacker
- Ranged attacker

### Field Model

- Each player has 2 rows and 5 columns.
- For each player, the row farther from that player is the front row.
- For each player, the row nearer to that player is the back row.
- One tile can hold at most one occupant.

Some cards may occupy more than one tile.

> TBD: Multi-tile footprint shape, orientation, and movement rules are not fixed yet.

## Glossary

| Term | Meaning |
| --- | --- |
| Battle | One match between the player and an AI opponent |
| Run | A multi-battle progression sequence that ends at 33 wins or 3 losses |
| Master Unit | The protected leader unit whose HP reaching 0 causes defeat |
| Front Row | The row farther from the owning player |
| Back Row | The row nearer to the owning player |
| Melee | Attack type that follows front-row blocking rules |
| Ranged | Attack type that can target enemy tiles regardless of front-row blockers |
| Gold (battle resource) | In-battle resource gained by battle rules and card effects |
| Resource Gold (meta currency) | Out-of-battle currency used for shop, upgrades, and tickets |
| Occupied Tile | A tile currently taken by a unit or building |
| Occupancy Size | The number of tiles a card requires when summoned |

## Core Systems Overview

### Battle System

- Random first player / second player
- Opening hand and mulligan
- Turn structure with draw/resource calculation, gain, main phase, and turn end
- Summon, move, attack, and spell usage in the main phase
- Melee and ranged targeting rules
- Counterattack rule for melee-versus-melee only
- Deck depletion damage when the deck is exhausted

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

> TBD: Master Unit base stats and board rules

> TBD: Exact mulligan procedure

> TBD: Exact formula for mana, qi, and power gain at turn start

> TBD: Caps, persistence, and reset rules for mana, qi, power, and battle gold

> TBD: Spell timing, targeting, and cost rules

> TBD: Multi-tile card footprint and placement rules

> TBD: Deck depletion damage target and timing details

> TBD: Direct attack rules against the Master Unit

> TBD: Civilization roster and power upkeep handling, including science civilization specifics
