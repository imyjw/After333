# Gwangma generated-art provenance

> Latest integration (2026-09-26): active PNGs/clips now live in Assets/Unit/BlueDragon or Assets/Unit/Gwangma and use the card's Animator Controller. Previous generated atlas/intermediate files were archived under Tools/Art/Source. Descriptions of former Resources-based character playback below are historical. See unit_animation_authoring.md for the current workflow.

Created 2026-09-26 using the built-in image_gen tool with transparent_background=true. User supplied Assets/Unit/Gwangma/{Attack,BeAttacked,Death,Idle,Run}.png as references and authorized reimagining. Idle.png was loaded and used as the character identity reference. No original source files were replaced; the original generated atlas PNGs are preserved unmodified. The user later explicitly requested a programmatic 512px frame conversion; the derived runtime files are documented below.

## Character generations

1. `exec-62ddeaaa-133d-4d49-8eb0-77658c2a661b.png`: first character sheet. Prompt requested a crisp pixel-art adult stylized chibi dual-sword wuxia martial artist, dark-plum high ponytail and red ribbon, crimson jacket, pale trousers, black boots, two thin silver curved blades. Five rows of six frames: idle/attack/hurt/run/death, right-facing, consistent body, no red hurt silhouette or white death silhouette. Attack frames contain anticipation, two crossing diagonal cuts, recovery. Rejected for slicing: art crossed mathematical cell boundaries.
2. `exec-3b2eb4d2-b451-42c8-a6ab-633088b503e4.png`: spacing correction. Requested smaller figures and transparent gutters while preserving all poses. Some equal-cell row boundaries still crossed art; not used in game.
3. **Final** `exec-b06237e8-77be-4bd6-b48f-376f098ee376.png`: requested much smaller figures (max40% eachcell) in large transparent gutters, same30poses/colors/order. Final1374×1145. Pixel inspection confirmed custom144×144 windows cover all30figures with no occupied edge pixels. The original PNG remains unmodified. Its initial runtime Sprite.Create windows were x=column*229+45, topY=82/311/543/758/960, width/height=144. The subsequent 512px conversion uses these same windows; the current runtime reads separate sheets instead.

## Cross-slash generation

**Final** `exec-1bb90756-df0b-4da0-a8eb-f6a797059ca2.png`, 1774×887. Prompt: eight-frame game VFX atlas of one sharply crossed X magical dual-sword slash; crisp pixels, silver-white core, restrained crimson/magenta edges, tapered curved blade trails and small sparks; no character/gore/scene/text; transparent4×2 equalcells with centeredintersection and generousgutters. Sequence: firstdiagonal, opposingdiagonal, completeX, compactflash, widening/fadingtrails, fragmentation, faintcross, finalsparks.

All8logicalcells have transparent boundaries. Dimensions need not be exactinteger multiples; Sprite.Create uses texture-relative floating rectangles. Import keeps full NPOT size and Point filtering. VFX pivot is centered on the shared lowered body-impact anchor.

## Integration notes

Files live under Assets/Project333/Resources/Project333/Presentation/Gwangma/. Image creation did not change card art, race, upgrade schedule, stats, costs, attack type or multi-hit rules. Old source files remain available. Native Unity render captures, when produced by validation, are verification of in-engine display and are distinct from these generator previews.

## User-approved 512px packaging (2026-09-26)

User explicitly chose to keep the current art and convert it programmatically, rather than redraw it. Tools/Art/ExportGwangma512.ps1 and GwangmaSpriteSheetExporter.cs crop the verified 144x144 windows and use nearest-neighbour pixel-center sampling to fill 512x512 frames. Outputs are Idle.png, Attack.png, BeAttacked.png, Run.png, Death.png, each3072x512 with six horizontal frames. No new painted details are claimed. Transparent frame margins, relative scale/baseline and playback timing are retained; BoardScale remains1. The original atlas and CrossSlashAtlas are unchanged. Importer metadata for the five derived textures was generated with Unity TextureImporter APIs.

Edit these five sheets directly going forward. Re-export is only for recovering the original generated poses, and refuses existing output files without -Force to avoid overwriting manual edits.