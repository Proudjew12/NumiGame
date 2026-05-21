using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class BackupTools
    {
        private const string ToolName = "Backup";
        private const string RootPath = "DreamScripts/Backup";
        private const int RootMenuPriorityBase = 60020;
        private static readonly string[] BackupExcludedRootFolders =
        {
            "Library",
            "Temp",
            "Logs",
            "Obj",
            "Build",
            "Builds",
            "UserSettings",
            "MemoryCaptures",
            ".git",
            ".plastic",
            ".vscode",
            ".idea"
        };

        static BackupTools()
        {
            DreamScriptRegistry.Register("Backup/CreateBackup", CreateBackup, priority: 20);
            DreamScriptRegistry.Register("Backup/RestoreBackup", RestoreBackup, priority: 21);
        }

        [MenuItem(RootPath + "/CreateBackup", false, RootMenuPriorityBase)]
        private static void CreateBackup()
        {
            if (!Confirm(
                    "CreateBackup",
                    "This will save the open scenes/assets and create a full project backup zip.\n\n"
                    + "Backup folder:\n" + BackupRoot + "\n\nContinue?"))
            {
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            var backup = CreateProjectBackup("CreateBackup");
            if (!backup.Success)
            {
                ShowResult("CreateBackup failed", backup.Output);
                return;
            }

            ShowResult(
                "CreateBackup complete",
                "Backup created:\n" + backup.Path);
        }

        [MenuItem(RootPath + "/RestoreBackup", false, RootMenuPriorityBase + 1)]
        private static void RestoreBackup()
        {
            var backupPath = EditorUtility.OpenFilePanel("RestoreBackup", BackupRoot, "zip");

            if (string.IsNullOrEmpty(backupPath))
            {
                return;
            }

            if (!File.Exists(backupPath))
            {
                ShowResult("RestoreBackup failed", "Backup file was not found:\n" + backupPath);
                return;
            }

            if (!Confirm(
                    "RestoreBackup",
                    "This will restore the selected backup into the current project.\n\n"
                    + "Selected backup:\n" + backupPath + "\n\n"
                    + "Before restoring, a safety backup of the current project will be created.\n\nContinue?"))
            {
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            var safetyBackup = CreateProjectBackup("BeforeRestoreBackup");
            if (!safetyBackup.Success)
            {
                ShowResult("RestoreBackup blocked", "Safety backup failed, so restore was not started.\n\n" + safetyBackup.Output);
                return;
            }

            var restore = RestoreProjectBackup(backupPath);
            if (!restore.Success)
            {
                ShowResult(
                    "RestoreBackup failed",
                    restore.Output + "\n\nSafety backup created before restore:\n" + safetyBackup.Path);
                return;
            }

            ShowResult(
                "RestoreBackup complete",
                "Restored backup:\n" + backupPath + "\n\n"
                + "Safety backup created before restore:\n" + safetyBackup.Path + "\n\n"
                + "Unity refreshed the project after restore.");
        }

        private static BackupResult CreateProjectBackup(string actionName)
        {
            Directory.CreateDirectory(BackupRoot);

            var projectName = new DirectoryInfo(ProjectRoot).Name;
            var backupPath = Path.Combine(
                BackupRoot,
                projectName + "_" + actionName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");

            EditorUtility.DisplayProgressBar(
                ToolName,
                "Creating project backup: " + actionName + "...",
                0.25f);

            try
            {
                using (var fileStream = new FileStream(backupPath, FileMode.CreateNew, FileAccess.ReadWrite))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    AddDirectoryToBackup(archive, new DirectoryInfo(ProjectRoot), ProjectRoot);
                }

                return new BackupResult(true, backupPath, "Backup created.");
            }
            catch (Exception exception)
            {
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
                catch
                {
                    // Keep the original backup error as the useful message.
                }

                return new BackupResult(false, backupPath, exception.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void AddDirectoryToBackup(ZipArchive archive, DirectoryInfo directory, string rootPath)
        {
            foreach (var childDirectory in directory.GetDirectories())
            {
                if (ShouldExcludeFromBackup(childDirectory, rootPath))
                {
                    continue;
                }

                AddDirectoryToBackup(archive, childDirectory, rootPath);
            }

            foreach (var file in directory.GetFiles())
            {
                var relativePath = MakeBackupEntryPath(file.FullName, rootPath);
                var entry = archive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.Fastest);

                using (var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var output = entry.Open())
                {
                    input.CopyTo(output);
                }
            }
        }

        private static bool ShouldExcludeFromBackup(DirectoryInfo directory, string rootPath)
        {
            var parent = directory.Parent;
            if (parent == null ||
                !string.Equals(
                    Path.GetFullPath(parent.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var excludedFolder in BackupExcludedRootFolders)
            {
                if (string.Equals(directory.Name, excludedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string MakeBackupEntryPath(string fullPath, string rootPath)
        {
            var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return Path.GetFullPath(fullPath).Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/');
        }

        private static BackupResult RestoreProjectBackup(string backupPath)
        {
            var tempBackupPath = Path.Combine(
                Path.GetTempPath(),
                "NumiDreamRestore_" + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                File.Copy(backupPath, tempBackupPath, overwrite: false);

                EditorUtility.DisplayProgressBar(
                    ToolName,
                    "Preparing project restore...",
                    0.05f);

                DeleteRestorableProjectContents();

                using (var fileStream = new FileStream(tempBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    var entryCount = Math.Max(archive.Entries.Count, 1);
                    for (var index = 0; index < archive.Entries.Count; index++)
                    {
                        var entry = archive.Entries[index];
                        EditorUtility.DisplayProgressBar(
                            ToolName,
                            "Restoring " + entry.FullName,
                            0.1f + 0.85f * index / entryCount);

                        ExtractBackupEntry(entry, ProjectRoot);
                    }
                }

                AssetDatabase.Refresh();
                return new BackupResult(true, backupPath, "Backup restored.");
            }
            catch (Exception exception)
            {
                return new BackupResult(false, backupPath, exception.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                try
                {
                    if (File.Exists(tempBackupPath))
                    {
                        File.Delete(tempBackupPath);
                    }
                }
                catch
                {
                    // A temp cleanup failure is not useful enough to hide the restore result.
                }
            }
        }

        private static void DeleteRestorableProjectContents()
        {
            var projectDirectory = new DirectoryInfo(ProjectRoot);

            foreach (var directory in projectDirectory.GetDirectories())
            {
                if (ShouldExcludeFromBackup(directory, ProjectRoot))
                {
                    continue;
                }

                FileUtil.DeleteFileOrDirectory(directory.FullName);
            }

            foreach (var file in projectDirectory.GetFiles())
            {
                FileUtil.DeleteFileOrDirectory(file.FullName);
            }
        }

        private static void ExtractBackupEntry(ZipArchiveEntry entry, string targetRoot)
        {
            var cleanEntryName = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(cleanEntryName) || cleanEntryName.EndsWith("/", StringComparison.Ordinal))
            {
                return;
            }

            var targetPath = Path.GetFullPath(Path.Combine(targetRoot, cleanEntryName));
            var normalizedRoot = Path.GetFullPath(targetRoot).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!targetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Backup contains an unsafe path:\n" + entry.FullName);
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            using (var input = entry.Open())
            using (var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }
        }

        private static bool Confirm(string title, string message)
        {
            return EditorUtility.DisplayDialog(title, TrimForDialog(message), "Continue", "Cancel");
        }

        private static void ShowResult(string title, string message)
        {
            EditorUtility.DisplayDialog(title, TrimForDialog(message), "OK");
            UnityEngine.Debug.Log("[" + ToolName + "] " + title + "\n" + message);
        }

        private static string TrimForDialog(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            const int maxLength = 4000;
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "\n\n...trimmed...";
        }

        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static string BackupRoot
        {
            get
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(userProfile))
                {
                    userProfile = Directory.GetParent(ProjectRoot).FullName;
                }

                return Path.Combine(userProfile, "NumiDream", "BackUp");
            }
        }

        private sealed class BackupResult
        {
            public BackupResult(bool success, string path, string output)
            {
                Success = success;
                Path = path;
                Output = output;
            }

            public bool Success { get; }
            public string Path { get; }
            public string Output { get; }
        }
    }
}
