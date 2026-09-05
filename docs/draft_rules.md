# Draft Rules

## Document Purpose

This document defines the full-game draft system used to create a 33-card deck before a run begins.

- High-level overview: [game_design.md](./game_design.md)
- Battle rules for the drafted deck: [battle_rules.md](./battle_rules.md)
- Meta progression context: [meta_rules.md](./meta_rules.md)

## Scope Separation

### Full Game Design Scope

- Draft is part of the full game design loop.
- The output of draft is a locked 33-card deck for a run.

### Current Account-Backed Implementation Scope

- Draft is implemented in the dedicated DeckBuilding scene.
- Starting a run creates a server-owned `draft_runs` record and spends the run ticket cost.
- The server generates every offer from the server card database and the run's draft seed.
- Unity sends only the run id, selected card id, and expected pick index.
- Every confirmed pick and the next displayed offer are persisted atomically through the server.
- If the client closes during draft, the same selected cards and the same current offer are restored after login.
- The server creates and locks the `33`-card run deck as part of accepting the final pick.

## Draft Objective

- Build exactly one `33`-card deck.
- That deck is then used for battle play.

## Core Draft Procedure

The draft loop is:

1. Present `3` different Legendary cards
2. Choose `1` Legendary card as the first deck card
3. Present `3` different eligible non-Legendary cards
4. Choose `1` card and add it to the deck
5. Repeat the non-Legendary offer for the remaining `32` picks

The full draft performs `33` choices: `1` Legendary opening pick and `32` non-Legendary picks.

## Deck Output Rule

- Final deck size after draft: `33` cards
- Deck editing after draft: not allowed

## Duplicate Limits

- Legendary cards: maximum `1` copy
- All non-Legendary cards: maximum `3` copies

## Rarity System

Confirmed rarity order from lowest to highest:

1. Common
2. Uncommon
3. Rare
4. Unique
5. Legendary

Confirmed appearance rule:

- The Legendary opening offer is separate from the non-Legendary rarity roll.
- Non-Legendary offer slots use these weights:
  - Common: `40`
  - Uncommon: `30`
  - Rare: `20`
  - Unique: `10`
- When a rarity has no eligible card for the current slot, only currently available rarity weights participate in that roll.

## Offer Generation

Confirmed:

- Each pick presents exactly `3` different card ids; one offer cannot contain duplicate cards.
- The global card pool is non-consuming. Each new offer samples independently from all currently eligible cards.
- A card that has reached its deck copy limit is excluded from later offers.
- Non-Legendary rarity is rolled independently for each offer slot using `40/30/20/10` weights.
- Before draft starts, the catalog must contain at least `3` unique Legendary cards.
- Before draft starts, the catalog must contain at least `13` unique non-Legendary cards.
- The non-Legendary pool must also provide at least `32` total copies under the three-copy limit.
- If fewer than `3` eligible non-Legendary card ids remain during draft, offer creation fails instead of presenting an invalid offer.

## Persistence And Resume

- `POST /runs/start` returns the server-generated Legendary opening offer.
- `POST /runs/draft-state` returns the server-owned ordered pick history and current offer.
- `POST /runs/select-draft-card` accepts only `runId`, `cardId`, and the client's expected zero-based `pickIndex`.
- The server reconstructs the authoritative current offer from `draft_seed` and ordered pick history before accepting a selection.
- A confirmed pick, its actual three-card offer, and the next offer are saved in one database transaction.
- Resume restores both the ordered selected-card list and the exact deterministic offer.
- The first restored pick must be Legendary; all later restored picks must be non-Legendary.
- A restored offer must contain exactly `3` unique, currently eligible card ids.
- Leaving DeckBuilding does not abandon or reroll the current draft.
- A stale or replayed pick index is rejected instead of adding a duplicate card.
- Unity cannot submit an arbitrary offer, full pick history, or completed deck as authority.
- The former client-authoritative `/runs/save-draft-picks` and `/runs/complete-draft` endpoints return `410 Gone`.

## AI and Draft

The player draft rule is fixed, but the full-game AI deck source is not.

> TBD: Whether AI opponents use drafted decks, fixed decks, generated decks, or curated encounter decks in the full game loop

## Relationship to Other Documents

- [game_design.md](./game_design.md) describes the account-backed run loop that owns the draft.
- [battle_rules.md](./battle_rules.md) defines how the finished deck behaves in battle.
- [meta_rules.md](./meta_rules.md) defines how the drafted deck is reused across a run.

## Open Issues / TBD

> TBD: AI deck sourcing for future non-demonstration PvE content

> TBD: Any future draft preview or undo feature beyond the current immediate-save flow
