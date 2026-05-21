using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class GitHubTools
    {
        private const string RootPath = "DreamScripts/GitHub";
        private const int RootMenuPriorityBase = 60025;
        private const int DialogTextLimit = 4200;

        static GitHubTools()
        {
            DreamScriptRegistry.Register("GitHub/Upload", Upload, priority: 22);
            DreamScriptRegistry.Register("GitHub/Import", Import, priority: 23);
            DreamScriptRegistry.Register("GitHub/EnterRepo", EnterRepo, priority: 24);
        }

        [MenuItem(RootPath + "/Upload", false, RootMenuPriorityBase)]
        private static void Upload()
        {
            SaveUnityState();

            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var branch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(branch))
            {
                Show("GitHub Upload blocked", "Could not detect the current Git branch.");
                return;
            }

            var lfs = RunGit("lfs install --local");
            if (!lfs.Success)
            {
                ShowGitFailure("GitHub Upload blocked", "Git LFS is not available to Unity.", lfs);
                return;
            }

            var add = RunGit("add -A");
            if (!add.Success)
            {
                ShowGitFailure("GitHub Upload failed", "Git could not stage the project changes.", add);
                return;
            }

            var status = RunGit("status --porcelain");
            if (!status.Success)
            {
                ShowGitFailure("GitHub Upload failed", "Git could not read the project status.", status);
                return;
            }

            var committed = false;
            var changeSummary = string.Empty;
            if (!string.IsNullOrWhiteSpace(status.Output))
            {
                changeSummary = RunGit("diff --cached --name-status").Output.Trim();
                var message = "Auto save " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var commit = RunGit("commit -m " + Quote(message));
                if (!commit.Success)
                {
                    ShowGitFailure("GitHub Upload failed", "Git could not create the auto commit.", commit);
                    return;
                }

                committed = true;
            }

            var push = RunGit("push -u origin " + Quote(branch));
            if (!push.Success)
            {
                ShowGitFailure("GitHub Upload failed", "Git could not push to GitHub.", push);
                return;
            }

            var head = RunGit("rev-parse --short HEAD").Output.Trim();
            var messageText =
                "Uploaded to GitHub.\n\n" +
                "Branch: " + branch + "\n" +
                "Commit: " + head + "\n" +
                "Auto commit created: " + (committed ? "Yes" : "No changes to commit") + "\n\n";

            if (!string.IsNullOrWhiteSpace(changeSummary))
            {
                messageText += "Uploaded changes:\n" + changeSummary;
            }
            else
            {
                messageText += "Your branch was pushed with the current committed state.";
            }

            Show("GitHub Upload complete", messageText);
        }

        [MenuItem(RootPath + "/Import", false, RootMenuPriorityBase + 1)]
        private static void Import()
        {
            SaveUnityState();

            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var branch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(branch))
            {
                Show("GitHub Import blocked", "Could not detect the current Git branch.");
                return;
            }

            if (!CreateSafetyCommitIfNeeded())
            {
                return;
            }

            var oldHead = RunGit("rev-parse HEAD").Output.Trim();
            var fetch = RunGit("fetch origin " + Quote(branch));
            if (!fetch.Success)
            {
                ShowGitFailure("GitHub Import failed", "Git could not fetch from GitHub.", fetch);
                return;
            }

            var remoteRef = "origin/" + branch;
            var remoteHead = RunGit("rev-parse --verify " + Quote(remoteRef));
            if (!remoteHead.Success)
            {
                ShowGitFailure("GitHub Import failed", "GitHub does not have branch '" + branch + "'.", remoteHead);
                return;
            }

            var incomingCommits = RunGit("log --oneline HEAD.." + Quote(remoteRef)).Output.Trim();
            var incomingFiles = RunGit("diff --name-status HEAD.." + Quote(remoteRef)).Output.Trim();

            if (string.IsNullOrWhiteSpace(incomingCommits) && string.IsNullOrWhiteSpace(incomingFiles))
            {
                Show("GitHub Import complete", "Already up to date.\n\nBranch: " + branch);
                return;
            }

            var pull = RunGit("pull --rebase origin " + Quote(branch));
            if (!pull.Success)
            {
                ShowGitFailure(
                    "GitHub Import stopped",
                    "Git could not import cleanly. Your project was not overwritten. Resolve the Git conflict, then run Import again.",
                    pull);
                return;
            }

            AssetDatabase.Refresh();

            var newHead = RunGit("rev-parse HEAD").Output.Trim();
            var appliedFiles = RunGit("diff --name-status " + Quote(oldHead) + ".." + Quote(newHead)).Output.Trim();

            var message =
                "Imported from GitHub.\n\n" +
                "Branch: " + branch + "\n\n" +
                "Commits imported:\n" + EmptyFallback(incomingCommits, "No commit list available.") + "\n\n" +
                "Files updated:\n" + EmptyFallback(appliedFiles, incomingFiles);

            Show("GitHub Import complete", message);
        }

        [MenuItem(RootPath + "/EnterRepo", false, RootMenuPriorityBase + 2)]
        private static void EnterRepo()
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var remote = RunGit("remote get-url origin");
            if (!remote.Success)
            {
                ShowGitFailure("GitHub EnterRepo failed", "Git could not read the origin remote.", remote);
                return;
            }

            var repoUrl = ToGitHubBrowserUrl(remote.Output.Trim());
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                Show(
                    "GitHub EnterRepo blocked",
                    "The origin remote does not look like a GitHub repository:\n\n" + remote.Output.Trim());
                return;
            }

            Application.OpenURL(repoUrl);
            Show("GitHub EnterRepo", "Opened:\n" + repoUrl);
        }

        private static bool CreateSafetyCommitIfNeeded()
        {
            var status = RunGit("status --porcelain");
            if (!status.Success)
            {
                ShowGitFailure("GitHub Import failed", "Git could not read the project status.", status);
                return false;
            }

            if (string.IsNullOrWhiteSpace(status.Output))
            {
                return true;
            }

            var confirm = EditorUtility.DisplayDialog(
                "GitHub Import",
                "You have local changes that are not committed yet.\n\n" +
                "Import will first create a local safety commit, then pull from GitHub.\n\nContinue?",
                "Create Safety Commit",
                "Cancel");

            if (!confirm)
            {
                return false;
            }

            var add = RunGit("add -A");
            if (!add.Success)
            {
                ShowGitFailure("GitHub Import failed", "Git could not stage the local safety commit.", add);
                return false;
            }

            var message = "Auto save before GitHub import " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var commit = RunGit("commit -m " + Quote(message));
            if (!commit.Success)
            {
                ShowGitFailure("GitHub Import failed", "Git could not create the local safety commit.", commit);
                return false;
            }

            return true;
        }

        private static bool EnsureGitRepository()
        {
            if (!Directory.Exists(Path.Combine(ProjectRoot, ".git")))
            {
                Show("GitHub tool blocked", "This project is not a Git repository yet.");
                return false;
            }

            return true;
        }

        private static bool EnsureOriginRemote()
        {
            var remote = RunGit("remote get-url origin");
            if (remote.Success && !string.IsNullOrWhiteSpace(remote.Output))
            {
                return true;
            }

            Show(
                "GitHub remote missing",
                "No GitHub remote named 'origin' is configured yet.\n\n" +
                "Add one from the terminal first:\n\n" +
                "git remote add origin <your-github-repo-url>\n" +
                "git push -u origin main\n\n" +
                "After that, DreamScripts/GitHub/Upload and Import will work.");
            return false;
        }

        private static string GetCurrentBranch()
        {
            var result = RunGit("rev-parse --abbrev-ref HEAD");
            if (!result.Success)
            {
                return string.Empty;
            }

            var branch = result.Output.Trim();
            return branch == "HEAD" ? string.Empty : branch;
        }

        private static void SaveUnityState()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static GitResult RunGit(string arguments)
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            AddLocalBinToPath(startInfo);

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data != null)
                    {
                        error.AppendLine(args.Data);
                    }
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    return new GitResult(-1, string.Empty, ex.Message);
                }

                return new GitResult(process.ExitCode, output.ToString(), error.ToString());
            }
        }

        private static void AddLocalBinToPath(ProcessStartInfo startInfo)
        {
            var path = startInfo.EnvironmentVariables["PATH"] ?? string.Empty;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localBin = Path.Combine(home, ".local", "bin");
            if (!path.Contains(localBin))
            {
                startInfo.EnvironmentVariables["PATH"] = localBin + Path.PathSeparator + path;
            }
        }

        private static string ProjectRoot
        {
            get
            {
                return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ToGitHubBrowserUrl(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl))
            {
                return string.Empty;
            }

            var url = remoteUrl.Trim();
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - 4);
            }

            if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            {
                return "https://github.com/" + url.Substring("git@github.com:".Length);
            }

            if (url.StartsWith("ssh://git@github.com/", StringComparison.OrdinalIgnoreCase))
            {
                return "https://github.com/" + url.Substring("ssh://git@github.com/".Length);
            }

            if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                return url.Replace("http://github.com/", "https://github.com/");
            }

            return string.Empty;
        }

        private static void ShowGitFailure(string title, string context, GitResult result)
        {
            Show(title, context + "\n\n" + result.Combined);
        }

        private static void Show(string title, string message)
        {
            var clipped = message;
            if (clipped.Length > DialogTextLimit)
            {
                clipped = clipped.Substring(0, DialogTextLimit) + "\n\n... output clipped. See Console for full details.";
            }

            UnityEngine.Debug.Log("[GitHubTools] " + title + "\n" + message);
            EditorUtility.DisplayDialog(title, clipped, "OK");
        }

        private readonly struct GitResult
        {
            public GitResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }
            public bool Success => ExitCode == 0;

            public string Combined
            {
                get
                {
                    var combined = (Output + "\n" + Error).Trim();
                    return string.IsNullOrWhiteSpace(combined)
                        ? "git exited with code " + ExitCode
                        : combined;
                }
            }
        }
    }
}
