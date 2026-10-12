# Owned card collection screen

Current behavior updated 2026-10-01. Dated validation sections below describe checks performed for the corresponding change, not a new app/device run.

## Player flow

- The main collection area spans the screen. Each page retains six columns and two rows (12 card slots); previous/next controls and page count remain visible.
- Existing catalog ordering and visibility are preserved, including cards not yet owned. Each card keeps its level/copy progress and the green border for upgrade readiness.
- Selecting a card opens a centered detail window. The initial collection load, account refresh, and background operation recovery never open this window automatically.
- The window displays large artwork with current stats, the card name, owned copies, level, next-level stat gains, required copies/gold, scrollable card rules, and the existing Upgrade button.
- Close, the dimmed area outside the window, or Escape/Android Back dismisses the window. A subsequent Back returns to the start screen through the existing navigation handler. Card/window contents intercept input; the gallery and header cannot be selected behind the window.
- Closing the window does not cancel an upgrade request. Its originally selected card ID remains fixed. Account/collection updates continue to arrive and update the gallery and any currently viewed card. Hidden detail windows remain hidden.

## Implementation

`OwnedCardsSceneController.Layout.cs` shares the gallery/modal layout between runtime scene entry and the existing editor materialization path. Existing serialized detail/card/upgrade references are retained; the detail panel is reparented under `CardDetailModal` rather than destroyed. Runtime activation converts the existing scene without a manual scene-rebuild step.

The existing teal/cream/gold styling, embedded Korean fonts, card artwork and safe-area/scaler setup are reused. `OwnedCardsResponsiveGrid` fits exactly 6 by 2 cells. The centered detail window is bounded to 1360 by 920 reference units and shrinks to the available safe area. Long effect descriptions use a masked vertical ScrollRect.

The global settings controller owns Escape/Android Back ordering, so a single input closes the detail window without also leaving the scene. The editor visual builder applies the shared layout last and does not automatically overwrite an already authored collection layout.

Upgrade prices, eligibility, per-card schedules, server authority, request IDs, atomic transactions and silent recovery remain governed by `meta_rules.md` and `account_operation_retries.md`. The current shared cost rule displays 6 copies and 6 account Resource Gold for Lv.1 -> Lv.2 (2026-10-02); the affordability border and Upgrade button use that same requirement.

## Initial modal validation (2026-09-26)

Final runtime compilation passed. An isolated Unity 6000.3.12f1 project compiled the actual runtime sources and loaded a copy of the actual OwnedCards scene, catalog and artwork. All 11 tests passed (10 EditMode cases plus a native PlayMode rendering/interaction case). Checked 1920x1080 and 2400x1080: twelve cards within the grid, real pointer opening/close/backdrop, modal input shielding, wheel/drag scrolling, final Korean rules text, and Back closing the detail without scene navigation. Upgrade-unavailable states, in-flight request preservation, background responses remaining hidden, and scroll continuity also passed.

Evidence: `TempBuild/OwnedCardsPresentation_20260926/unity-final.xml`, `unity-final.log`, `Captures/`. Test balances/copies are synthetic; no account requests or live economy changes occurred. Main Editor scenes were not opened or saved. Android hardware and client build/install were not tested; the user builds the app.
## Unit race display (2026-09-28)

Unit card details show 종족 immediately above 소속, using the card definition's races. Names are displayed in Korean, multiple races are comma-separated, and an empty race list displays 없음. Buildings and spells omit the row.

## Canonical card specifications (2026-09-28)

Collection detail values, stat badges and upgrade eligibility now resolve existing card IDs from the bundled battle cards.json. Costs, rarity, affiliation, footprint, races, stats and effect descriptions use that record. Assets retain artwork/controller references and localized keyword text; unknown custom IDs fall back to their asset. JSON edits invalidate the cached records. Corrected stale serialized values: Golem physical defense 1→3, BlueDragon mana 8→9, RedDragon mana 9→10, including the dragon asset generator costs.

Damage spells and damaging scripted spells display damage with the account upgrade applied. Effect prose is labeled as base values when an upgraded damage spell is selected, avoiding confusion with the leveled damage row. No battle balance, economy or server data is changed by this UI fix.
Additional audit correction: firebolt footprint OneByOne→None. Isolated Unity validation: 21/21 OwnedCardsPresentationTests passed; all 36 collection cards checked at Lv.0–13, stale asset values overridden, upgraded spell damage and previous modal behavior covered. Evidence: TempBuild/OwnedCardsPresentation_20260926/current-specs-final.xml. No client build/install or Android screen verification.

## Artwork flip and exact upgrade increments (2026-10-01)

- Click the detail artwork to flip it into a black background with white text and grid lines. Click the same region again to restore the artwork. The transition takes 0.36 seconds using unscaled time and positive horizontal scale, so the printed table never appears mirrored.
- The table has one row for each Lv.1–13 and columns for level, HP, ATK and the applicable effect. Each row is that level's increase over the preceding level, not cumulative stats, owned-copy level or a battle buff. It is built from the resolved current definition and shared upgrade rules.
- PlasmaShield/ManaShield are non-upgradeable (2026-10-03): their detail table shows 강화 불가 with no upgrade rows; defense descriptions stay +10 even if the account retains an old level. Non-upgradeable cards display 강화 불가; missing definitions or unsupported schedules are reported instead of inventing gains.
- Opening another card, closing the modal or disabling the scene resets to the front and cancels an unfinished flip. A background refresh of the same card keeps the chosen face while updating its current data. Repeated clicks during a flip do not start overlapping transitions.
- Implementation: `Assets/Project333/Runtime/Presentation/OwnedCards/OwnedCardsSceneController.Flip.cs` and `OwnedCardsUpgradeTableData.cs`.
- Earlier dedicated validation: 201/201 isolated Unity checks in `TempBuild/OwnedCardsUpgradeFlip_20261001/final-verification.json`. The later Orc ATK change's 224/224 regression run also covered current detail/table data; neither was a device install or a production account test.

## Current specification examples

The native collection contains 51 cards; the shared database defines 58, including unreleased definitions. Current JSON values override stale serialized detail values. OrcWarrior and OrcBerserker both have base ATK 20, with HP 40/50 and Lv.13 base stats 24/49 and 24/59. Their table still prints ATK+1 at Lv.3/6/9/13 and HP+1 at other levels. Golem has Physical Defense 3 and ManaWeaver has ATK 1. The full current values and per-level schedules live in [card_details.md](card_details.md) and [meta_rules.md](meta_rules.md); battle-only bonuses are not printed as permanent account upgrades.