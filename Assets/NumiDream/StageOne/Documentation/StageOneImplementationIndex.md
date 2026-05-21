# Numi Stage One Implementation Index

Source document: `NumiGameStageOnePlan.pdf`

## Design Target

Stage 1 is a 2.5D platformer sequence about insecurity. The mood should be melancholic, fragile, and emotional, not horror. The whole world should have a soft constant screen shake until the final bridge made from stone hands locks into place.

Primary interaction language: controller only. Most special interactions are driven by left-stick rotation, where the game detects rotation direction and speed and converts it into a float progress value from 0 to 100.

## Core Systems To Build

- `ControllerInput`: gamepad-only movement, jump, interact, and left-stick rotation tracking.
- `StickRotationMeter`: converts left-stick circular motion into signed rotation speed and 0-100 interaction progress.
- `StageScreenShake`: soft stage-wide shake with adjustable intensity, disabled after puzzle 4 completes.
- `MemoryFragmentCollectible`: trigger pickup for floating color fragments, with emission or glow progression.
- `MemoryReveal`: end-of-level alpha-mask reveal, driven by stick rotation progress.
- `StageEventFlow`: puzzle-complete events that open or activate the next traversal route.

## Puzzle Flow

1. Crumbling hand bridge: player touches hand-platform bridge, then bridge pieces enable physics sequentially. This teaches constant movement.
2. Bicycle wheel: player rotates left stick to roll a heavy wheel into a ground socket. Once placed, the wheel becomes a temporary platform but pushes the player away if they linger.
3. Bicycle bell rhythm: player pushes a rock to reveal a visual rhythm clue, then presses A/X in a configurable short/short/long timing pattern. Correct input lowers stone platforms.
4. Stone hands bridge: intense left-stick rotation raises two stone hands from the void. When locked, the stage shake stops.
5. Bicycle goal and memory reveal: collected color fragments get pulled into the bicycle, then the player rotates the stick to expand the alpha mask and reveal the memory image.

## Imported Asset Map

| Folder | Purpose |
| --- | --- |
| `Assets/NumiDream/StageOne/Art/ParallaxLayers` | Numbered back/mid/front layout slices for the 2.5D parallax scene. |
| `Assets/NumiDream/StageOne/Art/Environment` | Larger environment pieces, moon/cloud/fog/ground elements, houses, trees, wheels, and UI prompt references. |
| `Assets/NumiDream/StageOne/Art/Puzzles/Crumbling-Hand/Frames` | `Crumbling-Hand-Piece-01` to `Crumbling-Hand-Piece-32`, usable as breakable bridge hand pieces or animation/reference slices. |
| `Assets/NumiDream/StageOne/Art/Puzzles/Crumbling-Hand/Reference` | Hand bridge reference image. |
| `Assets/NumiDream/StageOne/Art/Puzzles/Obstacles` | Rock, platform, wheel, and obstacle concept sprites for stage puzzle blocking. |
| `Assets/NumiDream/StageOne/Art/Puzzles/Bicycle-Bell` | Bell and rope art used by the rhythm puzzle. |
| `Assets/NumiDream/StageOne/Art/Puzzles/Final-Stone-Hands` | Final raised-hands bridge art. |
| `Assets/NumiDream/StageOne/Art/Collectibles/Memory-Fragment` | Floating memory fragment/color collectible sprite sheet. |
| `Assets/NumiDream/StageOne/Art/Collectibles/Frames/Memory-Fragments` | Individual memory fragment animation frames. |
| `Assets/NumiDream/Art/Characters/Nomi/SourceGifs` | Source GIF animation references for idle, walk, and jump. |
| `Assets/NumiDream/Art/Characters/Nomi/SpriteSheets` | Generated idle, walk, and jump sprite sheets for previewing and later animation-clip setup. |

## Suggested Unity Setup

- Sorting layers: `Background`, `Midground`, `Gameplay`, `Foreground`, `UI`.
- Camera: orthographic, with Cinemachine later if needed. Keep `SceneStageOne` as the Stage-One assembly scene.
- Physics: use 2D colliders/rigidbodies for the platformer unless a specific object needs 3D depth.
- Parallax: parent parallax sprites under empty transforms named by depth, such as `Parallax_Back_00`, `Parallax_Mid_00`, and `Parallax_Front_00`.
- Memory reveal: keep sketch and color reveal as separate sprite layers. Drive a mask radius shader/material property from stick rotation progress.

## Notes From Asset Indexing

- Imported source art count: 119 PNGs and 5 GIF animation references.
- The PNG art is automatically imported as `Sprite (2D and UI)` by `Assets/NumiDream/Editor/NumiDreamArtImportSettings.cs`.
- GIF files are kept as source references. Unity does not treat GIFs as runtime sprite animations by default, so convert them to sprite sheets or frame sequences when building Nomi's animator.
- Nomi idle, walk, and jump GIFs have generated sprite sheets documented in `NomiSpriteSheets.md`.
- The original `Game-Nomi-new` source folder was removed after verifying the copied asset tree.
