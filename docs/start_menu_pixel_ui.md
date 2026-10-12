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

The bottom-left `튜토리얼` button opens `Tutorial_VSlice` without requiring login, tickets or a server. Its small child Canvas sorts above the account login gate and inherits the start menu safe area. Only the button intercepts clicks; it does not cover the normal menu. Existing serialized start menu layouts remain unchanged.

The current lesson includes the introduction and chapter 1 only. Six topic buttons are visible in tutorial selection; chapters 2–6 remain disabled as `준비 중`. Skip returns to `GameStart_VSlice`. The practice `게임 시작` button spends three ephemeral tutorial tickets and shows a local deck-building preview, without invoking the actual account-backed start/draft scenes. The fixed lesson restarts from the same state. See [the revised script](tutorial_script_ko.md).
