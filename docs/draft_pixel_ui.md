# Draft Pixel UI

The Draft lobby uses the same teal, gold, cream and PixelMenuButton sprite as
GameStart, OwnedCards, Shop and DeckBuilding. The existing scene controller and
button handlers remain the source of behavior; no server or battle rules change.

## Layout

- Header: battle preparation title, Back to Start, wallet and existing gear.
- Left: current deck, live count/summary and a larger scrolling card list.
- Right: Wins/Losses and Last Battle, then the normal battle and Gold Dungeon buttons (legacy PVP/PVE component names remain internally). The existing
  conditional Start Draft button has a separate slot and is not forced visible.
- Bottom right: the existing live status text, including save/connection errors.
- Claim Rewards stays centered in its separate canvas. Reward popup dimensions,
  click-to-close behavior, matchmaking overlay and reconnect handling are kept.

The panels use proportional anchors. The CanvasScaler uses a 1920 x 1080
reference and height matching, consistent with the wallet and other game UI.
SafeAreaFitter keeps controls inside the safe area and the backdrop full bleed.

## Apply And Customize

The first script import outside Play applies version 1. Once applied, the version
marker prevents resetting the design on every compile or game launch. A loaded
scene with unsaved edits is marked dirty, not automatically saved or closed.

To deliberately restore the layout defaults:
`Tools > Project333 > Draft > Apply Pixel Draft Layout`.
Do not use the scene-creation tools merely to restyle this scene.

Under Canvas/DraftSceneController, edit HeaderPanel, RunStatusPanel, ControlPanel
and DeckPanel before Play. RuntimeDeckListScrollView and the other missing UI
elements are materialized in edit mode. Fonts, sizes, images and layout remain
scene-authored after applying the pixel layout. Keep Apply Runtime Draft UI
Layout disabled to avoid overwriting the deck panel's authored regions.

Labels describing wins/losses, last battle, deck contents, wallet, button state
and status remain dynamic. In particular, PVP's caption still switches to its
reconnect countdown, and completed runs still control Claim Rewards visibility.
The style builder does not start a run, make a pick, contact the server or claim
any rewards. All decorative images/text ignore raycasts.

DraftSceneVisualStyleTests covers preservation of button/wallet styling and the
legacy layout defaults. Full gameplay and mobile visual checks require Unity.

## Current behavior reference (2026-10-01)

Normal battle uses human-first matchmaking and a 20-second AI fallback. Gold Dungeon shows server-owned remaining attempts out of 3 for the current run started with 3 tickets. Midnight and relogin do not reset them; a new paid run has its own three attempts. Rewards remain separate without changing run score. Both reuse the persisted 33-card deck and support a 70-second reconnect grace. Notice/confirmation/remaining-time panels use [notice_ui.md](notice_ui.md); the current mode rules live in [battle_modes.md](battle_modes.md). These runtime policies do not require resetting authored layout.