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

### Current Vertical Slice Implementation Scope

- Draft is outside the current vertical slice implementation scope.
- The current vertical slice uses fixed decks instead of draft.

## Draft Objective

- Build exactly one `33`-card deck.
- That deck is then used for battle play.

## Core Draft Procedure

The draft loop is:

1. Present `3` candidate cards
2. Choose `1` card
3. Add the chosen card to the deck
4. Repeat until the deck contains `33` cards

This means the full draft performs `33` choices.

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

- Higher rarity cards appear less often than lower rarity cards

## Offer Generation

Confirmed:

- Each pick presents exactly `3` cards
- Rarity affects how often cards appear

Not yet fixed:

> TBD: Exact rarity weights or probability table

> TBD: Whether the same card can appear more than once within a single 3-card offer

> TBD: Whether the global card pool is depleted by picks or is sampled independently each time

> TBD: How draft handles edge cases when copy limits have already been reached for many cards

## AI and Draft

The player draft rule is fixed, but the full-game AI deck source is not.

> TBD: Whether AI opponents use drafted decks, fixed decks, generated decks, or curated encounter decks in the full game loop

## Relationship to Other Documents

- [game_design.md](./game_design.md) explains why draft is outside the current vertical slice implementation scope.
- [battle_rules.md](./battle_rules.md) defines how the finished deck behaves in battle.
- [meta_rules.md](./meta_rules.md) defines how the drafted deck is reused across a run.

## Open Issues / TBD

> TBD: Exact rarity probability table

> TBD: Per-offer duplicate behavior

> TBD: Global card pool sampling policy

> TBD: AI deck sourcing in the full game

> TBD: Any draft UI rules such as preview, undo, or confirmation flow
