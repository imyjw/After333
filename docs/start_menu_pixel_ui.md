# GameStart Pixel Menu

The Start, Shop, Owned Cards and PvP Reconnect buttons share the generated `PixelMenuButton` sprite.
Their Korean labels are Unity Text objects using the bundled Noto Sans KR Bold font.
Start/resume/reward labels continue to follow account state.

- Source PNG: `Assets/Project333/Resources/Project333/StartScene/PixelMenuButton.png`
- Editor setup: `Tools > Project333 > Start > Apply Pixel Menu Layout`
- First script reload applies layout version 2 once. Upgrading from version 1 copies the current Owned Cards style to Reconnect without resetting the other buttons or wallet. Subsequent reloads preserve Inspector changes.
- Existing open dirty scenes are marked dirty, not silently saved over the user's unsaved edits.
- Buttons: `GameStartSceneController/CenterPanel`, width 420, height 114, vertical stride 132 canvas units. PvP Reconnect is the fourth slot, directly below Owned Cards. The panel reserves additional height to avoid overlapping the footer. Reconnect is still visible only when there is a reconnectable match.
- Wallet: `GlobalSettingsMenuCanvas/SafeAreaRoot/WalletPanel/TicketText`.
- Wallet panel uses the same safe area and scaler as the gear: size 560 x 86, top/right offset 24/126. Gear width 86, right offset 24; the gap is 16.
- Wallet values are refreshed by the existing account state flow; no displayed balances are hardcoded.
- Sprite import uses Point filtering, no compression or mipmaps, and one Sprite Editor slice excluding transparent margins. The original PNG is preserved.

## Horizontal alignment and phone safe areas

- The background fills the entire screen. Its baked-in After333 logo uses the physical screen center.
- At runtime, only GameStartSceneController/CenterPanel uses that same horizontal center. SafeAreaFitter.ConfigureScreenCenteredElements converts screen x=0.5 into the safe-area parent's anchor coordinates, correcting asymmetric left/right notch insets.
- The menu's Inspector-authored size, anchored offset, vertical placement and child layout remain intact. Keep the horizontal offset at zero to align beneath the logo. Account information, wallet, gifts and settings continue to use the normal safe area.
- Do not fix this by moving the background or adding a device-specific negative x offset: opposite landscape orientation requires the opposite correction.
- Verified 2026-09-26 with the actual scene and simulated left/right/symmetric insets at 2340x1080, 1920x1080 and 2400x1080. Main scene file preserved; client rebuild required.
## Image Generation

Generated using the built-in image_gen tool. One shared image deliberately gives all three buttons identical graphics; text and interaction states are applied by Unity.

Prompt:

Use case: stylized-concept. Asset type: one reusable Unity pixel-art menu button background for a fantasy card game. Create exactly ONE wide horizontal rectangular button, straight front view, about 4.5:1 width to height, centered with very small transparent margins, true transparent alpha background. Dark deep teal inset flat center with generous blank space for game text, chunky stepped pixel corners, restrained warm antique gold double border, small symmetric square bronze corner rivets. Strong readable silhouette, crisp square pixel clusters, low resolution 16-bit RPG sprite look enlarged with nearest neighbor, limited palette, subtle two-tone bevel. The entire button is visible, no cropping. No words, no letters, no icons, no watermark, no glow, no blur, no photographic textures, no perspective. This single identical background will be reused for Start, Shop and Collection buttons; their Korean labels are rendered separately in Unity.

## Current reconnect scope (2026-10-01)

The button's legacy PVP field/method names do not restrict it to human opponents: the account-backed normal battle and Gold Dungeon can restore their existing battle through this flow. Display and remaining grace come from the server (70 seconds from detected disconnect), including existing challenge reservation recovery. See [battle_modes.md](battle_modes.md). Menu labels retain their authored font; notices and settings confirmations use the separate NeoDunggeunmo Pro theme in [notice_ui.md](notice_ui.md).

## Local tutorial entry (2026-10-02)

The bottom-left `튜토리얼` button opens the local `Tutorial_VSlice` without consuming tickets or calling a server. The login gate covers and blocks this entry while it is visible (2026-10-22 user decision, replacing the earlier pre-login shortcut). The button otherwise inherits the start menu safe area. Tutorial simulation remains isolated from accounts and economy. Existing serialized start menu layouts remain unchanged.

All six chapters are implemented (2026-10-20 review). The lesson borrows the actual GameStart, DeckBuilding, Draft, Battle, Shop and OwnedCards UI, with GaebangChief and dialogue layered above it. Practice uses deterministic local state rather than live account/run/economy APIs. Required actions hide Next and highlight the actual target green; all six topics are selectable. Skip/completion return to GameStart. See [implementation and editing](tutorial_implementation.md) and [the user-authored script](tutorial_script_ko.md).


## Login backdrop and input order (2026-10-22)

`AccountLoginGateView` stays under the start menu safe area so its authored panel and controls retain their placement. Only its translucent Backdrop expands to the full root Canvas, using transformed Canvas corners rather than a fixed phone inset. Opening the gate and later safe-area/resolution changes refresh the coverage.

Login sorts at 29995, above Tutorial (5001), Gifts (29990) and the start wallet (29994). `GlobalSettingsMenuController` creates the wallet-only nested Canvas at runtime because the authored wallet otherwise shares the Settings Canvas. The gear and its settings/confirmation menus keep 30000 and remain usable. The session-replacement confirmation uses 29996 so it also stays visible above login. Backdrop raycasts block the covered start-menu controls; hiding login restores their ordinary input.

Isolated Unity 6000.3.12f1 EditMode 94/94 passed, including the native start scene, rendered pointer raycasts and asymmetric safe areas at 1280×720, 1920×1080, 2340×1080 and 2560×1080. A separate PlayMode lifecycle check passed 1/1, including forced-logout Google readiness and the replacement confirmation above login. This is simulated layout/input validation, not a phone retest. No source scene asset was changed; the user must rebuild/install the client. No server or DB change is required.


## Editable UI before Play (2026-10-23)

The shared editor coordinator now prepares the current saved UI before Play, preserving existing scene objects and attached button components. See [editable_scene_layouts.md](editable_scene_layouts.md) for the common menu, dirty-scene saving policy and the distinction between editable UI templates and live account/battle data.
