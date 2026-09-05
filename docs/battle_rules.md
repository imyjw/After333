# Battle Rules

## Document Purpose

This document is the source of truth for battle setup, tile notation, actions, targeting, and combat resolution.

- High-level overview: [game_design.md](./game_design.md)
- Draft design: [draft_rules.md](./draft_rules.md)
- Meta progression design: [meta_rules.md](./meta_rules.md)

## Battle Scope

### Full Game Design

- Battles are repeated within a run until the run ends at 33 wins or 3 losses.

### Current Account-Backed Implementation Scope

- Battles use a persisted `33`-card run deck created through draft.
- The same battle rules are resolved by the server for PvE and account-matched 1:1 PvP.
- PvP clients send intended commands only; the server validates commands, mutates battle state, and publishes `StateView` and `BattleEvents`.
- Battle results are persisted into the owning account's current run.
- A run continues across battles until it reaches `33` wins or `3` losses.

## Battle Objective

- Each player has a Master Unit.
- A player loses the battle when their Master Unit HP reaches 0 or below.
- If both Master Units reach `0` or below during the same resolution, compare their final HP after all damage from that resolution is applied.
- The Master with the lower final HP loses; if both final HP values are equal, the battle is a draw.
- A draw does not add a win or a loss to either player's draft run record.

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
- Each player's deck is shuffled independently before either opening hand is drawn.
- Starting hand size: both players draw `3` cards.
- Starting mana: `0`
- Starting qi: `0`
- Starting power: `0`
- Starting gold: `3`
- On battle turn `1`, the first player does not receive the normal base `gold +1`, so their first main phase begins with `3` gold.
- On battle turn `2`, the second player receives the normal base `gold +1`, so their first main phase begins with `4` gold if no other resource changes occurred.
- Each player begins with their Master Unit already on the field at `(2,1)`.

### Mulligan

- Each player may mulligan once.
- A mulligan is partial, not all-or-nothing.
- The player chooses any number of cards from their starting hand.
- Chosen copies are held outside the deck while replacements are drawn from the remaining deck.
- The player draws the same number of replacement cards.
- Another copy of the same card type may be drawn if one remains in the deck.
- After replacement cards are drawn, the chosen copies are returned and the deck is shuffled.
- PvE AI keeps its opening hand without choosing replacements.
- In PvP, both players choose simultaneously and the first turn waits until both players confirm.
- The server allows `33` seconds for mulligan selection; an unconfirmed player automatically keeps their current hand when time expires.
- Mulligan does not create a Coin card or any other bonus card.
- Opening cards are shown as large card images in a center-screen mulligan overlay.
- The player selects replacement cards in that overlay, then presses the mulligan confirm button once.
- Replacement cards are shown in the same positions as the selected cards.
- A PvP player who confirms first completes their replacement immediately and sees `상대의 멀리건을 기다리는 중...` until the opponent confirms or times out.
- Once both players are confirmed, the final mulligan result remains on screen for `3` seconds and then closes automatically.

## Turn Structure

Each turn follows this sequence:

1. Resolve early scripted turn-start effects, including Timed Bomb countdown/detonation
2. Gain base resources and active occupant resources
3. Resolve active Power Plant gold-to-power effects
4. Resolve power-upkeep payment
5. Resolve active Robot Factory effects
6. Resolve Firewall and Biochemical Bomb turn-start effects together in cast order
7. Resolve the base draw
8. Resolve Sealbound countdown/release
9. Main phase
10. Turn end

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

### Drained Units During Turn Start

- A unit that is currently drained remains drained through that turn's draw/resource calculation and gain step.
- A drained unit does not contribute its effects during that step.
- After resources are gained, power-upkeep payment is resolved.
- If the unit's power payment succeeds, the unit is active from that point onward.
- If the unit's power payment fails again, the unit remains drained.

### Resource Storage

- Mana, qi, power, and gold have no maximum value.
- Unspent mana, qi, power, and gold persist between turns.
- Gold can substitute for missing mana, qi, and power at a `1:1` rate for every resource payment,
  including card costs and ongoing upkeep.
- When a card cost includes both gold and another battle resource, pay the listed resource first if available and use gold to cover any remaining deficit.

## Power-Upkeep Payment

- Any unit with `sciencePowerUpkeep > 0`, plus an explicitly defined building such as Robot Factory, requires power payment at turn start.
- Power payment happens after the turn-start resource gain and Power Plant steps, and before Robot Factory, Firewall/Biochemical Bomb effects, and the base draw.
- Payment is attempted automatically in a fixed tile order.
- The fixed order is:

```text
(0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1), (4,0), (4,1)
```

- Empty tiles are skipped.
- Occupants with `sciencePowerUpkeep <= 0` are skipped.
- Power is spent first, then gold substitutes for any missing power at a `1:1` rate.
- If an occupant's full power payment can be made, that payment is made and the occupant is not drained.
- If an occupant's full power payment cannot be made, no partial payment is made and that occupant becomes or remains drained.
- Units and buildings use the same Drained result when power upkeep cannot be paid.

### Drained State

When a unit is drained:

- It cannot attack.
- It cannot move.
- It cannot counterattack.
- Its effects do not activate.
- Its effects and special effects are treated as nullified.
- Its physical defense and magic defense are treated as `0`.
- It takes triple damage from every incoming physical, magic, or fixed damage instance.
- It still occupies its tile.
- It can still be targeted by attacks and effects.
- It does not participate in front-row blocking.

### Erasure State

Erasure (`망각`) may affect Units, Buildings, and Master Units. It lasts until the occupant leaves the field.

- Tooltip: `망각: 카드의 효과와 버프·디버프가 무효화됩니다.`
- The occupant may still attack, counterattack, and move when its printed rules otherwise allow those actions.
- Its card effects and traits are suppressed, including Berserker, multi-hit, additional attacks, Endure, Shielder, LifeSteal, Rush, Replicate-related occupant traits, resource production, Robot Factory, SpellPower, Hiding, Flying, and Invincible.
- Its science power upkeep is suppressed. Applying Erasure immediately clears Drained, and an Erasure occupant cannot become Drained.
- Applying Erasure removes every combat-time ATK, max-HP, Physical Defense, and Magic Defense modifier already on the occupant. Robot Fusion bonuses and the Daehwandan ATK bonus are included.
- The original summon baseline remains: printed stats plus the account upgrade level used at summon. Current HP is never healed when a max-HP bonus is removed and is clamped only when it exceeds the restored maximum.
- Buffs and debuffs applied after Erasure take effect normally. Reapplying Erasure removes the modifiers that exist at that later application time.
- Base attack type, damage type, movement permission, tile size, baseline ATK/HP, baseline defenses, and account upgrade stats remain.
- Robot classification is suppressed for battle searches, so an Erasure Robot cannot be counted or selected by Robot Fusion.
- Erasure does not set defenses to `0`, multiply incoming damage, or remove front-row blocking. Shielder is suppressed and therefore cannot redirect, but an ordinary front-row Erasure occupant still blocks.
- Sealbound is a separate state and is not removed by Erasure.
- Reconnect state stores `IsErasure` and the original combat-stat baseline. Legacy snapshots containing `IsDisabled` are migrated to `IsErasure` when restored.

## Hand, Deck, and Draw Failure Rules

### Maximum Hand Size

- Maximum hand size is `10`.
- Some effects may increase maximum hand size, but it can never exceed `13`.

If a draw would exceed the maximum hand size:

- The card is not drawn.
- The card does not remain in the deck.
- The card does not go to the discard pile.
- The card is removed instead.

### Opponent Played Card Reveal

- In both server-backed PvE and PvP, a newly played opponent card is revealed as a large card image at screen center.
- Unit and building cards show their server-authored upgraded ATK and HP values.
- The reveal is presentation-only and never blocks battle input or command processing.
- A reveal fades in for `0.15` seconds, remains visible, then fades out for `0.15` seconds without dimming the battle background.
- Normal total display time is `3` seconds. While another card is waiting in the FIFO reveal queue, the current total display time is shortened to `2` seconds.
- Reconnecting clients do not replay card-use history from before the reconnect.

### Discard Pile

- The discard pile is not reshuffled into the deck.

### Draw Failure Damage

If a player attempts to draw a card and their deck has no card to draw:

- The draw fails.
- The player's Master Unit takes damage instead of drawing.
- Draw-failure damage is Fixed damage and ignores both defenses.
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
- Gold may substitute for missing mana, qi, or power at a `1:1` rate during that payment.
- Additional ongoing payment only exists when a rule or card text specifically says so.

## Summon Rules

- A summon requires valid empty tile space.
- Units and buildings may only be placed on the player's own field.
- Buildings cannot move.
- If a card requires more than one tile, every required tile must be empty.

## Move Rules

- The player selects one of their own movable units.
- The player then selects a tile on their own field.
- The unit moves directly to that tile.
- If the selected destination tile is empty, the unit moves there normally.
- If the selected destination tile is occupied by another allied movable unit, the two units swap positions instead.
- Movement is not allowed onto occupied tiles that do not contain another allied movable unit.
- Immobile units cannot move.
- Buildings cannot move.
- A movable unit may move on the same turn it is summoned.

## Attack Availability

### Units

- Units normally attack once per turn.
- A unit with `0` ATK cannot attack.
- Some card effects may allow two or more attacks in a turn.
- A unit summoned this turn cannot normally attack until the next turn.
- Rush (`hasRush: true`) allows a unit played from hand to attack during the turn it is summoned.
- Rush removes only summoning sickness. It does not grant extra attacks or bypass targeting, `0` ATK,
  Drained, or any other attack restriction. If Erasure is applied during the summon turn, Rush is suppressed and normal summoning sickness applies again.

### Buildings

- Some buildings can attack.
- A building with `0` ATK cannot attack.
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
- If the enemy front-row tile in a column is empty, or occupied only by a drained unit, the enemy back-row tile in that column may be targeted by a melee attack.

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

## Damage Types and Defenses

### Damage Types

- Every attack-capable unit or building has one normal-attack damage type: `Physical`, `Magic`, or `Fixed`.
- A non-attacking occupant may use `None`.
- Counterattacks use the counterattacker's own damage type.
- Damage-dealing card effects declare their own damage type independently of the source occupant's normal attack.
- The Master Unit's normal attack is `Physical`.

### Defense Values

- Field occupants have `Physical Defense` and `Magic Defense` values.
- Defense values cannot be negative.
- All currently defined cards and the Master Unit start with `0 Physical Defense / 0 Magic Defense`.
- Physical damage is reduced by Physical Defense.
- Magic damage is reduced by Magic Defense.
- Fixed damage ignores both defenses.

For a recipient that is not drained:

```text
Physical final damage = max(0, original damage - Physical Defense)
Magic final damage    = max(0, original damage - Magic Defense)
Fixed final damage    = original damage
```

For a drained recipient, both defenses are first treated as `0`, then the resulting physical, magic, or fixed damage is multiplied by `3`.

### Repeated Hits

- Double Attack and Triple Attack resolve as separate sequential hits.
- The matching defense and drained multiplier are applied separately to every hit.
- Damage popups and LifeSteal use each hit's actual HP loss after this calculation.

### Locked Effect Damage Types

- Firebolt deals `10 Magic` damage to its selected occupied tile. It may target an occupant on either player's field, including either Master Unit.
- An upgradeable damage spell gains `+1` base damage per card level, up to level `13`.
- Red Dragon deals `33 Magic` damage to every enemy tile occupant at the end of its owner's turn.
- Empty-deck draw-failure damage is `Fixed`.
- The development-only `333` Master damage command is `Fixed`.
- Endure prevents the first lethal HP-damage packet from any source and leaves its active bearer at `1 HP`. This includes normal attacks, counterattacks, Physical/Magic/Fixed spell damage, persistent or delayed effects, area damage, destruction-trigger damage, and empty-deck damage.
- Endure is consumed only when it prevents lethal damage. Later lethal damage defeats the occupant normally, and non-damage removal such as fusion-material consumption is not prevented.

### Gaebang Branch

- Gaebang Branch is a Common, non-attacking `1x1` Murim building with cost `2 Gold`, ATK `0`, HP `20`, defense `0/0`, and DamageType `None`.
- At the end of its owner's turn, it draws `2` cards if that owner's current Gold is exactly `0`.
- A Gaebang Branch summoned during the current turn can trigger at that same turn end.
- Every living Gaebang Branch whose effects are not suppressed resolves independently in fixed tile order `(0,0), (0,1), ... (4,1)`.
- A full hand removes each excess drawn card immediately. An empty deck causes the normal cumulative Fixed draw-failure damage for each attempted draw.
- Drained, Erasure, and Sealbound suppress the effect. The effect continues at later eligible turn ends until the building leaves the field.
- Gaebang Branch upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Gaebang Branch remains excluded from draft offers and random card rewards until its card art is ready.

### Merchant Caravan

- Merchant Caravan (`MerchantCaravan`, `상단`) is an Uncommon, non-attacking `1x1` Murim building with cost `4 Gold`, ATK `0`, HP `30`, defense `0/0`, and DamageType `None`.
- It starts triggering on its owner's next turn start after it is summoned; it never triggers immediately on summon.
- During the active-occupant resource-gain step, each living Merchant Caravan whose effects are not suppressed grants its owner `3 Gold`.
- Multiple active Merchant Caravans stack and each grants `3 Gold` independently.
- Drained, Erasure, and Sealbound suppress this effect. It resumes on a later owner turn start after suppression ends.
- Merchant Caravan upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Merchant Caravan remains excluded from draft offers and random card rewards until its card art is ready.

### Inn

- Inn (`Inn`, `객잔`) is a Common, non-attacking `1x1` Murim building with cost `2 Qi`, ATK `0`, HP `20`, defense `0/0`, and DamageType `None`.
- It starts triggering on its owner's next turn start after it is summoned; it never triggers immediately on summon.
- During the active-occupant resource-gain step, each living Inn whose effects are not suppressed grants its owner `1 Gold`.
- Multiple active Inns stack and each grants `1 Gold` independently.
- Drained, Erasure, and Sealbound suppress this effect. It resumes on a later owner turn start after suppression ends.
- Inn upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Inn remains excluded from draft offers and random card rewards until its card art is ready.

### Power Bank

- Power Bank is a Common Science Civilization scripted spell with cost `3 Gold` and no board target.
- It can be cast only during its owner's main phase by dragging it upward from the hand.
- Casting first pays the full `3 Gold` cost and moves the card from hand to discard, then immediately grants that player `6 Power`.
- It creates no persistent effect and cannot use Gold to pay its printed Gold cost through substitution.
- Power Bank is not upgradeable and remains excluded from draft offers and random card rewards until its card art is ready.

### Mana Stone

- Mana Stone is a Common Fantasy scripted spell with cost `4 Gold` and no board target.
- It can be cast only during its owner's main phase by dragging it upward from the hand.
- Casting first pays the full `4 Gold` cost and moves the card from hand to discard, then immediately grants that player `6 Mana`.
- It creates no persistent effect and cannot use Gold to pay its printed Gold cost through substitution.
- Mana Stone is not upgradeable and remains excluded from draft offers and random card rewards until its card art is ready.

### Mana Stone Bundle

- Mana Stone Bundle is an Uncommon Fantasy scripted spell with cost `6 Gold` and no board target.
- It can be cast only during its owner's main phase by dragging it upward from the hand.
- Casting first pays the full `6 Gold` cost and moves the card from hand to discard, then immediately grants that player `9 Mana`.
- It creates no persistent effect and cannot use Gold to pay its printed Gold cost through substitution.
- Mana Stone Bundle is not upgradeable and remains excluded from draft offers and random card rewards until its card art is ready.

### Firewall

- Firewall is a persistent scripted spell with a base cost of `3 Mana` and base damage of `33 Magic`.
- The caster selects row `0` or row `1` on either player's field by targeting any tile in that row.
- An entirely empty row on either field is still a legal target.
- The selected row is fixed when the spell is cast. Units moving into or out of that row before a trigger are evaluated at trigger time.
- Firewall does not deal damage immediately when cast.
- It triggers at the next two global turn starts: normally once at the opponent's next turn start and once at the caster's following turn start.
- Each trigger occurs after base resources, occupant resources, power-upkeep payment, and Robot Factory effects, but before the base draw.
- Each trigger deals its stored Magic damage to every current occupant in the selected row, including allied or enemy units, buildings, and the Master Unit.
- If Firewall defeats its caster's Master, the opposing player wins.
- Firewall is an area effect, so Shielder does not redirect it. Every occupant in the row resolves defense and drained multiplication independently.
- All damage from one Firewall instance is applied before defeated units/buildings are removed and Master defeat is checked.
- An empty-row trigger still consumes one of the two triggers.
- Multiple Firewall effects may coexist and resolve independently in creation order.
- The spell card goes to discard when cast; the separate persistent effect expires after its second trigger.

### Biochemical Bomb

- Biochemical Bomb is an Uncommon Science Civilization scripted spell with cost `4 Power + 1 Gold` and base damage `44 Physical`.
- The caster must select one of two areas on the opponent's field: left `4x2` (`columns 0..3`) or right `4x2` (`columns 1..4`). It cannot target the caster's own field.
- The selected eight tiles are highlighted red while targeting. The selected start column is fixed when the spell is cast.
- It does not deal damage immediately. It triggers at each of the next four global turn starts, regardless of whose turn begins, then expires.
- A normal sequence after Player casts it is: opponent turn start `1`, Player turn start `2`, opponent turn start `3`, Player turn start `4` and expiration.
- Each trigger evaluates the current occupants in the fixed area, so an occupant summoned or moved into the area after casting is damaged by later triggers.
- Each trigger deals the stored Physical damage to every current unit, building, or Master in the area. Empty tiles do not prevent a trigger from being consumed.
- Physical Defense, Invincible, and the Drained damage multiplier resolve independently for every target. Sealbound occupants take no damage.
- Biochemical Bomb is an area effect, so Shielder does not redirect it and Hiding does not avoid it.
- All damage from one Biochemical Bomb instance is applied before defeated units/buildings are removed and Master defeat is checked.
- Firewall and Biochemical Bomb instances share one creation-order pass after Robot Factory and before the base draw, so mixed effects resolve in the order their cards were cast.
- Multiple Biochemical Bomb effects may coexist. Each keeps its own fixed area, damage, and remaining trigger count.
- Each upgrade level adds `+1` to every trigger's stored Physical damage; for example, Lv.3 deals `47 Physical`. Physical damage does not receive SpellPower.
- The spell card goes to discard when cast. Biochemical Bomb remains excluded from draft offers and random card rewards until its art is ready.

### Timed Bomb

- Timed Bomb is a Common Science Civilization scripted spell with cost `3 Power + 1 Gold`, base damage `33 Physical`, and no board target.
- It does not deal damage immediately. Casting it creates a separate persistent effect and sends the spell card to discard.
- Every global turn start decreases its countdown once, regardless of whose turn begins.
- If Player casts it and then ends the turn, the first countdown is the opponent's next turn start, the second is Player's following turn start, and it detonates at the opponent's next turn start.
- On detonation it deals its stored Physical damage to every current occupant on the opposing field, including units, buildings, and the Master Unit.
- Timed Bomb is an area effect, so Shielder does not redirect it and Hiding does not avoid it. Sealbound occupants remain untargetable and take no damage.
- Physical Defense, Invincible, and the Drained damage multiplier resolve independently for every target.
- All targets take damage before defeated non-Master occupants are removed and Master defeat is checked.
- Each upgrade level adds `+1` to the stored Physical damage. Physical damage does not receive SpellPower.
- Multiple Timed Bomb effects coexist and count down independently in creation order.
- Timed Bomb remains excluded from draft offers and random card rewards until its card art is ready.

## Attack and Counterattack Resolution

### Core Rule

Only melee-versus-melee combat causes a counterattack.
An occupant with `0` ATK cannot counterattack.

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
- If both Master Units are defeated by the exchange, the higher final HP wins; equal final HP produces a draw.

### LifeSteal

- A LifeSteal occupant heals by the actual HP it removes with an attack or counterattack after defense, drained multiplication, and remaining-HP limits are applied.
- LifeSteal does not trigger while the occupant's effects are suppressed.
- In melee-versus-melee combat, both attack and counterattack damage are applied first, then occupants at `0` or less HP are treated as defeated, then surviving LifeSteal occupants heal.
- If a LifeSteal attacker or counterattacker is defeated by the same melee exchange, it does not heal from that exchange.

### Robot Tag

- `Robot` is a unit classification tag used by Robot searches.
- A Robot unit remains eligible while Drained, but Erasure suppresses its Robot classification for battle-rule searches.
- The Robot tag has no standalone stat or combat effect.
- Robot Fusion is playable when its owner controls at least two living Robot units.
- This count is based on living Robot unit instances, not distinct CardIds. Two living copies of the same Robot card satisfy the condition.
- Casting Robot Fusion pays its listed cost, discards the spell, and enters an ordered friendly-Robot selection mode.
- At least two unique living Robot units on the caster's field must be selected before resolution.
- The selected Robot with the highest science power upkeep survives. If upkeep ties, the Robot selected earlier survives.
- Every other selected Robot contributes its current ATK and current HP to the survivor as permanent ATK and max/current HP increases.
- A damaged Robot contributes only its remaining current HP, so fusion never restores the donor's missing HP for free.
- Special effects, attacks per turn, damage type, science power upkeep, Drained state, and Erasure state are not transferred. The survivor retains its own values and states.
- Absorbed Robots leave the board as fusion materials and are not treated as defeated occupants.

### Robot Factory

- Robot Factory is a Rare, non-attacking `1x1` Science Civilization building with cost `1 Power`, ATK `0`, HP `50`, defense `0/0`, DamageType `None`, and `sciencePowerUpkeep 1`.
- Robot Factory itself does not have the Robot tag.
- It starts triggering on its owner's next turn start after it is summoned; it never triggers immediately on summon.
- After power upkeep resolves, every living Robot Factory whose effects are not suppressed generates one card in fixed tile order `(0,0), (0,1), ... (4,1)`.
- The candidate pool contains only Unit definitions where `hasRobot == true` and `includeInDraft == true`.
- Every eligible Robot definition has equal selection probability. Selection is server-authoritative.
- Multiple active factories each generate one independently selected card.
- The generated card is added directly to its owner's hand and does not come from the deck.
- If the hand is full, the generated card is removed immediately. This does not cause empty-deck damage.
- A Drained or Erasure factory cannot generate a card. If upkeep succeeds and clears Drained, it can trigger in that same turn's Robot Factory step. Erasure suppresses both its upkeep and generation effect.
- Generated cards use the owning account's current upgrade level when later played.
- The owner sees the generated card identity in combat history. The opponent sees only that Robot Factory generated one card, while the owner's public hand count increases normally.
- Robot Factory upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.

### Power Plant

- Power Plant is a Common, non-attacking `1x1` Science Civilization building with cost `1 Power`, ATK `0`, HP `30`, defense `0/0`, DamageType `None`, and no power upkeep.
- It starts triggering on its owner's next turn start after it is summoned; it never triggers immediately on summon.
- After base and active-occupant resource gain, but before power upkeep, each living Power Plant whose effects are not suppressed automatically pays exactly `1 Gold` and grants `2 Power`.
- Gold is the effect's explicit payment, not a substitute payment. If `1 Gold` is unavailable, that Power Plant skips its trigger with no partial payment or debt.
- Multiple active Power Plants resolve independently in fixed tile order `(0,0), (0,1), ... (4,1)`.
- Drained, Erasure, and Sealbound suppress this effect. It resumes on a later owner turn start after suppression ends.
- Power Plant upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Power Plant remains excluded from draft offers and random card rewards until its card art is ready.

### Nuclear Power Plant

- Nuclear Power Plant is a Rare, non-attacking `1x1` Science Civilization building with cost `5 Power`, ATK `0`, HP `50`, defense `0/0`, DamageType `None`, and no power upkeep.
- At its owner's turn-start active-occupant resource step, every living Nuclear Power Plant whose effects are not suppressed grants `5 Power`.
- When a Nuclear Power Plant is destroyed, it is removed first and then deals `40 Physical` damage to every current occupant on both fields, including Units, Buildings, and both Master Units.
- Physical Defense, Invincible, Sealbound immunity, and the Drained damage multiplier resolve independently for every target. Shielder does not redirect this area damage.
- Drained, Erasure, and Sealbound suppress both its turn-start resource effect and its destruction effect.
- If an explosion destroys another active Nuclear Power Plant, that plant is removed and creates another explosion. Each destroyed plant can trigger only once.
- All queued Nuclear Power Plant explosions resolve before Master defeat is checked. If both Masters are defeated, the higher final HP wins; equal final HP produces a draw.
- Nuclear Power Plant upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Nuclear Power Plant remains excluded from draft offers and random card rewards until its card art is ready.

### Gu

- `Gu` (`고독`) is a Unique Murim non-damage Scripted Spell with cost `10 Qi` and `effectId: "gu"`.
- It targets exactly one living enemy Unit. Master Units, Buildings, Sealbound occupants, and active Hiding units are not legal targets.
- The caster must have an empty field tile. If no destination exists or the declared target is illegal, the command is rejected before any resource or hand-card consumption.
- The controlled Unit moves permanently to the first empty caster tile in this fixed order: `(0,0), (0,1), (1,0), (1,1), ... (4,1)`.
- The same occupant instance and RuntimeId are retained. Current ATK, current/max HP, defenses, attack and damage types, traits, buffs, debuffs, suppression states, Invincible durations, attack counts, and all other occupant state remain attached.
- Drained, Erasure, and Invincible Units are legal targets and keep those states. Shielder neither redirects nor prevents Gu because Gu deals no damage.
- After control changes, owner-dependent upkeep, turn-start resource gain, Shielder, SpellPower, Robot classification, and owner-turn duration checks use the new owner.
- Persistent effects keep their own original owner. An effect attached by target RuntimeId continues to follow the controlled Unit until that effect's normal end condition.
- Immediately after control changes, the Unit cannot attack during that turn. It becomes normally attack-ready at the start of its new owner's next turn.
- An active Rush trait is the only exception: a non-suppressed Rush Unit receives its normal attack allowance immediately after control changes. Drained, Erasure, or Sealbound suppression prevents this Rush exception.
- Movement availability is preserved and continues to follow the Unit's normal movement rules.
- The transfer is not destruction, defeat, summon, or resurrection. It does not trigger removal/death semantics.
- StateView and server-restart snapshots serialize the Unit under its new owner and board while preserving its RuntimeId and occupant state.
- Gu is not redirected by Shielder, gains no SpellPower, cannot be upgraded, and remains excluded from draft offers and random rewards until its card art and presentation are ready.

### HuanShu

- `HuanShu` (`환술`) is an Uncommon Murim non-damage Scripted Spell with cost `3 Qi` and `effectId: "huan_shu"`.
- It targets exactly one living enemy Unit. Master Units, Buildings, allied occupants, Sealbound occupants, and active enemy Hiding units are not legal declared targets.
- A successful cast applies HuanShu permanently while that occupant remains on the field. Recasting an already afflicted Unit does not stack or change the status.
- The duration decreases at the end of each turn belonging to the afflicted Unit's current owner, even if that Unit did not attack or could not act.
- When the afflicted Unit declares an otherwise legal normal attack, the server replaces the target with one uniformly random legal HuanShu candidate before combat resolves.
- The original player-selected target must still be legal under ordinary attack rules. HuanShu does not make an illegal attack command legal.
- The random candidate pool contains every living Unit, Building, and Master Unit on both fields except the afflicted attacker itself.
- Ownership, front-row blocking, and the originally selected target do not restrict the random candidate pool. The original target remains one possible candidate.
- Sealbound occupants and active enemy Hiding occupants are excluded. A Hiding occupant allied with the afflicted attacker may be selected.
- A non-Flying melee afflicted attacker cannot be redirected to an active Flying occupant. Ranged attackers and Flying attackers may be redirected to Flying occupants normally.
- Invincible occupants remain candidates and take `0` damage under the normal Invincible rule.
- The server selects once per declared attack. Every hit of a multi-hit attack uses that same resolved target. Each additional attack declaration performs a new random selection.
- Counterattacks are never redirected by HuanShu. After target replacement, normal damage, Shielder, counterattack, LifeSteal, Endure, Berserker, removal, and victory rules resolve without special exceptions.
- Drained prevents the afflicted Unit from attacking but does not remove HuanShu. Applying Erasure immediately removes HuanShu; a Unit already under Erasure may receive a newly cast HuanShu.
- Sealbound does not remove an existing HuanShu. A new HuanShu cast cannot target a Sealbound Unit.
- Gu preserves HuanShu when ownership changes.
- Robot Fusion does not transfer HuanShu from absorbed materials. A surviving afflicted Robot keeps its own HuanShu status.
- Server-authoritative random selection is included in synchronized battle events. StateView and server-restart snapshots preserve the remaining duration and countdown context; reconnect never rerolls a completed attack.
- Both players see only a small purple HuanShu status icon on the occupant. Long-pressing the occupant shows the permanent HuanShu explanation without a turn counter.
- HuanShu cannot be upgraded and remains excluded from draft offers and random rewards until its card art is ready.

### Replicate

- `Replicate` (`hasReplicate: true`, Korean display name `복제`) may be used by units, buildings, and spells.
- After a Replicate card is successfully played from hand and consumed, its owner receives one temporary copy of the same CardId in hand.
- A rejected play does not create a copy. If the hand is full when creation is attempted, no copy is created.
- The temporary copy retains Replicate, so playing it successfully creates the next temporary copy.
- A Replicate card with a printed total cost of `0` is legal and can continue replicating while every other play requirement remains legal.
- Every temporary copy uses the original CardDefinition cost and base rules. Temporary in-battle hand changes such as cost reduction, ATK, or HP changes on the played instance are not inherited.
- The owning account's battle-start card upgrade level is resolved normally whenever a copy is played. Account upgrade progression is not a temporary hand modification.
- Every hand card has a runtime identity. This distinguishes permanent copies from temporary Replicate copies even when their CardIds match.
- Unused temporary Replicate copies vanish at the end of their owner's turn. They do not enter the discard pile.
- A played copy resolves like the underlying card: units and buildings enter the field, while played spells enter discard after resolving normally.
- The opponent sees only the resulting hand-count change and never receives the temporary copy's hidden identity.
- Reconnect snapshots preserve each hand card's runtime identity and temporary-Replicate flag.

### Werewolf Pack Attack

- The card-specific Werewolf rule applies only to occupants whose CardId is exactly `Werewolf`.
- A living, active Werewolf gains `ATK +10` for each other living allied Werewolf currently on the same field.
- The receiving Werewolf gains no pack bonus while Drained, under Erasure, or Sealbound.
- When counting the other Werewolves, Drained and Erasure occupants still count because their CardId remains Werewolf. Sealbound and defeated Werewolves do not count.
- The bonus is recalculated whenever ATK is read. Summoning, defeat, removal, Sealbound changes, and ownership transfer therefore update every affected Werewolf immediately without storing a permanent ATK modifier.
- Card-level ATK upgrades remain part of the Werewolf's stored base combat stat. The pack bonus is added afterward and is not copied into reconnect snapshots as a permanent stat.
- StateView carries both stored `BaseAttack` and final display `Attack`, so the client rebuilds the field from the stored value and applies the live bonus exactly once.
- Werewolf also has Replicate and follows every normal Replicate rule above.

### Sealbound

- `Sealbound` uses the card-data field `sealboundOwnerTurnStarts: n` on Unit and Building cards only.
- `0` means the card does not enter Sealbound. A value of `1` or greater makes the summoned occupant enter Sealbound immediately with that many owner-turn starts remaining.
- The summon turn does not reduce the counter. Each later turn start of the Sealbound occupant's owner reduces it by `1`.
- A Sealbound occupant continues to occupy its tile. It cannot move, exchange positions with another occupant, attack, or counterattack.
- A Sealbound occupant cannot activate effects or pay science power upkeep.
- A Sealbound occupant cannot be selected or affected by attacks, spells, area damage, healing, buffs, or debuffs, and it takes no damage while Sealbound.
- A Sealbound occupant is excluded from Shielder, Robot Fusion material selection, Robot Factory searches, and equivalent occupant-search effects.
- A Sealbound occupant does not participate in front-row blocking.
- Sealbound countdown and release are the final operations of the owner's turn-start sequence, after resources, upkeep, Robot Factory, Firewall/Biochemical Bomb effects, and base draw have resolved.
- When the remaining counter reaches `0`, Sealbound ends. Because release occurs last, the released occupant does not contribute resources, pay upkeep, or activate turn-start effects during that same turn start.
- Unless it has Rush, an occupant released from Sealbound has summoning sickness for the release turn and cannot attack until its owner's following turn. Rush removes only this release-turn attack restriction.
- Sealbound is independent from Drained and Erasure. A Sealbound occupant is immune to Erasure: applying Erasure while it is Sealbound has no effect and does not remove attached effects. Drained and upkeep evaluation are suspended while Sealbound and resume during the next applicable upkeep step after release.
- Reconnect snapshots store `IsSealbound`, the remaining owner-turn-start count, and Rush so release-turn attack eligibility survives reconnection.
- Master Units cannot be Sealbound.
- Sealbound is implemented. Existing cards remain unaffected while `sealboundOwnerTurnStarts` is omitted or `0`.

### Demon King Return

- `DemonKing` is a movable `1x1` Fantasy Unit with melee Magic damage, base `33 ATK / 33 HP`, and `3 / 3` Physical/Magic Defense.
- When a living Demon King reaches `0 HP` or is directly destroyed, it remains on the same tile at `0 HP` and enters a dedicated revival Sealbound state instead of being removed.
- If its effects are suppressed by Drained or Erasure at the moment of death, the return effect does not trigger and the Demon King is removed permanently.
- The countdown belongs to the player whose turn was active when the Demon King died, not necessarily the Demon King's owner. The death turn itself is never counted.
- Each of that countdown player's next three turn starts reduces the counter `3 -> 2 -> 1 -> 0`. Revival resolves in the final Sealbound step of the third applicable turn start.
- On each revival, cumulative `ATK +33` and `Max HP +33` are applied from the account-upgraded original stats, current HP is restored to the new maximum, and the revival count increases by one. The effect can repeat without a fixed limit.
- Temporary combat buffs, debuffs, Drained, Erasure, Hiding reveal, HuanShu, Invincible effects, and consumed Endure state are cleared on revival. Card identity, account upgrade stats, damage/attack types, defenses, movement, and accumulated revival count remain.
- A revived Demon King has summoning sickness and no attacks on the release turn. It has no Rush exception.
- Removal that is explicitly not a death, such as being consumed as Robot Fusion material, does not trigger the return effect.
- StateView and reconnect snapshots store the pending flag, remaining countdown, countdown player, eligible death turn, and cumulative revival count.

### Hiding

- `Hiding` uses the reserved future Unit-card flag `hasHiding: true`. Buildings and Master Units cannot have Hiding.
- A Hiding unit enters the field hidden and remains hidden without a turn limit until it declares a legal normal attack or enters Drained or Erasure.
- While hidden, the unit cannot be selected as the target of an opponent's normal attack or opponent's single-target spell.
- Area spells and area effects are not blocked by Hiding.
- A hidden unit does not participate in front-row blocking, so a melee attacker may target an otherwise legal back-row occupant in the same column.
- Movement, turn changes, and activating a non-attack effect do not reveal the unit.
- After the server accepts a legal normal-attack command, Hiding is removed before the first hit, damage, LifeSteal, and counterattack are resolved. A rejected or illegal attack attempt does not reveal it.
- A revealed melee attacker receives a legal melee counterattack normally.
- Multi-hit and additional-attack units reveal before the first hit of their first declared attack. Hiding does not return for later hits or attacks.
- A hidden Shielder does not redirect attacks or effects. Shielder becomes active after Hiding is removed if no other state suppresses it.
- `HasHiding` and `HidingRevealed` are separate values. Entering Drained or Erasure permanently sets `HidingRevealed`; clearing Drained never restores Hiding, and Erasure itself remains for the occupant's time on the field.
- If a Unit has both Sealbound and Hiding, Sealbound rules take priority while sealed. It becomes hidden when released unless Hiding was already permanently revealed by a separate legal rule.
- StateView and reconnect snapshots preserve both the immutable `HasHiding` trait and `HidingRevealed` so the current state survives reconnection and server restoration.
- Hiding is implemented. Existing cards remain unaffected while `hasHiding` is omitted or `false`.

### Flying

- `Flying` uses the occupant flag `hasFlying: true`. Units, Buildings, and Master Units may have Flying.
- A Flying occupant cannot be selected as the target of a normal attack made by a non-Flying melee attacker.
- Ranged normal attacks may target Flying occupants normally.
- A Flying attacker may target another Flying occupant with a normal attack even when the Flying attacker's attack type is Melee.
- A Flying melee attacker ignores enemy front-row blocking and may target an otherwise legal back-row occupant. If the defender is melee and can counterattack, it counterattacks the Flying attacker normally.
- A Flying occupant does not participate in front-row blocking.
- Flying does not protect against single-target spells, area spells, healing, buffs, debuffs, or other non-normal-attack effects.
- Flying does not change movement range, movement count, destination legality, or position-exchange rules.
- A Flying Shielder cannot redirect a non-Flying melee normal attack. It may redirect ranged normal attacks, normal attacks from Flying attackers, and other Shielder-eligible effects.
- A non-Flying Shielder may protect a Flying occupant from any attack or effect that could legally target that Flying occupant.
- Flying is temporarily suppressed by Drained and remains suppressed for the duration of Erasure. While suppressed, a non-Flying melee attacker may target the occupant. Drained prevents front-row blocking through its own rule; an Erasure occupant whose Flying is suppressed participates in front-row blocking.
- Clearing Drained restores Flying. Erasure does not clear while the occupant remains on the field, so Flying does not reactivate during that occupancy.
- Active Hiding takes priority over Flying targeting permissions. After Hiding is revealed, the remaining Flying rules apply normally.
- Sealbound takes priority while sealed. Flying becomes active again when Sealbound ends, subject to any current Drained or Erasure suppression.
- Robot Fusion does not transfer Flying from absorbed materials. The survivor retains Flying only if it already had Flying.
- Flying does not end when the occupant attacks and lasts for that occupant's entire time on the field unless temporarily suppressed by a locked rule above.
- StateView and reconnect snapshots preserve `HasFlying` for every occupant, including Buildings and Master Units.
- Flying is implemented. Existing cards remain unaffected while `hasFlying` is omitted or `false`.

### Piercing

- `Piercing` uses the occupant flag `hasPiercing: true`. Units and attack-capable Buildings may define it in card data, and Master Units may receive it from runtime battle setup.
- Piercing applies to accepted normal attacks whose actual resolved target occupies either row of a field column.
- After each normal-attack hit resolves, an occupied tile in the opposite row of the same column receives a separate damage packet with the attacker's same per-hit ATK amount and normal-attack DamageType. A front-row target pierces into the rear row, and a rear-row target pierces into the front row.
- Units, Buildings, and Master Units are valid front and rear occupants for Piercing.
- The opposite-row packet does not use Shielder redirection and never causes a counterattack. The opposite-row occupant applies its own matching defense, Drained defense loss and triple-damage rule, Invincible, Endure, removal, and victory rules normally.
- Hiding and Flying do not prevent the opposite-row packet because Piercing does not select that occupant as a new attack target. Sealbound occupants take `0` Piercing damage under the normal Sealbound rule.
- Multi-hit attacks apply one Piercing packet after every hit. Additional attacks evaluate Piercing independently for each declared attack.
- LifeSteal includes the opposite-row occupant's actual HP loss after defense and prevention. On the final melee hit, direct and opposite-row damage resolve before the counterattack, then a surviving attacker heals from the combined actual HP loss.
- When HuanShu redirects an attack, Piercing uses the actual random target's row and column rather than the originally selected target.
- Drained and Sealbound prevent an occupant from attacking through their normal rules. Erasure suppresses Piercing even if the immutable `HasPiercing` trait remains stored.
- StateView and server-restart snapshots preserve `HasPiercing` for Units, Buildings, and Master Units.
- Piercing is implemented. Existing cards remain unaffected while `hasPiercing` is omitted or `false`.

### SpellPower

- `SpellPower +n` uses the reserved future occupant value `spellPower: n`. Units, Buildings, and Master Units may provide SpellPower; `0` means none and negative values are invalid.
- SpellPower applies only to Magic-damage packets produced by cards whose card type is Spell, including delayed or persistent Spell-card effects.
- Physical and Fixed damage packets never receive SpellPower, even when their source card is a Spell.
- Magic normal attacks and Magic-damage effects originating from Units, Buildings, or Master Units do not receive SpellPower.
- If one Spell produces mixed damage types, only its Magic packets receive the bonus.
- Sum every living, active allied occupant's SpellPower with no maximum. Drained, Erasure, and Sealbound occupants provide none; Hiding and Flying do not suppress SpellPower.
- Resolve raw Magic Spell damage as `card-level-adjusted damage + total SpellPower`, then apply Magic Defense, Drained multiplication, Shielder, Endure, removal, and victory rules in their normal order.
- Area Magic Spell damage receives the full SpellPower bonus separately for every target.
- Multi-hit Magic Spell damage receives the full SpellPower bonus on every hit.
- A persistent or delayed Magic-damage Spell captures the caster's total SpellPower when the server accepts the play from hand. Later gains, losses, state changes, or removal of SpellPower sources do not alter that effect.
- Every persistent trigger and every target uses the captured value. StateView and reconnect snapshots preserve it as `CapturedSpellPower`.
- Every separately played Replicate copy calculates SpellPower again at its own accepted cast time.
- SpellPower applies when a Magic-damage Spell legally targets its caster's own occupant as well as an enemy occupant.
- Destroy, remove, set-HP, or other effects that do not calculate a Magic damage amount receive no SpellPower bonus.
- Robot Fusion does not transfer SpellPower from absorbed materials. Card-level upgrades do not increase an occupant's SpellPower unless a future card-specific rule explicitly says so.
- SpellPower is implemented. Existing cards remain unaffected while `spellPower` is omitted or `0`.

### Invincible

- `Invincible` (`무적`) may be granted to Units, Buildings, and Master Units.
- An Invincible occupant remains a legal target. Attacks, counterattacks, spells, persistent effects, and area effects still resolve their target and visual presentation, but damage does not reduce its HP.
- Invincible prevents Physical, Magic, and Fixed damage. This includes normal attacks, counterattacks, spell damage, persistent and area damage, and empty-deck draw-failure damage.
- Destroy, remove, sacrifice, fusion-material removal, set-HP, and other non-damage resolution are not prevented by Invincible.
- Invincible effects use one of these duration conditions: permanent (`Always`), the summon turn (`SummonTurn`), only the owner's turn (`OwnerTurnOnly`), only the opponent's turn (`OpponentTurnOnly`), until the current global turn ends (`UntilTurnEnd`), the owner's next `n` turns (`OwnerTurns(n)`), or the next `n` global turn endings (`GlobalTurnEnds(n)`).
- Korean card text for `OwnerTurns(n)` must say `내 n턴 동안 무적`. It is counted by the owner's turn endings, not by global turns. `OwnerTurns(2)` remains active until the owner has reached two turn endings after receiving the effect.
- `GlobalTurnEnds(n)` decreases at every accepted turn end regardless of which player owns the ending turn. The turn in which the effect is granted is included if that turn later ends normally.
- `SummonTurn` lasts from successful summon resolution through the end of that current global turn.
- Separate Invincible effects keep separate duration state. The occupant remains Invincible while at least one unsuppressed effect is active.
- Drained temporarily suppresses Invincible without deleting it. Erasure suppresses Invincible for the remainder of that occupant's time on the field. Invincible duration continues to advance normally.
- Sealbound immunity takes priority while sealed. Hiding and Flying first determine whether a target is legal; if the target is legal, Invincible then prevents its HP loss.
- An Invincible Shielder resolves incoming damage as `0`, so it produces no overflow damage to the protected occupant and completely blocks that eligible hit.
- Because actual HP loss is `0`, LifeSteal heals `0`, damage-based Berserker gains do not trigger, and Endure is not consumed.
- Every hit of a multi-hit attack still resolves and may play its hit presentation, but each hit deals `0` actual damage while Invincible is active.
- A legal attack or hit event still occurs. No positive HP-damage event is produced; the client should show `무적` feedback instead of a damage number.
- Robot Fusion does not transfer Invincible or its duration from absorbed materials. The survivor retains only its own Invincible effects. Normal card upgrades do not extend Invincible unless a future card-specific rule explicitly says so.
- StateView and reconnect snapshots preserve every active Invincible effect's duration condition, start context, and remaining owner-turn count.
- Invincible is implemented. Existing cards remain unaffected while `invincibleDuration` is omitted or `None`.

### Hero Growth

- `Hero` is a movable `1x1` Fantasy Unit with melee Fixed damage, base `3 ATK / 3 HP`, `0 / 0` Physical/Magic Defense, and a cost of `3 mana`.
- On summon, Hero gains `GlobalTurnEnds(3)` Invincible. The summon turn's ending changes the count `3 -> 2`; the next opponent turn ending changes it `2 -> 1`; the next turn ending changes it `1 -> 0` and removes Invincible.
- At every global turn end, each living Hero independently gains either permanent `Base ATK +13` or permanent `Max HP +13` with equal probability.
- An HP growth also heals `Current HP +13`, so the Hero keeps the same amount of existing damage rather than being fully healed.
- Hero growth has no stack limit. Multiple Heroes resolve independently in deterministic board order: Player board first, AI board second, then `(0,0), (0,1), (1,0), ... (4,1)` within each board.
- Drained, Erasure, and Sealbound suppress Hero growth. Suppression does not pause the separate Invincible countdown.
- Card-level upgrades are applied before battle. Hero gains ATK +1 at Lv.3, Lv.6, and Lv.9, ATK +3 at Lv.13, and HP +1 at every other level.
- The authoritative server chooses each growth result. StateView and reconnect snapshots preserve the resulting base ATK, maximum/current HP, and remaining Invincible count; reconnect never rerolls completed growth.
- Hero remains excluded from draft offers and random rewards until its card art is ready.

### Shielder Damage Transfer

- Shielder still redirects eligible single-target attacks and effects before damage is applied.
- First, resolve the incoming damage against the Shielder using the Shielder's matching defense and state.
- The Shielder absorbs up to the HP it had immediately before that hit.
- Only the resolved damage above that pre-hit HP is transferred to the protected occupant.
- The protected occupant then applies its own matching defense and state to the transferred damage.
- Area effects are not redirected; the Shielder and every other declared area target each resolve their own damage.

### Worked Examples

- `A` melee `5/10` attacks `B` melee `3/15`: `A` loses `3 HP`, `B` loses `5 HP`
- `A` melee `5/10` attacks `C` ranged `2/7`: `C` loses `5 HP`
- `C` ranged `2/7` attacks `A` melee `5/10`: `A` loses `2 HP`
- `C` ranged `2/7` attacks `D` ranged `4/9`: `D` loses `2 HP`

## Damage Resolution and Removal

- Apply all damage from a single attack or effect before checking for removal.
- After that single attack or effect finishes resolving, any unit or building with `0` or less HP is removed immediately.
- Removing a defeated occupant queues its eligible destruction effects. Resolve the queued effects, remove any newly defeated occupants, and queue their eligible destruction effects until no trigger remains.
- Check Master defeat only after that complete destruction-trigger chain finishes, so simultaneous Master defeat compares the true final HP values.
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

