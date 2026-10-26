# Battle Rules

2026-10-20 문서 대조: 현재 카드 기본값·비용은 [카드 목록](card_catalog.md)을 따르며 상단 기5·개방 분타 기2·마정석 꾸러미 골드6·파이어스피어30·파이어월15·뉴클리어 전력13을 반영했다. 시작 손패5장, 첫 턴 전 배정 골드3/4, 이후 기본 골드+3·드로우1 및 기존 판정 순서는 유지한다.

## Document Purpose

This document is the source of truth for battle setup, tile notation, actions, targeting, and combat resolution.

Current-source review: 2026-10-01. Confirmed implemented rules below are distinct from the explicitly listed Open Issues / TBD. Dated test/build records in linked documents describe those runs, not deployment status.

- High-level overview: [game_design.md](./game_design.md)
- Draft design: [draft_rules.md](./draft_rules.md)
- Meta progression design: [meta_rules.md](./meta_rules.md)

## Battle Scope

### Full Game Design

- Battles are repeated within a run until the run ends at 33 wins or 3 losses.

### Current Account-Backed Implementation Scope

- Human players use a persisted `33`-card run deck created through draft.
- Online PvE opponents use one of ten fixed 33-card decks, selected uniformly at each new battle and shuffled normally. Each contains Legendary 1, Unique 5, Rare 9, Uncommon 11, Common 7; only draft-enabled cards, at most 3 copies per card. See [pve_ai_decks.md](./pve_ai_decks.md).
- The same battle rules are resolved by the server for human and AI opponents. Normal matchmaking and the separate per-run Gold Dungeon use different progression/reward rules; see [battle_modes.md](./battle_modes.md).
- PvP clients send intended commands only; the server validates commands, mutates battle state, and publishes `StateView` and `BattleEvents`.
- Battle results are persisted into the owning account's current run.
- A run continues across battles until it reaches `33` wins or `3` losses.

## Client Disconnection and Reconnect

User-confirmed 2026-09-28: PvE uses the same `70`-second reconnect grace as PvP while the server remains running.

- When the server detects a disconnected participant in an unfinished battle, reserve that account's original seat for `70` seconds. The server owns the deadline and the remaining reconnect time.
- An authenticated reconnect within that deadline restores the same battle, participant, run, deck and battle state. Restarting the app must not start a new PvE battle or choose a new AI opponent in place of the interrupted battle.
- If the participant does not return before the deadline and the battle is still unfinished, the server records the disconnect forfeit once against that battle's mode: normal run result or Gold Dungeon loss. A Gold Dungeon loss does not alter run scores or refund the consumed attempt. A reconnect does not itself change run results, tickets or account resources.
- Existing turn deadlines and AI progression continue; disconnect/reconnect does not pause or reset the turn timer. If the battle ends before reconnection, its actual server result remains authoritative.
- Reconnect-status queries and duplicate old-connection cleanup do not renew the grace period. A successful reconnect cancels the old disconnect reservation; its delayed timeout callback must not forfeit the restored connection.
- The same account cannot start a second PvE battle or enter the other battle mode while it already has an active battle or an unexpired reconnect reservation.
- This grace applies to client disconnection only. A server interruption follows the cancellation policy below instead.

## Server Interruption Policy

User-confirmed 2026-09-13:

- If the server stops while a PvP or PvE battle is unfinished, cancel that battle without a win, loss, or draw result. Do not resume that battle from a saved snapshot after restart.
- Keep the existing run, deck, prior win/loss record, tickets, and other account resources unchanged. The same run/deck can start another battle; cancellation does not create a new run or refund its original entry tickets.
- For a Gold Dungeon canceled by the server, restore its consumed attempt against its original run. Do not grant extra attempts to a later run. Normal app disconnection does not authorize this refund. The 2026-10-09 per-run revision supersedes the former KST-date refund rule.
- A result already finalized and durably recorded before the interruption remains valid, including a pending result journal awaiting DB delivery. Deliver these results before canceling unfinished DB matches.
- Normal client disconnection while the server continues running still uses the existing reconnect/grace/forfeit behavior. A missing session alone never authorizes the client to invent a defeat.
- Server shutdown must stop new actions, AI actions, turn-timeout resolution, and pending disconnect forfeits for unfinished battles. Restart cleanup clears stale queue, connection, and seat reservations without writing participant results.

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
- Starting hand size: both players draw `5` cards.
- Starting mana: `0`
- Starting qi: `0`
- Starting power: `0`
- Starting gold: first player `3`, second player `4`, assigned during battle setup before mulligan.
- On battle turns `1` and `2`, neither player receives additional base gold on their first turn; opening gold is already available during mulligan.
- From battle turn `3` onward, each active player gains the normal base `gold +3`. Card-based resource gains still resolve on opening turns.
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
- Presentation (2026-10-02): `시작 핸드 카드` / `교환할 카드 선택`, green keep auras, large red X and `교체` labels for selected slots, and a `확인` button below. See [mulligan presentation](mulligan_presentation.md).
- When entering an unconfirmed mulligan, the five opening card images fly one by one from the bottom-right own deck and flip face up. Selection is enabled after about 1.00 second. This is presentation only: no extra draw/resource/event mutation, and the server's 33-second timer continues. Already-confirmed reconnects skip the entrance.
- The player selects replacement cards in that overlay, then presses the mulligan confirm button once.
- On confirmation, only selected card slots disappear while awaiting the authoritative replacement hand. New cards fly from the own deck into those same selected positions using the opening entrance animation; kept cards stay visible. Submission failure restores the selected cards. Repeated snapshots do not repeat the flight.
- A PvP player who confirms first completes their replacement immediately and sees `상대의 멀리건을 기다리는 중...` until the opponent confirms or times out.
- Once both players are confirmed, any replacement flight finishes first. The final mulligan result then remains on screen for `3` seconds and closes automatically. Already-confirmed reconnects skip historical replacement flights.

## Turn Structure

Each turn follows this sequence:

1. Resolve early scripted turn-start effects, including Timed Bomb countdown/detonation
2. Gain base resources and active occupant resources
3. Resolve Fallout damage and its destruction chains
4. Resolve Firewall and Biochemical Bomb turn-start effects together in cast order, including destruction chains
5. Resolve power-upkeep payment for surviving occupants, then active Robot Factory effects
6. Resolve the base draw
7. Resolve Sealbound countdown/release
8. Main phase (including optional occupant active effects)
9. Turn end

### Turn-end input lock (2026-09-28)

- A local player submitting EndTurn immediately locks further turn-end and gameplay input (cards, attacks, moves, active effects and fusion). Clicks during the lock are discarded, never buffered for a later turn. Surrender remains available.
- Unlock only on a later own Main phase. Online play requires the authoritative StateView with a greater TurnNumber and the local seat active; same-turn snapshots and the opponent's turn cannot unlock it.
- A synchronous enqueue failure or server rejection correlated to that exact EndTurn sequence releases the pending lock. Unrelated/uncorrelated errors do not. Reconnect uses a fresh authoritative snapshot and does not resend EndTurn automatically.
- New clients stamp commands with ExpectedTurnNumber. The server rejects mismatched turns before mutation (except surrender); a stale previous-turn command cannot consume the next turn. The optional field preserves older-client compatibility, so older clients without the stamp need an app update for this protection.
## Draw and Resource Gain

### Base Values

- Base draw per turn: `1` card
- Base gold gain per turn: `3`
- Base natural mana gain per turn: `0`
- Base natural qi gain per turn: `0`
- Base natural power gain per turn: `0`

### Base draw behavior (2026-10-08)

The base draw attempts one card on every own turn, including the opening turns. Opening hand/mulligan use five cards. A card drawn at the hand limit burns; an empty-deck attempt increments the failed-draw count and applies the existing escalating damage. Card effects that draw multiple cards still resolve each attempt independently and stop when the battle ends.

### Card-Based Gain

- Units or buildings may grant `mana +n`, `qi +n`, `power +n`, or `gold +n`.
- Those gains are included during the turn-start resource calculation and gain step.

### Drained Units During Turn Start

- A unit that is currently drained remains drained through that turn's draw/resource calculation and gain step.
- A drained unit does not contribute its effects during that step.
- After resources are gained, turn-start damage/destruction is resolved before power-upkeep payment.
- If the unit's power payment succeeds, the unit is active from that point onward.
- If the unit's power payment fails again, the unit remains drained.

### Resource Storage

- Mana, qi, power, and gold have no maximum value.
- Unspent mana, qi, power, and gold persist between turns.
- Gold can substitute for missing mana, qi, and power at a `1:1` rate for every resource payment,
  including card costs and ongoing upkeep.
- When a card cost includes both gold and another battle resource, pay the listed resource first if available and use gold to cover any remaining deficit.

## Power Consumption (전력 소모)

- `전력 소모:n` is an innate special effect for units or buildings: on each owner turn start, automatically pay n Power. Its numeric implementation remains `sciencePowerUpkeep`; a positive value grants this effect and zero means none. Existing Power-first, 1:1 Gold substitution and all-or-nothing payment are preserved (user confirmed 2026-10-02).
- Power payment happens after resource gain and all turn-start damage/destruction (including Fallout, Firewall, Biochemical Bomb and destruction chains), and before Robot Factory and the base draw. Dead occupants pay neither Power nor substitute Gold; only survivors are considered.
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
- Units and buildings use the same Drained result when Power Consumption cannot be paid. Successful payment on a later owner turn clears Drained; Erasure/Sealbound retain their existing suppression rules.
- Preview tooltip title/body: `전력소모: n` / `내 턴 시작 시 n의 전력을 지불해야한다. 지불하지 못하면 '방전' 디버프가 부여된다.` Whenever this tooltip appears, an explanatory `방전` side tooltip appears beside the card too, including hand/draft/deck previews and active occupants. This does not apply Drained or duplicate its compact attached-state indicator.

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
- Unit races persist through Erasure. An Erasure Robot remains eligible for Robot Fusion unless excluded by another rule such as Sealbound.
- Erasure does not set defenses to `0`, multiply incoming damage, or remove front-row blocking. Shielder is suppressed and therefore cannot redirect, but an ordinary front-row Erasure occupant still blocks.
- Sealbound is a separate state and is not removed by Erasure.
- Reconnect state stores `IsErasure` and the original combat-stat baseline. Legacy snapshots containing `IsDisabled` are migrated to `IsErasure` when restored.

### Stun and Freeze (2026-10-01)

- Stun (`스턴`) prevents normal attacks, counterattacks and active-effect activation. It does not prevent movement or front-row blocking.
- Freeze (`빙결`) prevents normal attacks, counterattacks, active-effect activation and movement, including swapping a frozen occupant. Frozen occupants do not block melee targeting of the back row.
- These are independent attached debuffs. They do not suppress passive card effects, traits, upkeep, resource production, SpellPower, auras, Shielder or Invincible. They do not change ATK/HP/defenses or incoming damage and do not remove existing Hiding.
- The applying card/effect specifies a positive number of global turn ends. At every player's accepted turn end, after turn-end occupant effects and before the next turn, remaining durations on both fields decrease by one. The application turn's end counts; zero expires the debuff. Existing durations also advance while ordinary Sealbound is active.
- Reapplying the same debuff uses `max(current remaining, new duration)`; durations do not add. Stun and Freeze can coexist and expire independently.
- Sealbound occupants cannot receive new Stun/Freeze. Applying Erasure removes these debuffs already attached; newly applied debuffs after Erasure still work normally. Demon King death/revival resets them. Movement and ownership transfer preserve them.
- Rejected attacks/actives/moves do not consume resources, attack allowance or active usage. These statuses do not restore already-spent actions on expiry.
- The remaining durations survive AI simulation, authoritative state views, JSON persistence and reconnect. Missing fields in older saves/views default to zero.
- Hold-preview attached tooltips show only `스턴` / `빙결`, without duration or extra descriptions. Combat history records entry/exit and state updates carry remaining durations.
- This addition defines the status system; no current card gains a new Stun/Freeze effect automatically. Future card mechanics choose targets and durations when authored.
- Presentation: Stun has orbiting stars above the visible head; Freeze has a translucent crystal and pauses the native body Animator/common clip sampler until expiry. Effects animate independently; death releases the pause. See [Stun/Freeze VFX](stun_freeze_status_vfx.md).

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
- RobotFusionResolved publicly reveals the opponent RobotFusion card once; OccupantAbsorbed does not repeat it.
- Already accepted card reveals finish before the final outcome banner covers the center and before the scene-return hold begins. This presentation wait does not lock battle actions. See [opponent_card_reveal.md](opponent_card_reveal.md).
- 트랩 설치는 상대에게 실제 카드 대신 금색 물음표 그림과 “트랩”만 공개한다. 자신의 설치는 중앙 재생이 필요 없다. 트랩 발동은 진짜 카드를 양쪽에 공개하며 같은 FIFO·3초/대기열2초·0.15초 페이드 규칙을 사용한다. 복원된 트랩 개수만으로 과거 공개 연출을 다시 만들지 않는다.

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
- 트랩을 무료로 설치한다 (발동 비용은 조건 충족 때 별도 지불)
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
- ManaCannon pays its explicit Mana 1 attack-action cost after every attack validation succeeds; Gold substitutes at 1:1. Failed commands do not consume this cost. See [ManaCannon attack payment](#mana-cannon-attack-payment).

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
- Drained, Sealbound and Frozen occupants cannot move. Both participants of a swap must be movable and free of movement-blocking statuses. Stun alone does not prohibit movement.

## Attack Availability

### Units

- Units normally attack once per turn.
- A unit with `0` ATK cannot attack.
- Drained, Sealbound, Stunned and Frozen occupants cannot attack or counterattack. Ending the turn locks gameplay commands until the next own turn; card inspection remains available.
- An active ManaCannon must be able to pay Mana 1, or Gold 1 when Mana is unavailable. Erasure suppresses this card-effect cost and does not bypass other attack restrictions.
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

### Attack Target Rejection Messages (A6, 2026-10-12)

Normal-attack availability and legal target rules are unchanged. An otherwise valid attack that fails target selection reports the actual target state: active Hiding → `은신한 유닛은 지정할 수 없습니다.`, Sealbound → `봉인된 대상은 지정할 수 없습니다.`, same-column front-row protection of a rear target against a non-flying melee attacker → `대상은 전열의 소환물에 의해 보호받고 있습니다.`, otherwise → `대상은 지정 불가 상태입니다.` Target-intrinsic Hiding/Sealbound takes precedence over front-row protection. Sealbound suppresses active Hiding, so a sealed Hiding card reports Sealbound. Empty target/attacker and unavailable-attacker errors keep their existing checks and messages. Server and tutorial/local combat share this reason selection.

### Melee Targeting

- Melee attacks obey front-row blocking.
- In a given enemy column, if the enemy front-row tile is occupied by a blocker, the enemy back-row tile in that same column cannot be directly targeted by a melee attack.
- If the enemy front-row tile in a column is empty, or occupied by a non-blocking occupant (including Drained or Frozen), the enemy back-row tile in that column may be targeted by a melee attack.

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
- Most current cards and the Master Unit start with `0 Physical Defense / 0 Magic Defense`; Golem starts with `3 / 0` and Demon King with `3 / 3`.
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

### Critical Hits

- Critical damage is twice the final mitigated damage packet, not twice the attack stat. Apply live SpellPower, matching defense, Drained, and Flying reduction (including integer rounding) first, then multiply by 2 once.
- Example: Magic ATK 10 against Magic Defense 5 deals 5 normally or 10 critically. A fully mitigated packet remains 0.
- Invincible and Sealbound still prevent damage. Endure applies after critical multiplication; LifeSteal and damage popups use actual HP lost, capped by remaining HP.
- For Shielder transfers, apply both the protected target's mitigation and the Shielder's mitigation before doubling once. Overflow uses that already doubled packet without another defense calculation or critical multiplier.
- Critical normal attacks also make their Piercing packets critical, independently applying each recipient's mitigation.
- Hiding currently grants guaranteed critical damage for every hit of the accepted normal attack that started hidden. Later additional attacks are normal once revealed. Counterattacks do not gain Hiding critical damage.
- The 50% critical example defines the damage calculation only; no existing card gains a random critical chance in this change.

### Repeated Hits

- Every active `hitsPerAttack: n` effect resolves as separate sequential hits in both a normal attack and an eligible counterattack, regardless of CardId.
- The matching defense and drained multiplier are applied separately to every hit.
- Damage popups and LifeSteal use each hit's actual HP loss after this calculation.

### Locked Effect Damage Types

- Firebolt deals `10 Magic` damage to its selected occupied tile. It may target an occupant on either player's field, including either Master Unit.
- An upgradeable damage spell gains `+1` base damage per card level, up to level `13`.
- Red Dragon deals `20 Magic` damage to every enemy tile occupant at the end of its owner's turn.
- Presentation (2026-10-07): reuse the exact former Firewall atlas on all ten enemy tiles, including empty tiles, for this actual turn-end activation. The ordered cue groups existing damage/hurt/death once and precedes the next turn's TimeBomb at equal raw offsets. Damage rules remain unchanged; see [Red Dragon presentation](red_dragon_presentation.md).
- Empty-deck draw-failure damage is `Fixed`.
- The development-only `333` Master damage command is `Fixed`.
- Endure prevents the first lethal HP-damage packet from any source and leaves its active bearer at `1 HP`. This includes normal attacks, counterattacks, Physical/Magic/Fixed spell damage, persistent or delayed effects, area damage, destruction-trigger damage, and empty-deck damage.
- Endure is consumed only when it prevents lethal damage. Later lethal damage defeats the occupant normally, and non-damage removal such as fusion-material consumption is not prevented.

### Gaebang Branch

- Gaebang Branch is a Common, non-attacking `1x1` Murim building with cost `2 Qi`, ATK `0`, HP `20`, defense `0/0`, and DamageType `None`.
- At the end of its owner's turn, it draws `2` cards if that owner's current Gold is exactly `0`.
- A Gaebang Branch summoned during the current turn can trigger at that same turn end.
- Every living Gaebang Branch whose effects are not suppressed resolves independently in fixed tile order `(0,0), (0,1), ... (4,1)`.
- A full hand removes each excess drawn card immediately. An empty deck causes the normal cumulative Fixed draw-failure damage for each attempted draw.
- Drained, Erasure, and Sealbound suppress the effect. The effect continues at later eligible turn ends until the building leaves the field.
- Gaebang Branch upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Gaebang Branch is enabled in draft offers and random card rewards.

### Merchant Caravan

- Merchant Caravan (`MerchantCaravan`, `상단`) is an Uncommon, non-attacking `1x1` Murim building with cost `5 Qi`, ATK `0`, HP `20`, defense `0/0`, and DamageType `None`.
- It starts triggering on its owner's next turn start after it is summoned; it never triggers immediately on summon.
- During the active-occupant resource-gain step, each living Merchant Caravan whose effects are not suppressed grants its owner `3 Gold`.
- Multiple active Merchant Caravans stack and each grants `3 Gold` independently.
- Drained, Erasure, and Sealbound suppress this effect. It resumes on a later owner turn start after suppression ends.
- Merchant Caravan upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Merchant Caravan is enabled in draft offers and random card rewards.

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
- Power Bank is not upgradeable. It is enabled in draft offers and excluded from random card rewards.

### Mana Stone

- Mana Stone is a Common Fantasy scripted spell with cost `2 Gold` and no board target.
- It can be cast only during its owner's main phase by dragging it upward from the hand.
- Casting first pays the full `2 Gold` cost and moves the card from hand to discard, then immediately grants that player `3 Mana`.
- It creates no persistent effect and cannot use Gold to pay its printed Gold cost through substitution.
- Mana Stone is not upgradeable. It is enabled in draft offers and excluded from random card rewards.

### Mana Stone Bundle

- Mana Stone Bundle is an Uncommon Fantasy scripted spell with cost `6 Gold` and no board target.
- It can be cast only during its owner's main phase by dragging it upward from the hand.
- Casting first pays the full `5 Gold` cost and moves the card from hand to discard, then immediately grants that player `9 Mana`.
- It creates no persistent effect and cannot use Gold to pay its printed Gold cost through substitution.
- Mana Stone Bundle is not upgradeable. It is enabled in draft offers and excluded from random card rewards.

### PlasmaShield

- PlasmaShield (`PlasmaShield` / 플라즈마 쉴드) is a Common ScienceCivilization instant scripted spell costing `1 Power`; existing Gold substitution applies. Draft enabled; not upgradeable and excluded from run rewards/card packs (2026-10-03).
- Select one living Unit (Master excluded) or Building on either field. Empty/dead/sealed/Master targets and hidden enemies are illegal. Hidden allies remain legal; applying this non-damage buff does not reveal Hiding.
- Grant `10` Physical Defense; stored card levels do not increase it. Repeat casts stack permanently without a turn timer or use count. This changes only Physical Defense, never HP, Magic Defense or damage. SpellPower, amplification and damage protection do not change the amount.
- Use the existing combat-stat buff source record. Erasure removes existing bonuses; bonuses applied after Erasure work normally. Drained retains the stored bonus but effective defense is0 while Drained; Gu transfer, AI clones and online snapshots preserve amount/source. Existing death/revival cleanup rules remain.
- Validate before spending and consume cost/selected card once; reject without changing resources, hand or target. The spell card goes to discard immediately and does not create a persistent spell-zone occupant.
- A successful local/AI/online cast plays a translucent blue hexagonal shield formation once on the actual selected target, then hides completely. Permanent defense does not require a permanent visual. Restoring a snapshot never replays the cast. No damage/hurt sound is generated. See [PlasmaShield presentation](plasma_shield_presentation.md).


### ManaShield

- ManaShield (`ManaShield` / 마나쉴드) is a Common Fantasy instant scripted spell costing `1 Mana`; insufficient Mana can be replaced by `1 Gold` under the existing substitution rule. Draft enabled; not upgradeable and excluded from run rewards/card packs (2026-10-03).
- Select one living Unit (Master excluded) or Building on either field. Empty/dead/sealed/Master targets and hidden enemies are illegal. Hidden allies remain legal; applying this non-damage buff does not reveal Hiding.
- Grant `10` Magic Defense; stored card levels do not increase it. Repeat casts stack permanently without a turn timer or use count. Only Magic Defense changes: HP, ATK, Physical Defense, movement and active effects remain unchanged. PlasmaShield Physical Defense and ManaShield Magic Defense stack independently.
- `EffectAmount` stores the grant, separately from `Damage: 0` / `DamageType: None`. SpellPower, amplification and damage protection do not change the bonus; this is not a damage packet.
- Use the existing combat-stat buff source record with `SourceCardId: ManaShield` and actual `MagicDefenseBonus`. Erasure removes existing bonuses; bonuses applied after Erasure work normally. Drained retains the stored bonus but effective defense is 0 while Drained; Gu transfer, AI clones and online snapshots preserve amount/source. Existing death/revival cleanup rules remain.
- Validate before spending and consume the cost and exact selected hand card once. A rejection changes neither resources, hand nor target. The spell goes directly to discard without creating a persistent spell-zone occupant.
- A successful local/AI/online cast plays a smooth, transparent blue mana sphere once on the actual selected target, then hides completely after about 0.817 seconds (eight frames at 12fps plus 0.15-second fade). The display has no hexagonal grid, preserves isotropic sphere geometry and source alpha, and uses Image peak alpha 0.28. Restoring a snapshot never replays the cast. No damage, hurt animation or sound is generated. See [ManaShield presentation](mana_shield_presentation.md).

### FireSpear

- FireSpear (`FireSpear` / 파이어스피어) is a Common Fantasy immediate scripted spell costing `3 Mana`, with base `30 Magic` damage. Draft/rewards are enabled; no persistent effect.
- A living allied or enemy Unit, Building or Master anchors its owner's own field column: front/rear two cells, following existing per-field column/Piercing coordinates. Empty tiles, Sealbound anchors and enemy Hiding anchors are invalid. Allied Hiding may be selected.
- Validate before paying once under existing Mana/Gold substitution and discarding the exact hand card once. Rejected casts preserve resources, hand and state.
- Both occupied column cells each receive one area Magic packet. Upgrade Lv.1–13 adds +1 per level (Lv.13:43). Read current caster SpellPower once before any direct damage, then apply matching Magic Defense and existing state protections to each target.
- Collateral Hiding does not avoid damage; Shielder redirection and Flying reduction do not apply to area spell packets. Sealbound, Invincible, Endure, Drained, destruction, revival and Master defeat retain their existing rules. Apply all direct packets before resolving destruction chains. Other columns and the other player's field take no direct FireSpear damage.
- Successful local/AI/online commands display a flame spear entering, passing through and exiting the selected owner's two-cell column. The current speed is 1.5×: 15fps, impact at 0.20 seconds and nominal total about 0.533 seconds. TileBackground Canvas anchors and screen-coordinate projection support the actual independent tile Canvases in both directions. Presentation never repeats authoritative damage, and restored snapshots do not replay a cast. See [FireSpear presentation](fire_spear_presentation.md).
### Meteor

- Meteor (`Meteor`) is a Unique Fantasy scripted spell costing `7 Mana` with base damage `40 Magic` and no board target. The user explicitly confirmed Fantasy affiliation and subsequently requested Unique rarity on 2026-09-30.
- Cast only during the caster's main phase. Pay the full cost under the existing Mana/Gold substitution rules, consume the exact selected hand card, and move it to discard once; create no persistent effect.
- On use, damage every current enemy Unit, Building, and Master once. Allies take no direct Meteor damage; destruction-effect collateral follows its own existing rules.
- Each upgrade level Lv.1-13 adds `+1` base damage: `40 + level`, or `53` at Lv.13. Cost does not change, and Buildings receive the same base Magic damage without a multiplier.
- Read the caster's current allied SpellPower once for this simultaneous hit and add the full bonus to every target before Magic Defense and normal protection/Drained rules. Lv.13 with SpellPower 20 supplies `73` incoming Magic damage per target; Magic Defense 4 reduces it to `69` before other protections.
- As area spell damage, Meteor ignores Shielder redirection, Hiding avoidance, and Flying damage reduction. Sealbound, Invincible, Endure, damage-triggered effects, revival, and destruction retain their existing rules.
- Apply every Meteor target packet before resolving defeated occupants, destruction chains, and Master defeat. UI animation does not delay or repeat authoritative damage.
- Meteor is upgradeable and included in draft offers and random rewards.
- Presentation: one giant meteor falls diagonally from above into the logical enemy field center in `0.65s`, then plays a `1.15s` explosion/tail with one incoming sound and one impact sound. Normal total is `1.80s`. Local/AI/online successful casts share the cue; snapshot restoration does not replay it. See [Meteor presentation](meteor_presentation.md).

### Earthquake

- Earthquake (`Earthquake`) is an Uncommon Neutral scripted spell with cost `3 Gold`, base damage `10 Physical`, and no board target.
- It can be cast during the caster's main phase. Pay the full Gold cost, consume the exact selected hand card, and move it to discard once; it creates no persistent effect.
- On use, damage every current enemy Unit, Building, and Master once. The caster's field takes no direct Earthquake damage; empty enemy tiles still show the presentation effect.
- Upgrade levels Lv.1 through Lv.13 each add `+1` to base damage. A Unit or Master receives incoming damage `10 + level`; a Building receives `3 * (10 + level)`. At Lv.13 these values are `23 Physical` and `69 Physical` before mitigation.
- Multiply Building damage before applying Physical Defense and the normal Drained damage multiplier. Example: Lv.13 against a Building with Physical Defense 4 deals `69 - 4 = 65` before other protections.
- Physical damage gains no SpellPower. This is area spell damage, so Shielder does not redirect it, Hiding does not avoid it, and Flying does not halve it. Sealbound, Invincible, Endure, and damage-triggered effects keep their existing rules.
- Apply all Earthquake target packets before removing defeated occupants, resolving destruction chains, and checking Master defeat. A destruction chain may still affect either field under its own rules.
- Earthquake is upgradeable and enabled in draft offers and random card rewards. Its cost and Building multiplier do not increase with level.
- Presentation (2026-09-30): grow and fade centered ground cracks on all ten enemy tiles, including empty tiles, with a modest screen tremor and one quiet rumble. Successful local/AI/online casts use the same result; restoring a snapshot does not replay the effect. See [Earthquake presentation](earthquake_presentation.md).

### Firewall

- Firewall is a persistent scripted spell with a base cost of `3 Mana` and base damage of `15 Magic`.
- The caster selects row `0` or row `1` on either player's field by targeting any tile in that row.
- An entirely empty row on either field is still a legal target.
- The selected row is fixed when the spell is cast. Units moving into or out of that row before a trigger are evaluated at trigger time.
- Firewall does not deal damage immediately when cast.
- It triggers at the next two global turn starts: normally once at the opponent's next turn start and once at the caster's following turn start.
- Each trigger occurs after base/occupant resource gains and Fallout, but before power-upkeep payment, Robot Factory, and the base draw. Resolve defeated occupants and destruction chains before charging survivors.
- Each trigger adds the caster's current SpellPower to its stored base Magic damage and deals it to every current occupant in the selected row, including allied or enemy units, buildings, and the Master Unit.
- If Firewall defeats its caster's Master, the opposing player wins.
- Firewall is an area effect, so Shielder does not redirect it. Every occupant in the row resolves defense and drained multiplication independently.
- All damage from one Firewall instance is applied before defeated units/buildings are removed and Master defeat is checked.
- An empty-row trigger still consumes one of the two triggers.
- Multiple Firewall effects may coexist and resolve independently in creation order.
- The spell card goes to discard when cast; the separate persistent effect expires after its second trigger.
- Presentation (2026-10-06): five independent upright flame walls stand on the five selected tiles, on either field including empty rows. Each sprite keeps its square aspect ratio and its own ground origin; do not rotate or stretch one strip across the row. Play once after a successful cast (no immediate damage) and once per actual next-two-global-turn-start trigger. Preserve gameplay damage/order; state restoration does not replay past cues. See [firewall presentation](firewall_presentation.md).

### Biochemical Bomb

- Biochemical Bomb is an Uncommon Science Civilization scripted spell with cost `4 Power + 1 Gold` and base damage `25 Fixed`.
- The caster must select one of two areas on the opponent's field: left `4x2` (`columns 0..3`) or right `4x2` (`columns 1..4`). It cannot target the caster's own field.
- The selected eight tiles are highlighted red while targeting. The selected start column is fixed when the spell is cast.
- It does not deal damage immediately. It triggers at each of the next four global turn starts, regardless of whose turn begins, then expires.
- A normal sequence after Player casts it is: opponent turn start `1`, Player turn start `2`, opponent turn start `3`, Player turn start `4` and expiration.
- Each trigger evaluates the current occupants in the fixed area, so an occupant summoned or moved into the area after casting is damaged by later triggers.
- Each trigger deals the stored Fixed damage to every current unit, building, or Master in the area. Empty tiles do not prevent a trigger from being consumed.
- Physical and Magic Defense are ignored. Invincible, Endure, and the Drained damage multiplier still resolve independently for every target. Sealbound occupants take no damage.
- Biochemical Bomb is an area effect, so Shielder does not redirect it and Hiding does not avoid it.
- All damage from one Biochemical Bomb instance is applied before defeated units/buildings are removed and Master defeat is checked.
- Firewall and Biochemical Bomb instances share one creation-order pass after Fallout and before power upkeep, Robot Factory, and the base draw, so mixed effects resolve in the order their cards were cast.
- Multiple Biochemical Bomb effects may coexist. Each keeps its own fixed area, damage, and remaining trigger count.
- Each upgrade level adds `+1` to every trigger's stored Fixed damage; for example, Lv.3 deals `28 Fixed`. Fixed damage does not receive SpellPower, whether present at cast time or gained later.
- The spell card goes to discard when cast. Biochemical Bomb is enabled in draft offers and random card rewards.

### Timed Bomb

- Timed Bomb is a Common Science Civilization scripted spell with cost `3 Power`, base damage `33 Physical`, and no board target.
- It does not deal damage immediately. Casting it creates a separate persistent effect and sends the spell card to discard.
- Every global turn start decreases its countdown once, regardless of whose turn begins.
- If Player casts it and then ends the turn, the first countdown is the opponent's next turn start, the second is Player's following turn start, and it detonates at the opponent's next turn start.
- On detonation it deals its stored Physical damage to every current occupant on the opposing field, including units, buildings, and the Master Unit.
- Timed Bomb is an area effect, so Shielder does not redirect it and Hiding does not avoid it. Sealbound occupants remain untargetable and take no damage.
- Physical Defense, Invincible, and the Drained damage multiplier resolve independently for every target.
- All targets take damage before defeated non-Master occupants are removed and Master defeat is checked.
- Each upgrade level adds `+1` to the stored Physical damage. Physical damage does not receive SpellPower.
- Multiple Timed Bomb effects coexist and count down independently in creation order.
- Timed Bomb is enabled in draft offers and random card rewards.
- Presentation only: each global turn start shows the centered bomb timer changing `00:00:03` to `00:00:02`, then `00:00:01`, then `00:00:00`; zero plays an explosion across all opponent tiles and one explosion sound. Reconnect state restoration does not replay earlier cues. See [Timed Bomb presentation](timed_bomb_presentation.md).

## Attack and Counterattack Resolution

### Core Rule

Only melee-versus-melee combat causes a counterattack.
An occupant with `0` ATK cannot counterattack.

- Counterattack eligibility and the initial counterattacker are unchanged: melee-versus-melee only, using the initial Shielder when the declared target is protected. The protected occupant does not also counterattack.
- Each declared attack causes at most one counterattack sequence. Its hit count is the counterattacker's own effective `hitsPerAttack`, not the attacker's hit count. A 3-hit attacker against a 2-hit counterattacker deals 3 attack hits and receives 2 counterattack hits, not 6.
- Capture each participant's effective hit count and per-hit ATK at the start of the attack action. Damage during that action does not recalculate its already captured Berserker ATK or add more hits.
- Resolve every counterattack hit as a separate damage packet with the counterattacker's own damage type. Apply the recipient's matching defense, Drained modifier, Invincible, and Endure to each packet independently.
- Erasure suppresses multi-hit, leaving one hit in an otherwise eligible counterattack. Drained and Sealbound prevent counterattack entirely. Ranged combat still causes no counterattack.

### Resolution Matrix

| Attacker | Defender | Defender takes damage | Attacker takes damage |
| --- | --- | --- | --- |
| Melee attacker | Melee attacker | Attacker's own hit count, each using its ATK | Eligible counterattacker's own hit count, each using its ATK |
| Melee attacker | Ranged attacker | Attacker's own hit count, each using its ATK | No |
| Ranged attacker | Melee attacker | Attacker's own hit count, each using its ATK | No |
| Ranged attacker | Ranged attacker | Attacker's own hit count, each using its ATK | No |

The matrix lists damage before mitigation; each hit uses the defenses and effects above. Shielder may change the initial counterattacker according to the existing redirection rule.

### Simultaneous Damage Rule

- Melee-versus-melee damage is simultaneous.
- Both participants deal their damage when the attack resolves.
- Both participants can be destroyed by the same combat.
- The attacker's final melee hit and the entire counterattack sequence form one simultaneous exchange. The initially eligible counterattacker completes its captured hit count even if either participant reaches `0` HP during the attack action; defeat removal follows the existing attack-resolution boundary.
- If both Master Units are defeated by the exchange, the higher final HP wins; equal final HP produces a draw.

### LifeSteal

- A LifeSteal occupant heals by the actual HP it removes with an attack or counterattack after defense, drained multiplication, and remaining-HP limits are applied.
- LifeSteal does not trigger while the occupant's effects are suppressed.
- In the final melee exchange, the final attack hit (including Piercing) and every counterattack hit are applied before LifeSteal. Sum the actual HP removed by that participant during this exchange; occupants at `0` or less HP cannot heal, and only surviving LifeSteal occupants recover that total. Earlier attack hits retain their existing per-hit LifeSteal timing.
- If a LifeSteal attacker or counterattacker is defeated by the same melee exchange, it does not heal from that exchange.

### Unit Races and Robot Fusion

- Unit cards have zero or more races, separate from special effects; Buildings, Spells and Master Units do not have the unit race field. See [confirmed assignments](unit_races.md).
- Races persist through Erasure, Drained and Sealbound. Sealbound still excludes an occupant from Robot Fusion targeting.
- Robot is a race, not a special effect, and has no standalone stat or combat effect.
- A Robot unit remains eligible while Drained or under Erasure.
- Robot Fusion is playable when its owner controls at least two living Robot units.
- This count is based on living Robot unit instances, not distinct CardIds. Two living copies of the same Robot card satisfy the condition.
- Casting Robot Fusion pays its listed cost, discards the spell, and enters an ordered friendly-Robot selection mode.
- At least two unique living Robot units on the caster's field must be selected before resolution.
- The selected Robot with the highest science power upkeep survives. If upkeep ties, the Robot selected earlier survives.
- Every other selected Robot contributes its current ATK and current HP to the survivor as permanent ATK and max/current HP increases.
- A damaged Robot contributes only its remaining current HP, so fusion never restores the donor's missing HP for free.
- Races, special effects, attacks per turn, damage type, science power upkeep, Drained state, and Erasure state are not transferred. The survivor retains its own values and states.
- Absorbed Robots leave the board as fusion materials and are not treated as defeated occupants.

### Robot Factory

- Robot Factory is a Rare, non-attacking `1x1` Science Civilization building with cost `1 Power`, ATK `0`, HP `50`, defense `0/0`, DamageType `None`, and `sciencePowerUpkeep 1`.
- Robot Factory is a Building and has no unit race field.
- It starts triggering on its owner's next turn start after it is summoned; it never triggers immediately on summon.
- After turn-start damage/destruction and power upkeep resolve, every living Robot Factory whose effects are not suppressed generates one card in fixed tile order `(0,0), (0,1), ... (4,1)`.
- The candidate pool contains only Unit definitions where `races` includes `Robot` and `includeInDraft == true`.
- Every eligible Robot definition has equal selection probability. Selection is server-authoritative.
- Multiple active factories each generate one independently selected card.
- The generated card is added directly to its owner's hand and does not come from the deck.
- If the hand is full, the generated card is removed immediately. This does not cause empty-deck damage.
- A Drained or Erasure factory cannot generate a card. If upkeep succeeds and clears Drained, it can trigger in that same turn's Robot Factory step. Erasure suppresses both its upkeep and generation effect.
- Generated cards use the owning account's current upgrade level when later played.
- The owner sees the generated card identity in combat history. The opponent sees only that Robot Factory generated one card, while the owner's public hand count increases normally.
- Robot Factory upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.

### Active Effects

- An active effect is an explicitly invoked occupant special effect, described as `액티브: 효과 내용`.
- Active effects are own-turn-only by default. Opponent-turn activation is an exception only when explicitly stated in the card description; Power Plant has no such exception. A future exception requires an explicit shared-service/command handler and tests, not only description text.
- During the owner Main phase, clicking an eligible friendly occupant shows a small Active button on its tile image. Pressing it sends an intent through the battle gateway. Selecting the tile does not spend resources.
- Dead, Erasure, Drained, Sealbound, Stunned and Frozen occupants cannot activate. Pending Robot Fusion, battle end and the opponent turn block activation.
- Insufficient resources do not hide or disable an otherwise eligible active button. Clicking it shows the cost failure (Power Plant: "골드가 부족합니다") without spending resources or consuming usage. The button remains available for retry after gaining resources. AI still requires affordability.
- The server/shared service validates the actor, source runtime identity, cost and per-occupant turn limit. Invalid/duplicate requests change neither resources nor usage state.
- Usage survives state projection, reconnect, AI simulation and server snapshots. It does not consume ordinary attack/movement actions.
- Individual active mechanics require an implemented handler; display text alone is not executable. Current supported active: PowerPlant. See [details](active_effects.md).

### Power Plant

- Power Plant is a Common, non-attacking `1x1` Science Civilization building with cost `1 Power`, ATK `0`, HP `30`, defense `0/0`, DamageType `None`, and no power upkeep.
- Text: `액티브: 1턴에 1번, 골드를 1 소모해서 전력을 2 얻는다.`
- Usable immediately on the summon turn, once per owner turn per Power Plant. Pay exactly 1 Gold and gain 2 Power together.
- No automatic turn-start conversion occurs. The player may keep Gold; other resources cannot substitute for the 1 Gold cost.
- Multiple plants have independent usage limits. A restored view does not refresh them; the next owner turn does.
- Power Plant upgrades grant HP `+1` at every level; cost, conversion and draft/reward eligibility remain unchanged.

### Nuclear Power Plant

- Nuclear Power Plant is a Rare, non-attacking `1x1` Science Civilization building with cost `5 Power`, ATK `0`, HP `30`, defense `0/0`, DamageType `None`, and no power upkeep.
- At its owner's turn-start active-occupant resource step, every living Nuclear Power Plant whose effects are not suppressed grants `3 Power`.
- When a Nuclear Power Plant is destroyed, it is removed first and then deals `30 Physical` damage to every current occupant on both fields, including Units, Buildings, and both Master Units.
- Physical Defense, Invincible, Sealbound immunity, and the Drained damage multiplier resolve independently for every target. Shielder does not redirect this area damage.
- Drained, Erasure, and Sealbound suppress both its turn-start resource effect and its destruction effect.
- If an explosion destroys another active Nuclear Power Plant, that plant is removed and creates another explosion. Each destroyed plant can trigger only once.
- All queued Nuclear Power Plant explosions resolve before Master defeat is checked. If both Masters are defeated, the higher final HP wins; equal final HP produces a draw.
- Nuclear Power Plant upgrades grant HP `+1` at every level and never grant the normal milestone ATK bonus.
- Nuclear Power Plant is enabled in draft offers and random card rewards.

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
- StateView and stored diagnostic snapshots serialize the Unit under its new owner and board while preserving its RuntimeId and occupant state.
- Gu is not redirected by Shielder, gains no SpellPower, and cannot be upgraded. It is enabled in draft offers and excluded from random rewards.

### HuanShu

- `HuanShu` (`환술`) is an Uncommon Murim non-damage Scripted Spell with cost `3 Qi` and `effectId: "huan_shu"`.
- It targets exactly one living enemy Unit. Master Units, Buildings, allied occupants, Sealbound occupants, and active enemy Hiding units are not legal declared targets.
- A successful cast applies HuanShu permanently while that occupant remains on the field. Recasting an already afflicted Unit does not stack or change the status.
- HuanShu has no turn countdown and does not expire at turn end.
- When the afflicted Unit declares an otherwise legal normal attack, the server replaces the target with one uniformly random legal HuanShu candidate before combat resolves.
- The original player-selected target must still be legal under ordinary attack rules. HuanShu does not make an illegal attack command legal.
- The random candidate pool contains every living Unit, Building, and Master Unit on both fields except the afflicted attacker itself.
- Ownership, front-row blocking, and the originally selected target do not restrict the random candidate pool. The original target remains one possible candidate.
- Sealbound occupants and active enemy Hiding occupants are excluded. A Hiding occupant allied with the afflicted attacker may be selected.
- HuanShu may redirect a non-Flying melee attacker to an active Flying occupant. That normal attack and its Piercing packets follow the same Flying damage reduction as an ordinary attack.
- Invincible occupants remain candidates and take `0` damage under the normal Invincible rule.
- The server selects once per declared attack. Every hit of a multi-hit attack uses that same resolved target. Each additional attack declaration performs a new random selection.
- Counterattacks are never redirected by HuanShu. After target replacement, normal damage, Shielder, counterattack, LifeSteal, Endure, Berserker, removal, and victory rules resolve without special exceptions.
- Drained prevents the afflicted Unit from attacking but does not remove HuanShu. Applying Erasure immediately removes HuanShu; a Unit already under Erasure may receive a newly cast HuanShu.
- Sealbound does not remove an existing HuanShu. A new HuanShu cast cannot target a Sealbound Unit.
- Gu preserves HuanShu when ownership changes.
- Robot Fusion does not transfer HuanShu from absorbed materials. A surviving afflicted Robot keeps its own HuanShu status.
- Server-authoritative random selection is included in synchronized battle events. StateView and stored diagnostic snapshots preserve the active status through compatibility fields; positive legacy remaining-turn values restore permanent HuanShu. Reconnect never rerolls a completed attack.
- Both players see only a small purple HuanShu status icon on the occupant. Long-pressing the occupant shows the permanent HuanShu explanation without a turn counter.
- HuanShu cannot be upgraded. It is enabled in draft offers and excluded from random rewards.

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
- Client presentation (2026-10-24): owned temporary hand instances show a small top-right hourglass, no text badge; their preview states `임시 복제: 사용하지 않으면 이번 턴 종료 시 손패에서 사라집니다.`. Confirmed turn-end removal dissolves the card edges into grey particles for0.65s. This cosmetic cue never changes rules, reveals opponent hand identities, or replays from reconnect snapshots; details: compact_battle_hand.md.
- A played copy resolves like the underlying card: units and buildings enter the field, while played spells enter discard after resolving normally.
- The opponent sees only the resulting hand-count change and never receives the temporary copy's hidden identity.
- Reconnect snapshots preserve each hand card's runtime identity and temporary-Replicate flag.

### Orc Chieftain

- User-confirmed 2026-09-30: `OrcChieftain` / 오크 족장 is a movable Rare Fantasy `1x1` Unit of Orc race costing `7 Mana`, with melee Physical damage, base `50 ATK / 70 HP`, Physical/Magic Defense `0 / 0`, and no innate traits.
- Each living Chieftain on the allied field grants `+10 ATK / +10 Max HP / +10 Current HP` to that field's current and future Orc Units, including itself. Contributions are per source and stack across multiple Chieftains; they are not a permanent on-summon bonus.
- When a source is destroyed or leaves the field, remove that source's `10 ATK / 10 Max HP / 10 Current HP` contribution. Current HP is reduced by the full `10`, not only clamped to the new maximum. Resulting HP at or below `0` can defeat recipients and remove another Chieftain's aura in the existing destruction chain.
- An emitter does not emit while EffectsSuppressed by Drained, Erasure or Sealbound. A Drained recipient may receive aura like ordinary existing combat buffs; a Sealbound recipient cannot receive a new contribution.
- Recipient Erasure keeps its existing combat-baseline reset, Max HP reset and Current HP clamp contract rather than imposing the ordinary full Current HP -10 aura-removal rule. Existing erased source RuntimeIds are remembered: the same source does not immediately regrant its erased contribution, a later distinct Chieftain may grant a new buff, and departure of an already-erased source never subtracts it twice.
- Gu transfers are atomic and retain the same source RuntimeId, including the existing self contribution. Reconcile old and new field recipients without creating a duplicate +10 grant or an artificial self-aura toggle.
- A Sealbound recipient cannot receive a new source contribution, but keeps an existing contribution while that source remains valid. Source destruction, departure or effect suppression immediately withdraws that existing ATK/Max HP/Current HP contribution even while the recipient is Sealbound. After release, reconcile currently valid, unapplied sources under the same deduplication and Erasure-tombstone rules. A Chieftain becoming Sealbound stops and withdraws its own source contribution everywhere, while retaining contributions already received from other valid Chieftains.
- Explicit upgrade schedule: ATK `+1` at Lv.3/6/9/13 and HP `+1` at all other levels through Lv.13. Lv.13 baseline is `54 ATK / 79 HP` before aura, or `64 ATK / 89 HP` with its one self aura. Aura amount, cost, defenses and race remain unchanged. Draft and random rewards are enabled.

### Orc Warrior

- User-confirmed base ATK revision 2026-10-01: `OrcWarrior` / 오크 전사 has base `20 ATK / 40 HP`. Its Common Fantasy Orc identity, `3 Mana` cost, melee Physical attack, defense `0 / 0`, movement and lack of innate traits remain unchanged.
- Its existing standard upgrade schedule remains ATK `+1` at Lv.3/6/9/13 and HP `+1` at all other levels through Lv.13, reaching `24 ATK / 49 HP` before combat effects. Draft and random rewards remain enabled.

### Orc Berserker

- User-confirmed 2026-09-30, base ATK revised 2026-10-01: `OrcBerserker` / 오크 광전사 is an Uncommon Fantasy `1x1` Unit of Orc race costing `5 Mana`, movable, with melee Physical damage, base `20 ATK / 50 HP` and Physical/Magic Defense `0 / 0`.
- No separate card effect. Its sole innate trait is Berserker: current ATK adds the current missing HP, `max(0, Max HP - Current HP)`, under the existing suppression, recovery and attack-action capture rules. It has one hit per normal attack or eligible counterattack and no Rush.
- Upgrade eligibility and every level were explicitly supplied: ATK `+1` at Lv.3/6/9/13, HP `+1` at all other levels through Lv.13. Lv.13 base stats are `24 ATK / 59 HP` before combat effects; cost, defenses, movement, race and Berserker remain unchanged.
- Draft and random rewards are enabled. Preserve the user-created `CardArtwork/OrcBerserker.png` and share the existing `Assets/Unit/Orc/OrcWarrior.controller`; no new animation assets or bespoke battle effect are added.

### Werewolf Pack Attack

- The card-specific Werewolf rule applies only to occupants whose CardId is exactly `Werewolf`.
- A living, active Werewolf gains `ATK +10` for each other living allied Werewolf currently on the same field.
- The receiving Werewolf gains no pack bonus while Drained, under Erasure, or Sealbound.
- When counting the other Werewolves, Drained and Erasure occupants still count because their CardId remains Werewolf. Sealbound and defeated Werewolves do not count.
- The bonus is recalculated whenever ATK is read. Summoning, defeat, removal, Sealbound changes, and ownership transfer therefore update every affected Werewolf immediately without storing a permanent ATK modifier.
- Card-level ATK upgrades remain part of the Werewolf's stored base combat stat. The pack bonus is added afterward and is not copied into reconnect snapshots as a permanent stat.
- StateView carries both stored `BaseAttack` and final display `Attack`, so the client rebuilds the field from the stored value and applies the live bonus exactly once.
- Werewolf also has Replicate and follows every normal Replicate rule above.

### Gaebang Chief Current-Gold Attack

- User-confirmed 2026-10-01: `GaebangChief` / 개방 방주 is a movable Unique Murim `1x1` Unit of Human race costing `7 Qi`, with melee Physical damage, base `10 ATK / 60 HP`, defense `0 / 0`, and no innate traits.
- Exact card effect text: "현재 소유한 골드 1당 공격력 +10. 골드가 변하면 즉시 반영되며 골드를 소모하지 않습니다."
- A living, active GaebangChief gains `ATK +10` for each battle Gold currently held by its current owner. This effect does not consume Gold; account Resource Gold is unrelated.
- The Gold bonus is recalculated whenever current ATK is read. Gold gains, spending and ownership changes immediately affect the bonus without adding a permanent stored modifier. If a card cost uses Gold to replace insufficient Qi/Mana/Power, pay that cost first and read the remaining Gold.
- The receiving GaebangChief gains no Gold bonus when defeated, Drained, under Erasure or Sealbound. These states do not remove its Human race.
- Gu preserves the occupant and stored baseline under the existing transfer rules, but the bonus immediately uses the new current owner's resources. Do not capture the old owner's Gold bonus into BaseAttack or transfer it as a permanent buff.
- Upgraded ATK is part of stored `BaseAttack`; the live Gold bonus is added afterward. StateView and reconnect distinguish stored `BaseAttack` from final display `Attack`, applying the bonus exactly once. Normal attacks and eligible counters use the same current-ATK rule and retain existing action-capture and damage semantics.
- Explicit upgrades are ATK `+1` at Lv.3/6/9/13 and HP `+1` at all other levels through Lv.13. Lv.13 printed baseline is `14 ATK / 69 HP`. With `3 Gold`, active Lv.0/Lv.13 ATK is `40/44`; the Gold multiplier, cost, defenses and race remain unchanged. Draft and random rewards are enabled.
- This is a card-specific effect with no `effectId` or added innate trait flag. There is one hit per normal attack or eligible counterattack and no Rush.

### Mana Cannon Attack Payment

- 2026-10-01 사용자 확정: `ManaCannon` / 마나 대포는 이동 가능한 판타지 언커먼 `1x1` Unit, 종족 없음(`races: []`), 소환 비용 `Mana 2`, 원거리 Magic 기본 `40 ATK / 10 HP`, 방어 `0 / 0`, 기본 일반 공격 1회·1타이며 고유 특수효과가 없다.
- 정확한 카드 문구: "공격할 때마다 마나 1 소모. 부족한 마나는 골드로 대신 지불할 수 있습니다."
- 카드 소환 비용과 별개로 적법한 일반 공격 행동마다 현재 소유자의 전투 Mana `1`을 한 번 지불한다. Mana를 우선 사용하고 부족하면 공통 `1:1` 대체 규칙으로 Gold `1`을 지불한다. 계정 Resource Gold는 이 비용과 무관하다.
- 공격 단계·턴·공격자·남은 공격 횟수·소환 턴·방전·봉인·선언 대상·환술 후 확정 대상 등의 검증이 실패하면 자원과 공격 횟수를 소비하지 않는다. 지불 가능 여부는 환술 무작위 선택 전에 검사하고 실제 차감은 모든 공격 검증을 마친 뒤 한 번 수행한다.
- 환술 대상 변경·쉴더 이전·피해 대상 수 때문에 같은 공격 비용을 다시 지불하지 않는다. 방어·보호·무적으로 피해가 `0`이더라도 유효한 공격 행동은 비용을 지불한다. 추가 일반 공격을 별도로 선언하면 비용을 새로 확인한다. 기본 원거리 유닛이므로 일반 반격하지 않는다.
- 예: Mana `1`/Gold `3`에서 공격하면 Mana `0`/Gold `3`. Mana `0`/Gold `1`이면 Mana `0`/Gold `0`. 둘 다 `0`인 활성 마나 대포의 공격은 거절되며 미차감이다.
- Erasure(망각)는 공격 비용을 카드 효과로서 억제한다. 망각 중에는 마나·골드 없이도 나머지 정상 조건을 만족하는 공격을 할 수 있다. 원거리·Magic·이동·강화 기준 수치·빈 종족 목록은 유지한다. Drained·Sealbound·소환 턴 제한은 기존 규칙을 따른다.
- Gu 이전 후 비용은 새 현재 소유자 자원에서 지불한다. 정상적인 원거리 마법 피해·현재 주문력·쉴더·피해 보호·사망·전투 종료 규칙은 그대로 사용한다. UI·AI·서버는 같은 공격 가능·비용 규칙을 사용하며 연출은 전투 자원을 변경하지 않는다.
- 강화 Lv.3/6/9/13 기본 ATK `+1`, 나머지 Lv.1–13 HP `+1`로 Lv.13 기준은 `44 ATK / 19 HP`다. 소환 Mana `2`·공격 Mana `1`·골드 대체율·방어·이동·종족은 강화로 변하지 않는다. 드래프트·실제 랜덤 보상에 포함하며 `effectId`나 새 고유 특수효과 플래그를 추가하지 않는다.

### Sealbound

- `Sealbound` uses the card-data field `sealboundOwnerTurnStarts: n` on Unit and Building cards only.
- `0` means the card does not enter Sealbound. A value of `1` or greater makes the summoned occupant enter Sealbound immediately with that many owner-turn starts remaining.
- The summon turn does not reduce the counter. Each later turn start of the Sealbound occupant's owner reduces it by `1`.
- A Sealbound occupant continues to occupy its tile. It cannot move, exchange positions with another occupant, attack, or counterattack.
- A Sealbound occupant cannot activate effects or pay science power upkeep.
- A Sealbound occupant cannot be selected or affected by attacks, spells, area damage, healing, buffs, or debuffs, and it takes no damage while Sealbound.
- A Sealbound occupant is excluded from Shielder, Robot Fusion material selection, Robot Factory searches, and equivalent occupant-search effects.
- A Sealbound occupant does not participate in front-row blocking.
- Sealbound countdown and release are the final operations of the owner's turn-start sequence, after resources, Fallout and Firewall/Biochemical Bomb damage/destruction, upkeep, Robot Factory, and base draw have resolved.
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
- A Hiding unit enters the field hidden and remains hidden without a turn limit until it declares a legal normal attack, loses HP to damage from any source, or enters Drained or Erasure.
- While hidden, the unit cannot be selected as the target of an opponent's normal attack or opponent's single-target spell.
- Area spells and area effects are not blocked by Hiding.
- A hidden unit does not participate in front-row blocking, so a melee attacker may target an otherwise legal back-row occupant in the same column.
- Movement, turn changes, and activating a non-attack effect do not reveal the unit.
- After the server accepts a legal normal-attack command, capture whether the attacker was hidden: all hits and Piercing packets of this attack are critical. Reveal Hiding before resolving combat. A rejected or illegal attack neither reveals Hiding nor consumes its critical opportunity.
- A revealed melee attacker receives a legal melee counterattack normally. Any actual HP loss from damage reveals Hiding, including normal attacks, counterattacks, Piercing, spells, and effects such as Firewall and Fallout. Damage fully prevented by defense, Flying rounding, Invincible, or Endure leaves Hiding intact if no HP is lost. This does not grant a critical counterattack. A protected target receiving no overflow keeps Hiding.
- All hits of that first multi-hit attack retain the captured critical status even though Hiding has already ended. Later additional attacks do not regain the bonus.
- A hidden Shielder does not redirect attacks or effects. Shielder becomes active after Hiding is removed if no other state suppresses it.
- `HasHiding` and `HidingRevealed` are separate values. Entering Drained or Erasure permanently sets `HidingRevealed`; clearing Drained never restores Hiding, and Erasure itself remains for the occupant's time on the field.
- If a Unit has both Sealbound and Hiding, Sealbound rules take priority while sealed. It becomes hidden when released unless Hiding was already permanently revealed by a separate legal rule.
- StateView and reconnect snapshots preserve both the immutable `HasHiding` trait and `HidingRevealed` so the current state survives reconnection within the running server.
- Hiding is implemented. Existing cards remain unaffected while `hasHiding` is omitted or `false`.

### Flying

- `Flying` uses the occupant flag `hasFlying: true`. Units, Buildings, and Master Units may have Flying.
- A Flying occupant may be selected by a non-Flying melee normal attacker, subject to the usual Hiding, Sealbound, and front-row rules. Each direct or Piercing hit from that attacker deals half damage after matching defense and damage modifiers, rounded down.
- This reduction applies to Physical, Magic, and Fixed normal-attack damage. Fixed damage still ignores armor before Flying halves it. Multi-hit attacks apply armor and rounding independently to every hit; damage 1 can become 0.
- Ranged normal attacks may target Flying occupants and do not receive Flying damage reduction.
- An active Flying attacker may attack another Flying occupant with full normal damage even when its attack type is Melee.
- A Flying melee attacker ignores enemy front-row blocking and may target an otherwise legal back-row occupant. If the defender is melee and can counterattack, it counterattacks the Flying attacker, applying Flying reduction when that defender has no active Flying.
- Counterattack damage from a non-Flying melee occupant is also halved against a Flying recipient after matching defense and modifiers, rounded down independently for each counter hit. A Flying counterattacker deals full damage. Spells and other effect damage are unchanged. Endure, Invincible, and LifeSteal use the resulting damage/actual HP loss normally.
- A Flying occupant does not participate in front-row blocking.
- Flying does not protect against single-target spells, area spells, healing, buffs, debuffs, or other non-normal-attack effects.
- Flying does not change movement range, movement count, destination legality, or position-exchange rules.
- A Flying Shielder may redirect non-Flying melee normal attacks as well as other eligible hits. For normal attacks and Piercing, calculate the protected target's incoming damage first (matching defense, state, then Flying reduction), then apply the Shielder's own defense/state to that packet. The Shielder's own Flying flag never halves redirected damage.
- Overflow from this already-mitigated normal-attack packet does not apply the protected target's defense, Drained multiplier, or Flying reduction again. HP-loss prevention and Endure remain effective.
- Example: ground melee ATK 10 against a Flying protected target with defense 2 gives (10-2)/2 = 4. A Shielder with defense 1 receives 3 damage. If its pre-hit HP was 2, the protected target receives the remaining 1 without another reduction.
- A non-Flying Shielder may protect a Flying occupant from any attack or effect that could legally target that Flying occupant.
- Flying is temporarily suppressed by Drained and remains suppressed for the duration of Erasure. Suppressed Flying provides no damage reduction or blocker bypass. Drained prevents front-row blocking through its own rule; an Erasure occupant whose Flying is suppressed participates in front-row blocking.
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
- The opposite-row packet uses normal-attack Shielder redirection but never causes another counterattack or recursively triggers Piercing. Determine damage against that opposite-row target independently: matching defense, Drained modifier, Invincible, and then Flying reduction for a ground melee source. If redirected, apply the Shielder's own defense/state next. Overflow is already mitigated for the original recipient, so do not apply that recipient's defense, Drained modifier, or Flying reduction again; Endure still applies to HP loss.
- Resolve Shielder eligibility again after the direct hit. A Shielder killed by the direct hit cannot protect against the following Piercing packet. For example, with no defense and sufficient Shielder HP, an ATK `10` attack against either row deals `20` total damage to the front Shielder and `0` to the protected rear occupant.
- Hiding does not prevent the opposite-row packet because Piercing does not select that occupant as a new attack target. Flying halves that packet from a non-Flying melee source, exactly as it halves a direct hit. Sealbound occupants take `0` Piercing damage under the normal Sealbound rule.
- Multi-hit attacks apply one Piercing packet after every hit. Additional attacks evaluate Piercing independently for each declared attack.
- LifeSteal includes actual HP lost to the Piercing packet by its recipients, including a redirecting Shielder and any overflow recipient, after defense and prevention. On the final melee hit, direct and opposite-row damage resolve before the counterattack, then a surviving attacker heals from the combined actual HP loss.
- When HuanShu redirects an attack, Piercing uses the actual random target's row and column rather than the originally selected target.
- Drained and Sealbound prevent an occupant from attacking through their normal rules. Erasure suppresses Piercing even if the immutable `HasPiercing` trait remains stored.
- StateView and stored diagnostic snapshots preserve `HasPiercing` for Units, Buildings, and Master Units.
- Piercing is implemented. Existing cards remain unaffected while `hasPiercing` is omitted or `false`.

### SpellPower

- `SpellPower +n` uses the occupant value `spellPower: n`. Units, Buildings, and Master Units may provide SpellPower; `0` means none and negative values are invalid.
- SpellPower applies to all Magic damage: normal attacks, counterattacks, Unit/Building/Master effects, and immediate, delayed, or persistent Spell effects.
- Physical and Fixed damage packets never receive SpellPower, even when their source card is a Spell.
- Use the damage source owner's current allied SpellPower immediately before resolving that damage, including damage to allies.
- If one Spell produces mixed damage types, only its Magic packets receive the bonus.
- Sum every living, active allied occupant's SpellPower and the owner's unexpired Magic Amplification Circle bonuses with no maximum. Drained, Erasure, and Sealbound occupants provide none; Hiding and Flying do not suppress SpellPower.
- Resolve raw Magic damage as `base damage after applicable upgrades/buffs + current total SpellPower`, then apply defense and existing damage rules. Add the bonus once to the original packet; Shielder redirection and overflow do not add it again. This is one increased damage packet, not a separate extra hit.
- Area Magic damage receives the full current bonus for every target. One simultaneous area effect reads the total before any target loses HP; later effects recalculate after the earlier effect finishes.
- Multi-hit Magic damage receives the full bonus on every hit. A normal hit and its Piercing target share that hit's current bonus. Existing simultaneous final melee hit/counterattack resolution reads each side's total before applying that exchange; it does not make counterattacks sequential.
- Persistent or delayed Magic damage does not capture SpellPower at cast time. Every trigger uses the effect owner's current allied total, including sources summoned, killed, suppressed, or reactivated after casting.
- Reconnect preserves occupants and their current states. `CapturedSpellPower` is retained only as a legacy wire/snapshot field and is ignored by damage resolution; new effects leave it at zero.
- Every separately played Replicate copy also uses the owner's current SpellPower whenever its Magic damage resolves.
- Tooltip: `마법 데미지를 입힐 때, 주문력만큼의 추가 마법 데미지를 준다.`
- Destroy, remove, set-HP, or other effects that do not calculate a Magic damage amount receive no SpellPower bonus.
- Robot Fusion does not transfer SpellPower from absorbed materials. Card-level upgrades do not increase an occupant's SpellPower unless a future card-specific rule explicitly says so.
- SpellPower is implemented. Omitting `spellPower` or setting it to `0` means that occupant provides no bonus; its Magic damage can still benefit from other active allied sources.

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
- Sealbound immunity takes priority while sealed. Hiding and front-row blocking determine target legality; Flying changes blocking and normal-attack damage, not target immunity. Invincible prevents HP loss from any otherwise legal damage.
- An Invincible Shielder resolves incoming damage as `0`, so it produces no overflow damage to the protected occupant and completely blocks that eligible hit.
- Because actual HP loss is `0`, LifeSteal heals `0`, damage-based Berserker gains do not trigger, and Endure is not consumed.
- Every hit of a multi-hit attack still resolves and may play its hit presentation, but each hit deals `0` actual damage while Invincible is active.
- A legal attack or hit event still occurs. No positive HP-damage event is produced; the client should show `무적` feedback instead of a damage number.
- Robot Fusion does not transfer Invincible or its duration from absorbed materials. The survivor retains only its own Invincible effects. Normal card upgrades do not extend Invincible unless a future card-specific rule explicitly says so.
- StateView and reconnect snapshots preserve every active Invincible effect's duration condition, start context, and remaining owner-turn count.
- Invincible is implemented. Existing cards remain unaffected while `invincibleDuration` is omitted or `None`.

### Hero Attack Kill Bonus

- Hero is a Human, Unique 1x1 Fantasy Unit: melee Fixed damage, base 33 ATK / 33 HP, defense 0/0, cost 3 mana, movable, with Rush.
- No innate Invincible and no turn-end growth. Special-effect text is 속공; card effect text is 상대방 소환물을 공격해 파괴할 때마다 공격력 또는 체력+33.
- After its own normal attack and simultaneous counterattack resolve, a living, unsuppressed Hero gains either permanent Base ATK +33 or Max/Current HP +33 with equal 50% probability per enemy non-Master occupant killed by that attack. Units and buildings both qualify. Each destroyed occupant counts once even for multiple hits; attack damage redirected to a Shielder and Piercing damage qualify.
- Counterattack kills, friendly kills under HuanShu, Master kills, spells, and collateral deaths from destruction effects do not qualify. A Hero killed in simultaneous combat cannot revive itself with this bonus.
- HP bonuses preserve existing damage by raising maximum and current HP equally. Bonuses have no stack cap; Erasure follows existing stat-reset rules and suppresses further bonuses.
- The authoritative server chooses each result. Reconnect restores resulting stats without rerolling. Combat logs record bonuses on attack resolution.
- Existing upgrade schedule remains: ATK +1 at Lv.3/6/9, ATK +3 at Lv.13, HP +1 at other levels. Lv.13 before combat bonuses is ATK 39 / HP 42.
- Draft and random rewards remain disabled.

### Shielder Damage Transfer

- Shielder still redirects eligible single-target attacks and effects before damage is applied.
- The separate opposite-row packet from Piercing is also eligible for normal-attack Shielder redirection; it is not treated as an unredirectable area effect.
- For normal attacks and Piercing, first resolve incoming damage against the protected target using its matching defense/state and any Flying reduction. Then resolve that packet against the Shielder using its own matching defense/state, ignoring the Shielder's Flying flag.
- The Shielder absorbs up to the HP it had immediately before that hit.
- Only the resolved damage above that pre-hit HP is transferred to the protected occupant.
- For normal attacks and Piercing, the protected occupant does not repeat defense, Drained multiplication, or Flying reduction on overflow; these already affected the packet before redirection. Invincible, Sealbound immunity, and Endure still govern actual HP loss.
- Single-target damage spells keep their existing transfer order: Shielder defense/state first, then the protected target's defense/state on any overflow. Flying does not modify spell damage.
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
- 설치형 트랩은 아래 별도 규칙을 따른다. 설치 시에는 비밀 영역에 보관하고 성공적으로 발동한 뒤 버린다.

### 트랩 (Trap)

- 트랩은 마법 카드의 별도 설치 유형이다. 자신의 Main 행동 턴에 손패의 트랩을 무료로 설치하며, 발동 비용은 설치 비용과 별도로 저장한다. 설치 당시 발동 조건·효과·발동 비용·강화가 반영된 효과 수치·레벨·설치 순서를 보존한다.
- 설치에는 살아 있는 자신의 마스터가 필요하다. UI에서는 대환단·보조배터리처럼 손패 카드를 위로 드래그한 뒤 허공에 놓아 설치한다. 마스터나 타일을 지정하지 않으며, `InstallTrapCommand`는 효과 대상 좌표를 받지 않는다. 설치 확정 시 놓은 위치에서 공통 푸른 조각 연출이 재생된다. 상대에게 트랩 정체를 공개하지 않는다.
- 발동 비용이 부족해도 설치할 수 있다. 같은 소유자는 동일 CardId를 동시에 하나만 설치할 수 있으며, 중복 설치는 손패·자원을 소비하지 않고 거절한다. 다른 CardId의 트랩은 함께 설치할 수 있다.
- 설치된 트랩은 필드 타일을 차지하지 않고 손패에서 별도 비밀 영역으로 이동한다. 설치 직후 버리지 않으며, 성공적으로 한 번 발동하면 소비되어 버림 더미로 간다. 기본 턴 만료는 없다.
- 하나의 조건이 발생하면 설치 순서대로 검사한다. 조건이 맞지 않거나 발동 비용을 낼 수 없는 트랩은 건너뛰고 그대로 유지한다. 처음으로 조건·비용을 모두 충족한 트랩 하나만 비용을 지불하고 발동·소모한다.
- 소유자는 자신의 트랩 상세를 알지만 상대는 개수만 받는다. 발동 전 CardId·런타임 ID·레벨·포착한 발동 비용·조건·효과를 상대 StateView/이벤트/AI에 보내지 않는다.
- 트랩이 없는 마스터에는 표시하지 않는다. 설치된 트랩이 있으면 양쪽 마스터의 공개 표시는 금색 `?`다. 1개면 `?`만, 2개 이상이면 옆에 `Xn`을 표시한다. 상대 설치는 중앙에 일반 숨김 카드 그림과 “트랩”을 표시하고, 실제 발동은 양쪽에 진짜 카드 그림을 공개한다. 표시는 입력과 판정을 막지 않으며 재접속은 개수·소유자 상세만 복원하고 과거 설치/발동 연출을 재생하지 않는다.
- 미래 트랩은 `TrapSpellCardDefinition`의 타입화된 조건·효과·발동 비용으로 추가한다. 새 카드의 구체 조건·비용·효과·강화/등장 정책은 사용자 확정이 필요하며 문구만으로 새 판정을 만들지 않는다.

<a id="trap-return"></a>
### 트랩: 리턴 (TrapReturn)

- 중립 Rare 마법, 설치 비용 0, 발동 비용 **전투 골드 4**, 강화 불가. 드래프트와 랜덤 보상에는 포함하며, 보상은 강화 불가 제외 정책의 사용자 지정 예외다.
- 문구: `트랩: 골드-4. 상대방 유닛이 내 마스터 공격 시, 그 유닛을 패로 되돌린다.(마스터 제외)`
- 적법하게 승인된 상대 일반 유닛의 일반 공격에서 실제 대상이 설치자 자신의 마스터이면 검사한다. 마스터·건물 공격자는 제외한다. 환술은 무작위 최종 대상을 먼저 정하므로 최종 대상이 이 마스터일 때만 해당한다.
- 일반 공격의 합법성·대상·공격 비용을 먼저 검증하고, 마나 대포 등 명시된 공격 비용도 먼저 지불한다. 실패한 공격은 트랩을 소모하거나 발동 비용을 내지 않는다. 승인된 공격은 쉴더 이전·HP 피해·연타·관통·반격 전에 리턴을 판정한다.
- 골드 4를 낼 수 있으면 이를 지불하고 트랩을 한 번 소모한 뒤 공격자를 전장에서 제거해 **공격자의 현재 소유자** 손패에 같은 CardId의 새 일반 카드로 넣는다. 새 손패 런타임 ID를 사용하고, 전장 현재 HP·버프·상태·임시 복제 할인은 가져오지 않는다. 다음 소환의 기본값과 강화는 현재 카드/소유자의 규칙으로 계산한다.
- 손패가 가득 차면 반환 카드를 추가하지 않고 소실시킨다. 드로우 실패 피해나 반환 카드의 버림 더미 추가는 없으며 트랩 비용·소모·전장 제거·공격 취소는 이루어진다. 골드가 부족하면 해당 트랩은 그대로 남겨 다음 트랩을 검사하고, 다른 트랩도 발동하지 않으면 원래 공격은 정상 진행한다.
- 발동하면 승인된 공격 행동 전체를 취소한다. 마스터/쉴더에 피해를 주지 않고 연타·관통·반격·흡혈 및 공격 타격 연출도 발생하지 않는다. 반환된 유닛 자체는 사망/파괴가 아니므로 처치 보상·사망 효과·마왕 부활·죽음 모션을 발생시키지 않는다. 해당 유닛의 퇴장에 따른 기존 오라 정리 등은 보존한다.
- JSON `damage: 0`, `damageType: "Magic"`은 카드 메타데이터다. 실제 피해는 없으며 주문력·마법 증폭·방어력·불굴·무적 계산으로 리턴을 피해 효과로 바꾸지 않는다.

### TrapMirrorField

- 트랩-미러 필드(`TrapMirrorField`)는 판타지 Rare 마법이다. 설치 무료, 발동 마나 2(부족한 마나는 전투 골드로 1:1 대체), 강화 불가, 덱 빌딩 포함·랜덤 보상 제외다.
- 문구: `트랩: 마나-2. 상대방 유닛이 내 유닛을 공격 시, 공격을 취소하고 그 유닛에게 해당 유닛의 공격력만큼 마법 데미지를 입힌다.`
- 공격자와 보호 대상의 유닛에는 마스터도 포함한다. 공격자 또는 최종 대상이 건물이거나 같은 소유자면 발동하지 않는다. 적법성·공격 비용 검증과 환술의 최종 대상 결정 뒤, 쉴더 이전·연타·관통·반격 전에 공통 설치 순서로 판정한다.
- 조건·발동 비용을 충족하면 해당 트랩 한 장을 소비하고 승인된 공격 전체를 취소한다. 공격 횟수는 1회 소진하며 마나 대포 등 이미 승인된 공격 비용은 돌려주지 않는다. 설치된 리턴 등 다른 트랩과 함께 있으면 공통 규칙에 따라 처음 발동 가능한 한 장만 발동한다.
- 발동 직전 공격자의 현재 ATK를 한 번 읽어 기본 반사 피해로 사용한다. 트랩 소유자의 발동 시점 주문력을 더하고 Magic 피해를 공격자 본인에게 직접 입힌다. 반사 대상이 명시된 공격자이므로 이 피해는 쉴더에게 이전하지 않는다. 일반 단일 대상 마법의 쉴더 이전 규칙은 그대로다.
- 공격자의 원래 일반공격 피해 속성·주문력·은신 치명타·n연타·관통·비행 반감·흡혈을 반사 피해에 추가하지 않는다. 마법 방어·봉인·무적·불굴·실제 HP 감소 시 은신 해제·정상 사망 효과·마왕 부활 및 마스터 패배는 공통 효과 피해 규칙을 따른다. 피해가 0이어도 공격 취소와 트랩 비용·소모는 유효하다.
- 합산 마나·골드로도 비용을 낼 수 없으면 트랩을 유지하고 다음 설치 트랩을 검사한다. 발동 전 상대에게는 일반 트랩 이미지와 개수만 공개하고, 발동 시 실제 미러 필드 이미지를 중앙에 공개한다. 로컬·AI·온라인은 취소된 일반공격 모션 없이 반사 피해의 공통 피격·수치·사망 표시를 재생한다. 재접속 복원은 발동을 재연하지 않는다.

### Persistent Spells

- Some spells create persistent effects.
- A persistent spell's effect remains in a separate persistent zone outside the `5x2` field.
- Persistent spells do not occupy field tiles.
- Persistent spells are not normal attack targets.
- The spell card itself goes to the discard pile when it is cast.
- The persistent effect does not have a default duration.
- Its end condition must be defined by the card text itself.

## Confirmed Field and Player Effects

### Nuclear and Fallout

Updated 2026-10-08: Nuclear is a Legendary Science spell costing 13 Power. It immediately deals one Fixed packet of 100 + account upgrade level to every current occupant on the opponent board, including the Master. It does not destroy the entire field directly. All area packets resolve before defeated-occupant triggers/chains and Master outcome checks; standard Invincible, Endure, Sealbound and Drained rules apply, Shielder/Hiding do not avoid area damage, and defense/SpellPower do not apply. Nuclear is upgradeable, +1 immediate damage at every level through Lv.13 (113); draft/rewards are enabled following the user-provided card artwork registration. Each cast also permanently adds one Fallout stack only to Units already on the opponent board at cast time, including the Master. Later summons receive no inherited stacks; each later cast adds only one new stack to its own current eligible recipients. Buildings receive neither new Fallout stacks nor Fallout turn-start damage; the immediate hit still includes them. Fallout deals one Fixed packet of 10 times stacks at every global turn start, after resource gains and before area damage, upkeep, factory generation, and draw; Nuclear upgrades do not increase this tick. Erasure and ownership transfer do not remove acquired Fallout. Only acquired occupant stacks survive transfer/reconnect. The legacy board counter is retained for snapshot compatibility but has no ongoing effect. Sealbound prevents new stacks during the cast; release/revival/placement never inherit historical board stacks. See [complete rules](nuclear_fallout.md).

### Magic Amplification Circle

- MagicAmplificationCircle: Common Fantasy spell, mana 1, no upgrade, excluded from draft/rewards.
- No target. Each successful cast grants its owner SpellPower +10 until the end of their turn; multiple casts add together. This player-owned bonus is independent of occupant suppression or death.
- Apply it to all Magic damage through the usual live SpellPower total. Retain it during owner turn-end effects, then expire it before the next turn starts. Thus Red Dragon benefits, but next-turn Firewall does not.
- Existing persistent-effect snapshot/view/AI paths preserve owner, stacks and expiry. See [card details](magic_amplification_circle.md).

## Open Issues / TBD

> TBD: Multi-tile unit and building footprint rules

> TBD: Any targeting or resolution rules that are unique to multi-tile cards

> TBD: Full civilization roster and any civilization rules beyond the confirmed science power-upkeep rule

These unresolved rules do not make the implemented Nuclear/Fallout or Magic Amplification Circle rules above provisional.

## Occupant Hold Preview Status Tooltips (2026-09-21)

- Hold-preview shows innate card rules at the left/right, current occupant buffs/debuffs below, and applicable player/field influences above. Release hides all regions.
- Permanent combat stat gains retain their source card (Hero, RobotFusion, Daehwandan, DemonKing) and applied amounts. Same-source gains aggregate. Erasure clears existing records; DemonKing revival rebuilds records from its reset stats. Ownership transfer preserves records. This is attribution, not an additional stat bonus.
- Snapshots, viewer state projections and AI clones preserve this attribution. Older snapshots without it show unattributed baseline differences; ordinary HP damage and account upgrade stats are not buffs.
- Below/above panels show only effect names and applicable current stat/damage values. Omit descriptions, category prefixes and duration/counter explanations; attached statuses such as Fallout and Invincible show their names only. Preserve effect presence, expiry, relevant tile filtering and all battle rules. Innate left/right explanations remain detailed. Player SpellPower and field damage effects are influences, not an occupant receiving that ability.
- Full display definitions: [occupant_status_tooltips.md](occupant_status_tooltips.md).


## Hand Card Hold Preview (2026-09-25)

- Hold a battle hand card for 0.4 seconds to show its image in the center; release closes it immediately. Shown stats reuse the hand's upgrade and current SpellPower calculation.
- A preview gesture does not select or play the card. Moving after the preview opens cannot convert that gesture into a card-use drag. A new short tap or ordinary drag retains the existing behavior.
- Card replacement/removal, owner disable, pointer exit, and app focus/pause cancel the preview. No battle command is sent by previewing.

### 손패 열람과 턴 종료 잠금 (2026-09-29)
- 턴 종료 대기 및 상대 행동 중에도 손패 카드를 클릭해 확대·선택하거나 다시 클릭해 해제할 수 있다. 열람은 전투 명령을 보내지 않는다.
- 턴 종료 후 카드 사용·공격·이동·액티브·추가 턴 종료의 입력 잠금은 유지하며, 열람으로 이 잠금을 해제하지 않는다.

### 카드 미리보기 공통 툴팁 (2026-09-29)
- 손패·드래프트/덱 빌딩의 제시 카드·덱 목록에도 좌우 특수효과 설명을 표시한다. 손패의 임시 복제는 아래, 아군 주문력·마법 증폭진은 위에 출처별로 표시한다. 적용된 상태가 없는 영역은 숨긴다. 미소환 카드에 소환물/타일 전용 상태를 부여하거나 표시하지 않는다. 상세: occupant_status_tooltips.md.

<a id="explosivereactivearmour"></a>
### 트랩 - 반응 장갑 (ExplosiveReactiveArmour)

- 과학문명 Rare 트랩 마법, 설치 무료·발동 전력 4, 강화 불가, 덱 빌딩 On·랜덤 보상 Off다. 전력을 먼저 쓰고 부족분은 전투 골드로 1:1 대체한다.
- 문구: `트랩: 전력-4. 상대방 유닛이 내 마스터를 근접 공격 시, 그 공격을 취소하고 그 유닛을 파괴한다.(마스터 제외)`
- 적법하게 승인된 상대 일반 근거리 유닛(`AttackType.Melee`)의 일반 공격에서 실제 최종 대상이 설치자 자신의 마스터일 때만 검사한다. 원거리 유닛·마스터·건물 공격자 및 같은 소유자의 공격은 제외한다. 근접 여부는 공격자의 공격 방식으로 판정하며 타일 거리나 Physical/Magic 피해 속성으로 추정하지 않는다. 환술은 최종 무작위 대상을 먼저 결정한다.
- 공격 가능 여부·대상·공격 비용 검증 뒤, 쉴더·HP 피해·연타·관통·반격 전에 공통 설치 순서로 검사한다. 처음 조건·비용을 충족한 트랩 하나만 발동하며 전력+골드가 부족하면 그대로 남긴다.
- 비용을 한 번 지불하고 트랩을 소비해 버림 더미로 보낸 뒤 승인된 공격 전체를 취소하고 공격자를 **직접 파괴**한다. 공격 1회와 이미 지불한 명시 공격 비용은 돌려주지 않는다.
- 직접 파괴는 피해 패킷이 아니다. 방어력·주문력·방전 피해 증폭·무적·불굴·흡혈·쉴더의 피해 이전을 계산하지 않으며 보호 대상의 HP를 변경하지 않는다. 가짜 피해 수치·피격음·취소된 일반 공격 타격 모션도 만들지 않는다.
- 공통 파괴 경로로 공격자 퇴장·오라 제거·연쇄 사망·유언·원자력 발전소 폭발·마왕 부활 봉인 및 최종 마스터 승패를 처리한다. 부활 대기 마왕은 영구 제거로 표현하지 않으며 연쇄로 실제 피해를 받은 다른 대상의 피해 표시는 유지한다.
- 설치 전후 비공개·마스터 ?/Xn·같은 CardId 중복 설치 불가·1회 소모는 공통 트랩 규칙이다. 성공 발동 시 양쪽 화면 중앙에 실제 반응 장갑 카드 그림을 공개한다. 재접속은 현재 상태만 복원한다.
- 타입화된 정의는 `EnemyMeleeUnitAttacksMaster`(3) / `DestroyAttacker`, JSON `damage: 0`, `damageType: "None"`이다. 기존 `EnemyUnitAttacksMaster`(1)와 `EnemyUnitAttacksUnit`(2)의 값·조건은 변경하지 않는다. 수치 피해 주문으로 해석하지 않는다.
- 설치 당시 조건·발동 비용을 보존하는 공통 계약에 따라, 수정된 전력4·근접 전용 정의는 새로 설치하는 트랩에 적용한다. 수정 전 이미 설치된 트랩과 그 상태의 복원은 포착된 `EnemyUnitAttacksMaster`(1)·전력5를 유지하며, 재접속만으로 새 조건·비용으로 바꾸지 않는다.
- 성공 발동 시 첫 컷의 배리어를 마스터와 공격자 사이에 표시하고, 두 번째 컷부터 공격자 쪽으로 폭발을 퍼뜨려 덮는 순간 실제 사망 모션으로 이어진다. 로컬·AI·온라인 동일한 성공 발동 연출이며 재접속 상태 복원에서는 재생하지 않는다. 자산·방향·타이밍은 [반응 장갑 연출](reactive_armour_presentation.md)을 참고한다.

<a id="irondome"></a>
### 트랩 - 아이언 돔 (IronDome)

- 과학문명 Rare 트랩 마법, 설치 무료·발동 전력 4, 강화 불가, 덱 빌딩 On·랜덤 보상 Off다. 전력을 먼저 쓰고 부족분은 전투 골드로 1:1 대체한다.
- 문구: `트랩: 전력-4. 상대방 원거리 유닛이 내 마스터 공격 시, 그 공격을 취소하고 그 유닛을 파괴한다.(마스터 제외)`
- 적법하게 승인된 상대 일반 원거리 유닛(`AttackType.Ranged`)의 일반 공격에서 실제 최종 대상이 설치자 자신의 실제 마스터일 때만 검사한다. 근거리 유닛·마스터·건물 공격자 및 같은 소유자의 공격은 제외한다. 원거리 여부는 공격자의 공격 방식으로 판정하며 거리·비행·Physical/Magic 피해 속성으로 추정하지 않는다. 환술은 무작위 최종 대상을 먼저 결정한다.
- 공격 가능 여부·최종 대상·명시 공격 비용 검증 뒤, 쉴더·HP 피해·연타·관통·반격 전에 공통 설치 순서로 검사한다. 조건이 맞지 않거나 전력+골드로 발동 비용을 낼 수 없으면 정체 공개·자원 차감·트랩 소모 없이 유지하고 다음 설치 트랩을 검사한다. 다른 트랩도 발동하지 않으면 원래 공격은 정상 진행한다.
- 처음 조건·비용을 충족한 트랩 하나만 비용을 한 번 지불하고 소비해 버림 더미로 보낸 뒤, 승인된 공격 전체를 취소하고 공격자를 **직접 파괴**한다. 공격 1회와 이미 지불한 명시 공격 비용은 돌려주지 않는다. 근거리 전용 반응 장갑과 다른 카드이므로 동시에 설치할 수 있지만 같은 CardId의 중복 설치는 불가하다.
- 직접 파괴는 피해 패킷이 아니다. 방어력·주문력·방전 피해 증폭·무적·불굴·흡혈·쉴더 이전을 계산하지 않으며 보호 대상의 HP를 변경하지 않는다. 취소된 공격의 일반 타격 모션·피해 수치·피격음도 만들지 않는다. 공통 파괴 경로로 공격자 퇴장·오라 제거·연쇄 사망·유언·부활 및 최종 승패를 처리하며 부활 대기 소환물을 영구 제거로 표현하지 않는다.
- 설치 당시 조건·발동 비용·효과·레벨·설치 순서를 포착하고 복원한다. 상대에게는 공통 물음표 카드와 마스터 ?/Xn만 공개하며 자신의 알려진 트랩만 AI가 판정·평가한다. 성공 발동 시 양쪽 중앙에 실제 아이언 돔 카드 그림을 공개하고, 설치자 마스터에서 공격자까지 0.5초 직선 미사일 비행 → 공격자 위치의 시한폭탄 폭발 16컷 → 폭발 최대 확장 시 기존 Death 모션을 재생한다. 취소된 공격·가짜 피격 및 추가 음원은 없다. 확인된 사망만 표현하고 부활 대기는 영구 사망으로 표시하지 않는다. 온라인 다음 상태는 연출 완료까지 대기하며 전투 판정은 그대로 유지한다. 재접속은 현재 상태만 복원하고 과거 연출을 재생하지 않는다.
- 타입화된 정의는 `EnemyRangedUnitAttacksMaster`(4) / `DestroyAttacker`(3), JSON `damage: 0`, `damageType: "None"`이다. 기존 조건 값 0–3을 유지하며 리턴·미러 필드·반응 장갑의 조건을 변경하지 않는다. 설치·이미지·강화 정책은 [아이언 돔 안내](iron_dome.md)를 참고한다.

<a id="summontrap"></a>

### 트랩 - 서먼 트랩 (SummonTrap)

- 중립 Common 트랩 마법이다. 설치 무료·발동 전투 골드 1, 강화 가능, 덱 빌딩·랜덤 보상 On이다. 문구: `트랩: 골드-1. 상대방이 유닛을 소환하면 그 유닛에게 물리 데미지 20을 입힌다.`
- 조건은 상대 일반 유닛을 손패에서 성공적으로 소환한 경우(`PlayUnitCard`)다. 카드/손패/위치/자원 검증과 실제 배치, 소환 비용 지불·손패 소비, 강화·속공·오라·낙진·무적 등 기존 정상 소환 초기화를 완료한 뒤 판정한다. 실패한 소환, 건물·마스터, 이동·고독·패 반환·부활은 이 조건에 포함하지 않는다. 현재 구현되지 않은 미래 효과 소환 판정을 임의로 추가하지 않는다.
- 상대 소환 유닛의 정확한 runtimeId를 대상으로 공통 설치 순서에서 조건·비용을 충족하는 최초 한 장만 발동한다. 전투 골드 1을 낼 수 없으면 정체 공개·차감·소모 없이 유지하고 다음 트랩을 검사한다. 기존 소환 비용과 사용한 손패는 돌려주지 않고 소환 자체를 취소하지 않는다.
- 기본 피해는 Physical 20이다. Lv.1–13 매 레벨 +1로 Lv.0 20·Lv.13 33이며 설치 시 `ArmedTrapState.EffectAmount`에 강화 반영 피해를 포착한다. 이후 정의나 계정 레벨 변경 및 복원이 이미 설치된 트랩의 피해를 다시 계산하지 않는다.
- 해당 소환물 본인에게 직접 효과 피해를 주므로 쉴더 이전은 없다. 물리 방어만 적용하고 주문력·마법 방어·비행 반감·일반공격 치명타·연타·흡혈은 추가하지 않는다. 무적·봉인 보호·불굴·방전 3배·실제 HP 감소 시 은신 해제는 공통 피해 규칙을 적용한다. 최종 피해가 0이어도 발동 비용 지불·1회 소모·버림·실제 카드 공개는 유효하다.
- 피해와 정상 사망·오라 철회·연쇄·마왕 부활을 공통 서비스에서 처리한다. 설치 당시 조건·비용·강화 피해·레벨·순서를 저장하고 소유자에게만 복원한다. 상대에게는 설치 개수와 공통 물음표만 공개하며 성공 발동 때 양쪽에 실제 서먼 트랩 카드를 공개한다. 재접속은 과거 연출을 재생하지 않는다. 다른 카드와의 설치 순서·동일 CardId 동시 설치 불가·비용 부족 시 유지·1회 소모는 공통 트랩 규칙을 따른다.

## 대환단 (2026-10-08 변경)

- 대상 선택 없이 사용 즉시 내 마스터의 공격력을 효과수치만큼 증가시킨다.
- 기본 효과수치 30, Lv.1–13 매 강화 +1: Lv.0 +30, Lv.13 +43. 비용은 기5이며 기존 골드 대체 지불을 유지한다.
- 별도의 기 생산 지속 효과를 등록하지 않는다. 주문력이나 마법 피해 증폭은 공격력 증가량에 적용하지 않는다.
- 기존 출처별 전투 공격력 버프를 사용하며 중첩·망각 제거·방전 등 억제·상태 저장 규칙은 그대로 따른다. 서버 전투 기록은 실제 적용한 공격력 증가량을 표시한다.
