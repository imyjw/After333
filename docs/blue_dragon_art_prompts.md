# Blue Dragon generated artwork prompts

> Latest integration (2026-09-26): active PNGs/clips now live in Assets/Unit/BlueDragon or Assets/Unit/Gwangma and use the card's Animator Controller. Previous generated atlas/intermediate files were archived under Tools/Art/Source. Descriptions of former Resources-based character playback below are historical. See unit_animation_authoring.md for the current workflow.

Created 2026-09-26 using built-in image_gen with transparent_background=true.
Original reference: Assets/Unit/BlueDragon/Idle.png. Original supplied assets preserved.

## DragonAtlas.png

Use case: stylized-concept. Asset type: production 2D pixel-art animation sprite sheet for the Blue Dragon in a Unity tactical card battler. Reference supplied Idle.png establishes identity. Redesign with excellent readable pixel-art craftsmanship: powerful sapphire blue dragon, deep navy outlines, cyan scales, pale icy wing membranes, swept silver horns, violet eyes, large wings, four limbs and expressive long tail. Right-facing three-quarter SIDE VIEW throughout. Pixel-art style, carefully placed hard pixel clusters, NO painterly gradients.
EXACT LAYOUT: transparent image 1536 wide by 1280 high, SIX equal columns and FIVE equal rows, total THIRTY sprites. Each cell exactly256x256, no lines or labels. Each dragon stays completely INSIDE its cell with 18px margin; same scale across EVERY frame, feet at local y=220, body occupies about205x160. Consistent silhouette and anatomy across animation; no changing dragon design. Six-frame left-to-right sequences:
Row1 IDLE: subtle inhale/exhale, chest expands, tail wave, small wing flex, final pose returns toward first.
Row2 ATTACK: crouch gathering water power, pull head/neck back and wings lift, thrust neck/mouth forward open, strong open-mouth exhale/recoil, recover, return. NO beam/projectile/water rendered into dragon sprite; projectiles are separate in engine. Mouth at attack release near local x=213,y=119.
Row3 HURT: normal dragon flinch, neck recoils, body leans back, wings tense, regains footing, recover. Preserve blue color and full detail, NOT a red silhouette.
Row4 MOVE: dynamic low bounding/galloping with wing-assisted motion, six distinct leg poses, same ground baseline.
Row5 DEATH: staggers, legs buckle, head sinks, collapses on side, settled blue body, settled fading-looking pose but preserve actual pixels. No white silhouette.
Transparent background alpha; no shadows touching neighboring cells, no floor/background scenery, no words, no numbers, no HUD, no grid. Main requirement is clean uniformly aligned production sprites with enough separation for exact slicing.

## WaterAtlas.png

Use case: stylized-concept. Asset type: production sprite atlas of a BLUE WATER CANNON and its impact for a pixel-art dragon game. Exactly FOUR equal columns and THREE equal rows, 1024x768 canvas, each cell 256x256, generous transparent separation and objects entirely inside each cell. Pixel-art hard-edged cyan navy white clusters, high contrast, energetic compressed WATER not fire or electricity. True transparent alpha background, no text, numbers, panels or lines.
ROW 1: Four consecutive looping frames of a dense blue water cannon projectile travelling RIGHT: bright rounded compressed foamy head on right, turbulent tapered water trail behind to left, width210px height90px, center(128,128) every frame. Silhouette legible at small size.
ROW 2: first four frames of an impact splash centered in each cell: small white cyan impact core, bursting crown with radial droplets, expanding curling splash, larger shattered water crown.
ROW 3: final four frames of same impact: dispersing radial droplets and broken rings, falling spray, sparse dissipating droplets, last tiny drops. Expansion then dissipation across all8 impact frames. Each splash max220px extent with at least18px padding. Everything blue water with foam highlights, no dark background, no glowing rectangular haze. No dragon or characters.



## WaterAtlas final refinement

Selected final: exec-0b59fcaf-857d-45a6-9532-8debf8da5c59.png (copied into the resource WaterAtlas.png, same grid). Built-in image_gen edit with transparent background.

Edit this production water-effect sprite atlas to correct animation consistency and clipping while keeping its cyan-blue pixel-art water design.
MUST preserve exact FOUR-column THREE-row equal-cell layout (12 cells), transparent alpha, no text/grid.
Each cell has a completely clear transparent safety margin of at least 12% on all four edges. No droplet, foam, glow or pixel may touch or cross a cell border. Remove noisy blue speckles and halos outside the clean intentional effect silhouettes.
Row1: FOUR looping frames of the SAME water cannon bolt. All four MUST have identical overall width and height, same centered position, same head location, and the same scale, occupying only 76% of each cell width and 30% height. Rounded white foam head points RIGHT at normalized cell x=.85, head tip consistently at x=.88; tail trails to left x=.12. Change interior swirling foam only; do not progressively grow the projectile. All noses rounded and UNCLIPPED.
Rows2-3: eight sequential frames of a water impact splash, the bright impact origin at normalized cell (x=.5,y=.68 measured from top), same baseline across all eight. Small impact -> expands into crown -> disperses into a few droplets. Max effect width/height76%cell. Clean transparent edges, vivid navy/cyan/white pixel clusters. Final2frames sparse clear droplets.
No characters, environment, titles or drop shadows. Exact even grid on truly transparent background.


## User-requested 512px conversion (2026-09-26)

The user requested the same programmatic conversion as Gwangma. The original1374x1145 DragonAtlas.png remains unchanged. Tools/Art/BlueDragonSpriteSheetExporter.cs and ExportBlueDragon512.ps1 extract each229x229 grid cell and use nearest-neighbour pixel-center sampling to512x512. Five3072x512 sheets (six frames each) are stored beside the original: Idle, Attack, BeAttacked, Run, Death. No new painted detail is claimed. Runtime reads these sheets with512x512 center-pivot slices; timing and generated-art display scale0.7 are preserved.

The restored original Assets/Unit/BlueDragon/Idle.png remains the normal idle. Converted Resources/Idle.png is only the generated-art fallback when no original controller is available. WaterAtlas, SFX and original Unit assets are unchanged. This is a resolution/packaging change, not the separate Animator Controller workflow migration discussed with the user.