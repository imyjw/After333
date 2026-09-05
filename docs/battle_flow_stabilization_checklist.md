# PVE/PVP Battle Flow Stabilization Checklist

## Purpose

This checklist defines the current stabilization target before deeper DB/account implementation continues.

The goal is to make the PVE and PVP battle flows reliable enough that later account, ticket, reward, deck-save, and upgrade persistence can be connected without mixing too many bug sources.

Related documents:

- Account DB design: [account_persistence_db_design.md](./account_persistence_db_design.md)
- PostgreSQL implementation plan: [postgresql_db_implementation_plan.md](./postgresql_db_implementation_plan.md)
- Online PvP prototype: [online_pvp_prototype.md](./online_pvp_prototype.md)
- Draft rules: [draft_rules.md](./draft_rules.md)
- Battle rules: [battle_rules.md](./battle_rules.md)

## Current Priority

Stabilize battle flow before continuing into guest login, account persistence, rewards, or card collection storage.

The PostgreSQL migration file and migration runner can stay in the project, but they should remain dormant unless `PROJECT333_DB_CONNECTION` is explicitly configured.

## PVE Flow Target

Expected PVE loop:

1. Start scene spends tickets and enters Draft scene.
2. Draft scene creates a 33-card deck.
3. Player presses PVE battle.
4. Battle scene starts local player-vs-AI battle using the drafted deck.
5. AI acts sequentially, one action at a time.
6. HP/resource/state changes are visible after each resolved action.
7. Battle ends with Victory or Defeat.
8. Battle scene returns to Draft scene.
9. Draft scene keeps the current drafted deck visible.
10. Draft scene displays updated win/loss and last battle result.

## PVP Flow Target

Expected PVP loop:

1. Start scene spends tickets and enters Draft scene.
2. Draft scene creates a 33-card deck.
3. Player presses PVP battle.
4. Battle scene enters matchmaking.
5. Server assigns external seats as PlayerA/PlayerB.
6. Server owns the true `BattleState`.
7. Clients send commands only.
8. Server broadcasts battle events and filtered StateView messages.
9. Client plays battle event animations before applying the following StateView.
10. Both clients converge to the same visible board state.
11. Battle ends with Victory or Defeat.
12. Battle scene returns to Draft scene.
13. Draft scene keeps the current drafted deck visible.
14. Draft scene displays updated win/loss and last battle result.

## First Locked Checks

These checks should stay covered by EditMode tests where possible:

- A completed draft deck can queue a battle start.
- The queued battle start preserves whether the launch mode is PVE or PVP.
- An incomplete draft deck cannot queue a battle start.
- Recording a battle result does not lose the completed draft deck.

## Manual Smoke Test Order

### PVE Smoke Test

1. Open Game Start scene.
2. Start game and draft 33 cards.
3. Press PVE battle.
4. Play until at least one AI turn runs.
5. Confirm each AI action animates and applies HP/state before the next AI action.
6. Force or naturally reach Victory/Defeat.
7. Confirm Draft scene returns with deck list, win/loss, and last result intact.

### PVP Smoke Test

1. Start local PvP server.
2. Open two clients or two testers.
3. Draft or provide matching test decks.
4. Press PVP battle or connect both clients to matchmaking.
5. Confirm both clients receive PlayerA/PlayerB labels.
6. Use card, move, attack, cast Firebolt, and end turn.
7. Confirm both clients converge to the same board after each command.
8. Confirm animations do not duplicate and StateView does not apply before the related animation finishes.
9. Finish the battle through normal lethal damage or surrender.
10. Confirm Draft scene returns with deck list, win/loss, and last result intact.

## Do Not Continue DB Work Until

- PVE battle start and return are stable.
- PVP matchmaking and StateView projection are stable.
- AI action sequencing applies damage/state after each action.
- Firebolt and normal attack event animations do not duplicate.
- Draft scene reliably preserves the current run deck after battle return.
