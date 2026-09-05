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

## Image Generation

Generated using the built-in image_gen tool. One shared image deliberately gives all three buttons identical graphics; text and interaction states are applied by Unity.

Prompt:

Use case: stylized-concept. Asset type: one reusable Unity pixel-art menu button background for a fantasy card game. Create exactly ONE wide horizontal rectangular button, straight front view, about 4.5:1 width to height, centered with very small transparent margins, true transparent alpha background. Dark deep teal inset flat center with generous blank space for game text, chunky stepped pixel corners, restrained warm antique gold double border, small symmetric square bronze corner rivets. Strong readable silhouette, crisp square pixel clusters, low resolution 16-bit RPG sprite look enlarged with nearest neighbor, limited palette, subtle two-tone bevel. The entire button is visible, no cropping. No words, no letters, no icons, no watermark, no glow, no blur, no photographic textures, no perspective. This single identical background will be reused for Start, Shop and Collection buttons; their Korean labels are rendered separately in Unity.
