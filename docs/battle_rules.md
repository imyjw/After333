# Battle Rules

## Document Purpose

This document is the source of truth for battle setup, tile notation, actions, targeting, and combat resolution.

- High-level overview: [game_design.md](./game_design.md)
- Draft design: [draft_rules.md](./draft_rules.md)
- Meta progression design: [meta_rules.md](./meta_rules.md)

## Battle Scope

### Full Game Design

- Battles are repeated within a run until the run ends at 33 wins or 3 losses.

### Current Vertical Slice Implementation Scope

- The current implementation scope is one battle only.
- The current slice uses fixed decks.
- Multi-battle run logic is not part of the current slice implementation.

## Battle Objective

- Each player has a Master Unit.
- A player loses the battle when their Master Unit HP reaches 0.

> TBD: Master Unit HP, attack rules, tile placement, and whether it occupies a normal field tile are not fixed yet.

## Battlefield Model

### Per-Player Field

- Each player owns a 5x2 tile field.
- Each field has 5 columns.
- Each field has 2 rows: front row and back row.

### Row Meaning

- Front row: the row farther from the owning player
- Back row: the row nearer to the owning player

### Coordinate Notation

This document uses two equivalent notations:

- Logical notation: `F0` to `F4` for front-row tiles, `B0` to `B4` for back-row tiles
- Matrix notation: `(row, column)`

When matrix notation is shown in examples:

- `row = 0` means the front row
- `row = 1` means the back row
- `column = 0..4` from left to right

Example:

```text
(0,0) (0,1) (0,2) (0,3) (0,4)
(1,0) (1,1) (1,2) (1,3) (1,4)
```

### Tile Occupancy

- One tile can hold at most one occupant.
- A unit or building can be summoned only if every required tile is empty.
- Cards with larger occupancy sizes require enough empty tiles to satisfy their tile requirement.

> TBD: The exact footprint shape, orientation, and contiguity rule for multi-tile cards are not fixed yet.

## Battle Setup

- First player / second player is decided randomly.
- Starting hand size: first player draws 3 cards.
- Starting hand size: second player draws 4 cards.
- Starting mana: 0
- Starting qi: 0
- Starting power: 0
- Starting battle gold: 0
- Mulligan exists.

> TBD: Mulligan procedure, replacement limits, and redraw handling are not fixed yet.

## Turn Structure

Each turn follows this sequence:

1. Turn start
2. Calculate draw count and resource gain
3. Draw cards and gain resources according to the calculation
4. Main phase
5. Turn end

## Draw and Resource Gain

### Confirmed Defaults

- Base draw per turn: 1 card
- Base battle gold gain per turn: 1
- Mana, qi, power, and battle gold can also be gained through unit or building effects

### Not Yet Fixed

> TBD: Base gain values for mana, qi, and power beyond card effects are not fixed yet.

> TBD: Resource caps, overflow handling, turn-end retention, and battle-end reset rules are not fixed yet.

## Main Phase Actions

The main phase is a free-action phase for legal actions.

Confirmed action categories:

- Summon
- Move
- Attack
- Play spell
- End turn

> TBD: Spell timing windows and resolution order are not fixed yet.

## Summon Rules

### Confirmed

- A summon requires valid empty tile space.
- If resources and tile requirements are satisfied, summon count is not globally capped.
- Unit summon costs use battle resources.

### Not Yet Fixed

> TBD: Whether building cards and spell cards use the same resource schema as unit summons is not fixed yet.

> TBD: Whether the Master Unit is deployed at battle start or is treated outside normal summon rules is not fixed yet.

## Move Rules

### Confirmed

- The player selects one of their own movable units.
- The player then selects an empty tile on their own field.
- The unit moves directly to that tile.
- Movement is not allowed onto occupied tiles.
- Immobile units cannot move.
- Buildings cannot move.
- Movement count is globally unlimited during the main phase.

### Not Yet Fixed

> TBD: Whether the same unit may move multiple times in one turn is strongly implied by the current rule set, but not separately called out as a final formal rule.

> TBD: Whether moving affects attack availability or attack order is not fixed yet.

## Attack Availability

### Confirmed

- Units can normally attack once per turn.
- Some card effects may allow two or more attacks in a turn.
- A unit summoned this turn cannot normally attack until the next turn.
- Some special effects can override that restriction.
- A unit that is allowed to move may move immediately on the turn it is summoned.

## Targeting Rules

### Melee Targeting

- Melee attacks obey front-row blocking.
- In a given enemy column, if the enemy front-row tile is occupied, the enemy back-row tile in that same column cannot be directly targeted by a melee attack.
- If the enemy front-row tile in a column is empty, the enemy back-row tile in that column may be targeted by a melee attack.

Example enemy field occupancy:

```text
(0,0) occupied
(0,2) occupied
(1,2) occupied
(1,4) occupied
```

Targetable by melee:

- `(0,0)`
- `(0,2)`
- `(1,4)`

Not targetable by melee:

- `(1,2)` because `(0,2)` is occupied

### Ranged Targeting

- Ranged attacks can target any enemy occupied tile regardless of front-row blockers.

Using the same example above, all of the following are targetable by ranged attacks:

- `(0,0)`
- `(0,2)`
- `(1,2)`
- `(1,4)`

### Exceptions

- Area effects may follow separate rules.
- Special effects may allow melee units to target back-row enemies despite normal blocking.

> TBD: Building targetability, Master Unit direct targetability, and multi-tile target resolution are not fixed yet.

## Attack and Counterattack Resolution

### Confirmed Rule

Only melee-versus-melee combat causes a counterattack.

### Resolution Matrix

| Attacker | Defender | Defender takes damage | Attacker takes damage |
| --- | --- | --- | --- |
| Melee unit | Melee unit | Yes, equal to attacker's attack | Yes, equal to defender's attack |
| Melee unit | Ranged unit | Yes, equal to attacker's attack | No |
| Ranged unit | Melee unit | Yes, equal to attacker's attack | No |
| Ranged unit | Ranged unit | Yes, equal to attacker's attack | No |

### Worked Examples

- `A` melee `5/10` attacks `B` melee `3/15`: `A` loses 3 HP, `B` loses 5 HP
- `A` melee `5/10` attacks `C` ranged `2/7`: `C` loses 5 HP
- `C` ranged `2/7` attacks `A` melee `5/10`: `A` loses 2 HP
- `C` ranged `2/7` attacks `D` ranged `4/9`: `D` loses 2 HP

### Not Yet Fixed

> TBD: Whether melee counterattack still occurs if the defender would be destroyed by the initial damage is not fixed yet.

> TBD: Destruction timing, removal timing, and simultaneous death handling are not fixed yet.

## Hand, Deck, and Exhaustion Rules

### Confirmed

- Maximum hand size: 9
- Discard pile is not reshuffled into the deck
- When the deck is exhausted, cumulative damage is applied by exhausted-turn count
- Exhaustion damage progression is `3, 6, 9, 12, ...`

### Not Yet Fixed

> TBD: The exact target of exhaustion damage is not fixed yet.

> TBD: The exact timing point of exhaustion damage application is not fixed yet.

## Resource Notes

### Confirmed

- Mana, qi, power, and battle gold are battle resources.
- Battle gold is distinct from Resource Gold.
- Power is also used as an end-of-turn payment for science civilization units.

### Not Yet Fixed

> TBD: The full civilization system is not fixed yet.

> TBD: What happens when required power upkeep cannot be paid is not fixed yet.

## Open Issues / TBD

> TBD: Master Unit base stats, tile logic, and direct attack rules

> TBD: Exact mulligan procedure

> TBD: Base mana and qi generation formula

> TBD: Resource caps, persistence, and reset rules

> TBD: Spell resolution rules

> TBD: Multi-tile footprint rules

> TBD: Building combat and targeting rules

> TBD: Unit/building destruction timing and cleanup rules

> TBD: Exhaustion damage target and timing

> TBD: Science civilization upkeep failure handling
