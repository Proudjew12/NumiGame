using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class QuickBuild
    {
        private const string RootPath = "DreamScripts/Quick Build";
        private const int RootMenuPriorityBase = 60040;

        static QuickBuild()
        {
            DreamScriptRegistry.Register("Quick Build/Linux", BuildLinux, priority: 40, isEnabled: ValidateBuildLinux);
            DreamScriptRegistry.Register("Quick Build/macOS", BuildMac, priority: 41, isEnabled: ValidateBuildMac);
        }

        [MenuItem(RootPath + "/Linux", false, RootMenuPriorityBase + 1)]
        private static void BuildLinux()
        {
            BuildForTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64, "Linux", ".x86_64");
        }

        [MenuItem(RootPath + "/Linux", true, RootMenuPriorityBase + 1)]
        private static bool ValidateBuildLinux()
        {
            return CanBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        }

        [MenuItem(RootPath + "/macOS", false, RootMenuPriorityBase + 2)]
        private static void BuildMac()
        {
            BuildForTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX, "macOS", ".app");
        }

        [MenuItem(RootPath + "/macOS", true, RootMenuPriorityBase + 2)]
        private static bool ValidateBuildMac()
        {
            return CanBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
        }

        private static bool CanBuildTarget(BuildTargetGroup group, BuildTarget target)
        {
            return GetEnabledScenes().Length > 0 && BuildPipeline.IsBuildTargetSupported(group, target);
        }

        private static void BuildForTarget(BuildTargetGroup group, BuildTarget target, string outputFolder, string extension)
        {
            var scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Quick Build", "No enabled scenes in Build Settings.", "OK");
                return;
            }

            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                EditorUtility.DisplayDialog("Quick Build", target + " build support is not installed in this Unity editor.", "OK");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                var switched = EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
                if (!switched)
                {
                    EditorUtility.DisplayDialog("Quick Build", "Failed to switch active build target to " + target + ".", "OK");
                    return;
                }
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var buildsFolder = Path.Combine(projectRoot, "Builds", outputFolder);
            Directory.CreateDirectory(buildsFolder);

            var productName = SanitizeFileName(PlayerSettings.productName);
            if (string.IsNullOrWhiteSpace(productName))
            {
                productName = "Build";
            }

            var outputPath = Path.Combine(buildsFolder, productName + extension);

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            Debug.Log("[QuickBuild] Building " + target + " -> " + outputPath);
            var report = BuildPipeline.BuildPlayer(buildOptions);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[QuickBuild] Success: " + summary.totalSize + " bytes in " + summary.totalTime + ".");
                EditorUtility.RevealInFinder(outputPath);
            }
            else
            {
                Debug.LogError("[QuickBuild] Failed: " + summary.result + ". See Console/Editor log for details.");
            }
        }

        private static string[] GetEnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = value.Trim();
            foreach (var character in invalidChars)
            {
                sanitized = sanitized.Replace(character, '_');
            }

            return sanitized;
        }
    }
}
