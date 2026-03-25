# AGENTS

## Purpose

This file defines the non-negotiable implementation contract for the current project.

- Do not invent missing rules.
- If a behavior is not fixed in the source documents, stop and ask.
- If this file and a rules document conflict, the rules document wins.

## Source Of Truth

Use documents in this order:

1. `docs/battle_rules.md`
2. `docs/game_design.md`
3. `docs/draft_rules.md`
4. `docs/meta_rules.md`

If a rule is still marked `TBD`, do not guess.

## Current Implementation Scope

Implement only the current vertical slice:

- fixed decks
- one offline battle
- player versus AI
- battle ends in victory or defeat

Do not implement yet:

- draft flow
- multi-battle run progression
- 33 wins / 3 losses run loop
- rewards
- shop
- upgrades
- tickets
- persistent meta economy

## Battle Invariants

### Board And Coordinates

- Each player has a `5x2` field.
- Coordinates use `(column, row)`.
- `column = 0..4` from left to right.
- `row = 0` is the front row.
- `row = 1` is the back row.
- One tile can hold at most one occupant.
- Units and buildings occupy field tiles.
- Persistent spells do not occupy field tiles.

### Master Unit

- Each player starts with a Master Unit already on the field.
- Master start position is `(2,1)`.
- Master occupies `1x1`.
- Master is movable.
- Master is a melee attacker.
- Master stats are `333 HP` and `3 ATK`.
- Master follows the same targeting and melee-versus-melee counterattack rules as a normal melee unit.
- A player loses when their Master HP reaches `0` or below.

### Battle Start

- First player is random.
- First player starts with `3` cards.
- Second player starts with `4` cards.
- Starting resources are:
  - mana `0`
  - qi `0`
  - power `0`
  - gold `3`

### Mulligan

- Each player may mulligan once.
- Mulligan is partial.
- Selected cards return to the deck.
- The deck is shuffled.
- The same number of cards is redrawn.
- AI does not mulligan in the current slice.

### Turn Structure

Each turn follows this order:

1. turn start
2. calculate draw count and resource gain
3. draw cards and gain resources
4. resolve science-civilization power payment
5. main phase
6. turn end

### Resources

- Battle resources are mana, qi, power, and gold.
- Resource Gold is meta currency and is out of scope for the current slice.
- Base turn gain:
  - draw `1`
  - gold `1`
  - mana `0`
  - qi `0`
  - power `0`
- Units and buildings may grant `mana +n`, `qi +n`, `power +n`, or `gold +n` at turn start.
- All battle resources have no maximum.
- Unspent battle resources persist between turns.
- Unit, building, and spell cards all pay their listed battle-resource costs.
- Ongoing payment exists only when a rule or card text explicitly says so.

### Hand, Deck, And Draw Failure

- Maximum hand size is `9`.
- If a draw would exceed hand size:
  - the card is not drawn
  - it does not remain in the deck
  - it does not go to discard
  - it is removed
- Discard pile is never reshuffled into the deck.
- If a player attempts to draw from an empty deck, the draw fails and that player's Master takes damage instead.
- Draw-failure damage sequence is `3, 6, 9, 12, 15, ...`

### Main Phase Actions

Legal main-phase actions are:

- summon unit
- summon building
- move movable unit
- attack with eligible attacker
- play spell
- end turn

No separate global action-point system exists.

### Movement

- A movable unit can move to any empty tile on its owner's field.
- Movement is direct.
- Occupied tiles cannot be moved onto.
- Buildings cannot move.
- Immobile units cannot move.
- A movable unit may move on the turn it is summoned.

### Attack Availability

- Units normally attack once per turn.
- Some effects may allow more than one attack.
- Units normally cannot attack on the turn they are summoned unless an effect says otherwise.
- Some buildings can attack.
- Any attack-capable building is always ranged.
- Attack-capable buildings normally cannot attack on the turn they are summoned unless an effect says otherwise.

### Targeting

- Melee obeys front-row blocking.
- In a given enemy column, if the front-row tile is occupied by a blocker, melee cannot target the back-row tile in that column.
- If the front-row tile is empty, or occupied only by a disabled science-civilization unit, melee may target the back-row tile.
- Ranged can target any enemy occupied tile regardless of front-row blockers.
- Units, buildings, and Master Units are valid field occupants.

### Combat Resolution

- Only melee-versus-melee causes counterattack.
- Melee-versus-melee damage is simultaneous.
- Both sides can die in the same combat.
- Apply all damage from one attack or effect first.
- After that single attack or effect finishes resolving, remove any unit or building with `0` or less HP immediately.
- Vacated tiles become empty immediately.

### Buildings

- Buildings occupy tiles.
- Buildings can be targeted.
- Buildings participate in front-row blocking.
- Some buildings can attack.
- Any attacking building is always ranged.

### Spells

- Spell cards can only be used during their owner's main phase.
- Spell targets are defined by card text.
- Non-persistent spells resolve immediately and then go to discard immediately.
- Persistent spells create effects in a separate persistent zone outside the field.
- Persistent spells do not occupy tiles.
- Persistent spells are not normal attack targets.
- The spell card itself goes to discard when cast.
- Persistent spells have no default duration.
- Their end condition must be defined by card text.

### Science-Civilization Power Payment

- Some science-civilization units require power payment at turn start.
- Power payment happens after turn-start draw and resource gain.
- Payment is automatic in this fixed order:
  - `(0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1), (4,0), (4,1)`
- Empty tiles are skipped.
- Non-science units are skipped.
- If full payment can be made, pay it and keep the unit active.
- If full payment cannot be made, do not partially pay.
- A unit that cannot be fully paid becomes or remains disabled.

### Disabled Science Unit State

When disabled, the unit:

- cannot attack
- cannot move
- cannot counterattack
- cannot activate effects
- has its effects and special effects nullified
- takes double damage when attacked
- still occupies its tile
- can still be targeted
- does not participate in front-row blocking

A disabled science unit remains disabled through that turn's draw/resource step.
It does not contribute effects during that step.
After resources are gained, if power payment succeeds, it becomes active from that point onward.

## Card Effect Whitelist v1

Only the following effect families are allowed in the current slice.
If a card needs anything outside this list, stop and ask before implementing it.

### Allowed

- unit summon
- building summon
- normal spell cast
- persistent spell cast
- listed battle-resource costs on all card types
- melee or ranged attack type
- movable or immobile
- turn-start resource gain `+n`
- science-civilization power upkeep
- summon-turn attack allowed
- additional attacks
- attack-capable buildings
- non-persistent spell single-target damage
- persistent spell turn-start resource gain `+n` with explicit card-text end condition

### Not Allowed

- area damage
- melee front-row blocking bypass
- reaction spells or interrupts
- healing
- max HP increase or decrease
- attack increase or decrease
- extra draw
- card generation, copy, discovery, or theft
- transform
- resurrection
- death, attack, hit, summon, or leave triggers
- random effects
- multi-tile cards
- persistent effects without explicit end condition
- damage-over-time
- new status ailments such as freeze, silence, poison, or stun

## Golden Tests v1

All current-slice implementations must preserve these locked scenarios:

1. battle start setup
2. one-time partial mulligan
3. turn-start draw and resource gain
4. over-hand draw removal
5. empty-deck failed draw damage
6. movement rules
7. summon-turn attack restriction
8. melee front-row blocking
9. ranged ignores blockers
10. Master targeting matches normal melee rules
11. melee-versus-melee simultaneous damage
12. no ranged counterattack
13. immediate removal after resolution
14. attack-capable buildings are always ranged
15. normal spells resolve immediately and discard immediately
16. persistent spell card and persistent effect are separated
17. science-civilization power payment order
18. disabled-unit occupancy, targeting, non-blocking, and double-damage behavior

If a code change breaks any of the above, it is not valid for the current slice.

## AI Minimum Behavior v1

### Core Rules

- AI acts only inside the current vertical slice.
- AI performs only legal actions.
- AI is deterministic.
- AI recalculates board state after every action.

### Main-Phase Action Order

In the main phase, AI checks and performs one action at a time in this order, then starts again from the top:

1. an available attack
2. a kill-capable single-target normal damage spell
3. a resource-producing card
4. a unit or building that creates an immediate attack this turn
5. any other playable unit or building
6. a move that creates an immediate attack
7. end turn if none of the above is possible

### Placement Tie-Breaker

If multiple legal placement tiles exist, use this coordinate priority:

- `(2,1) -> (2,0) -> (1,1) -> (1,0) -> (3,1) -> (3,0) -> (0,1) -> (0,0) -> (4,1) -> (4,0)`

If the card can attack immediately, prefer the placement that creates the highest-priority attack target first.
If still tied, use the coordinate priority above.

### Movement Tie-Breaker

- AI only considers moves when no higher-priority main-phase action is available.
- AI only chooses moves that create an immediate attack.
- If multiple such moves exist, choose the one whose resulting attack has the highest attack-target priority.
- If still tied, prefer destination coordinate priority first, then attacker coordinate priority.
- Coordinate priority is:
  - `(2,1) -> (2,0) -> (1,1) -> (1,0) -> (3,1) -> (3,0) -> (0,1) -> (0,0) -> (4,1) -> (4,0)`

### Attack Target Priority

If multiple single-action attack targets are legal, use this order:

1. enemy Master if this attack can kill the enemy Master
2. kill-capable enemy resource-producing building or unit
3. kill-capable enemy occupant with the highest ATK
4. enemy Master if the enemy Master can be attacked directly
5. otherwise the legal target with the lowest current HP

If tied, use this coordinate priority:

- `(2,1) -> (2,0) -> (1,1) -> (1,0) -> (3,1) -> (3,0) -> (0,1) -> (0,0) -> (4,1) -> (4,0)`

For AI evaluation:

- a resource-producing building or unit is any occupant with a turn-start `mana +n`, `qi +n`, `power +n`, or `gold +n` effect
- kill-capable means the target would reach `0` or less HP from that single attack or single normal damage spell
- if multiple attackers can perform the same highest-priority attack, prefer the attacker with the higher ATK
- if still tied, use attacker coordinate priority:
  - `(2,1) -> (2,0) -> (1,1) -> (1,0) -> (3,1) -> (3,0) -> (0,1) -> (0,0) -> (4,1) -> (4,0)`

## Deferred Rules

The following are intentionally not fixed yet and must not be invented:

- multi-tile footprint rules
- multi-tile targeting or resolution rules
- full civilization roster beyond the confirmed science power-upkeep rule
- exact draft rarity probabilities
- per-offer duplicate behavior in draft
- AI deck sourcing in the full game loop
- reward formulas
- shop rules
- upgrade rules
- ticket rules
- full save or persistence rules for the meta loop

## Implementation Guardrails

- Do not extend scope beyond one fixed-deck battle unless explicitly asked.
- Do not add draft or meta progression logic.
- Do not implement card effects outside the whitelist.
- Do not invent hidden exceptions for unclear card interactions.
- Prefer simple, testable implementations over speculative systems.
- If a card or feature requires a rule that is not already locked, stop and ask.
