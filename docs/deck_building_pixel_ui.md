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
- Right deck list: stronger heading, larger text, existing vertical scrolling.
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
