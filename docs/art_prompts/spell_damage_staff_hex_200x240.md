# Simplified spell damage badge, 200 x 240

Built-in image generation, no CLI image model fallback. The original complex icon was redrawn with fewer details; it was not merely resized.

Final asset: Assets/Project333/Resources/Project333/UI/SpellDamageStaffHex_200x240.png

Actual output verified: 200 x 240 pixels, 32-bit ARGB PNG, transparent exterior corners, 55,247 bytes. No damage numbers are baked in. Previous assets and runtime code remain unchanged.

The image tool returned a larger cutout (1180 x 1333). Its simplified artwork was exported to the exact requested canvas using aspect-preserving high-quality bicubic resampling and transparent letterboxing in System.Drawing. The image was NOT natively generated at 200 x 240. No image API fallback was used.

## Design prompt

Use case: stylized-concept game UI sprite redesign. Redraw the referenced hexagonal magic staff damage badge specifically for a TINY 200 pixel wide by 240 pixel high final game icon. Final requested canvas is exactly 200x240 pixels, portrait 5:6 ratio. Do NOT merely shrink the reference: simplify the design for small-screen readability. Keep a full upright point-top six-sided gold hexagonal border surrounding a diagonal magic staff, with the whole staff contained inside. Plain thick warm-gold rim with only one simple light bevel and one dark outline, NO corner gemstones, NO engraved details, NO metal scratches or intricate facets. Flat very dark charcoal/blue interior with NO stone/leather texture or ornamental pattern. Inside, a broad dark-bronze short staff running bottom-left to top-right, a compact recognizable blue crystal head at upper right in a simple gold mount, few chunky clear highlights, bold clean silhouette. The staff is a prominent midground emblem. The center of the hexagon must remain relatively calm because a large live white damage number will later be overlaid in front of the STAFF at badge center, not confined to the blue jewel. Do NOT include any number or lettering. Crisp small pixel-game UI style with deliberate clean pixel clusters, limited palette and hard clean edges, no photorealistic textures, no fuzzy shading, no microdetail, no glow, no particles. One badge only. Fits fully inside the 200x240 canvas with about 6 pixels transparent padding. Transparent PNG cutout: TRUE alpha=0 outside the hexagonal badge; no painted checkerboard, no white background. Opaque dark interior stays. If your native render is larger, maintain exactly this 5:6 composition and LOW-detail 200x240 visual design so the final export is readable.

## Intermediate export prompt

Remove the background and export this game icon at EXACTLY 200 pixels wide by 240 pixels high. Keep the simple hexagonal gold badge, dark opaque interior and blue-crystal staff intact. Delete the unwanted gray-and-white checkerboard outside the badge. Return a real transparent-alpha PNG cutout, not a drawing of transparency, no white or checkered backdrop. Final image file pixel dimensions must be 200 x 240. Fit entire badge within the canvas, preserve proportions and a little transparent margin. Crisp readable small-size icon, no added texture, numbers, letters or decorations.

## Successful background-removal prompt

Remove the background of this image. Return the isolated hexagonal badge as a transparent-background PNG cutout. All space outside the hexagonal gold outline must be transparent, with real alpha=0, not an illustrated transparency grid. Keep the dark filled interior and the staff inside it intact. Do not include the gray-and-white checkerboard. Do not add numbers or text. This is background removal, not a new drawing.

