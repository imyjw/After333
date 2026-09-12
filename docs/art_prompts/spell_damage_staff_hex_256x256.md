# Spell damage badge: developed 256 x 256 variant

Method: built-in image generation, then built-in background extraction. No CLI image model fallback.

Final file: Assets/Project333/Resources/Project333/UI/SpellDamageStaffHex_256x256.png

Verified: 256 x 256 pixels; 32-bit ARGB PNG; transparent exterior; 86,338 bytes. Damage numbers intentionally absent. Previous icons and game code unchanged.

The badge was newly generated with moderate sculpted detail between the prior minimal and ornate references. The tool returned a 1312 x 1199 cutout; System.Drawing exported it proportionally to the exact 256 x 256 transparent canvas using high-quality bicubic resampling. It was not natively rendered at 256 x 256.

## Design prompt

Use case: stylized-concept. Create one polished fantasy-game magic-damage UI badge, redesigned for final use at 256 x 256 pixels. Reference 1 is TOO PLAIN, with a flat empty border and toy-like staff. Reference 2 is TOO BUSY, with excessive fine texture. Aim for a tasteful MIDPOINT: clearly richer, more dimensional and crafted than reference 1, but much cleaner and readable at small size than reference 2. Square composition. A front-facing upright point-top HEXAGON with six straight sides encloses the entire magic staff. Thick antique-gold and dark bronze layered beveled frame, several broad sculpted facets, subtle curved metal accents only at the top and bottom vertices; no jewels on the six corners. An opaque charcoal-blue inset with gentle recessed depth and a small amount of broad tonal variation, NOT coarse stone texture. A chunky diagonal wizard scepter across it from lower-left to upper-right. Moderately detailed gold prongs cradle a deep sapphire-blue crystal with 5-7 large clear facets, restrained cyan rim light inside the gem, dark iron shaft with gold collars and one sculpted pommel. This must look like carefully hand-painted fantasy inventory art matching a metal gauntlet stat icon, NOT a flat vector illustration and NOT hyperrealism. Bold readable silhouette, deliberate chunky pixel-game-friendly highlights, few broad shadows, restrained ornamental work; no filigree, tiny scratches, noisy microtexture, confetti, flames, smoke or glow outside the silhouette. The badge's geometric center, across the staff shaft rather than on the gem, will later receive a large white damage number as LIVE UI on top. DO NOT draw numbers, letters, runes or a separate number plaque. Keep the staff clearly readable behind that future text position. Icon occupies roughly 90 percent of a square canvas, whole frame visible with a small transparent margin. Deliver transparent PNG with a REAL alpha channel OUTSIDE the hexagon, no painted checkerboard and no background scene. Target final file 256x256; maintain a square layout and design all detail to remain visible at 256x256.

## Background extraction prompt

Remove the background of this image. Return the isolated hexagonal badge as a transparent-background PNG cutout. All space outside the hexagonal gold outline must be transparent, with real alpha=0, not an illustrated transparency grid. Keep the dark filled interior and the staff inside it intact. Do not include the gray-and-white checkerboard. Do not add numbers or text. This is background removal, not a new drawing.

