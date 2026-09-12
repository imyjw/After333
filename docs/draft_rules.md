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
- Non-Legendary offer slots use these integer weights (total 10,000):
  - Common: `6600` (66%)
  - Uncommon: `2000` (20%)
  - Rare: `1000` (10%)
  - Unique: `400` (4%)
- Server and local draft use the shared `DraftRarityWeights` constants.
- These are per-slot probabilities, not predetermined deck counts. Under an ideal independent-slot model where the player always chooses the highest rarity, the expected non-Legendary selected proportions are Unique 11.53%, Rare 24.87%, Uncommon 34.86%, Common 28.75%. Player choices and exhausted candidate pools can change the actual proportions.
- When a rarity has no eligible card for the current slot, only currently available rarity weights participate in that roll.

## Offer Generation

Confirmed:

- Each pick presents exactly `3` different card ids; one offer cannot contain duplicate cards.
- The global card pool is non-consuming. Each new offer samples independently from all currently eligible cards.
- A card that has reached its deck copy limit is excluded from later offers.
- Non-Legendary rarity is rolled separately for each offer slot using `6600/2000/1000/400` weights (Common/Uncommon/Rare/Unique), subject to candidate availability.
- Before draft starts, the catalog must contain at least `3` unique Legendary cards.
- Before draft starts, the catalog must contain at least `13` unique non-Legendary cards.
- The non-Legendary pool must also provide at least `32` total copies under the three-copy limit.
- If fewer than `3` eligible non-Legendary card ids remain during draft, offer creation fails instead of presenting an invalid offer.

## Persistence And Resume

- `POST /runs/start` returns the server-generated Legendary opening offer.
- `POST /runs/draft-state` returns the server-owned ordered pick history and current offer.
- `POST /runs/select-draft-card` accepts only `runId`, `cardId`, and the client's expected zero-based `pickIndex`.
- The server reads the authoritative current offer from the saved `current_offer_card_ids`, preserving all three card IDs and their order. It does not regenerate an already issued offer from the seed or current catalog.
- A confirmed pick, its actual three-card offer, and the next offer are saved in one database transaction.
- Resume restores both the ordered selected-card list and the exact saved offer. Legacy runs without a saved offer initialize it once under the run row lock.
- Opening and later-offer rarity rules apply when generating new offers. Later changes to rarity or draft eligibility do not invalidate previously issued offers or accepted picks.
- A restored offer must contain exactly `3` distinct card IDs. A selection is checked against those saved IDs; the client cannot replace the offer.
- Cards always resolve through the current deployed card definitions: stats, effects and rarity are not frozen per run. Previously selected IDs and copy counts remain intact after a card update; newly generated offers use current rarity and eligibility.
- A missing current card definition or corrupt saved offer produces an error instead of silently substituting another card or rerolling. Existing IDs must remain available in the catalog even when excluded from future drafts.
- Leaving DeckBuilding does not abandon or reroll the current draft.
- A stale or replayed pick index is rejected instead of adding a duplicate card.
- Unity cannot submit an arbitrary offer, full pick history, or completed deck as authority.
- The former client-authoritative `/runs/save-draft-picks` and `/runs/complete-draft` endpoints return `410 Gone`.

## AI and Draft

Online PvE uses ten fixed cross-civilization AI decks from `Server/Project333.PvpServer/Data/pve_ai_decks.json`.
At each new battle the server chooses one deck uniformly (10% each), then shuffles it with the normal battle setup.
Each deck contains 33 cards: 1 Legendary, 5 Unique, 9 Rare, 11 Uncommon, and 7 Common.
Only includeInDraft=true cards are eligible (omitted flags default to true), with at most 3 copies per card and 1 Legendary total.
The selected composition stays fixed for that battle; restoring an existing battle does not reroll it.
AI upgrades remain level 0. This does not change player draft rules or PvP opponent decks.
See [pve_ai_decks.md](./pve_ai_decks.md) for all ten lists and maintenance rules.

## Relationship to Other Documents

- [game_design.md](./game_design.md) describes the account-backed run loop that owns the draft.
- [battle_rules.md](./battle_rules.md) defines how the finished deck behaves in battle.
- [meta_rules.md](./meta_rules.md) defines how the drafted deck is reused across a run.

## Open Issues / TBD

> TBD: Future PvE difficulty tiers or win-count-based encounter selection

> TBD: Any future draft preview or undo feature beyond the current immediate-save flow
