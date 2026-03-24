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
- A player loses the battle when their Master Unit HP reaches 0 or below.

## Master Unit

- The Master Unit is placed on the field and occupies `1x1` tile space.
- The Master Unit starts at `(2,1)` on its owner's field.
- The Master Unit is a movable melee attacker.
- The Master Unit has `333 HP` and `3 ATK`.
- The Master Unit follows the same targeting rules as other melee units.
- The Master Unit follows the same melee-versus-melee counterattack rules as other melee units.

## Battlefield Model

### Per-Player Field

- Each player owns a `5x2` tile field.
- Each field has `5` columns.
- Each field has `2` rows: front row and back row.
- Coordinates are written from the owning field's perspective unless noted otherwise.

### Coordinate Notation

This document uses `(column, row)` notation.

- `column = 0..4` from left to right
- `row = 0` for the front row
- `row = 1` for the back row

Example field:

```text
(0,0) (1,0) (2,0) (3,0) (4,0)
(0,1) (1,1) (2,1) (3,1) (4,1)
```

### Row Meaning

- Front row: the row farther from the owning player
- Back row: the row nearer to the owning player

### Tile Occupancy

- One tile can hold at most one occupant.
- A unit or building can be summoned only if every required tile is empty.
- Units and buildings occupy field tiles.
- Persistent spells do not occupy field tiles.
- Some cards may require more than one tile.

> TBD: The exact footprint shape, orientation, and contiguity rule for multi-tile cards are not fixed yet.

## Battle Setup

- First player / second player is decided randomly.
- Starting hand size: first player draws `3` cards.
- Starting hand size: second player draws `4` cards.
- Starting mana: `0`
- Starting qi: `0`
- Starting power: `0`
- Starting gold: `3`
- Each player begins with their Master Unit already on the field at `(2,1)`.

### Mulligan

- Each player may mulligan once.
- A mulligan is partial, not all-or-nothing.
- The player chooses any number of cards from their starting hand.
- Chosen cards are returned to the deck.
- The deck is shuffled.
- The player draws the same number of replacement cards.

## Turn Structure

Each turn follows this sequence:

1. Turn start
2. Calculate draw count and resource gain
3. Draw cards and gain resources according to the calculation
4. Resolve science-civilization power payment
5. Main phase
6. Turn end

## Draw and Resource Gain

### Base Values

- Base draw per turn: `1` card
- Base gold gain per turn: `1`
- Base natural mana gain per turn: `0`
- Base natural qi gain per turn: `0`
- Base natural power gain per turn: `0`

### Card-Based Gain

- Units or buildings may grant `mana +n`, `qi +n`, `power +n`, or `gold +n`.
- Those gains are included during the turn-start resource calculation and gain step.

### Disabled Science Units During Turn Start

- A science-civilization unit that is currently disabled remains disabled through that turn's draw/resource calculation and gain step.
- A disabled unit does not contribute its effects during that step.
- After resources are gained, science-civilization power payment is resolved.
- If the unit's power payment succeeds, the unit is active from that point onward.
- If the unit's power payment fails again, the unit remains disabled.

### Resource Storage

- Mana, qi, power, and gold have no maximum value.
- Unspent mana, qi, power, and gold persist between turns.

## Science-Civilization Power Payment

- Some science-civilization units require power payment at turn start.
- Power payment happens after the turn-start draw/resource gain step.
- Payment is attempted automatically in a fixed tile order.
- The fixed order is:

```text
(0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1), (4,0), (4,1)
```

- Empty tiles are skipped.
- Non-science units are skipped.
- If a science-civilization unit's full power payment can be made, that payment is made and the unit remains active.
- If a science-civilization unit's full power payment cannot be made, no partial payment is made and that unit becomes or remains disabled.

### Disabled State

When a science-civilization unit is disabled:

- It cannot attack.
- It cannot move.
- It cannot counterattack.
- Its effects do not activate.
- Its effects and special effects are treated as nullified.
- It takes double damage when attacked.
- It still occupies its tile.
- It can still be targeted by attacks and effects.
- It does not participate in front-row blocking.

## Hand, Deck, and Draw Failure Rules

### Maximum Hand Size

- Maximum hand size is `9`.

If a draw would exceed the maximum hand size:

- The card is not drawn.
- The card does not remain in the deck.
- The card does not go to the discard pile.
- The card is removed instead.

### Discard Pile

- The discard pile is not reshuffled into the deck.

### Draw Failure Damage

If a player attempts to draw a card and their deck has no card to draw:

- The draw fails.
- The player's Master Unit takes damage instead of drawing.
- The first failed draw deals `3` damage.
- The second failed draw deals `6` damage.
- The third failed draw deals `9` damage.
- The fourth failed draw deals `12` damage.
- The pattern continues as `3, 6, 9, 12, 15, ...`

## Main Phase Actions

The main phase is a free-action phase for legal actions.

Confirmed action categories:

- Summon a unit
- Summon a building
- Move a movable unit
- Attack with an eligible attacker
- Play a spell
- End turn

Unless another rule or effect says otherwise:

- If resources and tile requirements are satisfied, summon count is not globally capped.
- Movement count is not globally capped.
- The rules do not impose a separate global action-point system.

## Card Costs

- Unit, building, and spell cards all use their listed battle-resource costs.
- Those costs may include mana, qi, power, gold, or any combination defined by the card.
- Playing or casting a card pays its listed cost once.
- Additional ongoing payment only exists when a rule or card text specifically says so.

## Summon Rules

- A summon requires valid empty tile space.
- Units and buildings may only be placed on the player's own field.
- Buildings cannot move.
- If a card requires more than one tile, every required tile must be empty.

## Move Rules

- The player selects one of their own movable units.
- The player then selects an empty tile on their own field.
- The unit moves directly to that tile.
- Movement is not allowed onto occupied tiles.
- Immobile units cannot move.
- Buildings cannot move.
- A movable unit may move on the same turn it is summoned.

## Attack Availability

### Units

- Units normally attack once per turn.
- Some card effects may allow two or more attacks in a turn.
- A unit summoned this turn cannot normally attack until the next turn.
- Some special effects can override that restriction.

### Buildings

- Some buildings can attack.
- Any building that can attack is always treated as a ranged attacker.
- An attack-capable building normally cannot attack on the turn it is summoned.
- Some special effects can override that restriction.

## Targeting Rules

### General Occupant Rule

- Enemy occupied tiles are legal targets only if the relevant targeting rule allows them.
- Units, buildings, and the Master Unit are all field occupants.

### Melee Targeting

- Melee attacks obey front-row blocking.
- In a given enemy column, if the enemy front-row tile is occupied by a blocker, the enemy back-row tile in that same column cannot be directly targeted by a melee attack.
- If the enemy front-row tile in a column is empty, or occupied only by a disabled science-civilization unit, the enemy back-row tile in that column may be targeted by a melee attack.

Example enemy field occupancy:

```text
(0,0) occupied
(2,0) occupied
(2,1) occupied
(4,1) occupied
```

Targetable by melee:

- `(0,0)`
- `(2,0)`
- `(4,1)`

Not targetable by melee:

- `(2,1)` because `(2,0)` is occupied by a blocker

### Ranged Targeting

- Ranged attacks can target any enemy occupied tile regardless of front-row blockers.

Using the same example above, all of the following are targetable by ranged attacks:

- `(0,0)`
- `(2,0)`
- `(2,1)`
- `(4,1)`

### Exceptions

- Area effects may follow separate rules from normal single-target attacks.
- Special effects may allow melee attackers to bypass normal front-row blocking.

## Attack and Counterattack Resolution

### Core Rule

Only melee-versus-melee combat causes a counterattack.

### Resolution Matrix

| Attacker | Defender | Defender takes damage | Attacker takes damage |
| --- | --- | --- | --- |
| Melee attacker | Melee attacker | Yes, equal to attacker's ATK | Yes, equal to defender's ATK |
| Melee attacker | Ranged attacker | Yes, equal to attacker's ATK | No |
| Ranged attacker | Melee attacker | Yes, equal to attacker's ATK | No |
| Ranged attacker | Ranged attacker | Yes, equal to attacker's ATK | No |

### Simultaneous Damage Rule

- Melee-versus-melee damage is simultaneous.
- Both participants deal their damage when the attack resolves.
- Both participants can be destroyed by the same combat.
- The melee counterattack still applies even if one side would be destroyed by the same exchange.

### Worked Examples

- `A` melee `5/10` attacks `B` melee `3/15`: `A` loses `3 HP`, `B` loses `5 HP`
- `A` melee `5/10` attacks `C` ranged `2/7`: `C` loses `5 HP`
- `C` ranged `2/7` attacks `A` melee `5/10`: `A` loses `2 HP`
- `C` ranged `2/7` attacks `D` ranged `4/9`: `D` loses `2 HP`

## Damage Resolution and Removal

- Apply all damage from a single attack or effect before checking for removal.
- After that single attack or effect finishes resolving, any unit or building with `0` or less HP is removed immediately.
- Tiles vacated by removed occupants become empty immediately.

## Spell Rules

### General Spell Timing

- Spell cards can only be used during their owner's main phase.
- Spell cards use their listed battle-resource costs.
- Targets are chosen according to the card's own valid-target text.

### Non-Persistent Spells

- A non-persistent spell resolves immediately when cast.
- After it resolves, the card goes to the discard pile immediately.

### Persistent Spells

- Some spells create persistent effects.
- A persistent spell's effect remains in a separate persistent zone outside the `5x2` field.
- Persistent spells do not occupy field tiles.
- Persistent spells are not normal attack targets.
- The spell card itself goes to the discard pile when it is cast.
- The persistent effect does not have a default duration.
- Its end condition must be defined by the card text itself.

## Open Issues / TBD

> TBD: Multi-tile unit and building footprint rules

> TBD: Any targeting or resolution rules that are unique to multi-tile cards

> TBD: Full civilization roster and any civilization rules beyond the confirmed science power-upkeep rule
