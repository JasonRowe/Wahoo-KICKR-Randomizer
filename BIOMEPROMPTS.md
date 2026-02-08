# Biome & Transition Prompts (Nano Banana)

Use `Images\\readme_image.PNG` and `Images\\scenic_background.png` as style references.

## Global Prompt Prefix
`2D side-scrolling game background, clean flat vector style, soft gradients, rounded clouds, layered distant mountains and rolling hills, color palette and lighting matched to the reference images, no characters, no UI, no text, no logos. Perfectly seamless horizontal tiling; left and right edges must match exactly. Consistent flat brown dirt road band at the bottom with the same height and color across all images.`

## Biome Prompts
1. Mountain
`[PREFIX] Mountain biome: cool blue mountains with snow caps, layered ridgelines, crisp air feel, gentle green foothills, sparse pine silhouettes in mid-ground, bright blue sky with soft white clouds.`

2. Plain
`[PREFIX] Plain biome: wide rolling green hills, lighter warm greens, scattered small trees far in the distance, open sky, calm pastoral feel.`

3. Desert
`[PREFIX] Desert biome: warm tan/orange palette, mesas and buttes in the distance, subtle dunes, sparse scrub, sky slightly warmer near horizon.`

4. Ocean
`[PREFIX] Ocean biome: coastal cliffs or headlands, distant calm ocean band, soft teal/blue palette, light sea haze near horizon, a few seabird-like cloud shapes (not birds).`

## Transition Prompts
1. Mountain -> Plain
`[PREFIX] Transition scene blending mountain biome on the left into plain biome on the right, smoothly shifting palette from cool blues to softer greens across the width, no abrupt edge changes.`

2. Plain -> Desert
`[PREFIX] Transition scene blending plain biome on the left into desert biome on the right, greens fading into warm sands, gentle mesas appearing toward the right.`

3. Desert -> Ocean
`[PREFIX] Transition scene blending desert biome on the left into ocean biome on the right, sand tones fading into coastal blues and a visible ocean band on the far right.`

4. Ocean -> Mountain
`[PREFIX] Transition scene blending ocean biome on the left into mountain biome on the right, coastal haze on the left, cool mountains emerging to the right.`

## Suggested Settings
- Mode: Image-to-image using both references.
- Denoise/strength: 0.35-0.45 to keep the style consistent.
- Resolution: 2048x1024 or 4096x1024 for horizontal tiles (or match existing asset size).
- Negative prompt: photorealistic, 3D, noisy texture, text, UI, characters, buildings, cars, animals.

## Seam Check
- Offset the image by 50 percent horizontally to inspect the seam.
- If a seam is visible, do a light edge blend or tiny paint fixes.
