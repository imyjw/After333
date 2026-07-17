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
3. Resolve power-upkeep payment
4. Resolve active Robot Factory effects
5. Resolve Firewall turn-start effects
6. Resolve the base draw
7. Main phase
8. Turn end

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
- Power payment happens after the turn-start resource gain step and before Robot Factory, Firewall, and the base draw.
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
- Its card effects and traits are suppressed, including Berserker, multi-hit, additional attacks, Endure, Guard, LifeSteal, Rush, Replicate-related occupant traits, resource production, Robot Factory, SpellPower, Hiding, Flying, and Invincible.
- Its science power upkeep is suppressed. Applying Erasure immediately clears Drained, and an Erasure occupant cannot become Drained.
- Applying Erasure removes every combat-time ATK, max-HP, Physical Defense, and Magic Defense modifier already on the occupant. Robot Fusion bonuses and the Daehwandan ATK bonus are included.
- The original summon baseline remains: printed stats plus the account upgrade level used at summon. Current HP is never healed when a max-HP bonus is removed and is clamped only when it exceeds the restored maximum.
- Buffs and debuffs applied after Erasure take effect normally. Reapplying Erasure removes the modifiers that exist at that later application time.
- Base attack type, damage type, movement permission, tile size, baseline ATK/HP, baseline defenses, and account upgrade stats remain.
- Robot classification is suppressed for battle searches, so an Erasure Robot cannot be counted or selected by Robot Fusion.
- Erasure does not set defenses to `0`, multiply incoming damage, or remove front-row blocking. Guard is suppressed and therefore cannot redirect, but an ordinary front-row Erasure occupant still blocks.
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
- Red Dragon deals `66 Physical` damage to every enemy tile occupant at the end of its owner's turn.
- Empty-deck draw-failure damage is `Fixed`.
- The development-only `333` Master damage command is `Fixed`.

### Gaebang Branch

- Gaebang Branch is a Common, non-attacking `1x1` Murim building with cost `2 Gold`, ATK `0`, HP `20`, defense `0/0`, and DamageType `None`.
- At the end of its owner's turn, it draws `2` cards if that owner's current Gold is exactly `0`.
- A Gaebang Branch summoned during the current turn can trigger at that same turn end.
- Every living Gaebang Branch whose effects are not suppressed resolves independently in fixed tile order `(0,0), (0,1), ... (4,1)`.
- A full hand removes each excess drawn card immediately. An empty deck causes the normal cumulative Fixed draw-failure damage for each attempted draw.
- Drained, Erasure, and Sealbound suppress the effect. The effect continues at later eligible turn ends until the building leaves the field.
- Gaebang Branch upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Gaebang Branch remains excluded from draft offers and random card rewards until its card art is ready.

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
- Firewall is an area effect, so Guard does not redirect it. Every occupant in the row resolves defense and drained multiplication independently.
- All damage from one Firewall instance is applied before defeated units/buildings are removed and Master defeat is checked.
- An empty-row trigger still consumes one of the two triggers.
- Multiple Firewall effects may coexist and resolve independently in creation order.
- The spell card goes to discard when cast; the separate persistent effect expires after its second trigger.

### Timed Bomb

- Timed Bomb is a Common Science Civilization scripted spell with cost `3 Power + 1 Gold`, base damage `33 Physical`, and no board target.
- It does not deal damage immediately. Casting it creates a separate persistent effect and sends the spell card to discard.
- Every global turn start decreases its countdown once, regardless of whose turn begins.
- If Player casts it and then ends the turn, the first countdown is the opponent's next turn start, the second is Player's following turn start, and it detonates at the opponent's next turn start.
- On detonation it deals its stored Physical damage to every current occupant on the opposing field, including units, buildings, and the Master Unit.
- Timed Bomb is an area effect, so Guard does not redirect it and Hiding does not avoid it. Sealbound occupants remain untargetable and take no damage.
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

### Sealbound

- `Sealbound` uses the card-data field `sealboundOwnerTurnStarts: n` on Unit and Building cards only.
- `0` means the card does not enter Sealbound. A value of `1` or greater makes the summoned occupant enter Sealbound immediately with that many owner-turn starts remaining.
- The summon turn does not reduce the counter. Each later turn start of the Sealbound occupant's owner reduces it by `1`.
- A Sealbound occupant continues to occupy its tile. It cannot move, exchange positions with another occupant, attack, or counterattack.
- A Sealbound occupant cannot activate effects or pay science power upkeep.
- A Sealbound occupant cannot be selected or affected by attacks, spells, area damage, healing, buffs, or debuffs, and it takes no damage while Sealbound.
- A Sealbound occupant is excluded from Guard, Robot Fusion material selection, Robot Factory searches, and equivalent occupant-search effects.
- A Sealbound occupant does not participate in front-row blocking.
- Sealbound countdown and release are the final operations of the owner's turn-start sequence, after resources, upkeep, Robot Factory, Firewall, and base draw have resolved.
- When the remaining counter reaches `0`, Sealbound ends. Because release occurs last, the released occupant does not contribute resources, pay upkeep, or activate turn-start effects during that same turn start.
- Unless it has Rush, an occupant released from Sealbound has summoning sickness for the release turn and cannot attack until its owner's following turn. Rush removes only this release-turn attack restriction.
- Sealbound is independent from Drained and Erasure. A Sealbound occupant is immune to Erasure: applying Erasure while it is Sealbound has no effect and does not remove attached effects. Drained and upkeep evaluation are suspended while Sealbound and resume during the next applicable upkeep step after release.
- Reconnect snapshots store `IsSealbound`, the remaining owner-turn-start count, and Rush so release-turn attack eligibility survives reconnection.
- Master Units cannot be Sealbound.
- Sealbound is implemented. Existing cards remain unaffected while `sealboundOwnerTurnStarts` is omitted or `0`.

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
- A hidden Guard does not redirect attacks or effects. Guard becomes active after Hiding is removed if no other state suppresses it.
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
- A Flying Guard cannot redirect a non-Flying melee normal attack. It may redirect ranged normal attacks, normal attacks from Flying attackers, and other Guard-eligible effects.
- A non-Flying Guard may protect a Flying occupant from any attack or effect that could legally target that Flying occupant.
- Flying is temporarily suppressed by Drained and remains suppressed for the duration of Erasure. While suppressed, a non-Flying melee attacker may target the occupant. Drained prevents front-row blocking through its own rule; an Erasure occupant whose Flying is suppressed participates in front-row blocking.
- Clearing Drained restores Flying. Erasure does not clear while the occupant remains on the field, so Flying does not reactivate during that occupancy.
- Active Hiding takes priority over Flying targeting permissions. After Hiding is revealed, the remaining Flying rules apply normally.
- Sealbound takes priority while sealed. Flying becomes active again when Sealbound ends, subject to any current Drained or Erasure suppression.
- Robot Fusion does not transfer Flying from absorbed materials. The survivor retains Flying only if it already had Flying.
- Flying does not end when the occupant attacks and lasts for that occupant's entire time on the field unless temporarily suppressed by a locked rule above.
- StateView and reconnect snapshots preserve `HasFlying` for every occupant, including Buildings and Master Units.
- Flying is implemented. Existing cards remain unaffected while `hasFlying` is omitted or `false`.

### SpellPower

- `SpellPower +n` uses the reserved future occupant value `spellPower: n`. Units, Buildings, and Master Units may provide SpellPower; `0` means none and negative values are invalid.
- SpellPower applies only to Magic-damage packets produced by cards whose card type is Spell, including delayed or persistent Spell-card effects.
- Physical and Fixed damage packets never receive SpellPower, even when their source card is a Spell.
- Magic normal attacks and Magic-damage effects originating from Units, Buildings, or Master Units do not receive SpellPower.
- If one Spell produces mixed damage types, only its Magic packets receive the bonus.
- Sum every living, active allied occupant's SpellPower with no maximum. Drained, Erasure, and Sealbound occupants provide none; Hiding and Flying do not suppress SpellPower.
- Resolve raw Magic Spell damage as `card-level-adjusted damage + total SpellPower`, then apply Magic Defense, Drained multiplication, Guard, Endure, removal, and victory rules in their normal order.
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
- Invincible effects use one of these duration conditions: permanent (`Always`), the summon turn (`SummonTurn`), only the owner's turn (`OwnerTurnOnly`), only the opponent's turn (`OpponentTurnOnly`), until the current global turn ends (`UntilTurnEnd`), or the owner's next `n` turns (`OwnerTurns(n)`).
- Korean card text for `OwnerTurns(n)` must say `내 n턴 동안 무적`. It is counted by the owner's turn endings, not by global turns. `OwnerTurns(2)` remains active until the owner has reached two turn endings after receiving the effect.
- `SummonTurn` lasts from successful summon resolution through the end of that current global turn.
- Separate Invincible effects keep separate duration state. The occupant remains Invincible while at least one unsuppressed effect is active.
- Drained temporarily suppresses Invincible without deleting it. Erasure suppresses Invincible for the remainder of that occupant's time on the field. Invincible duration continues to advance normally.
- Sealbound immunity takes priority while sealed. Hiding and Flying first determine whether a target is legal; if the target is legal, Invincible then prevents its HP loss.
- An Invincible Guard resolves incoming damage as `0`, so it produces no overflow damage to the protected occupant and completely blocks that eligible hit.
- Because actual HP loss is `0`, LifeSteal heals `0`, damage-based Berserker gains do not trigger, and Endure is not consumed.
- Every hit of a multi-hit attack still resolves and may play its hit presentation, but each hit deals `0` actual damage while Invincible is active.
- A legal attack or hit event still occurs. No positive HP-damage event is produced; the client should show `무적` feedback instead of a damage number.
- Robot Fusion does not transfer Invincible or its duration from absorbed materials. The survivor retains only its own Invincible effects. Normal card upgrades do not extend Invincible unless a future card-specific rule explicitly says so.
- StateView and reconnect snapshots preserve every active Invincible effect's duration condition, start context, and remaining owner-turn count.
- Invincible is implemented. Existing cards remain unaffected while `invincibleDuration` is omitted or `None`.

### Guard Damage Transfer

- Guard still redirects eligible single-target attacks and effects before damage is applied.
- First, resolve the incoming damage against the Guard using the Guard's matching defense and state.
- The Guard absorbs up to the HP it had immediately before that hit.
- Only the resolved damage above that pre-hit HP is transferred to the protected occupant.
- The protected occupant then applies its own matching defense and state to the transferred damage.
- Area effects are not redirected; the Guard and every other declared area target each resolve their own damage.

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

