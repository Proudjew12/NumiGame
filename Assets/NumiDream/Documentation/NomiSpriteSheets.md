# Nomi Sprite Sheets

Generated from the GIF source references in `Assets/NumiDream/Art/Characters/Nomi/SourceGifs`.

| Sheet | Source GIF | Frames | Columns | Cell Size | Sheet Size |
| --- | --- | ---: | ---: | --- | --- |
| `Nomi-Idle-Sprite-Sheet.png` | `Nomi-Idle-With-Eyes.gif` | 6 | 6 | 828 x 1910 | 4968 x 1910 |
| `Nomi-Walk-Sprite-Sheet.png` | `Nomi-Walk-With-Eyes.gif` | 5 | 5 | 916 x 2495 | 4580 x 2495 |
| `Nomi-Jump-Sprite-Sheet.png` | `Nomi-Jump-Color.gif` | 9 | 5 | 1068 x 2203 | 5340 x 4406 |

The sheets are stored in `Assets/NumiDream/Art/Characters/Nomi/SpriteSheets`.

`NumiDreamArtImportSettings.cs` imports these sheets as multi-sprite textures. Use `DreamScripts > Sprite Sheet Previewer` to preview the animation without creating clips.

The same previewer can also create:

- frame PNGs under `Assets/NumiDream/Animations/Nomi/Frames`
- animation clips under `Assets/NumiDream/Animations/Nomi/Clips`
- `NomiAnimator.controller` under `Assets/NumiDream/Animations/Nomi/Controllers`

The generated controller uses:

- `Speed` float: `Idle` to `Walk` when greater than `0.1`, `Walk` to `Idle` when less than `0.1`
- `Jump` trigger: `Any State` to `Jump`, then back to `Idle` or `Walk` depending on `Speed`

Preview frame names:

- Idle frames: `Nomi-Idle-Frame-01` through `Nomi-Idle-Frame-06`
- Walk frames: `Nomi-Walk-Frame-01` through `Nomi-Walk-Frame-05`
- Jump frames: `Nomi-Jump-Frame-01` through `Nomi-Jump-Frame-09`

The original GIFs are kept as source references. If you need actual Unity animation clips later, slice the sheets in Unity's Sprite Editor or build clips from the frame data above.
