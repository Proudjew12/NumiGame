# NumiGameOriginal

NumiGameOriginal is a Unity 6 2D/2.5D platformer project for the NumiDream game. The current playable content is focused on Stage One: a melancholic dream sequence built from hand-drawn/parallax art, Nomi player movement, platforming puzzles, collectibles, camera zoom triggers, respawn points, and editor automation tools for faster iteration.

## Unity Version

Open this project with:

- Unity Editor: `6000.3.8f1`
- Revision: `6000.3.8f1 (1c7db571dde0)`

This version is recorded in `ProjectSettings/ProjectVersion.txt`.

## Repository Contents

The repository is source-control ready and should contain the complete Unity source project:

- `Assets/` - all game assets, scenes, scripts, art, animations, editor tools, settings assets, and plugin DLLs that belong to the project.
- `Packages/manifest.json` and `Packages/packages-lock.json` - the exact Unity Package Manager dependency list and resolved package versions.
- `ProjectSettings/` - Unity project settings, build scene list, render pipeline setup, tags/layers, input settings, physics settings, quality settings, and version control settings.
- `.gitignore` - ignores generated/local Unity folders and machine-specific files.
- `.gitattributes` - keeps Unity YAML files text/merge-friendly and tracks binary assets with Git LFS.
- `README.md` - this project guide.

Do not commit generated local folders such as `Library/`, `Temp/`, `Logs/`, `Builds/`, `UserSettings/`, `.vscode/`, generated `.csproj` files, or generated solution files. Unity and the IDE recreate those locally.

## Git And LFS

Git LFS is required because the project contains PNGs, PDFs, animation/art source files, and other binary media. Before cloning or pulling on a new machine, install Git LFS:

```bash
git lfs install
git clone <repo-url>
cd NumiGameOriginal
git lfs pull
```

Tracked binary patterns include PNG, JPG, PSD, PSB, PDF, WAV, MP3, MP4, FBX, OBJ, ZIP, and other common media formats through `.gitattributes`.

There is no remote configured in this local repository until `origin` is added. The DreamScripts GitHub tools expect a normal Git remote named `origin`.

## Project Settings

Important settings currently verified from Unity and `ProjectSettings`:

| Setting | Value |
| --- | --- |
| Product name | `NumiGameOriginal` |
| Company name | `DefaultCompany` |
| Bundle version | `0.1.0` |
| Active build target | `StandaloneLinux64` |
| Color space | `Linear` |
| Render pipeline | Universal Render Pipeline |
| Standalone scripting backend | `Mono2x` |
| API compatibility | `.NET Standard 2.0` |
| Input package | Unity Input System package is installed and `InputSystem_Actions.inputactions` is in the project |
| Enabled build scene | `Assets/NumiDream/StageOne/SceneStageOne.unity` |

Supported game targets for now:

- Linux x86_64
- macOS

## Main Packages

The project is locked through `Packages/packages-lock.json`. Key dependencies from `Packages/manifest.json` include:

- Universal Render Pipeline `17.3.0`
- Input System `1.18.0`
- 2D Animation `13.0.5`
- 2D PSD Importer `12.0.2`
- 2D Aseprite Importer `3.0.2`
- Addressables `2.11.1`
- Cinemachine `3.1.6`
- AI Navigation `2.0.10`
- Splines `2.8.4`
- Test Framework `1.6.0`
- Unity MCP package from `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`
- Tri Inspector `1.15.1` from OpenUPM
- PrimeTween `1.3.3` from OpenUPM

The OpenUPM scoped registry is configured for:

- `com.codewriter.triinspector`
- `com.kyrylokuzyk.primetween`

## Game Structure

Primary project folders:

- `Assets/NumiDream/Scripts/Nomi` - player movement and Nomi visual/animation helpers.
- `Assets/NumiDream/Scripts/Collectables` - collectible pickup behavior.
- `Assets/NumiDream/StageOne/Scripts` - Stage One runtime systems.
- `Assets/NumiDream/StageOne/Art` - organized Stage One environment, parallax, puzzle, collectible, and background art.
- `Assets/NumiDream/Animations/Nomi` - generated Nomi animation frames, clips, and animator controller.
- `Assets/NumiDream/Documentation` - project documentation for Nomi sprite sheets.
- `Assets/NumiDream/StageOne/Documentation` - Stage One design/index documentation.
- `Assets/DreamScripts/Editor` - custom Unity editor toolbar and maintenance tools.
- `Assets/Settings` - render pipeline and related settings assets.
- `Assets/Plugins/Roslyn` - Roslyn editor DLLs, configured as editor-only so they do not enter player builds.

## Current Runtime Systems

The active Stage One scene currently uses these runtime scripts:

- `NomiPlayerMovement` - 2D player movement.
- `NomiAnimatorDriver` and `NomiProceduralAnimator` - Nomi animation/visual behavior.
- `NomiChildTransformLock` - keeps child transform behavior stable for the player setup.
- `CameraFollow2D` - orthographic camera follow with runtime offset support.
- `ParallaxLayer2D` - parallax background movement.
- `Collectable` - memory fragment pickup behavior.
- `PlayerRespawnController` and `RespawnPoint` - respawn system.
- `CrumblingHandBridgeCollapse` - crumbling-hand bridge behavior.
- `BicycleWheelPuzzle` and `BicycleWheelTargetPoint` - bicycle wheel puzzle path and target points.
- `FloatingIslandImpact2D` - floating island impact behavior.
- `PuzzleThreeBellIslands` - bell islands puzzle behavior.
- `PuzzleFourRopeHandsLift` - rope/hands puzzle behavior.
- `TriggerZoomZoneBoundary` and `CameraZoomMarkerTrigger` - camera zoom regions and markers.

Unused runtime scripts were removed during cleanup. The current scene/prefab scan reports zero missing scripts and zero unattached runtime scripts under `Assets/NumiDream`.

## DreamScripts Tools

The custom `DreamScripts` Unity toolbar includes:

- `Reload`
- `Backup/CreateBackup`
- `Backup/RestoreBackup`
- `GitHub/Upload`
- `GitHub/Import`
- `GitHub/EnterRepo`
- `Quick Build/Linux`
- `Quick Build/macOS`
- `Refresh Fallback/Enable`
- `Refresh Fallback/Disable`
- `Refresh Fallback/Refresh Now`
- `AutoSaveTime` actions
- `Cleanup/Clean Temp Logs`
- `Sprite Sheet Preview`
- `CopyComponent` copy/paste actions

The backup tool writes project backups under:

```text
~/NumiDream/BackUp/
```

Build outputs are written under:

```text
<project-root>/Builds/
```

Both folders are local workflow output and should not be committed.

## Opening The Project

1. Clone the repository with Git LFS enabled.
2. Open the repository root in Unity Hub using Unity `6000.3.8f1`.
3. Let Unity restore packages from `Packages/manifest.json`.
4. Open `Assets/NumiDream/StageOne/SceneStageOne.unity`.
5. Check the Unity Console for package import or compile errors.

If package resolution fails, run:

```text
Window > Package Manager
```

Then allow Unity to restore packages. The OpenUPM registry is already declared in `Packages/manifest.json`.

## Building

Use the Unity editor toolbar:

```text
DreamScripts > Quick Build
```

Available targets:

- Linux
- macOS

The enabled scene list contains:

```text
Assets/NumiDream/StageOne/SceneStageOne.unity
```

Build outputs are intentionally ignored by Git.

## Validation Status

Last local validation after repository cleanup:

- Unity compile: clean
- Unity Console: 0 errors, 0 warnings
- Scene validation: clean
- Missing scripts: 0
- Unattached runtime scripts under `Assets/NumiDream`: 0
- EditMode tests: 1 passed, 0 failed
- Play Mode smoke test: 0 errors, 0 warnings
- Linux and macOS build compatibility previously verified clean in this repo

## Source-Control Rules For Future Work

Commit:

- `Assets/**`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/**`
- `.gitignore`
- `.gitattributes`
- documentation such as `README.md`

Do not commit:

- `Library/`
- `Temp/`
- `Logs/`
- `Builds/`
- `UserSettings/`
- generated IDE files such as `.csproj`, `.sln`, `.slnx`
- local editor folders such as `.vscode/`, `.idea/`, `.vs/`
- crash dumps and generated logs

Keep `.meta` files committed for every Unity asset. They preserve GUIDs and prevent broken scene/prefab references.
