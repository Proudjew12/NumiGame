# DreamScripts

Unity editor toolbar tools live here.

## What is included

- `Editor/DreamToolbar.cs`: adds a `DreamScripts` dropdown to the Unity top toolbar.
- `Editor/DreamScriptRegistry.cs`: registry used by toolbar actions.
- `Editor/DreamDefaultTools.cs`: registers `Reload` and toolbar separators.
- `Editor/SpriteSheetPreviewer.cs`: adds `DreamScripts/Sprite Sheet Preview` for large sprite-sheet animation previews, frame export, clip creation, and Nomi animator generation.
- `Editor/CopyPlayComponent.cs`: adds `DreamScripts/CopyComponent/Copy`, `.../Paste`, and `DreamScripts/RightClickPlayer` for moving the Player with a right-click.
- `Editor/AutoSaveTIme.cs`: adds `DreamScripts/AutoSaveTime` actions for `Save Now`, `Status`, `Off`, and `On/2..10 Minutes`.
- `Editor/QuickBuild.cs`: adds quick build menu actions for Linux and macOS.
- `Editor/CleanupTools.cs`: adds `DreamScripts/Cleanup/Clean Temp Logs`.
- `Editor/VersionControlEasy.cs`: adds `DreamScripts/Backup/CreateBackup` and `.../RestoreBackup`.
- `Editor/GitHubTools.cs`: adds `DreamScripts/GitHub/SetRepo`, branch-picking `Upload`, branch-picking `Import`, and `EnterRepo`.
- `Editor/RefreshFallback.cs`: forces `AssetDatabase.Refresh()` on startup/focus as a fallback when file watching fails.

## How to add your next tool

Create an editor script under `Assets/DreamScripts/Editor` and register it:

```csharp
using UnityEditor;
using DreamScripts.EditorTools;

[InitializeOnLoad]
internal static class MyDreamTool
{
    static MyDreamTool()
    {
        DreamScriptRegistry.Register("My Tools/Do Something", Run, priority: 200);
    }

    private static void Run()
    {
        EditorUtility.DisplayDialog("DreamScripts", "Tool executed.", "OK");
    }
}
```
