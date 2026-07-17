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

For the card-authoring workflow, use `docs/card_authoring_checklist.md`.

## Product Naming

- The public game name is `After333`.
- Player-facing titles, build names, server display names, and public test documents must use `After333`.
- Existing internal identifiers remain `Project333` for compatibility unless a dedicated migration is explicitly requested.
- Do not rename the `Project333` C# namespaces, assembly names, asset/resource paths, `PROJECT333_*` environment variables, PostgreSQL database name, command-line switches, or PlayerPrefs keys as part of a branding-only change.

## Current Implementation Scope

Implement the current account-backed online PvP foundation:

- server-backed guest account login and session restore
- first-party Game ID and future social-login ready account schema
- account-owned tickets, Resource Gold, card collection, and card upgrade state
- server-verified rewarded video ticket grants
- draft deck building, draft resume data, and saved run decks
- 33 wins / 3 losses run loop with run-end rewards
- player versus AI as a server-backed PvE demonstration mode
- server-authoritative 1:1 PvP command handling
- account-based PvP matchmaking and reconnect groundwork
- Unity clients sending intended commands only
- server-authored StateView/BattleEvents as the source of presented battle state

Do not implement yet unless explicitly requested:

- ranked matchmaking
- MMR or seasons
- paid shop or real-money purchase flows
- production social OAuth provider integration
- production anti-cheat beyond server-side rule validation
- server restart battle recovery beyond the documented snapshot/command-log plan

## Rewarded Ad Contract

- Rewarded ads are opt-in and currently appear only on the Game Start scene.
- One verified completion grants `1` account ticket.
- Temporary limits are `3` rewards per UTC day and a `60`-second reward cooldown per account.
- Unity ad callbacks are presentation signals only and must never mutate the wallet directly.
- Only a valid provider server-to-server callback may grant the ticket.
- Provider event IDs and reward attempts must be persisted and idempotent.
- The provider private key is server-only and must never be stored in Unity assets, scenes, builds, or source control.

## Online PvP Scope

The current PvP target is account-based matchmaking and reconnectable 1:1 battles.

- Guest accounts are real server accounts.
- Game ID login and social login must link to `auth_identities` instead of creating client-local profiles.
- A matchmaking request must be associated with an authenticated account.
- A battle seat must be recoverable from account identity, match id, and server-side match-player rows.
- Reconnect must reattach the same account to the same seat when the match is still active or in reconnect grace.

The earlier custom-room 1:1 prototype remains useful only as a smoke-test path.

- The server owns the true battle state.
- Unity clients send intended battle commands only.
- Clients must not decide final battle results.
- Clients must not send damage, healing, resource, draw, or winner results as authority.
- Server-side battle resolution must use the same command/rules model as local battle.
- Hidden information must be filtered per viewer.
- A player may see their own hand and public board state.
- A player may see only the opponent's public board state and hidden-zone counts.
- Start with fixed decks or already drafted deck lists.
- Do not add ranked matchmaking until custom-room PvP works.

## Battle Gateway Contract

Online work must pass through a gateway boundary.

- Presentation code should depend on `IBattleGateway` for battle actions.
- `LocalBattleGateway` must preserve the existing offline behavior.
- Future online gateways must treat the remote server as authoritative.
- Do not make UI code directly decide command success for online play.
- Do not bypass existing battle services when resolving local commands.

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
- Each player's deck is shuffled independently before either opening hand is drawn.
- First player starts with `3` cards.
- Second player starts with `3` cards.
- Starting resources are:
  - mana `0`
  - qi `0`
  - power `0`
  - gold `3`

### Mulligan

- Each player may mulligan once.
- Mulligan is partial.
- Selected copies stay outside the deck while the same number of replacements are drawn.
- Another copy of the same card type may still be drawn.
- Selected copies return after the replacement draw, then the deck is shuffled.
- PvE AI keeps its opening hand without replacing cards.
- PvP players choose simultaneously and battle waits for both confirmations.
- Mulligan selection has a server-authoritative `33`-second limit; timeout keeps the current hand.
- Mulligan does not grant a Coin card.
- Opening cards are presented as large card images in a dedicated center-screen overlay.
- A player selects replacement cards in that overlay and confirms once.
- Confirmed replacement cards appear in the selected positions immediately.
- The first PvP player to confirm sees their replacement result while `상대의 멀리건을 기다리는 중...` remains visible.
- After both players confirm, the final mulligan result remains visible for `3` seconds before the overlay closes.

### Turn Structure

Each turn follows this order:

1. turn start
2. gain base resources and active occupant resources
3. resolve power-upkeep payment
4. resolve active Robot Factory effects
5. resolve Firewall turn-start effects
6. resolve the base draw
7. main phase
8. turn end

### Resources

- Battle resources are mana, qi, power, and gold.
- Resource Gold is meta currency and is out of scope for the current slice.
- Base turn gain:
  - draw `1`
  - gold `1`
  - mana `0`
  - qi `0`
  - power `0`
- Exception: on battle turn `1`, the first player does not receive the base `gold +1` and enters the first main phase with the initial `3` gold.
- The second player receives the normal base `gold +1` on battle turn `2` and enters their first main phase with `4` gold if no other resource changes occurred.
- Units and buildings may grant `mana +n`, `qi +n`, `power +n`, or `gold +n` at turn start.
- All battle resources have no maximum.
- Unspent battle resources persist between turns.
- Unit, building, and spell cards all pay their listed battle-resource costs.
- Gold can substitute for missing mana, qi, and power at a `1:1` rate for every resource payment,
  including card costs and ongoing upkeep.
- Typed resources are spent first when available, then gold covers any remaining mana/qi/power deficit, and finally any listed gold cost is paid from gold.
- Ongoing payment exists only when a rule or card text explicitly says so.

### Hand, Deck, And Draw Failure

- Maximum hand size is `13`.
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
- If the chosen destination tile contains another allied movable unit, the two units swap positions instead of blocking the action.
- Movement is direct.
- Occupied tiles that do not contain another allied movable unit cannot be moved onto.
- Buildings cannot move.
- Immobile units cannot move.
- A movable unit may move on the turn it is summoned.

### Attack Availability

- Units normally attack once per turn.
- Some effects may allow more than one attack.
- Units normally cannot attack on the turn they are summoned unless an effect says otherwise.
- Rush (`hasRush: true`) removes only that summon-turn attack restriction for a unit played from hand.
- Rush does not grant extra attacks or bypass targeting, `0` ATK, Drained, or other attack restrictions.
- Erasure applied during the summon turn suppresses Rush and restores normal summoning sickness.
- Replicate (`hasReplicate: true`) creates one temporary same-CardId hand copy only after a successful play.
- Replicate is legal on units, buildings, and spells, including cards whose printed total cost is `0`.
- Replicate copies retain Replicate, use original definition costs/rules, retain account upgrade level resolution, and do not inherit temporary in-hand changes.
- Unused Replicate copies vanish without entering discard at the end of their owner's turn.
- Some buildings can attack.
- Any attack-capable building is always ranged.
- Attack-capable buildings normally cannot attack on the turn they are summoned unless an effect says otherwise.

### Targeting

- Melee obeys front-row blocking.
- In a given enemy column, if the front-row tile is occupied by a blocker, melee cannot target the back-row tile in that column.
- If the front-row tile is empty, or occupied only by a drained unit, melee may target the back-row tile.
- Ranged can target any enemy occupied tile regardless of front-row blockers.
- Units, buildings, and Master Units are valid field occupants.

### Combat Resolution

- Only melee-versus-melee causes counterattack.
- Melee-versus-melee damage is simultaneous.
- Both sides can die in the same combat.
- Normal attacks and counterattacks use the acting occupant's own damage type.
- Physical damage uses `max(0, original damage - Physical Defense)`.
- Magic damage uses `max(0, original damage - Magic Defense)`.
- Fixed damage ignores Physical Defense and Magic Defense.
- Double Attack and Triple Attack apply defense separately to every hit.
- LifeSteal heals only the actual HP removed after damage mitigation.
- Guard applies its own defense first; resolved damage above the Guard's pre-hit HP transfers to the protected target, which then applies its own defense.
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

### Power-Upkeep Payment

- Any unit with `sciencePowerUpkeep > 0`, plus an explicitly defined building such as Robot Factory, requires power payment at turn start.
- Power payment happens after turn-start resource gain and before Robot Factory, Firewall, and the base draw.
- Payment is automatic in this fixed order:
  - `(0,0), (0,1), (1,0), (1,1), (2,0), (2,1), (3,0), (3,1), (4,0), (4,1)`
- Empty tiles are skipped.
- Occupants with `sciencePowerUpkeep <= 0` are skipped.
- Power is spent first, then gold substitutes for any missing power at a `1:1` rate.
- If full payment can be made, pay it and keep the occupant active.
- If full payment cannot be made, do not partially pay.
- Any unit or building that cannot be fully paid becomes or remains drained.

### Drained State

When drained, the occupant:

- cannot attack
- cannot move
- cannot counterattack
- cannot activate effects
- has its effects and special effects nullified
- treats Physical Defense and Magic Defense as `0`
- takes triple damage from every incoming physical, magic, or fixed damage instance
- still occupies its tile
- can still be targeted
- does not participate in front-row blocking

A drained occupant remains drained through that turn's draw/resource step.
It does not contribute effects during that step.
After resources are gained, if power payment succeeds, it becomes active from that point onward.

### Erasure State

When affected by Erasure (`망각`), the occupant:

- may attack, counterattack, and move when its baseline rules allow
- cannot activate card effects or special traits
- has all combat-time ATK, max-HP, Physical Defense, and Magic Defense modifiers present at application removed
- returns to its account-upgraded summon baseline without healing current HP
- immediately clears Drained, pays no science power upkeep, and cannot become Drained
- keeps baseline attack type, damage type, movement permission, tile size, ATK/HP, defenses, and account upgrade stats
- accepts buffs and debuffs applied after Erasure normally
- has Robot classification and effects such as Berserker, multi-hit, extra attacks, Endure, Guard, LifeSteal, Rush, resource production, Robot Factory, SpellPower, Hiding, Flying, and Invincible suppressed
- still occupies its tile, uses baseline defenses, takes normal damage, and participates in front-row blocking
- remains Erasure until it leaves the field
- cannot be applied to a Sealbound occupant; a Sealbound target remains unchanged and its attached effects are not removed

### Sealbound State

- Unit and Building cards use `sealboundOwnerTurnStarts: n`; omitted or `0` means no Sealbound.
- A value of `1` or greater applies Sealbound immediately on summon. Master Units cannot be Sealbound.
- The counter decreases only at the final step of each later owner turn start. Release therefore contributes no resources, upkeep, or turn-start effects during that same turn start.
- Sealbound occupants occupy their tiles but cannot attack, counterattack, move, exchange positions, activate effects, pay upkeep, receive Erasure, or participate in front-row blocking.
- Sealbound occupants cannot be targeted or affected by attacks, spells, area damage, healing, buffs, or debuffs and are excluded from Guard and Robot searches.
- A released occupant may attack on the release turn only if it has Rush. Otherwise it can attack from its following owner turn.
- StateView and reconnect snapshots preserve Sealbound, its remaining counter, and Rush.

### Hiding State

- Only Unit cards may use `hasHiding: true`; Buildings and Master Units cannot have Hiding.
- A Hiding Unit cannot be targeted by an opponent's normal attack or opponent's single-target spell, but area effects still affect it.
- A Hiding Unit does not participate in front-row blocking and its Guard is inactive.
- A legal normal-attack declaration reveals Hiding before the first hit, LifeSteal, and counterattack resolve. Rejected attacks do not reveal it.
- Entering Drained or Erasure permanently reveals Hiding. Clearing Drained does not restore it.
- Sealbound takes priority. Erasure cannot reveal a Sealbound Hiding Unit, and it becomes hidden when released unless already revealed.
- StateView and reconnect snapshots preserve `HasHiding` and `HidingRevealed`.

### Flying State

- Unit and Building cards may use `hasFlying: true`; Master Units may receive Flying through runtime battle setup.
- A non-Flying melee normal attack cannot target an active Flying occupant. Ranged attackers and active Flying attackers may target it.
- A Flying melee attacker ignores front-row blocking, and an active Flying occupant does not participate in front-row blocking.
- Normal melee counterattack rules still apply when a Flying melee attacker legally attacks a melee defender.
- Flying does not prevent spells, effects, healing, buffs, or debuffs and does not change movement rules.
- A Flying Guard cannot redirect a non-Flying melee normal attack, but other eligible normal attacks and effects may be redirected normally.
- Drained and Erasure suppress Flying. Clearing Drained restores it; an Erasure occupant remains suppressed and blocks normally.
- Hiding and Sealbound targeting rules take priority. Robot Fusion never transfers Flying from absorbed materials.
- StateView and reconnect snapshots preserve `HasFlying` for Units, Buildings, and Master Units.

### SpellPower

- Units and Buildings use `spellPower: n`; Master Units may receive SpellPower through runtime battle setup.
- Only Magic damage produced by a Spell card receives SpellPower. Physical and Fixed Spell damage, normal attacks, and non-Spell effects do not.
- Sum every living allied occupant's active SpellPower. Drained, Erasure, and Sealbound suppress it; Hiding and Flying do not.
- Add SpellPower after card-level damage changes and before defense, Drained multiplication, Guard, Endure, removal, and victory resolution.
- Area Magic Spell damage receives the full bonus per target, and multi-hit Magic Spell damage receives it per hit.
- Persistent Magic Spell damage captures SpellPower when the card is accepted from hand. StateView and reconnect snapshots preserve `CapturedSpellPower`.
- Robot Fusion does not transfer SpellPower, and card upgrades do not increase it unless a card-specific rule says otherwise.

### Invincible

- Unit and Building cards use `invincibleDuration`; Master Units may receive Invincible through runtime battle setup.
- Supported durations are `Always`, `SummonTurn`, `OwnerTurnOnly`, `OpponentTurnOnly`, `UntilTurnEnd`, and `OwnerTurns`. `OwnerTurns` requires `invincibleOwnerTurns > 0`.
- Invincible keeps the occupant targetable but reduces Physical, Magic, and Fixed HP damage to `0`. Non-damage removal, sacrifice, fusion-material removal, and set-HP effects are not prevented.
- Drained temporarily suppresses Invincible, Erasure suppresses it for the occupant's remaining battlefield lifetime, and duration counters continue to advance while suppressed or Sealbound.
- An Invincible Guard absorbs the eligible hit with no overflow. LifeSteal heals `0`, damage-based Berserker does not trigger, and Endure is not consumed.
- Multi-hit attacks resolve each hit for presentation but each hit deals `0`; clients show `무적` feedback instead of a damage number.
- Robot Fusion does not transfer Invincible. StateView and reconnect snapshots preserve every effect's duration, application context, and remaining owner-turn count.

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
- power upkeep through `sciencePowerUpkeep > 0`
- Rush (`hasRush: true`) summon-turn attack permission
- Flying (`hasFlying: true`) normal-attack targeting and front-row bypass rules
- SpellPower (`spellPower: n`) for Magic damage produced by Spell cards
- Invincible (`invincibleDuration`) damage prevention with explicit duration rules
- Replicate (`hasReplicate: true`) temporary same-turn hand copies with runtime identity and end-turn cleanup
- additional attacks
- attack-capable buildings
- physical, magic, fixed, or none normal-attack damage type
- physical defense and magic defense
- LifeSteal based on actual HP removed after defense
- Guard damage redirection with defense applied to Guard and overflow target in sequence
- non-persistent spell single-target damage
- explicitly defined area damage effects such as Red Dragon
- Robot Factory card generation from equally weighted draft-enabled Robot unit definitions
- persistent spell turn-start resource gain `+n` with explicit card-text end condition

### Not Allowed

- melee front-row blocking bypass outside the explicitly locked Flying rule
- reaction spells or interrupts
- healing
- max HP increase or decrease
- attack increase or decrease
- extra draw
- card generation, copy, discovery, or theft outside an explicitly locked effect such as Robot Factory or Replicate
- transform
- resurrection
- death, attack, hit, summon, or leave triggers
- random effects outside an explicitly locked server-authoritative candidate pool and weighting rule
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
17. power-upkeep payment order
18. drained-unit occupancy, targeting, non-blocking, and triple-damage behavior
19. physical and magic damage use only their matching defense
20. fixed damage ignores both defenses
21. drained recipients ignore both defenses and take triple damage, including fixed damage
22. multi-hit attacks apply defense separately per hit
23. Guard applies defense before overflow and the protected target applies defense again
24. LifeSteal uses actual HP removed after defense
25. Robot Factory resolves after upkeep and before Firewall/base draw
26. Robot Factory hides the generated card identity from the opponent
27. Flying normal-attack targeting, front-row bypass, Guard interaction, and suppression rules
28. SpellPower Magic-only application, suppression, persistent capture, and snapshot projection
29. Invincible damage prevention, duration advancement, suppression, Guard, and snapshot projection

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
- ranked matchmaking rules
- MMR, season, and ladder rules
- production OAuth provider setup details for Google, Kakao, and Naver
- exact pack contents and probabilities
- shop rules
- crafting rules
- paid purchase rules
- server restart battle recovery beyond the documented snapshot/command-log plan

## Implementation Guardrails

- Account, draft, reward, upgrade, and PvP persistence must flow through the server/API layer.
- Draft offers, pick validation, copy limits, and final deck creation are server-authoritative; Unity sends selection intent only.
- Unity may cache display data and tokens, but must not be authoritative for account progression.
- PvP clients must send intended commands only; the server decides command validity and battle results.
- Presentation code must not directly mutate online battle truth.
- Guest, Game ID, and social login must attach to `auth_identities`.
- Reconnect behavior must be account-based, not local object based.
- Do not implement card effects outside the whitelist.
- Do not invent hidden exceptions for unclear card interactions.
- Prefer simple, testable implementations over speculative systems.
- If a card or feature requires a rule that is not already locked, stop and ask.
