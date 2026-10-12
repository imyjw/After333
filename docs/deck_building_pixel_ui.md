# Deck Building Pixel UI

The deck-building screen shares the teal, gold and cream palette and pixel
Back button used by GameStart, OwnedCards and Shop. This is presentation only:
offer generation, selection, account levels, save/resume and server calls are
unchanged. TitleText and StatusText keep their existing live text content.

## Layout

- Header: Back to Start on the left, title/status in the middle, account wallet
  on the right with room reserved for the global settings gear.
- Three card choices: thin gold frames; existing 550 x 733 reference size,
  spacing 50, stat anchors and stat font size are preserved.
- Right deck list: each row shows only quantity/card name on the left and resource cost on the right. Costs use a separate, right-aligned UI column; no DECK/Left, Type, Rarity, Curve, CostSum, CARD LIST or rarity suffix is displayed inside DeckListScrollView. Names wrap within their column without overlapping costs. The panel heading and existing vertical scrolling remain.
- Footer: a short selection hint, separated from the choices by a thin rule.
- The existing responsive card/deck calculation and CanvasScaler remain active.
  Decorative header regions use proportional anchors inside the safe area.

## Apply And Edit

The editor applies the layout once after importing the scripts, outside Play
mode. The version marker prevents a fresh reset on subsequent script reloads.
An already open scene with unsaved edits is not automatically saved or closed.

To deliberately reapply the layout, use:
`Tools > Project333 > Deck Building > Apply Pixel Deck Building Layout`.
This resets visual styling, so do not run it after customizing the design unless
you want to restore these defaults. It does not reset a player's draft progress.

Objects are materialized in the scene before Play, including BackToStartButton,
DeckBuildingHeader, DeckBuildingFooter and DeckListScrollView. Inspect them under
DraftOverlayPresenter. The editor refresh does not request offers or start a run.

Use DraftOverlayPresenter's Card Size, Option Spacing, Option Vertical Offset,
Deck Panel Reference Size and Deck Panel Right Padding for layout sizing. Those
areas remain driven by the responsive layout rather than arbitrary child offsets.
Use its Option Stat fields for ATK/HP styling and placement.

Title, status, deck heading, deck list and Back button retain their scene font
sizes and regions after applying the new layout. Their contents may still update
with game state. Back Button Label controls the Back button's caption. The
Interface Font is the bundled Korean fallback used when creating missing text.

## Current data and preview behavior (2026-10-01)

The 50-card draft pool is distinct from the 57 shared definitions and the 40-card actual random reward pool. Current rarity, account upgrades and offered cards follow server draft rules; dated layout sizes above are presentation defaults. Supported choice/deck-list long-press previews share trait/effect panels with the common card preview path. See [card_catalog.md](card_catalog.md), [draft_rules.md](draft_rules.md) and [occupant_status_tooltips.md](occupant_status_tooltips.md).## Name and cost columns (2026-10-09)

DraftOverlayPresenter enables DeckListCardHoldPreview's namesAndCostsOnly mode for DeckBuilding, including the native scene reused by tutorial practice. DeckListColumnsView generates CardName and CardCost Text children from the same grouped, sorted draft IDs. Font, size and color come from the authored DeckListText; the cost column is measured from the current costs and anchored to the right edge as the viewport width changes. A row's height follows its wrapped name. Long-pressing either column uses the existing preview lifecycle, viewport clipping and ScrollRect forwarding. Tutorial guidance uses the whole row bounds. The completed-deck Draft lobby retains its existing display.
