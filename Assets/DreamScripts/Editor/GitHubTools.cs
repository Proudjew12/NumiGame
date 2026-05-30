using System;
using System.Collections.Generic;
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
            DreamScriptRegistry.Register("GitHub/SetRepo", SetRepo, priority: 24);
            DreamScriptRegistry.Register("GitHub/EnterRepo", EnterRepo, priority: 25);
        }

        [MenuItem(RootPath + "/Upload", false, RootMenuPriorityBase)]
        private static void Upload()
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Upload blocked", "Could not detect the current Git branch.");
                return;
            }

            var branchListWarning = RefreshOriginBranchList(blockOnFailure: false);
            var choices = GetBranchChoices(currentBranch, remoteOnly: false);
            BranchPickerWindow.Open(
                "GitHub Upload",
                "Choose the GitHub branch that will receive the current saved project state. Select an existing branch or type a new branch name.",
                "Upload",
                "New or existing GitHub branch",
                currentBranch,
                choices,
                allowManualBranch: true,
                warning: branchListWarning,
                onSelected: UploadToBranch);
        }

        private static void UploadToBranch(string targetBranch)
        {
            SaveUnityState();

            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            targetBranch = NormalizeBranchInput(targetBranch);
            if (!IsValidBranchName(targetBranch, out var branchError))
            {
                Show("GitHub Upload blocked", branchError);
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
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

            var push = PushToRemoteBranch(targetBranch, currentBranch, forceWithLease: false);
            if (!push.Success)
            {
                if (IsAuthenticationFailure(push))
                {
                    ShowAuthenticationHelp("GitHub Upload blocked", push);
                    return;
                }

                if (IsNonFastForwardFailure(push))
                {
                    push = ReplaceRemoteBranch(targetBranch, currentBranch, push);
                    if (!push.Success)
                    {
                        if (IsAuthenticationFailure(push))
                        {
                            ShowAuthenticationHelp("GitHub Upload blocked", push);
                            return;
                        }

                        ShowGitFailure("GitHub Upload failed", "Git could not push to GitHub.", push);
                        return;
                    }
                }
                else
                {
                    ShowGitFailure("GitHub Upload failed", "Git could not push to GitHub.", push);
                    return;
                }
            }

            var head = RunGit("rev-parse --short HEAD").Output.Trim();
            var messageText =
                "Uploaded to GitHub.\n\n" +
                "Local branch: " + currentBranch + "\n" +
                "GitHub branch: " + targetBranch + "\n" +
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
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Import blocked", "Could not detect the current Git branch.");
                return;
            }

            var branchListWarning = RefreshOriginBranchList(blockOnFailure: true);
            if (!string.IsNullOrWhiteSpace(branchListWarning))
            {
                return;
            }

            var choices = GetBranchChoices(currentBranch, remoteOnly: true);
            BranchPickerWindow.Open(
                "GitHub Import",
                "Choose the GitHub branch to import into this local project. Import creates a safety backup first, then restores the current local branch from the selected GitHub branch.",
                "Import",
                "GitHub branch name",
                currentBranch,
                choices,
                allowManualBranch: true,
                warning: string.Empty,
                onSelected: ImportFromBranch);
        }

        private static void ImportFromBranch(string branch)
        {
            SaveUnityState();
            var openScenePaths = GetOpenScenePaths();
            var activeScenePath = EditorSceneManager.GetActiveScene().path;

            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            branch = NormalizeBranchInput(branch);
            if (!IsValidBranchName(branch, out var branchError))
            {
                Show("GitHub Import blocked", branchError);
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Import blocked", "Could not detect the current Git branch.");
                return;
            }

            if (!CreateSafetyCommitIfNeeded(out var createdSafetyCommit))
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

            var remoteHeadHash = remoteHead.Output.Trim();
            var localOnlyCommits = RunGit("log --oneline " + Quote(remoteRef) + "..HEAD").Output.Trim();
            var incomingCommits = RunGit("log --oneline HEAD.." + Quote(remoteRef)).Output.Trim();
            var incomingFiles = RunGit("diff --name-status HEAD.." + Quote(remoteRef)).Output.Trim();
            var headDiffersFromRemote = !string.Equals(oldHead, remoteHeadHash, StringComparison.Ordinal);

            if (!headDiffersFromRemote && !createdSafetyCommit)
            {
                Show(
                    "GitHub Import complete",
                    "Already up to date.\n\n" +
                    "Local branch: " + currentBranch + "\n" +
                    "GitHub branch: " + branch);
                return;
            }

            var backupBranch = string.Empty;
            if (createdSafetyCommit || headDiffersFromRemote)
            {
                backupBranch = CreateImportBackupBranch(currentBranch);
                if (string.IsNullOrWhiteSpace(backupBranch))
                {
                    return;
                }
            }

            var scenesTemporarilyClosed = OpenTemporarySceneBeforeExternalRestore(openScenePaths);
            var importResult = RunGit("reset --hard " + Quote(remoteRef));
            if (!importResult.Success)
            {
                if (scenesTemporarilyClosed)
                {
                    ReloadOpenScenesFromDisk(openScenePaths, activeScenePath);
                }

                ShowGitFailure(
                    "GitHub Import stopped",
                    "Git could not restore the project from GitHub." +
                    (string.IsNullOrWhiteSpace(backupBranch)
                        ? string.Empty
                        : "\n\nYour local state is still available on backup branch:\n" + backupBranch),
                    importResult);
                return;
            }

            AssetDatabase.Refresh();
            var reloadedSceneCount = ReloadOpenScenesFromDisk(openScenePaths, activeScenePath);

            var newHead = RunGit("rev-parse HEAD").Output.Trim();
            var appliedFiles = RunGit("diff --name-status " + Quote(oldHead) + ".." + Quote(newHead)).Output.Trim();

            var message =
                "Imported from GitHub.\n\n" +
                "Local branch: " + currentBranch + "\n" +
                "GitHub branch: " + branch + "\n\n" +
                "Commits imported:\n" + EmptyFallback(incomingCommits, "No commit list available.") + "\n\n" +
                "Files updated:\n" + EmptyFallback(appliedFiles, incomingFiles);

            if (!string.IsNullOrWhiteSpace(localOnlyCommits))
            {
                message += "\n\nLocal commits moved to backup:\n" + localOnlyCommits;
            }

            if (!string.IsNullOrWhiteSpace(backupBranch))
            {
                message += "\n\nLocal safety backup branch:\n" + backupBranch;
            }

            if (reloadedSceneCount > 0)
            {
                message += "\n\nReloaded open scenes from GitHub: " + reloadedSceneCount;
            }

            Show("GitHub Import complete", message);
        }

        [MenuItem(RootPath + "/SetRepo", false, RootMenuPriorityBase + 2)]
        private static void SetRepo()
        {
            if (!EnsureGitRepository())
            {
                return;
            }

            var clipboard = (EditorGUIUtility.systemCopyBuffer ?? string.Empty).Trim();
            var repoUrl = ToGitHubBrowserUrl(clipboard);
            if (string.IsNullOrWhiteSpace(repoUrl))
            {
                Show(
                    "GitHub SetRepo",
                    "Copy your GitHub repository URL first, then press SetRepo again.\n\n" +
                    "Accepted examples:\n" +
                    "https://github.com/user/repo.git\n" +
                    "https://github.com/user/repo\n" +
                    "git@github.com:user/repo.git");
                return;
            }

            var currentRemote = RunGit("remote get-url origin");
            if (currentRemote.Success && !string.IsNullOrWhiteSpace(currentRemote.Output))
            {
                var replace = EditorUtility.DisplayDialog(
                    "GitHub SetRepo",
                    "Replace the current origin remote?\n\n" +
                    "Current:\n" + currentRemote.Output.Trim() + "\n\n" +
                    "New:\n" + clipboard,
                    "Replace",
                    "Cancel");

                if (!replace)
                {
                    return;
                }
            }

            var command = currentRemote.Success && !string.IsNullOrWhiteSpace(currentRemote.Output)
                ? "remote set-url origin " + Quote(clipboard)
                : "remote add origin " + Quote(clipboard);

            var setRemote = RunGit(command);
            if (!setRemote.Success)
            {
                ShowGitFailure("GitHub SetRepo failed", "Git could not save the origin remote.", setRemote);
                return;
            }

            Show(
                "GitHub SetRepo complete",
                "Origin remote saved:\n" + clipboard + "\n\n" +
                "Website URL:\n" + repoUrl + "\n\n" +
                "You can now use GitHub/EnterRepo and GitHub/Upload.");
        }

        [MenuItem(RootPath + "/EnterRepo", false, RootMenuPriorityBase + 3)]
        private static void EnterRepo()
        {
            if (!EnsureGitRepository())
            {
                return;
            }

            var remote = RunGit("remote get-url origin");
            if (!remote.Success)
            {
                var createUrl = "https://github.com/new?name=" + Uri.EscapeDataString(new DirectoryInfo(ProjectRoot).Name);
                Application.OpenURL(createUrl);
                Show(
                    "GitHub remote missing",
                    "No GitHub remote named 'origin' is configured yet.\n\n" +
                    "I opened GitHub's new repository page.\n\n" +
                    "After creating the repo, copy its GitHub URL and press:\n" +
                    "DreamScripts/GitHub/SetRepo\n\n" +
                    "Then press EnterRepo again.");
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

        private static bool CreateSafetyCommitIfNeeded(out bool createdSafetyCommit)
        {
            createdSafetyCommit = false;

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
                "Import will first create a local safety commit and backup branch, then restore this branch to GitHub's version.\n\nContinue?",
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

            createdSafetyCommit = true;
            return true;
        }

        private static string CreateImportBackupBranch(string branch)
        {
            var safeBranchName = string.IsNullOrWhiteSpace(branch)
                ? "branch"
                : branch.Replace('/', '-').Replace('\\', '-');
            var backupBranch = "backup/import-" + safeBranchName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backup = RunGit("branch " + Quote(backupBranch) + " HEAD");

            if (!backup.Success)
            {
                ShowGitFailure(
                    "GitHub Import failed",
                    "Git could not create a safety backup branch before restoring from GitHub.",
                    backup);
                return string.Empty;
            }

            return backupBranch;
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
                "Create the GitHub repository, copy its repository URL, then press:\n\n" +
                "DreamScripts/GitHub/SetRepo\n\n" +
                "After that, DreamScripts/GitHub/Upload, Import, and EnterRepo will work.");
            return false;
        }

        private static string RefreshOriginBranchList(bool blockOnFailure)
        {
            var fetch = RunGit("fetch origin --prune");
            if (fetch.Success)
            {
                return string.Empty;
            }

            if (blockOnFailure)
            {
                ShowGitFailure(
                    "GitHub Import failed",
                    "Git could not refresh the GitHub branch list before import.",
                    fetch);
                return "blocked";
            }

            return "Could not refresh the GitHub branch list. Showing locally known branches.\n\n" + Clip(fetch.Combined);
        }

        private static GitResult PushToRemoteBranch(string targetBranch, string currentBranch, bool forceWithLease)
        {
            var upstreamFlag = string.Equals(targetBranch, currentBranch, StringComparison.Ordinal)
                ? "-u "
                : string.Empty;
            var forceFlag = forceWithLease ? "--force-with-lease " : string.Empty;
            return RunGit("push " + forceFlag + upstreamFlag + "origin " + Quote("HEAD:refs/heads/" + targetBranch));
        }

        private static GitResult ReplaceRemoteBranch(string targetBranch, string currentBranch, GitResult originalPushFailure)
        {
            var replace = EditorUtility.DisplayDialog(
                "GitHub Upload needs sync",
                "GitHub already has commits that are not in this local project.\n\n" +
                "This usually happens when the GitHub repository was created with an initial README/license commit.\n\n" +
                "Upload can replace the GitHub branch '" + targetBranch + "' with this Unity project using --force-with-lease. This keeps the local project as the source of truth and refuses to overwrite GitHub if it changed after the last fetch.\n\n" +
                "Original Git message:\n" + Clip(originalPushFailure.Combined),
                "Replace GitHub Branch",
                "Cancel");

            if (!replace)
            {
                return originalPushFailure;
            }

            var fetch = RunGit("fetch origin " + Quote(targetBranch));
            if (!fetch.Success)
            {
                return fetch;
            }

            return PushToRemoteBranch(targetBranch, currentBranch, forceWithLease: true);
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

        private static List<BranchChoice> GetBranchChoices(string currentBranch, bool remoteOnly)
        {
            var byName = new Dictionary<string, BranchChoice>(StringComparer.Ordinal);

            foreach (var branch in ReadBranchNames("for-each-ref --format=%(refname:short) refs/heads"))
            {
                AddBranchChoice(byName, branch, existsLocal: true, existsRemote: false, currentBranch: currentBranch);
            }

            foreach (var branch in ReadBranchNames("for-each-ref --format=%(refname:short) refs/remotes/origin"))
            {
                var normalized = NormalizeRemoteBranchName(branch);
                AddBranchChoice(byName, normalized, existsLocal: false, existsRemote: true, currentBranch: currentBranch);
            }

            if (!remoteOnly)
            {
                AddBranchChoice(byName, currentBranch, existsLocal: true, existsRemote: false, currentBranch: currentBranch);
            }

            var choices = new List<BranchChoice>();
            foreach (var choice in byName.Values)
            {
                if (remoteOnly && !choice.ExistsRemote)
                {
                    continue;
                }

                choices.Add(choice);
            }

            choices.Sort(CompareBranchChoices);
            return choices;
        }

        private static List<string> ReadBranchNames(string gitArguments)
        {
            var result = RunGit(gitArguments);
            var branches = new List<string>();
            if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            {
                return branches;
            }

            var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var branch = line.Trim();
                if (!string.IsNullOrWhiteSpace(branch))
                {
                    branches.Add(branch);
                }
            }

            return branches;
        }

        private static void AddBranchChoice(
            Dictionary<string, BranchChoice> branches,
            string branch,
            bool existsLocal,
            bool existsRemote,
            string currentBranch)
        {
            branch = NormalizeBranchInput(branch);
            if (string.IsNullOrWhiteSpace(branch) || string.Equals(branch, "HEAD", StringComparison.Ordinal))
            {
                return;
            }

            if (!branches.TryGetValue(branch, out var choice))
            {
                choice = new BranchChoice(branch);
                branches.Add(branch, choice);
            }

            choice.ExistsLocal = choice.ExistsLocal || existsLocal;
            choice.ExistsRemote = choice.ExistsRemote || existsRemote;
            choice.IsCurrent = string.Equals(branch, currentBranch, StringComparison.Ordinal);
        }

        private static int CompareBranchChoices(BranchChoice a, BranchChoice b)
        {
            if (a.IsCurrent != b.IsCurrent)
            {
                return a.IsCurrent ? -1 : 1;
            }

            if (a.ExistsRemote != b.ExistsRemote)
            {
                return a.ExistsRemote ? -1 : 1;
            }

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRemoteBranchName(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
            {
                return string.Empty;
            }

            branch = branch.Trim();
            if (branch.StartsWith("origin/", StringComparison.Ordinal))
            {
                branch = branch.Substring("origin/".Length);
            }

            return branch == "HEAD" ? string.Empty : branch;
        }

        private static string NormalizeBranchInput(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
            {
                return string.Empty;
            }

            branch = branch.Trim();
            const string headsPrefix = "refs/heads/";
            const string remotePrefix = "refs/remotes/origin/";

            if (branch.StartsWith(remotePrefix, StringComparison.Ordinal))
            {
                branch = branch.Substring(remotePrefix.Length);
            }
            else if (branch.StartsWith(headsPrefix, StringComparison.Ordinal))
            {
                branch = branch.Substring(headsPrefix.Length);
            }
            else if (branch.StartsWith("origin/", StringComparison.Ordinal))
            {
                branch = branch.Substring("origin/".Length);
            }

            return branch.Trim();
        }

        private static bool IsValidBranchName(string branch, out string error)
        {
            branch = NormalizeBranchInput(branch);
            if (string.IsNullOrWhiteSpace(branch))
            {
                error = "Choose or type a branch name.";
                return false;
            }

            if (string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                error = "HEAD is not a branch name. Choose a real branch.";
                return false;
            }

            if (branch.StartsWith("-", StringComparison.Ordinal) ||
                branch.StartsWith("/", StringComparison.Ordinal) ||
                branch.EndsWith("/", StringComparison.Ordinal) ||
                branch.EndsWith(".", StringComparison.Ordinal) ||
                branch.Contains("//") ||
                branch.Contains("..") ||
                branch.Contains("@{"))
            {
                error = "Branch name is not valid:\n" + branch;
                return false;
            }

            foreach (var c in branch)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c) || "~^:?*[\\".IndexOf(c) >= 0)
                {
                    error = "Branch name contains a character Git does not allow:\n" + branch;
                    return false;
                }
            }

            var parts = branch.Split('/');
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part) ||
                    part == "." ||
                    part == ".." ||
                    part.StartsWith(".", StringComparison.Ordinal) ||
                    part.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
                {
                    error = "Branch name is not valid:\n" + branch;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static void SaveUnityState()
        {
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
        }

        private static string[] GetOpenScenePaths()
        {
            var paths = new List<string>();
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded || string.IsNullOrWhiteSpace(scene.path) || paths.Contains(scene.path))
                {
                    continue;
                }

                paths.Add(scene.path);
            }

            return paths.ToArray();
        }

        private static bool OpenTemporarySceneBeforeExternalRestore(string[] scenePaths)
        {
            if (scenePaths == null || scenePaths.Length == 0)
            {
                return false;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return true;
        }

        private static int ReloadOpenScenesFromDisk(string[] scenePaths, string activeScenePath)
        {
            if (scenePaths == null || scenePaths.Length == 0)
            {
                return 0;
            }

            var loadedCount = 0;
            var activeScene = default(UnityEngine.SceneManagement.Scene);

            foreach (var scenePath in scenePaths)
            {
                if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(Path.Combine(ProjectRoot, scenePath)))
                {
                    continue;
                }

                var mode = loadedCount == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive;
                var scene = EditorSceneManager.OpenScene(scenePath, mode);
                loadedCount++;

                if (string.Equals(scenePath, activeScenePath, StringComparison.Ordinal))
                {
                    activeScene = scene;
                }
            }

            if (activeScene.IsValid())
            {
                EditorSceneManager.SetActiveScene(activeScene);
            }

            return loadedCount;
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
            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

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

        private static bool IsAuthenticationFailure(GitResult result)
        {
            var combined = result.Combined;
            return combined.IndexOf("could not read Username", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("Authentication failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("Repository not found", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsNonFastForwardFailure(GitResult result)
        {
            var combined = result.Combined;
            return combined.IndexOf("non-fast-forward", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("fetch first", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   combined.IndexOf("rejected", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ShowAuthenticationHelp(string title, GitResult result)
        {
            Show(
                title,
                "GitHub rejected the upload because this computer is not logged in for Git HTTPS pushes.\n\n" +
                "Run this once in a terminal:\n\n" +
                "cd " + ProjectRoot + "\n" +
                "gh auth login --hostname github.com --git-protocol https --scopes repo --web\n" +
                "gh auth setup-git\n\n" +
                "Then press DreamScripts/GitHub/Upload again.\n\n" +
                "Git said:\n" + result.Combined);
        }

        private static string Clip(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            const int maxLength = 1200;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "\n... clipped ...";
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

        private sealed class BranchChoice
        {
            public BranchChoice(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public bool ExistsLocal { get; set; }
            public bool ExistsRemote { get; set; }
            public bool IsCurrent { get; set; }

            public string StatusLabel
            {
                get
                {
                    if (IsCurrent && ExistsRemote)
                    {
                        return "current + GitHub";
                    }

                    if (IsCurrent)
                    {
                        return "current local";
                    }

                    if (ExistsLocal && ExistsRemote)
                    {
                        return "local + GitHub";
                    }

                    return ExistsRemote ? "GitHub" : "local only";
                }
            }
        }

        private sealed class BranchPickerWindow : EditorWindow
        {
            private readonly List<BranchChoice> _choices = new List<BranchChoice>();
            private Action<string> _onSelected;
            private string _actionLabel;
            private string _description;
            private string _manualBranch;
            private string _manualLabel;
            private string _search;
            private string _selectedBranch;
            private string _titleText;
            private string _warning;
            private bool _allowManualBranch;
            private Vector2 _scroll;

            public static void Open(
                string title,
                string description,
                string actionLabel,
                string manualLabel,
                string defaultBranch,
                List<BranchChoice> choices,
                bool allowManualBranch,
                string warning,
                Action<string> onSelected)
            {
                var window = CreateInstance<BranchPickerWindow>();
                window.titleContent = new GUIContent(title);
                window._titleText = title;
                window._description = description;
                window._actionLabel = actionLabel;
                window._manualLabel = manualLabel;
                window._allowManualBranch = allowManualBranch;
                window._warning = warning ?? string.Empty;
                window._onSelected = onSelected;

                if (choices != null)
                {
                    window._choices.AddRange(choices);
                }

                window._selectedBranch = PickDefaultBranch(defaultBranch, window._choices);
                window.minSize = new Vector2(520, 520);
                window.ShowUtility();
                window.Focus();
            }

            private static string PickDefaultBranch(string defaultBranch, List<BranchChoice> choices)
            {
                if (choices != null && choices.Count > 0)
                {
                    foreach (var choice in choices)
                    {
                        if (string.Equals(choice.Name, defaultBranch, StringComparison.Ordinal))
                        {
                            return choice.Name;
                        }
                    }

                    return choices[0].Name;
                }

                return NormalizeBranchInput(defaultBranch);
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField(_titleText, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_description, MessageType.Info);

                if (!string.IsNullOrWhiteSpace(_warning))
                {
                    EditorGUILayout.HelpBox(_warning, MessageType.Warning);
                }

                DrawSearch();
                DrawBranchList();
                DrawManualBranchField();
                DrawFooter();
            }

            private void DrawSearch()
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();
                _search = EditorGUILayout.TextField("Search", _search ?? string.Empty);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_search)))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(64)))
                    {
                        _search = string.Empty;
                        GUI.FocusControl(null);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            private void DrawBranchList()
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Branches", EditorStyles.boldLabel);

                var visibleCount = 0;
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(190));
                foreach (var choice in _choices)
                {
                    if (!MatchesSearch(choice))
                    {
                        continue;
                    }

                    visibleCount++;
                    DrawBranchRow(choice);
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No branches match the current search.", MessageType.None);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawBranchRow(BranchChoice choice)
            {
                var selected = string.Equals(_selectedBranch, choice.Name, StringComparison.Ordinal);
                var previousColor = GUI.backgroundColor;
                if (selected)
                {
                    GUI.backgroundColor = new Color(0.55f, 0.72f, 1f);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Toggle(selected, choice.Name, GUI.skin.button, GUILayout.Height(26)))
                {
                    _selectedBranch = choice.Name;
                    _manualBranch = string.Empty;
                }

                GUI.backgroundColor = previousColor;
                GUILayout.Label(choice.StatusLabel, GUILayout.Width(128));
                EditorGUILayout.EndHorizontal();
            }

            private void DrawManualBranchField()
            {
                if (!_allowManualBranch)
                {
                    return;
                }

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(_manualLabel, EditorStyles.boldLabel);
                _manualBranch = EditorGUILayout.TextField(_manualBranch ?? string.Empty);
                EditorGUILayout.HelpBox("Leave this empty to use the selected branch above. Type a branch name here to use a new or hidden branch.", MessageType.None);
            }

            private void DrawFooter()
            {
                EditorGUILayout.Space(8);
                var canConfirm = TryGetCandidate(out var branch, out var error);
                if (canConfirm)
                {
                    EditorGUILayout.HelpBox("Selected branch: " + branch, MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(error, MessageType.Warning);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(!canConfirm))
                {
                    if (GUILayout.Button(_actionLabel, GUILayout.Width(120), GUILayout.Height(30)))
                    {
                        Confirm(branch);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            private bool MatchesSearch(BranchChoice choice)
            {
                if (string.IsNullOrWhiteSpace(_search))
                {
                    return true;
                }

                return choice.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       choice.StatusLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private bool TryGetCandidate(out string branch, out string error)
            {
                var raw = string.IsNullOrWhiteSpace(_manualBranch) ? _selectedBranch : _manualBranch;
                branch = NormalizeBranchInput(raw);
                return IsValidBranchName(branch, out error);
            }

            private void Confirm(string branch)
            {
                var onSelected = _onSelected;
                Close();

                if (onSelected == null)
                {
                    return;
                }

                EditorApplication.delayCall += () => onSelected(branch);
            }
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
