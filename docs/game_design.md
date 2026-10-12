# After333 Game Design

Actual release status (2026-10-02): the separately approved card-pack server/DB0020–0022 rollout and Android APK(versionCode5) build passed. See [actual release evidence](shop_card_packs.md). Earlier source-only snapshots retain their scope; physical-device installation/runtime remains untested.

Updated 2026-10-02 for confirmed shop card-pack purchase/opening; the earlier card-content snapshot below retains its own scope. This describes source behavior; it does not certify the version installed on a device or deployed to production.

## Document Purpose

This document defines the high-level game design, shared terminology, and the boundary between the implemented account-backed prototype and later full-game systems.

- Detailed battle rules live in [battle_rules.md](./battle_rules.md).
- Draft rules live in [draft_rules.md](./draft_rules.md).
- Meta progression rules live in [meta_rules.md](./meta_rules.md).

## Game Snapshot

- Public game name: `After333`
- Genre: 2.5D mobile landscape turn-based card game
- Current play modes: normal matchmaking (human opponent first; fallback AI after 20 seconds) and a separate per-run Gold Dungeon. Both use the current run deck. See [battle_modes.md](./battle_modes.md).
- Field size: each player owns a 5x2 tile field
- Core deck concept: build a 33-card deck through a 3-choose-1 draft
- Implemented run goal: reach 33 cumulative wins before 3 cumulative losses

## Scope Separation

### Full Game Design Scope

The full game design loop is:

1. Build a 33-card deck through draft
2. Fight repeated battles using that deck
3. End the run at 33 cumulative wins or 3 cumulative losses
4. Receive rewards after run end

### Current Account-Backed Implementation Scope

The current implemented loop is:

1. Login with a server-backed Game ID or Google account, or refresh its saved session
2. Spend `3` tickets to start a run
3. Build and persist a `33`-card deck through draft
4. Play normal matchmaking battles against a human or fallback AI; optionally use up to three Gold Dungeon attempts per paid run for separate Resource Gold
5. Persist run wins and losses until `33` wins or `3` losses
6. Claim run-end Resource Gold and random card-copy rewards
7. Spend Resource Gold and matching card copies to upgrade eligible cards
8. Buy ticket/card-pack products in the shop; a 33 Resource Gold pack immediately grants ten independent random reward-eligible card copies and then presents its opening

The current prototype does not yet implement:

- Kakao or Naver OAuth login
- Ranked matchmaking, MMR, seasons, or ladders
- Individual card-shop inventory/pricing/refresh beyond the implemented ticket/card-pack products
- Crafting
- Paid purchase flows

## Core Concepts

### Match Unit

- A normal battle advances the run and may have a human or AI opponent. Gold Dungeon uses the completed active run deck, with up to three attempts per run started with 3 tickets and separate rewards; it does not advance or end the run.
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
| Unit Race | Zero or more user-confirmed unit classifications, independent of effects and preserved by Erasure |
| Stun | Prevents attack, counterattack and active abilities; duration decreases at each player's turn end |
| Freeze | Stun restrictions plus no movement or front-row blocking; pauses the occupant Animator until removed |
| Trap | A privately identified, freely installed spell that pays activation resources and is consumed once when its condition succeeds |
| Spell Power | Adds the damage owner's current effective Spell Power to Magic damage when damage is resolved |

## Core Systems Overview

### Battle System

- Random first player / second player
- Opening hands of `3` for both players
- One mulligan per player, using Hearthstone-style partial replacement
- Starting resources of `0 mana`, `0 qi`, `0 power`, and gold `3` for the first player / `4` for the second player, assigned before mulligan; neither first turn adds base gold (battle turns `1` and `2`); normal own-turn gain is `gold +3` from battle turn `3` onward
- Turn structure with base draw of 1 card every own turn (including opening turns), resource calculation/gain, science power payment, main phase, and turn end
- Summon, move, attack, and spell usage in the main phase
- Melee and ranged targeting rules
- Melee-versus-melee counterattack only
- Physical, Magic, and Fixed damage with separate Physical/Magic defenses
- Per-hit defense calculation for multi-hit attacks
- Draw failure damage to the Master Unit when the deck is empty
- Paid attacks (ManaCannon), live resource-based ATK (GaebangChief), stackable source-bound OrcChieftain auras, and permanent stackable defenses
- Stun/Freeze durations set by their applying effect, refreshed to the longer remaining duration, and decreased at every player's turn end
- Private, free trap installation with ordered, affordable, pre-damage activation; Return cancels and returns an ordinary attacker, Mirror Field cancels and reflects Magic damage
- Immediate enemy-field spells, column-piercing FireSpear, delayed TimedBomb and Nuclear's unit/Master-only permanent Fallout
- Fresh battle resources for each new battle; rejoining the same battle restores its existing resource state

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
- Resource Gold is currently spent on card upgrades, ticket purchases and 33-gold ten-card packs
- The ticket shop, server-verified rewarded-ticket ads and card-pack purchase/opening source are implemented; individual card-shop inventory, separate unopened-pack inventory, crafting and paid purchases remain deferred
- Card packs use per-copy Common/Uncommon/Rare/Unique/Legendary odds of 40/30/18/9/3%. Duplicates are allowed. The server atomically commits charge, ten collection copies and original receipt before the sealed pack appears; interrupted opening restores presentation without another grant. See [card packs](shop_card_packs.md).

See [meta_rules.md](./meta_rules.md).

## Current Content and Presentation

- Shared definitions: 59 cards (27 units, 7 buildings, 25 spells); native catalog and draft pool: 52. Actual random reward pool: 40; upgradeable definitions: 44. See the catalog for flags and unreleased cards rather than treating all definitions as playable.
- Four hidden traps share free installation, captured activation costs, ordered one-shot triggers and duplicate-card exclusion. Reactive Armour protects the owner Master from ordinary melee Units; Iron Dome protects that Master from ordinary ranged Units. Both cost 4 Power on activation with Gold substitution, cancel the attack and destroy its attacker. See [battle rules](battle_rules.md#irondome).
- Hand cards fold at bottom center, expand on click and move/scale smoothly. Both fields, the settings gear and End Turn preserve the expanded hand. End Turn locks actions immediately while keeping hand inspection available.
- Affordable selected cards pulse their legal tile targets with reduced white brightness. Untargeted spells only show tile hints where their input rules explicitly support them.
- Long-press previews share trait/effect information across supported card surfaces; live board previews additionally show statuses and applied effects from the actual occupant.
- Owned-card details resolve bundled current definitions, show unit races, and flip between artwork and a black/white Lv.1–13 increment table.
- Card-pack presentation dims the shop, tears a central foil pack, rapidly ejects white glowing cards and reveals ten actual copies in a responsive 5×2 grid. Rare/Unique/Legendary borders glow blue/red/yellow; a guarded click anywhere returns to the shop.
- Notices share a dark navy rounded panel, thin border and NeoDunggeunmo Pro. The original gold pixel Your Turn banner is an explicit exception. Ordinary connection-progress startup messages stay hidden.
- Character attacks, whole-field spells, status loops and one-shot shields have dedicated animation/VFX/audio paths. Timed spell presentation completes its relevant impact before the following state is projected.

## Document Reference Map

- [README.md](./README.md) indexes current guides and distinguishes historical verification records.
- This document is the overview and glossary.
- [card_catalog.md](./card_catalog.md), [card_details.md](./card_details.md) and [card_authoring_checklist.md](./card_authoring_checklist.md) describe current cards, exact upgrade schedules and release flags.
- [battle_modes.md](./battle_modes.md) covers matchmaking, three Gold Dungeon attempts per paid run without a midnight reset and the 70-second reconnect policy.
- [pve_ai.md](./pve_ai.md) documents current evaluation and protection rules.
- [unit_races.md](./unit_races.md), [special_effects.md](./special_effects.md) and [occupant_status_tooltips.md](./occupant_status_tooltips.md) cover classifications and effect presentation.
- [battle_rules.md](./battle_rules.md) is the source of truth for battle state, coordinates, actions, targeting, and combat resolution.
- [draft_rules.md](./draft_rules.md) is the source of truth for 33-pick deck construction.
- [meta_rules.md](./meta_rules.md) is the source of truth for run progression, rewards, and meta spending.

## Open Issues / TBD

> TBD: Multi-tile unit and building footprint rules

> TBD: Full civilization roster and any civilization rules beyond the confirmed science power-upkeep rule

Current battles start with mana/qi/power 0 and gold 3. The deck and account upgrade levels carry across the run; battle resources and temporary occupant states do not. Any different future carryover rule needs a separate user decision.


