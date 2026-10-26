# Shop Pixel UI

Current source reviewed 2026-10-23. Ticket Gold purchase, ad ticket reward and card-pack panels reuse the same styled product-card layout; account refresh preserves authored fonts/layout. [Card-pack purchase and recovery](shop_card_packs.md) remains server-authoritative.

The shop shares the GameStart/OwnedCards teal, gold and cream palette and the
PixelMenuButton sprite. This is a presentation change, not an economy change.
Ticket purchase still uses the server API; rewarded ads still require server
verification. LevelPlay configuration is left unchanged.

## Layout

- Header: shop title, current account wallet, Back button, global settings gear.
- Left: ticket Gold purchase (x anchors .055–.335).
- Center: card pack,10-card explanation, Gold33 price and purchase button (x anchors .36–.64).
- Right: ad ticket reward, watch-ad button and provider/reward status (x anchors .665–.945).
- The three products share the card-pack panel/title/art/price/button style. The existing ad host and button instances remain bound to their original behavior.
- Product rectangles use proportional anchors inside the safe area. The existing
  canvas scaler handles resolution scaling; text can shrink to fit its region.
- Both ticket illustrations use the user-provided ServerTicketIcon128 Sprite on the editable TicketIllustration Image. The legacy child rectangles/captions remain disabled; the current-layout upgrade preserves that artwork and button sound components.

## Editing

The first script reload/opening the shop/return from Play prepares layout version 3. The shared presentation-only preparation creates and saves the card-pack product and applies the current three-column layout without entering Play. An already-open dirty scene is updated in memory but is not automatically saved; use Ctrl+S to keep those edits. Later reloads skip the upgrade.

If the old view remains, exit Play and use:
`Tools > Project333 > Shop > Update Current Shop Layout`.
This uses `ShopSceneController.EnsureEditableHierarchy`; it does not invoke runtime Awake/OnEnable, initialize the ad provider, restore purchases or call account APIs. This is not ExecuteAlways on the shop/account controller.

Version 3 treats the saved panels as authored UI: runtime initialization rebinds the controls without resetting panel/button positions, fonts, ticket artwork or added components. The old runtime-generated layout remains a fallback for legacy/incomplete scenes.

Version 1 to 2 replaces only `TicketProductPanel/PriceGoldIcon` with the user's
`Resources/Project333/UI/ServerGold.png`, imported as a Single Sprite with Point
filtering and no compression. It does not reset panel positions or fonts, and
does not change the separate battle Gold texture. To reapply only this icon, use
`Tools > Project333 > Shop > Update Gold Icon`.

To intentionally restore the design, exit Play mode and use:
`Tools > Project333 > Shop > Apply Pixel Shop Layout`.
That explicit reset recreates the legacy ticket illustration; use Update Current Shop Layout for normal upgrades so custom ticket images remain intact. Both paths now finish with the current three-product layout.
Do not use `Create Shop Scene And Update Start Scene` just to restyle the shop.

Edit `ShopSceneController/TicketProductPanel` and
`ShopSceneController/RewardedTicketProductPanel` in the Hierarchy.
The buttons, status text and ticket illustration are all visible before Play. Card-pack controls are under `ShopSceneController/CardPackProductPanel`; select `PurchaseCardPackButton` there to attach `ButtonTouchSound` or edit the label/Image in the Inspector. Save outside Play so those components persist.
Do not move the ad button/status out of RewardedTicketProductPanel: that parent
is the configured rewarded-ad UI host.

Button/state labels and wallet/price numbers remain data-driven. Font, font size,
images and RectTransform settings are not rewritten by normal account refresh.
The purchase cost is taken from ShopSceneController's existing configured cost;
the server remains authoritative for payment and grants.

## Current scope (2026-10-01)

The implemented shop provides ticket purchase, server-verified rewarded tickets and 33-Gold/10-card packs. Since 2026-10-12, all three product panels share the card-pack visual style. Individual card inventory, crafting, unopened-pack inventory and real-money purchases remain outside scope. Purchase/upgrade recovery is automatic and does not add a manual recovery button or a transient recovery notice. Shared notice/confirmation styling follows [notice_ui.md](notice_ui.md); economic authority and retries follow [meta_rules.md](meta_rules.md) and [account_operation_retries.md](account_operation_retries.md).

## Editor layout validation (2026-10-23)

Isolated Unity6000.3.12f1: the pre-fix native scene fails the saved-card-pack check1/1. After automatic version3 preparation, 32/32 EditMode checks pass, including existing card-pack purchase/reveal UI tests. New checks open/reopen the saved scene without runtime initialization, verify all three products and native card-pack artwork, preserve both supplied ticket icons, keep custom button position/font/sound across repeated initialization, and verify an already-dirty scene is not saved over. Updating the Gold icon cannot downgrade version3. The isolated scene was also rendered without Play for visual review.

Evidence: session visualization ShopEditableLayout_20261023/{Baseline.xml,Final.xml,shop-before-play.png,verification.json,task.patch}. The source scene is not overwritten from the isolated copy: the user's Editor runs the migration under its normal dirty-scene safeguards. No app build/install, live purchase/ad/provider test, server deployment or DB operation was performed.
