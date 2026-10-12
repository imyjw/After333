# Owned Cards Pixel Layout

The collection uses the same teal/gold button artwork as GameStart. The pixel layout implementation preserved card rules, upgrade costs, account requests, pagination and the six-column/two-row list. Current upgrade prices were separately revised on 2026-10-01, with target Lv.2 adjusted from 5/5 to 6/6 on 2026-10-02; see the [target-level cost table](meta_rules.md#card-upgrade-costs). The list/detail requirements and green upgrade border use the shared cost rule.

## Apply

The editor applies layout version 1 once after compiling the new scripts.
If OwnedCards is open with unsaved edits, the scene is marked dirty rather than
silently saving those edits. Save the scene after inspecting the result.
To deliberately reset the design, use
`Tools > Project333 > Owned Cards > Apply Pixel Collection Layout`.
Do not use Create Owned Cards Scene to update an existing scene.

## Inspector Editing

- `OwnedCardsPanel`: collection position and overall size.
- `CardGridRoot > OwnedCardsResponsiveGrid`: maximum slot size and spacing.
  The component fits all 12 slots inside the available rectangle, both in edit
  mode and Play mode. GridLayoutGroup cell size is consequently driven by it.
- `CardSlot_00` through `CardSlot_11`: artwork margins, count/level text styling.
- `DetailPanel`: overall detail area.
- `DetailArtworkImage`: card preview region. Existing normalized ATK/HP anchors
  on OwnedCardsSceneController are retained.
- `DetailOwnershipText`: upgrade requirements and next-level information.
- `DetailInfoScroll/Viewport/DetailMetaText`: metadata/effect typography.
  Height follows the text's preferred height; scroll the viewport to read long text.
- `UpgradeButton`: remains outside the scrolling area.
- `HeaderPanel/BackButton`: uses the shared pixel menu sprite.

Typography is authored once. Account refreshes and entering Play mode do not
reset the collection's authored font or font size. Dynamic text (card stats,
wallet balance, upgrade availability) is still refreshed from game/account data.
The existing green upgrade-ready outline and gold selection background are kept.

## Current detail behavior (2026-10-01)

The gallery opens a centered modal with current JSON-backed stats, unit races above affiliation and scrollable rules. Its artwork can flip to the black/white Lv.1–13 upgrade-increment table and back. The flip changes only the artwork region; upgrade actions, scrolling, modal close and background response handling keep their existing behavior. See [owned_cards_ui.md](owned_cards_ui.md) for current input and data rules. Collection styling remains separate from the shared notice theme in [notice_ui.md](notice_ui.md).