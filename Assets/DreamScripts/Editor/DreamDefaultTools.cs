using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class DreamDefaultTools
    {
        private const int RootMenuPriority = 60010;

        static DreamDefaultTools()
        {
            DreamScriptRegistry.Register("Reload", Reload, priority: 10);
            DreamScriptRegistry.RegisterSeparator("AfterBackup", priority: 30);
            DreamScriptRegistry.RegisterSeparator("AfterCleanup", priority: 70);
        }

        [MenuItem("DreamScripts/Reload", false, RootMenuPriority)]
        private static void ReloadFromMenu()
        {
            Reload();
        }

        private static void Reload()
        {
            EnsureTempFolder();
            EditorApplication.delayCall += () =>
            {
                EnsureTempFolder();
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
                Debug.Log("[DreamScripts] Reload completed.");
            };
        }

        private static void EnsureTempFolder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return;
            }

            Directory.CreateDirectory(Path.Combine(projectRoot, "Temp"));
        }
    }
}
