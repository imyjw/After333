# Meta Rules

## Document Purpose

This document defines the full-game run structure, win/loss tracking, and out-of-battle reward economy.

- High-level overview: [game_design.md](./game_design.md)
- Battle details: [battle_rules.md](./battle_rules.md)
- Draft details: [draft_rules.md](./draft_rules.md)

## Scope Separation

### Full Game Design Scope

The full meta loop is:

1. Start a run
2. Build a 33-card deck through draft
3. Fight repeated battles with that deck
4. End the run at 33 cumulative wins or 3 cumulative losses
5. Receive rewards after run end
6. Spend Resource Gold in meta systems

### Current Vertical Slice Implementation Scope

The following systems are outside the current vertical slice implementation scope:

- Multi-battle run progression
- 33 cumulative win tracking
- 3 cumulative loss tracking
- Run-end reward payout
- Shop
- Card upgrades
- Ticket purchase
- Persistent out-of-battle economy

The current slice ends after one fixed-deck battle.

## Run Definition

- A run is a multi-battle progression sequence.
- A run uses one drafted `33`-card deck.
- That deck is reused across repeated battles within the run.

## Run End Conditions

- A run ends immediately when cumulative wins reach `33`.
- A run ends immediately when cumulative losses reach `3`.

## Rewards

- Rewards are paid after the run ends.
- The reward currency is Resource Gold.

> TBD: The exact reward formula is not fixed yet.

## Meta Currency

### Gold

- Gold is an in-battle resource.
- Gold is used inside battle rules.
- Gold is not the same as Resource Gold.

See [battle_rules.md](./battle_rules.md).

### Resource Gold

- Resource Gold is an out-of-battle currency.
- Resource Gold is gained through run-end rewards.
- Resource Gold is used to buy cards in the shop.
- Resource Gold is used to upgrade cards.
- Resource Gold is used to buy tickets to start the game.

## Meta Systems

### Shop

Confirmed purpose:

- Buy cards

> TBD: Shop inventory generation, pricing, and refresh rules are not fixed yet.

### Card Upgrade

Confirmed purpose:

- Upgrade cards

> TBD: Upgrade rules, upgrade limits, and cost tables are not fixed yet.

### Ticket

Confirmed purpose:

- Buy tickets needed to start the game

> TBD: Ticket cost, ticket consumption timing, and inventory rules are not fixed yet.

## Relationship to Other Documents

- [game_design.md](./game_design.md) separates full-game design from the current slice scope.
- [draft_rules.md](./draft_rules.md) defines how the run deck is created before the run begins.
- [battle_rules.md](./battle_rules.md) defines what happens inside each individual battle during a run.

## Out-of-Scope Notes for the Current Slice

These items are intentionally documented for future implementation, but are not current slice requirements:

- Draft UI and deck construction flow
- Repeated battle chaining
- Multi-battle opponent generation
- Reward payout screen
- Resource Gold persistence
- Shop loop
- Upgrade loop
- Ticket loop

## Open Issues / TBD

> TBD: Exact reward formula and payout timing details

> TBD: Whether any rewards exist before run end

> TBD: Shop pricing and inventory rules

> TBD: Card upgrade rules

> TBD: Ticket pricing and consumption rules

> TBD: Opponent sourcing across repeated battles in a run

> TBD: Persistence and save/load behavior for run and meta economy
