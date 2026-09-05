# Shop Pixel UI

The shop shares the GameStart/OwnedCards teal, gold and cream palette and the
PixelMenuButton sprite. This is a presentation change, not an economy change.
Ticket purchase still uses the server API; rewarded ads still require server
verification. LevelPlay configuration is left unchanged.

## Layout

- Header: shop title, current account wallet, Back button, global settings gear.
- Left: gold exchange, ticket illustration, configured gold price, purchase button,
  purchase status.
- Right: ad reward, ticket illustration, watch-ad button, provider/reward status.
- Product rectangles use proportional anchors inside the safe area. The existing
  canvas scaler handles resolution scaling; text can shrink to fit its region.
- Ticket illustrations are editable Unity Images/Text, not baked text in a PNG.

## Editing

The first script reload applies version 2 and saves the scene if it was not
already open with unsaved changes. Existing dirty scenes are only marked dirty.
After that, the version marker prevents reapplying the layout on every reload.

Version 1 to 2 replaces only `TicketProductPanel/PriceGoldIcon` with the user's
`Resources/Project333/UI/ServerGold.png`, imported as a Single Sprite with Point
filtering and no compression. It does not reset panel positions or fonts, and
does not change the separate battle Gold texture. To reapply only this icon, use
`Tools > Project333 > Shop > Update Gold Icon`.

To intentionally restore the design, exit Play mode and use:
`Tools > Project333 > Shop > Apply Pixel Shop Layout`.
Do not use `Create Shop Scene And Update Start Scene` just to restyle the shop.

Edit `ShopSceneController/TicketProductPanel` and
`ShopSceneController/RewardedTicketProductPanel` in the Hierarchy.
The buttons, status text and ticket illustration are all visible before Play.
Do not move the ad button/status out of RewardedTicketProductPanel: that parent
is the configured rewarded-ad UI host.

Button/state labels and wallet/price numbers remain data-driven. Font, font size,
images and RectTransform settings are not rewritten by normal account refresh.
The purchase cost is taken from ShopSceneController's existing configured cost;
the server remains authoritative for payment and grants.
