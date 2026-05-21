using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class CleanupTools
    {
        private const string RootPath = "DreamScripts/Cleanup/Clean Temp Logs";
        private const int RootMenuPriority = 60055;

        static CleanupTools()
        {
            DreamScriptRegistry.Register("Cleanup/Clean Temp Logs", CleanTempLogs, priority: 65);
        }

        [MenuItem(RootPath, false, RootMenuPriority)]
        private static void CleanTempLogs()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var logsPath = Path.Combine(projectRoot, "Logs");
            var tempPath = Path.Combine(projectRoot, "Temp");

            var filesDeleted = 0;
            var dirsDeleted = 0;
            var deleteErrors = 0;

            CleanDirectoryContents(logsPath, ref filesDeleted, ref dirsDeleted, ref deleteErrors);
            CleanDirectoryContents(tempPath, ref filesDeleted, ref dirsDeleted, ref deleteErrors);

            AssetDatabase.Refresh();

            var message =
                "Cleanup finished\n" +
                "Files deleted: " + filesDeleted + "\n" +
                "Folders deleted: " + dirsDeleted + "\n" +
                "Delete errors: " + deleteErrors;

            Debug.Log("[Cleanup] " + message.Replace("\n", " | "));
            EditorUtility.DisplayDialog("DreamScripts Cleanup", message, "OK");
        }

        private static void CleanDirectoryContents(string directoryPath, ref int filesDeleted, ref int dirsDeleted, ref int deleteErrors)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            try
            {
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        filesDeleted++;
                    }
                    catch (Exception)
                    {
                        deleteErrors++;
                    }
                }

                var directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);
                Array.Sort(directories, (left, right) => right.Length.CompareTo(left.Length));

                foreach (var directory in directories)
                {
                    try
                    {
                        Directory.Delete(directory, false);
                        dirsDeleted++;
                    }
                    catch (Exception)
                    {
                        deleteErrors++;
                    }
                }
            }
            catch (Exception exception)
            {
                deleteErrors++;
                Debug.LogWarning("[Cleanup] Failed to clean " + directoryPath + ": " + exception.Message);
            }
        }
    }
}
