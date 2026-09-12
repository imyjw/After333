# Spell damage badge: thin-border 256 x 256 variant

Built-in image generation edit, no CLI image model fallback. Intended change: thinner outer gold hexagonal band; retain the existing staff, sapphire and top/bottom accents. The 30-35 percent thickness reduction was a prompt target, not a measured geometric guarantee.

Final asset: Assets/Project333/Resources/Project333/UI/SpellDamageStaffHex_256x256_ThinBorder.png

Verified 256 x 256 pixels, 32-bit ARGB PNG, transparent exterior corners; 88,060 bytes. Original files preserved; no card artwork or runtime rendering changes.

The built-in tool returned a 1254 x 1254 cutout. Exported proportionally to 256 x 256 with high-quality bicubic resampling using System.Drawing. No damage number is baked in.

## Edit prompt

Precise-object-edit of this game UI badge. Make ONE CHANGE ONLY: reduce the thickness of the OUTER SIX-SIDED GOLD HEXAGON FRAME to about 65-70 percent of its current thickness (about 30-35 percent thinner). Narrow the gold band's inner edge so more of the dark interior is visible; keep the outside hexagon silhouette, badge proportions, centered placement and margin unchanged. Retain its beveled antique-gold sculpted finish and thin dark outline. Preserve the existing top and bottom curved gold accents. Do not reduce or change any of the gold parts on the STAFF itself. Keep the blue sapphire staff, crystal, shaft, handle and pommel exactly the same size, angle, shape, position, color and level of detail. Keep the dark blue-charcoal inset texture and shading. No other redesign, no extra ornament, no labels, no numbers or text. The image is used at 256x256 pixels; keep crisp small-icon readability. Maintain real transparent alpha OUTSIDE the entire badge. No white backdrop, no gray background, no painted checkerboard. Use the attached image as the exact edit target, not loose inspiration.

## Initial background extraction

Remove the background of this image. Return the isolated hexagonal badge as a transparent-background PNG cutout. All space outside the hexagonal gold outline must be transparent, with real alpha=0, not an illustrated transparency grid. Keep the THIN gold frame, dark filled interior and the staff inside it intact. Do not thicken the gold frame. Do not include the gray-and-white checkerboard. Do not add numbers or text. This is background removal, not a new drawing.

## Final background removal

Remove the white background from this image and return a transparent PNG cutout of the badge. Keep the badge itself exactly as it is. Actual transparency with alpha=0 outside the badge, not a white or checkerboard background. This is only background removal, not a redraw.

