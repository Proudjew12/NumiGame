using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class RenderPipelineGuard
    {
        private const string RootPath = "DreamScripts/Project";
        private const int RootMenuPriorityBase = 60040;
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        private static bool s_checkedThisSession;

        static RenderPipelineGuard()
        {
            DreamScriptRegistry.Register("Project/Fix Render Pipeline", FixRenderPipeline, priority: 40);
            EditorApplication.delayCall += EnsureRenderPipelineOnStartup;
        }

        [MenuItem(RootPath + "/Fix Render Pipeline", false, RootMenuPriorityBase)]
        private static void FixRenderPipeline()
        {
            EnsureRenderPipeline(showDialog: true);
        }

        private static void EnsureRenderPipelineOnStartup()
        {
            if (s_checkedThisSession)
            {
                return;
            }

            s_checkedThisSession = true;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                s_checkedThisSession = false;
                EditorApplication.delayCall += EnsureRenderPipelineOnStartup;
                return;
            }

            EnsureRenderPipeline(showDialog: false);
        }

        private static void EnsureRenderPipeline(bool showDialog)
        {
            var pcPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PcPipelinePath);
            if (pcPipeline == null)
            {
                var message = "Could not find the required Universal Render Pipeline asset:\n" + PcPipelinePath;
                Debug.LogError("[DreamScripts] " + message);

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Render Pipeline missing", message, "OK");
                }

                return;
            }

            var changed = false;

            if (GraphicsSettings.defaultRenderPipeline != pcPipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pcPipeline;
                EditorUtility.SetDirty(GraphicsSettings.GetGraphicsSettings());
                changed = true;
            }

            changed |= EnsureQualityPipelineAssignments(pcPipeline);

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[DreamScripts] Render Pipeline fixed: PC_RPAsset is assigned in Graphics and Quality settings.");
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Render Pipeline",
                    changed
                        ? "Fixed. PC_RPAsset is now assigned in Graphics and Quality settings."
                        : "Already good. PC_RPAsset is assigned.",
                    "OK");
            }
        }

        private static bool EnsureQualityPipelineAssignments(RenderPipelineAsset pcPipeline)
        {
            var changed = false;
            var originalQuality = QualitySettings.GetQualityLevel();
            var qualityNames = QualitySettings.names;

            for (var i = 0; i < qualityNames.Length; i++)
            {
                var currentPipeline = QualitySettings.GetRenderPipelineAssetAt(i);
                var isPcQuality = string.Equals(qualityNames[i], "PC", StringComparison.OrdinalIgnoreCase);

                if (!isPcQuality && currentPipeline != null)
                {
                    continue;
                }

                if (currentPipeline == pcPipeline)
                {
                    continue;
                }

                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pcPipeline;
                changed = true;
            }

            if (originalQuality >= 0 && originalQuality < qualityNames.Length)
            {
                QualitySettings.SetQualityLevel(originalQuality, applyExpensiveChanges: false);
            }

            if (changed)
            {
                EditorUtility.SetDirty(QualitySettings.GetQualitySettings());
            }

            return changed;
        }
    }
}
