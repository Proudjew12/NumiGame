using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private const string MainBranch = "main";
        private const int CommitHistoryLimit = 30;
        private const string GitHubToolsPath = "Assets/DreamScripts/Editor/GitHubTools.cs";
        private const string GitHubTestsPath = "Assets/NumiDream/Tests/EditMode/GitHubToolsTests.cs";
        private const string PackageManifestPath = "Packages/manifest.json";
        private const float BranchLastUpdateColumnWidth = 162f;
        private const float BranchAuthorColumnWidth = 132f;
        private const float BranchStatusColumnWidth = 128f;
        private const float BranchRowHeight = 30f;
        private static readonly string LeftToRightMarker = ((char)0x200E).ToString();
        private static bool _branchStylesInitialized;
        private static bool _branchStylesProSkin;
        private static GUIStyle _branchHeaderStyle;
        private static GUIStyle _branchHeaderLabelStyle;
        private static GUIStyle _branchOddRowStyle;
        private static GUIStyle _branchEvenRowStyle;
        private static GUIStyle _branchSelectedRowStyle;
        private static GUIStyle _branchNameStyle;
        private static GUIStyle _branchSelectedNameStyle;
        private static GUIStyle _branchMetaStyle;
        private static GUIStyle _statusCurrentRemoteStyle;
        private static GUIStyle _statusCurrentLocalStyle;
        private static GUIStyle _statusLocalRemoteStyle;
        private static GUIStyle _statusRemoteOnlyStyle;
        private static GUIStyle _statusLocalOnlyStyle;

        static GitHubTools()
        {
            DreamScriptRegistry.Register("GitHub/Push/Upload", Upload, priority: 22);
            DreamScriptRegistry.Register("GitHub/Pull/Import", Import, priority: 23);
            DreamScriptRegistry.Register("GitHub/Commit/Write Commit", CommitChanges, priority: 24);
            DreamScriptRegistry.Register("GitHub/History/Branch History", ShowBranchHistory, priority: 25);
            DreamScriptRegistry.Register("GitHub/Merge/Branch Into Current", MergeBranchIntoCurrent, priority: 26);
            DreamScriptRegistry.Register("GitHub/Merge/Branches Into Main", MergeBranchesIntoMain, priority: 27);
            DreamScriptRegistry.Register("GitHub/Repo/Status", ShowRepoStatus, priority: 28);
            DreamScriptRegistry.Register("GitHub/Repo/SetRepo", SetRepo, priority: 29);
            DreamScriptRegistry.Register("GitHub/Repo/EnterRepo", EnterRepo, priority: 30);
        }

        [MenuItem(RootPath + "/Push/Upload", false, RootMenuPriorityBase)]
        private static void Upload()
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Push/Upload blocked", "Could not detect the current Git branch.");
                return;
            }

            var branchListWarning = RefreshOriginBranchList(blockOnFailure: false);
            var choices = GetBranchChoices(currentBranch, remoteOnly: false);
            BranchPickerWindow.Open(
                "GitHub Push/Upload",
                "Choose where to push/upload the current saved project state. You will get a safety check before GitHub is changed.",
                "Push/Upload",
                "New GitHub branch name",
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
                Show("GitHub Push/Upload blocked", branchError);
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Push/Upload blocked", "Could not detect the current Git branch.");
                return;
            }

            var status = RunGit("status --porcelain");
            if (!status.Success)
            {
                ShowGitFailure("GitHub Push/Upload failed", "Git could not read the project status.", status);
                return;
            }

            if (!string.IsNullOrWhiteSpace(status.Output))
            {
                var statusSummary = RunGit("status --short").Output.Trim();
                CommitMessageWindow.Open(
                    "Push/Upload Commit",
                    "Write the commit message for the project changes that will be pushed/uploaded.",
                    "Commit + Push/Upload",
                    DefaultCommitMessage("Push/upload"),
                    statusSummary,
                    commitMessage => CommitThenFinishUpload(targetBranch, currentBranch, commitMessage));
                return;
            }

            FinishUploadToBranch(targetBranch, currentBranch, committed: false, commitMessage: string.Empty, changeSummary: string.Empty);
        }

        private static void CommitThenFinishUpload(string targetBranch, string currentBranch, string commitMessage)
        {
            if (!CommitPendingChanges(commitMessage, "GitHub Push/Upload", out var changeSummary))
            {
                return;
            }

            FinishUploadToBranch(targetBranch, currentBranch, committed: true, commitMessage: commitMessage, changeSummary: changeSummary);
        }

        private static void FinishUploadToBranch(
            string targetBranch,
            string currentBranch,
            bool committed,
            string commitMessage,
            string changeSummary)
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var lfs = RunGit("lfs install --local");
            if (!lfs.Success)
            {
                ShowGitFailure("GitHub Push/Upload blocked", "Git LFS is not available to Unity.", lfs);
                return;
            }

            var head = RunGit("rev-parse --short HEAD").Output.Trim();
            var confirmMessage =
                "Safety check before pushing/uploading to GitHub.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Local branch: " + currentBranch + "\n" +
                "GitHub branch: " + targetBranch + "\n" +
                "Commit: " + EmptyFallback(head, "Could not read HEAD.") + "\n" +
                "New commit created: " + (committed ? "Yes" : "No") + "\n";

            if (committed)
            {
                confirmMessage += "Commit message: " + commitMessage + "\n";
            }

            confirmMessage += "\nFiles that will be included:\n" +
                              EmptyFallback(changeSummary, "No new local changes. This pushes the current committed branch state.");

            if (!ConfirmSafety("Push/Upload safety check", confirmMessage, "Push/Upload"))
            {
                return;
            }

            if (!ConfirmMainBranchAction(targetBranch, "push/upload directly to main"))
            {
                return;
            }

            var push = PushToRemoteBranch(targetBranch, currentBranch, forceWithLease: false);
            if (!push.Success)
            {
                if (IsAuthenticationFailure(push))
                {
                    ShowAuthenticationHelp("GitHub Push/Upload blocked", push);
                    return;
                }

                if (IsNonFastForwardFailure(push))
                {
                    push = ReplaceRemoteBranch(targetBranch, currentBranch, push);
                    if (!push.Success)
                    {
                        if (IsAuthenticationFailure(push))
                        {
                            ShowAuthenticationHelp("GitHub Push/Upload blocked", push);
                            return;
                        }

                        ShowGitFailure("GitHub Push/Upload failed", "Git could not push to GitHub.", push);
                        return;
                    }
                }
                else
                {
                    ShowGitFailure("GitHub Push/Upload failed", "Git could not push to GitHub.", push);
                    return;
                }
            }

            var localBranchNote = SyncLocalBranchReferenceAfterUpload(targetBranch, currentBranch);
            var messageText =
                "Pushed/uploaded to GitHub.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Local branch: " + currentBranch + "\n" +
                "GitHub branch: " + targetBranch + "\n" +
                "Commit: " + head + "\n" +
                "Commit created by this action: " + (committed ? "Yes" : "No") + "\n\n";

            if (!string.IsNullOrWhiteSpace(changeSummary))
            {
                messageText += "Pushed/uploaded changes:\n" + changeSummary;
            }
            else
            {
                messageText += "Your branch was pushed with the current committed state.";
            }

            if (!string.IsNullOrWhiteSpace(localBranchNote))
            {
                messageText += "\n\n" + localBranchNote;
            }

            Show("GitHub Push/Upload complete", messageText);
        }

        [MenuItem(RootPath + "/Pull/Import", false, RootMenuPriorityBase + 1)]
        private static void Import()
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Pull/Import blocked", "Could not detect the current Git branch.");
                return;
            }

            var branchListWarning = RefreshOriginBranchList(blockOnFailure: true);
            if (!string.IsNullOrWhiteSpace(branchListWarning))
            {
                return;
            }

            var choices = GetBranchChoices(currentBranch, remoteOnly: true);
            BranchPickerWindow.Open(
                "GitHub Pull/Import",
                "Choose the GitHub branch to pull/import into this local project. Import creates a safety backup first and asks again before replacing files.",
                "Pull/Import",
                "GitHub branch name",
                currentBranch,
                choices,
                allowManualBranch: false,
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
                Show("GitHub Pull/Import blocked", branchError);
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Pull/Import blocked", "Could not detect the current Git branch.");
                return;
            }

            var fetch = RunGit("fetch origin " + Quote(branch));
            if (!fetch.Success)
            {
                ShowGitFailure("GitHub Pull/Import failed", "Git could not fetch from GitHub.", fetch);
                return;
            }

            var remoteRef = "origin/" + branch;
            var remoteHead = RunGit("rev-parse --verify " + Quote(remoteRef));
            if (!remoteHead.Success)
            {
                ShowGitFailure("GitHub Pull/Import failed", "GitHub does not have branch '" + branch + "'.", remoteHead);
                return;
            }

            if (!RemoteKeepsRestoredDreamScripts(remoteRef, out var dreamScriptsCheck))
            {
                Show(
                    "GitHub Pull/Import blocked",
                    "This branch does not contain the restored DreamScripts GitHub tools.\n\n" +
                    "Branch: " + branch + "\n" +
                    "Time: " + NowStamp() + "\n\n" +
                    "No files were imported. This prevents an old branch from undoing Push/Upload, Pull/Import, Repo, Merge, and tests again.\n\n" +
                    "Ask the teammate to update their branch from the restored DreamScripts branch first, then import again.\n\n" +
                    "Missing or outdated:\n" + dreamScriptsCheck);
                return;
            }

            if (!CreateSafetyCommitIfNeeded(out var createdSafetyCommit))
            {
                return;
            }

            var oldHead = RunGit("rev-parse HEAD").Output.Trim();
            var remoteHeadHash = remoteHead.Output.Trim();
            var localOnlyCommits = RunGit("log --oneline " + Quote(remoteRef) + "..HEAD").Output.Trim();
            var incomingCommits = RunGit("log --oneline HEAD.." + Quote(remoteRef)).Output.Trim();
            var incomingFiles = RunGit("diff --name-status HEAD.." + Quote(remoteRef)).Output.Trim();
            var headDiffersFromRemote = !string.Equals(oldHead, remoteHeadHash, StringComparison.Ordinal);

            if (!headDiffersFromRemote && !createdSafetyCommit)
            {
                Show(
                    "GitHub Pull/Import complete",
                    "Already up to date.\n\n" +
                    "Time: " + NowStamp() + "\n" +
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

            var safetyMessage =
                "Safety check before pulling/importing from GitHub.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Local branch: " + currentBranch + "\n" +
                "GitHub branch: " + branch + "\n" +
                "Safety backup branch: " + EmptyFallback(backupBranch, "No backup needed.") + "\n\n" +
                "Incoming commits:\n" + EmptyFallback(incomingCommits, "No incoming commit list available.") + "\n\n" +
                "Files that may be replaced:\n" + EmptyFallback(incomingFiles, "No file list available.");

            if (!ConfirmSafety("Pull/Import safety check", safetyMessage, "Pull/Import"))
            {
                return;
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
                    "GitHub Pull/Import stopped",
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
                "Pulled/imported from GitHub.\n\n" +
                "Time: " + NowStamp() + "\n" +
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

            Show("GitHub Pull/Import complete", message);
        }

        [MenuItem(RootPath + "/Repo/SetRepo", false, RootMenuPriorityBase + 2)]
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

            var confirmSetRepo = EditorUtility.DisplayDialog(
                "Repo safety check",
                "Set this project's GitHub origin remote?\n\n" +
                "Time: " + NowStamp() + "\n" +
                "New remote:\n" + clipboard + "\n\n" +
                "Browser URL:\n" + repoUrl + "\n\n" +
                "This changes where Push/Upload, Pull/Import, history, and merge tools read and write.",
                "Set Repo",
                "Cancel");

            if (!confirmSetRepo)
            {
                return;
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
                "Time: " + NowStamp() + "\n" +
                "Origin remote saved:\n" + clipboard + "\n\n" +
                "Website URL:\n" + repoUrl + "\n\n" +
                "You can now use GitHub/Repo/EnterRepo and GitHub/Push/Upload.");
        }

        [MenuItem(RootPath + "/Repo/EnterRepo", false, RootMenuPriorityBase + 3)]
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
                    "DreamScripts/GitHub/Repo/SetRepo\n\n" +
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

        [MenuItem(RootPath + "/Commit/Write Commit", false, RootMenuPriorityBase + 4)]
        private static void CommitChanges()
        {
            SaveUnityState();

            if (!EnsureGitRepository())
            {
                return;
            }

            var status = RunGit("status --porcelain");
            if (!status.Success)
            {
                ShowGitFailure("GitHub Commit failed", "Git could not read the project status.", status);
                return;
            }

            if (string.IsNullOrWhiteSpace(status.Output))
            {
                Show("GitHub Commit", "No uncommitted project changes found.\n\nTime: " + NowStamp());
                return;
            }

            var statusSummary = RunGit("status --short").Output.Trim();
            CommitMessageWindow.Open(
                "Write Git Commit",
                "Write a commit message for the current project changes.",
                "Commit",
                DefaultCommitMessage("Manual save"),
                statusSummary,
                commitMessage =>
                {
                    if (!CommitPendingChanges(commitMessage, "GitHub Commit", out var changeSummary))
                    {
                        return;
                    }

                    var head = RunGit("rev-parse --short HEAD").Output.Trim();
                    Show(
                        "GitHub Commit complete",
                        "Created local commit.\n\n" +
                        "Time: " + NowStamp() + "\n" +
                        "Commit: " + EmptyFallback(head, "Could not read HEAD.") + "\n" +
                        "Message: " + commitMessage + "\n\n" +
                        "Committed files:\n" + EmptyFallback(changeSummary, "No file list available."));
                });
        }

        [MenuItem(RootPath + "/History/Branch History", false, RootMenuPriorityBase + 5)]
        private static void ShowBranchHistory()
        {
            if (!EnsureGitRepository())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            var branchListWarning = EnsureOriginRemote() ? RefreshOriginBranchList(blockOnFailure: false) : string.Empty;
            var choices = GetBranchChoices(currentBranch, remoteOnly: false);

            BranchPickerWindow.Open(
                "GitHub Branch History",
                "Choose a branch to see recent commits with author, date, time, and message.",
                "Show History",
                "Branch name",
                currentBranch,
                choices,
                allowManualBranch: false,
                warning: branchListWarning,
                onSelected: ShowHistoryForBranch);
        }

        private static void ShowHistoryForBranch(string branch)
        {
            branch = NormalizeBranchInput(branch);
            if (!IsValidBranchName(branch, out var branchError))
            {
                Show("GitHub History blocked", branchError);
                return;
            }

            var revision = ResolveBranchRevision(branch);
            var history = RunGit(
                "log -n " + CommitHistoryLimit +
                " --date=iso-local --pretty=format:" + Quote("%h | %ad | %an | %d%n    %s%n") +
                " " + Quote(revision));

            if (!history.Success)
            {
                ShowGitFailure("GitHub History failed", "Git could not read commit history for '" + branch + "'.", history);
                return;
            }

            Show(
                "GitHub Branch History",
                "Branch: " + branch + "\n" +
                "Generated: " + NowStamp() + "\n\n" +
                EmptyFallback(history.Output.Trim(), "No commits found."));
        }

        [MenuItem(RootPath + "/Repo/Status", false, RootMenuPriorityBase + 6)]
        private static void ShowRepoStatus()
        {
            if (!EnsureGitRepository())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            var head = RunGit("rev-parse --short HEAD").Output.Trim();
            var status = RunGit("status -sb");
            var remotes = RunGit("remote -v");
            var recent = RunGit(
                "log -n 8 --date=iso-local --pretty=format:" +
                Quote("%h | %ad | %an | %d%n    %s%n"));

            var message =
                "Repo status generated: " + NowStamp() + "\n" +
                "Current branch: " + EmptyFallback(currentBranch, "Unknown") + "\n" +
                "HEAD: " + EmptyFallback(head, "Unknown") + "\n\n" +
                "Status:\n" + EmptyFallback(status.Output.Trim(), status.Combined) + "\n\n" +
                "Remotes:\n" + EmptyFallback(remotes.Output.Trim(), "No remotes configured.") + "\n\n" +
                "Recent commits:\n" + EmptyFallback(recent.Output.Trim(), recent.Combined);

            Show("GitHub Repo Status", message);
        }

        [MenuItem(RootPath + "/Merge/Branch Into Current", false, RootMenuPriorityBase + 7)]
        private static void MergeBranchIntoCurrent()
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Merge blocked", "Could not detect the current Git branch.");
                return;
            }

            var branchListWarning = RefreshOriginBranchList(blockOnFailure: true);
            if (!string.IsNullOrWhiteSpace(branchListWarning))
            {
                return;
            }

            var choices = GetBranchChoices(currentBranch, remoteOnly: false);
            BranchPickerWindow.Open(
                "Merge Branch Into Current",
                "Choose a branch to merge into the current local branch. A backup branch is created first.",
                "Merge",
                "Branch name",
                currentBranch,
                choices,
                allowManualBranch: false,
                warning: string.Empty,
                onSelected: MergeBranchIntoCurrentBranch);
        }

        [MenuItem(RootPath + "/Merge/Branches Into Main", false, RootMenuPriorityBase + 8)]
        private static void MergeBranchesIntoMain()
        {
            if (!EnsureGitRepository() || !EnsureOriginRemote())
            {
                return;
            }

            var currentBranch = GetCurrentBranch();
            var branchListWarning = RefreshOriginBranchList(blockOnFailure: true);
            if (!string.IsNullOrWhiteSpace(branchListWarning))
            {
                return;
            }

            var choices = GetBranchChoices(currentBranch, remoteOnly: true);
            choices.RemoveAll(choice => string.Equals(choice.Name, MainBranch, StringComparison.Ordinal));
            MultiBranchPickerWindow.Open(
                "Merge Branches Into Main",
                "Select the GitHub branches to merge into main. The tool creates a backup branch, merges one branch at a time, and pushes main only after all merges succeed.",
                "Merge Into Main",
                choices,
                selectedBranches => MergeSelectedBranchesIntoMain(selectedBranches));
        }

        private static void MergeBranchIntoCurrentBranch(string sourceBranch)
        {
            SaveUnityState();

            sourceBranch = NormalizeBranchInput(sourceBranch);
            if (!IsValidBranchName(sourceBranch, out var branchError))
            {
                Show("GitHub Merge blocked", branchError);
                return;
            }

            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrWhiteSpace(currentBranch))
            {
                Show("GitHub Merge blocked", "Could not detect the current Git branch.");
                return;
            }

            if (string.Equals(sourceBranch, currentBranch, StringComparison.Ordinal))
            {
                Show("GitHub Merge blocked", "Choose a different source branch. You are already on '" + currentBranch + "'.");
                return;
            }

            if (!EnsureCleanWorkingTreeOrCreateSafetyCommit("GitHub Merge", "merge branch into current", out _))
            {
                return;
            }

            var sourceRevision = ResolveBranchRevision(sourceBranch);
            var verifySource = RunGit("rev-parse --verify " + Quote(sourceRevision));
            if (!verifySource.Success)
            {
                ShowGitFailure("GitHub Merge failed", "Git could not find source branch '" + sourceBranch + "'.", verifySource);
                return;
            }

            var commits = RunGit(
                "log --date=iso-local --pretty=format:" + Quote("%h | %ad | %an%n    %s%n") +
                " " + Quote("HEAD.." + sourceRevision)).Output.Trim();
            var files = RunGit("diff --name-status " + Quote("HEAD.." + sourceRevision)).Output.Trim();
            var backupBranch = CreateBackupBranch("merge-current", currentBranch, "GitHub Merge failed");
            if (string.IsNullOrWhiteSpace(backupBranch))
            {
                return;
            }

            var confirm =
                "Safety check before merging.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Target branch: " + currentBranch + "\n" +
                "Source branch: " + sourceBranch + "\n" +
                "Backup branch: " + backupBranch + "\n\n" +
                "Incoming commits:\n" + EmptyFallback(commits, "No new commits found. Git may report already up to date.") + "\n\n" +
                "Files that may change:\n" + EmptyFallback(files, "No file changes detected.");

            if (!ConfirmSafety("Merge safety check", confirm, "Merge"))
            {
                return;
            }

            if (!ConfirmMainBranchAction(currentBranch, "merge into main"))
            {
                return;
            }

            var openScenePaths = GetOpenScenePaths();
            var activeScenePath = EditorSceneManager.GetActiveScene().path;
            var scenesTemporarilyClosed = OpenTemporarySceneBeforeExternalRestore(openScenePaths);
            var merge = RunGit("merge --no-ff --no-edit " + Quote(sourceRevision));

            if (!merge.Success)
            {
                RunGit("merge --abort");
                RunGit("reset --hard " + Quote(backupBranch));
                if (scenesTemporarilyClosed)
                {
                    ReloadOpenScenesFromDisk(openScenePaths, activeScenePath);
                }

                ShowGitFailure(
                    "GitHub Merge stopped",
                    "Merge conflict or Git failure while merging '" + sourceBranch + "' into '" + currentBranch + "'.\n\n" +
                    "The branch was reset back to safety backup:\n" + backupBranch,
                    merge);
                return;
            }

            AssetDatabase.Refresh();
            var reloadedSceneCount = ReloadOpenScenesFromDisk(openScenePaths, activeScenePath);
            var head = RunGit("rev-parse --short HEAD").Output.Trim();

            Show(
                "GitHub Merge complete",
                "Merged branch safely.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Target branch: " + currentBranch + "\n" +
                "Source branch: " + sourceBranch + "\n" +
                "New HEAD: " + EmptyFallback(head, "Unknown") + "\n" +
                "Backup branch: " + backupBranch + "\n" +
                "Reloaded scenes: " + reloadedSceneCount + "\n\n" +
                "Review in Unity, then use Push/Upload when ready.");
        }

        private static void MergeSelectedBranchesIntoMain(List<string> sourceBranches)
        {
            SaveUnityState();

            if (sourceBranches == null || sourceBranches.Count == 0)
            {
                Show("GitHub Merge blocked", "Select at least one branch to merge into main.");
                return;
            }

            if (!EnsureCleanWorkingTreeOrCreateSafetyCommit("GitHub Merge Into Main", "merge selected branches into main", out _))
            {
                return;
            }

            var fetch = RunGit("fetch origin --prune");
            if (!fetch.Success)
            {
                ShowGitFailure("GitHub Merge Into Main failed", "Git could not refresh GitHub branches.", fetch);
                return;
            }

            var mainRevision = BranchExistsRemote(MainBranch)
                ? "origin/" + MainBranch
                : (BranchExistsLocal(MainBranch) ? MainBranch : string.Empty);

            if (string.IsNullOrWhiteSpace(mainRevision))
            {
                Show("GitHub Merge Into Main blocked", "Could not find a local or GitHub branch named 'main'.");
                return;
            }

            var preview = BuildMergeIntoMainPreview(sourceBranches, mainRevision);
            var confirm =
                "Safety check before merging selected branches into main.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Main source: " + mainRevision + "\n" +
                "Branches selected:\n" + string.Join("\n", sourceBranches.ToArray()) + "\n\n" +
                preview + "\n\n" +
                "Process:\n" +
                "1. Save Unity and close loaded scenes temporarily.\n" +
                "2. Checkout/update main with --ff-only.\n" +
                "3. Create a backup branch from main.\n" +
                "4. Merge each selected branch one by one.\n" +
                "5. If any merge conflicts, abort and reset main back to the backup.\n" +
                "6. Push main to GitHub only if every merge succeeds.";

            if (!ConfirmSafety("Merge branches into main safety check", confirm, "Merge Into Main"))
            {
                return;
            }

            if (!ConfirmMainBranchAction(MainBranch, "merge selected branches and push main"))
            {
                return;
            }

            var openScenePaths = GetOpenScenePaths();
            var activeScenePath = EditorSceneManager.GetActiveScene().path;
            var originalBranch = GetCurrentBranch();
            var scenesTemporarilyClosed = OpenTemporarySceneBeforeExternalRestore(openScenePaths);

            var checkoutMain = BranchExistsLocal(MainBranch)
                ? RunGit("checkout " + Quote(MainBranch))
                : RunGit("checkout -B " + Quote(MainBranch) + " " + Quote(mainRevision));

            if (!checkoutMain.Success)
            {
                if (scenesTemporarilyClosed)
                {
                    ReloadOpenScenesFromDisk(openScenePaths, activeScenePath);
                }

                ShowGitFailure("GitHub Merge Into Main failed", "Git could not checkout main.", checkoutMain);
                return;
            }

            var updateMain = RunGit("pull --ff-only origin " + Quote(MainBranch));
            if (!updateMain.Success)
            {
                RestoreBranchAfterMainMergeFailure(originalBranch, openScenePaths, activeScenePath, scenesTemporarilyClosed);
                ShowGitFailure(
                    "GitHub Merge Into Main stopped",
                    "Main has local or remote history that cannot be fast-forwarded safely. Resolve main first, then retry.",
                    updateMain);
                return;
            }

            var backupBranch = CreateBackupBranch("main-before-merge", MainBranch, "GitHub Merge Into Main failed");
            if (string.IsNullOrWhiteSpace(backupBranch))
            {
                RestoreBranchAfterMainMergeFailure(originalBranch, openScenePaths, activeScenePath, scenesTemporarilyClosed);
                return;
            }

            foreach (var branch in sourceBranches)
            {
                var sourceRevision = ResolveBranchRevision(branch);
                var merge = RunGit("merge --no-ff --no-edit " + Quote(sourceRevision));
                if (merge.Success)
                {
                    continue;
                }

                RunGit("merge --abort");
                RunGit("reset --hard " + Quote(backupBranch));
                RestoreBranchAfterMainMergeFailure(originalBranch, openScenePaths, activeScenePath, scenesTemporarilyClosed);
                ShowGitFailure(
                    "GitHub Merge Into Main stopped",
                    "Conflict or Git failure while merging '" + branch + "'.\n\n" +
                    "Main was reset back to backup branch:\n" + backupBranch,
                    merge);
                return;
            }

            var push = RunGit("push origin " + Quote(MainBranch));
            if (!push.Success)
            {
                RestoreBranchAfterMainMergeFailure(originalBranch, openScenePaths, activeScenePath, scenesTemporarilyClosed);
                ShowGitFailure(
                    "GitHub Merge Into Main merged locally but push failed",
                    "The selected branches merged into local main, but GitHub did not accept the push.\n\n" +
                    "Safety backup branch:\n" + backupBranch,
                    push);
                return;
            }

            RestoreBranchAfterMainMergeFailure(originalBranch, openScenePaths, activeScenePath, scenesTemporarilyClosed);
            AssetDatabase.Refresh();
            var head = RunGit("rev-parse --short " + Quote(MainBranch)).Output.Trim();

            Show(
                "GitHub Merge Into Main complete",
                "Merged selected branches into main and pushed main to GitHub.\n\n" +
                "Time: " + NowStamp() + "\n" +
                "Branches:\n" + string.Join("\n", sourceBranches.ToArray()) + "\n\n" +
                "Main HEAD: " + EmptyFallback(head, "Unknown") + "\n" +
                "Safety backup branch: " + backupBranch);
        }

        private static bool CommitPendingChanges(string commitMessage, string title, out string changeSummary)
        {
            changeSummary = string.Empty;
            commitMessage = (commitMessage ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                Show(title, "Commit message is required.");
                return false;
            }

            var lfs = RunGit("lfs install --local");
            if (!lfs.Success)
            {
                ShowGitFailure(title + " blocked", "Git LFS is not available to Unity.", lfs);
                return false;
            }

            var add = RunGit("add -A");
            if (!add.Success)
            {
                ShowGitFailure(title + " failed", "Git could not stage the project changes.", add);
                return false;
            }

            changeSummary = RunGit("diff --cached --name-status").Output.Trim();
            if (string.IsNullOrWhiteSpace(changeSummary))
            {
                return true;
            }

            var commit = RunGit("commit -m " + Quote(commitMessage));
            if (!commit.Success)
            {
                ShowGitFailure(title + " failed", "Git could not create the commit.", commit);
                return false;
            }

            return true;
        }

        private static bool EnsureCleanWorkingTreeOrCreateSafetyCommit(string title, string operation, out bool createdSafetyCommit)
        {
            createdSafetyCommit = false;

            var status = RunGit("status --porcelain");
            if (!status.Success)
            {
                ShowGitFailure(title + " failed", "Git could not read the project status.", status);
                return false;
            }

            if (string.IsNullOrWhiteSpace(status.Output))
            {
                return true;
            }

            var statusSummary = RunGit("status --short").Output.Trim();
            var confirm = EditorUtility.DisplayDialog(
                title,
                "You have local changes that are not committed yet.\n\n" +
                "Operation: " + operation + "\n" +
                "Time: " + NowStamp() + "\n\n" +
                "For safety, this tool will create a local safety commit before continuing.\n\n" +
                "Changes:\n" + Clip(statusSummary),
                "Create Safety Commit",
                "Cancel");

            if (!confirm)
            {
                return false;
            }

            var message = "Safety commit before " + operation + " " + NowStamp();
            if (!CommitPendingChanges(message, title, out _))
            {
                return false;
            }

            createdSafetyCommit = true;
            return true;
        }

        private static string ResolveBranchRevision(string branch)
        {
            branch = NormalizeBranchInput(branch);
            if (BranchExistsLocal(branch))
            {
                return branch;
            }

            if (BranchExistsRemote(branch))
            {
                return "origin/" + branch;
            }

            return branch;
        }

        private static bool BranchExistsRemote(string branch)
        {
            return RunGit("show-ref --verify --quiet " + Quote("refs/remotes/origin/" + branch)).Success;
        }

        private static string CreateBackupBranch(string operation, string branch, string failureTitle)
        {
            var safeOperation = MakeSafeBranchSegment(operation);
            var safeBranchName = MakeSafeBranchSegment(branch);
            var backupBranch = "backup/" + safeOperation + "-" + safeBranchName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backup = RunGit("branch " + Quote(backupBranch) + " HEAD");

            if (!backup.Success)
            {
                ShowGitFailure(
                    failureTitle,
                    "Git could not create a safety backup branch before continuing.",
                    backup);
                return string.Empty;
            }

            return backupBranch;
        }

        private static string BuildMergeIntoMainPreview(List<string> branches, string mainRevision)
        {
            var preview = new StringBuilder();
            foreach (var branch in branches)
            {
                var sourceRevision = ResolveBranchRevision(branch);
                var commits = RunGit(
                    "log --date=iso-local --pretty=format:" + Quote("%h | %ad | %an%n    %s%n") +
                    " " + Quote(mainRevision + ".." + sourceRevision)).Output.Trim();
                var files = RunGit("diff --name-status " + Quote(mainRevision + ".." + sourceRevision)).Output.Trim();

                preview.AppendLine("Branch: " + branch);
                preview.AppendLine("Incoming commits:");
                preview.AppendLine(EmptyFallback(commits, "No new commits found."));
                preview.AppendLine("Files that may change:");
                preview.AppendLine(EmptyFallback(files, "No file changes detected."));
                preview.AppendLine();
            }

            return preview.ToString().Trim();
        }

        private static void RestoreBranchAfterMainMergeFailure(
            string originalBranch,
            string[] openScenePaths,
            string activeScenePath,
            bool scenesTemporarilyClosed)
        {
            if (!string.IsNullOrWhiteSpace(originalBranch) && !string.Equals(originalBranch, MainBranch, StringComparison.Ordinal))
            {
                RunGit("checkout " + Quote(originalBranch));
            }

            if (scenesTemporarilyClosed)
            {
                ReloadOpenScenesFromDisk(openScenePaths, activeScenePath);
            }
        }

        private static bool ConfirmSafety(string title, string message, string action)
        {
            return EditorUtility.DisplayDialog(title, message, action, "Cancel");
        }

        private static bool ConfirmMainBranchAction(string branch, string operation)
        {
            if (!string.Equals(NormalizeBranchInput(branch), MainBranch, StringComparison.Ordinal))
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                "Main branch safety check",
                "You are about to " + operation + ".\n\n" +
                "Branch: main\n" +
                "Time: " + NowStamp() + "\n\n" +
                "Only continue if you have reviewed the branch, commit list, and file changes.",
                "I Understand, Continue",
                "Cancel");
        }

        private static string DefaultCommitMessage(string action)
        {
            return action + " " + NowStamp();
        }

        private static string NowStamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static string FormatBranchLastUpdate(long unixSeconds)
        {
            try
            {
                var utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds);
                return LeftToRightMarker + utc.ToLocalTime().ToString("ddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
        }

        private static string MakeSafeBranchSegment(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "branch" : value.Trim();
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
            }

            var result = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(result) ? "branch" : result;
        }

        private static bool RemoteKeepsRestoredDreamScripts(string revision, out string details)
        {
            var problems = new List<string>();

            var gitHubTools = ReadFileAtRevision(revision, GitHubToolsPath);
            if (string.IsNullOrWhiteSpace(gitHubTools))
            {
                problems.Add(GitHubToolsPath + " is missing.");
            }
            else
            {
                RequireMarker(gitHubTools, "GitHub/Push/Upload", GitHubToolsPath, problems);
                RequireMarker(gitHubTools, "GitHub/Pull/Import", GitHubToolsPath, problems);
                RequireMarker(gitHubTools, "GitHub/Repo/SetRepo", GitHubToolsPath, problems);
                RequireMarker(gitHubTools, "GitHub/Merge/Branches Into Main", GitHubToolsPath, problems);
            }

            var manifest = ReadFileAtRevision(revision, PackageManifestPath);
            if (string.IsNullOrWhiteSpace(manifest))
            {
                problems.Add(PackageManifestPath + " is missing.");
            }
            else
            {
                RequireMarker(manifest, "com.coplaydev.unity-mcp", PackageManifestPath, problems);
                RequireMarker(manifest, "#v9.7.1", PackageManifestPath, problems);
            }

            var tests = ReadFileAtRevision(revision, GitHubTestsPath);
            if (string.IsNullOrWhiteSpace(tests))
            {
                problems.Add(GitHubTestsPath + " is missing.");
            }
            else
            {
                RequireMarker(tests, "MergeBranchesIntoMainConflictRestoresMainAndDoesNotPush", GitHubTestsPath, problems);
                RequireMarker(tests, "PullImportRejectsBranchWithoutRestoredDreamScripts", GitHubTestsPath, problems);
            }

            details = problems.Count == 0 ? "DreamScripts GitHub tooling is present." : string.Join("\n", problems.ToArray());
            return problems.Count == 0;
        }

        private static string ReadFileAtRevision(string revision, string path)
        {
            var result = RunGit("show " + Quote(revision + ":" + path));
            return result.Success ? result.Output : string.Empty;
        }

        private static void RequireMarker(string content, string marker, string path, List<string> problems)
        {
            if (content.IndexOf(marker, StringComparison.Ordinal) < 0)
            {
                problems.Add(path + " does not contain '" + marker + "'.");
            }
        }

        internal static bool TestCommitPendingChanges(string projectRoot, string commitMessage, out string changeSummary)
        {
            var localChangeSummary = string.Empty;
            var success = WithTestProjectRoot(
                projectRoot,
                () => CommitPendingChanges(commitMessage, "GitHub Commit Test", out localChangeSummary));

            changeSummary = localChangeSummary;
            return success;
        }

        internal static bool TestPushToRemoteBranch(
            string projectRoot,
            string targetBranch,
            string currentBranch,
            bool forceWithLease,
            out string resultMessage)
        {
            var localResultMessage = string.Empty;
            var success = WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    var result = PushToRemoteBranch(targetBranch, currentBranch, forceWithLease);
                    localResultMessage = result.Combined;
                    return result.Success;
                });

            resultMessage = localResultMessage;
            return success;
        }

        internal static bool TestImportFromBranchNoDialogs(string projectRoot, string branch, out string backupBranch, out string resultMessage)
        {
            var localBackupBranch = string.Empty;
            var localResultMessage = string.Empty;
            var success = WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    branch = NormalizeBranchInput(branch);
                    var fetch = RunGit("fetch origin " + Quote(branch));
                    if (!fetch.Success)
                    {
                        localResultMessage = fetch.Combined;
                        return false;
                    }

                    var remoteRef = "origin/" + branch;
                    var verify = RunGit("rev-parse --verify " + Quote(remoteRef));
                    if (!verify.Success)
                    {
                        localResultMessage = verify.Combined;
                        return false;
                    }

                    if (!RemoteKeepsRestoredDreamScripts(remoteRef, out localResultMessage))
                    {
                        return false;
                    }

                    localBackupBranch = CreateBackupBranch("test-import", GetCurrentBranch(), "GitHub Pull/Import test failed");
                    if (string.IsNullOrWhiteSpace(localBackupBranch))
                    {
                        localResultMessage = "Could not create backup branch.";
                        return false;
                    }

                    var reset = RunGit("reset --hard " + Quote(remoteRef));
                    localResultMessage = reset.Combined;
                    return reset.Success;
                });

            backupBranch = localBackupBranch;
            resultMessage = localResultMessage;
            return success;
        }

        internal static bool TestMergeBranchIntoCurrentNoDialogs(
            string projectRoot,
            string sourceBranch,
            out string backupBranch,
            out string resultMessage)
        {
            var localBackupBranch = string.Empty;
            var localResultMessage = string.Empty;
            var success = WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    var sourceRevision = ResolveBranchRevision(sourceBranch);
                    localBackupBranch = CreateBackupBranch("test-merge-current", GetCurrentBranch(), "GitHub Merge test failed");
                    if (string.IsNullOrWhiteSpace(localBackupBranch))
                    {
                        localResultMessage = "Could not create backup branch.";
                        return false;
                    }

                    var merge = RunGit("merge --no-ff --no-edit " + Quote(sourceRevision));
                    if (merge.Success)
                    {
                        localResultMessage = merge.Combined;
                        return true;
                    }

                    RunGit("merge --abort");
                    RunGit("reset --hard " + Quote(localBackupBranch));
                    localResultMessage = merge.Combined;
                    return false;
                });

            backupBranch = localBackupBranch;
            resultMessage = localResultMessage;
            return success;
        }

        internal static bool TestMergeBranchesIntoMainNoDialogs(
            string projectRoot,
            string[] sourceBranches,
            out string backupBranch,
            out string resultMessage)
        {
            var localBackupBranch = string.Empty;
            var localResultMessage = string.Empty;
            var success = WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    var fetch = RunGit("fetch origin --prune");
                    if (!fetch.Success)
                    {
                        localResultMessage = fetch.Combined;
                        return false;
                    }

                    var mainRevision = BranchExistsRemote(MainBranch)
                        ? "origin/" + MainBranch
                        : (BranchExistsLocal(MainBranch) ? MainBranch : string.Empty);

                    if (string.IsNullOrWhiteSpace(mainRevision))
                    {
                        localResultMessage = "Could not find main.";
                        return false;
                    }

                    var checkoutMain = BranchExistsLocal(MainBranch)
                        ? RunGit("checkout " + Quote(MainBranch))
                        : RunGit("checkout -B " + Quote(MainBranch) + " " + Quote(mainRevision));

                    if (!checkoutMain.Success)
                    {
                        localResultMessage = checkoutMain.Combined;
                        return false;
                    }

                    var updateMain = RunGit("pull --ff-only origin " + Quote(MainBranch));
                    if (!updateMain.Success)
                    {
                        localResultMessage = updateMain.Combined;
                        return false;
                    }

                    localBackupBranch = CreateBackupBranch("test-main-before-merge", MainBranch, "GitHub Merge Into Main test failed");
                    if (string.IsNullOrWhiteSpace(localBackupBranch))
                    {
                        localResultMessage = "Could not create backup branch.";
                        return false;
                    }

                    foreach (var branch in sourceBranches)
                    {
                        var sourceRevision = ResolveBranchRevision(branch);
                        var merge = RunGit("merge --no-ff --no-edit " + Quote(sourceRevision));
                        if (merge.Success)
                        {
                            continue;
                        }

                        RunGit("merge --abort");
                        RunGit("reset --hard " + Quote(localBackupBranch));
                        localResultMessage = merge.Combined;
                        return false;
                    }

                    var push = RunGit("push origin " + Quote(MainBranch));
                    localResultMessage = push.Combined;
                    return push.Success;
                });

            backupBranch = localBackupBranch;
            resultMessage = localResultMessage;
            return success;
        }

        internal static string TestBranchHistory(string projectRoot, string branch)
        {
            return WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    var revision = ResolveBranchRevision(branch);
                    var history = RunGit(
                        "log -n " + CommitHistoryLimit +
                        " --date=iso-local --pretty=format:" + Quote("%h | %ad | %an | %d%n    %s%n") +
                        " " + Quote(revision));
                    return history.Output.Trim();
                });
        }

        internal static string TestRepoStatus(string projectRoot)
        {
            return WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    var status = RunGit("status -sb");
                    var remotes = RunGit("remote -v");
                    return status.Output.Trim() + "\n" + remotes.Output.Trim();
                });
        }

        internal static string[] TestBranchChoiceRows(string projectRoot, string currentBranch, bool remoteOnly)
        {
            return WithTestProjectRoot(
                projectRoot,
                () =>
                {
                    var choices = GetBranchChoices(currentBranch, remoteOnly);
                    var rows = new List<string>();
                    foreach (var choice in choices)
                    {
                        rows.Add(
                            choice.Name + "|" +
                            choice.LastUpdatedLabel + "|" +
                            choice.LastAuthorLabel + "|" +
                            choice.StatusLabel);
                    }

                    return rows.ToArray();
                });
        }

        internal static bool TestRemoteKeepsRestoredDreamScripts(
            string projectRoot,
            string revision,
            out string details)
        {
            var localDetails = string.Empty;
            var success = WithTestProjectRoot(
                projectRoot,
                () => RemoteKeepsRestoredDreamScripts(revision, out localDetails));

            details = localDetails;
            return success;
        }

        internal static string TestNormalizeBranchInput(string branch)
        {
            return NormalizeBranchInput(branch);
        }

        internal static bool TestIsValidBranchName(string branch, out string error)
        {
            return IsValidBranchName(branch, out error);
        }

        internal static string TestToGitHubBrowserUrl(string remoteUrl)
        {
            return ToGitHubBrowserUrl(remoteUrl);
        }

        private static T WithTestProjectRoot<T>(string projectRoot, Func<T> action)
        {
            var previousRoot = TestProjectRootOverride;
            TestProjectRootOverride = projectRoot;

            try
            {
                return action();
            }
            finally
            {
                TestProjectRootOverride = previousRoot;
            }
        }

        private static bool CreateSafetyCommitIfNeeded(out bool createdSafetyCommit)
        {
            createdSafetyCommit = false;

            var status = RunGit("status --porcelain");
            if (!status.Success)
            {
                ShowGitFailure("GitHub Pull/Import failed", "Git could not read the project status.", status);
                return false;
            }

            if (string.IsNullOrWhiteSpace(status.Output))
            {
                return true;
            }

            var confirm = EditorUtility.DisplayDialog(
                "GitHub Pull/Import",
                "You have local changes that are not committed yet.\n\n" +
                "Pull/Import will first create a local safety commit and backup branch, then restore this branch to GitHub's version.\n\nContinue?",
                "Create Safety Commit",
                "Cancel");

            if (!confirm)
            {
                return false;
            }

            var add = RunGit("add -A");
            if (!add.Success)
            {
                ShowGitFailure("GitHub Pull/Import failed", "Git could not stage the local safety commit.", add);
                return false;
            }

            var message = "Safety commit before GitHub pull/import " + NowStamp();
            var commit = RunGit("commit -m " + Quote(message));
            if (!commit.Success)
            {
                ShowGitFailure("GitHub Pull/Import failed", "Git could not create the local safety commit.", commit);
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
                    "GitHub Pull/Import failed",
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
                "DreamScripts/GitHub/Repo/SetRepo\n\n" +
                "After that, Push/Upload, Pull/Import, history, and merge tools will work.");
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
                    "GitHub Pull/Import failed",
                    "Git could not refresh the GitHub branch list before pull/import.",
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
                "GitHub Push/Upload needs sync",
                "GitHub already has commits that are not in this local project.\n\n" +
                "This usually happens when the GitHub repository was created with an initial README/license commit.\n\n" +
                "Push/Upload can replace the GitHub branch '" + targetBranch + "' with this Unity project using --force-with-lease. This keeps the local project as the source of truth and refuses to overwrite GitHub if it changed after the last fetch.\n\n" +
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

        private static string SyncLocalBranchReferenceAfterUpload(string targetBranch, string currentBranch)
        {
            if (string.Equals(targetBranch, currentBranch, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var existedLocal = BranchExistsLocal(targetBranch);
            var branchCommand = existedLocal
                ? "branch -f " + Quote(targetBranch) + " HEAD"
                : "branch " + Quote(targetBranch) + " HEAD";
            var branch = RunGit(branchCommand);
            if (!branch.Success)
            {
                return "GitHub branch was uploaded, but the local branch list could not be updated:\n" + Clip(branch.Combined);
            }

            var fetch = RunGit("fetch origin " + Quote("refs/heads/" + targetBranch + ":refs/remotes/origin/" + targetBranch));
            if (!fetch.Success)
            {
                return (existedLocal ? "Updated" : "Created") +
                       " local branch '" + targetBranch + "', but Git could not refresh its GitHub tracking ref:\n" +
                       Clip(fetch.Combined);
            }

            var upstream = RunGit("branch --set-upstream-to=" + Quote("origin/" + targetBranch) + " " + Quote(targetBranch));
            if (!upstream.Success)
            {
                return (existedLocal ? "Updated" : "Created") +
                       " local branch '" + targetBranch + "', but Git could not attach its upstream:\n" +
                       Clip(upstream.Combined);
            }

            return (existedLocal ? "Updated" : "Created") +
                   " local branch '" + targetBranch + "' and linked it to GitHub.";
        }

        private static bool BranchExistsLocal(string branch)
        {
            return RunGit("show-ref --verify --quiet " + Quote("refs/heads/" + branch)).Success;
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
            choice.ConsiderLastCommit(ReadBranchLastCommit(existsRemote ? "origin/" + branch : branch));
        }

        private static BranchLastCommit ReadBranchLastCommit(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision))
            {
                return default(BranchLastCommit);
            }

            var result = RunGit("log -1 --format=" + Quote("%ct%x09%an") + " " + Quote(revision));
            if (!result.Success)
            {
                return default(BranchLastCommit);
            }

            var output = result.Output.TrimEnd('\r', '\n');
            var separator = output.IndexOf('\t');
            if (separator <= 0)
            {
                return default(BranchLastCommit);
            }

            if (!long.TryParse(output.Substring(0, separator), out var unixSeconds))
            {
                return default(BranchLastCommit);
            }

            return new BranchLastCommit
            {
                HasValue = true,
                UnixSeconds = unixSeconds,
                Author = output.Substring(separator + 1).Trim()
            };
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
                if (!string.IsNullOrWhiteSpace(TestProjectRootOverride))
                {
                    return TestProjectRootOverride;
                }

                return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            }
        }

        internal static string TestProjectRootOverride { get; set; }

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
                "Then press DreamScripts/GitHub/Push/Upload again.\n\n" +
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
            private long _lastCommitUnixSeconds = long.MinValue;
            private string _lastAuthorLabel = string.Empty;
            private string _lastUpdatedLabel = string.Empty;

            public BranchChoice(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public bool ExistsLocal { get; set; }
            public bool ExistsRemote { get; set; }
            public bool IsCurrent { get; set; }

            public string LastUpdatedLabel
            {
                get { return EmptyFallback(_lastUpdatedLabel, "unknown"); }
            }

            public string LastAuthorLabel
            {
                get { return EmptyFallback(_lastAuthorLabel, "unknown"); }
            }

            public void ConsiderLastCommit(BranchLastCommit lastCommit)
            {
                if (!lastCommit.HasValue)
                {
                    return;
                }

                if (_lastCommitUnixSeconds != long.MinValue && lastCommit.UnixSeconds < _lastCommitUnixSeconds)
                {
                    return;
                }

                _lastCommitUnixSeconds = lastCommit.UnixSeconds;
                _lastUpdatedLabel = FormatBranchLastUpdate(lastCommit.UnixSeconds);
                _lastAuthorLabel = lastCommit.Author;
            }

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

        private struct BranchLastCommit
        {
            public bool HasValue;
            public long UnixSeconds;
            public string Author;
        }

        private sealed class CommitMessageWindow : EditorWindow
        {
            private Action<string> _onCommit;
            private string _actionLabel;
            private string _changeSummary;
            private string _commitMessage;
            private string _description;
            private string _titleText;
            private Vector2 _scroll;

            public static void Open(
                string title,
                string description,
                string actionLabel,
                string defaultMessage,
                string changeSummary,
                Action<string> onCommit)
            {
                var window = CreateInstance<CommitMessageWindow>();
                window.titleContent = new GUIContent(title);
                window._titleText = title;
                window._description = description;
                window._actionLabel = actionLabel;
                window._commitMessage = defaultMessage ?? string.Empty;
                window._changeSummary = changeSummary ?? string.Empty;
                window._onCommit = onCommit;
                window.minSize = new Vector2(560, 520);
                window.ShowUtility();
                window.Focus();
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField(_titleText, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_description, MessageType.Info);

                EditorGUILayout.LabelField("Commit message", EditorStyles.boldLabel);
                GUI.SetNextControlName("DreamScriptsGitHubCommitMessage");
                _commitMessage = EditorGUILayout.TextArea(_commitMessage ?? string.Empty, GUILayout.MinHeight(76));

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Changes", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(220));
                EditorGUILayout.TextArea(EmptyFallback(_changeSummary, "No changes listed."), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                var canCommit = !string.IsNullOrWhiteSpace(_commitMessage);
                if (!canCommit)
                {
                    EditorGUILayout.HelpBox("Write a commit message before continuing.", MessageType.Warning);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(!canCommit))
                {
                    if (GUILayout.Button(_actionLabel, GUILayout.Width(160), GUILayout.Height(30)))
                    {
                        var onCommit = _onCommit;
                        var message = (_commitMessage ?? string.Empty).Trim();
                        Close();

                        if (onCommit != null)
                        {
                            EditorApplication.delayCall += () => onCommit(message);
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private sealed class MultiBranchPickerWindow : EditorWindow
        {
            private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<BranchChoice> _choices = new List<BranchChoice>();
            private Action<List<string>> _onSelected;
            private string _actionLabel;
            private string _description;
            private string _search;
            private string _titleText;
            private Vector2 _scroll;

            public static void Open(
                string title,
                string description,
                string actionLabel,
                List<BranchChoice> choices,
                Action<List<string>> onSelected)
            {
                var window = CreateInstance<MultiBranchPickerWindow>();
                window.titleContent = new GUIContent(title);
                window._titleText = title;
                window._description = description;
                window._actionLabel = actionLabel;
                window._onSelected = onSelected;

                if (choices != null)
                {
                    window._choices.AddRange(choices);
                }

                window.minSize = new Vector2(820, 560);
                window.ShowUtility();
                window.Focus();
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField(_titleText, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(_description, MessageType.Info);

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

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("GitHub branches", EditorStyles.boldLabel);
                DrawBranchColumnsHeader();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(300));
                var visibleCount = 0;
                foreach (var choice in _choices)
                {
                    if (!MatchesSearch(choice))
                    {
                        continue;
                    }

                    var rowIndex = visibleCount;
                    visibleCount++;
                    EditorGUILayout.BeginHorizontal(GetBranchRowStyle(selected: _selected.Contains(choice.Name), rowIndex: rowIndex), GUILayout.Height(BranchRowHeight));
                    var selected = _selected.Contains(choice.Name);
                    var nextSelected = GUILayout.Toggle(selected, GUIContent.none, GUILayout.Width(24), GUILayout.Height(24));
                    if (GUILayout.Button(choice.Name, selected ? _branchSelectedNameStyle : _branchNameStyle, GUILayout.Height(24)))
                    {
                        nextSelected = !selected;
                    }

                    if (nextSelected != selected)
                    {
                        if (nextSelected)
                        {
                            _selected.Add(choice.Name);
                        }
                        else
                        {
                            _selected.Remove(choice.Name);
                        }
                    }

                    DrawBranchMetadataColumns(choice);
                    EditorGUILayout.EndHorizontal();
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No branches match the current search.", MessageType.None);
                }

                EditorGUILayout.EndScrollView();

                var selectedCount = _selected.Count;
                EditorGUILayout.HelpBox(
                    selectedCount == 0
                        ? "Select one or more branches to merge into main."
                        : "Selected branches: " + selectedCount,
                    selectedCount == 0 ? MessageType.Warning : MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Close();
                }

                using (new EditorGUI.DisabledScope(selectedCount == 0))
                {
                    if (GUILayout.Button(_actionLabel, GUILayout.Width(160), GUILayout.Height(30)))
                    {
                        var selected = new List<string>(_selected);
                        selected.Sort(StringComparer.OrdinalIgnoreCase);
                        var onSelected = _onSelected;
                        Close();

                        if (onSelected != null)
                        {
                            EditorApplication.delayCall += () => onSelected(selected);
                        }
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
                       choice.LastUpdatedLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       choice.LastAuthorLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       choice.StatusLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
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
            private bool _useManualBranch;
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
                window.minSize = new Vector2(820, 520);
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

                if (_allowManualBranch)
                {
                    DrawModeTabs();
                }

                if (_allowManualBranch && _useManualBranch)
                {
                    DrawNewBranchPanel();
                }
                else
                {
                    DrawSearch();
                    DrawBranchList();
                }

                DrawFooter();
            }

            private void DrawModeTabs()
            {
                EditorGUILayout.Space(8);
                var nextMode = GUILayout.Toolbar(
                    _useManualBranch ? 1 : 0,
                    new[] { "Existing Branch", "New Branch" },
                    GUILayout.Height(30));

                var nextUseManual = nextMode == 1;
                if (nextUseManual != _useManualBranch)
                {
                    _useManualBranch = nextUseManual;
                    GUI.FocusControl(null);
                }
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
                DrawBranchColumnsHeader();

                var visibleCount = 0;
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(190));
                foreach (var choice in _choices)
                {
                    if (!MatchesSearch(choice))
                    {
                        continue;
                    }

                    var rowIndex = visibleCount;
                    visibleCount++;
                    DrawBranchRow(choice, rowIndex);
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No branches match the current search.", MessageType.None);
                }

                EditorGUILayout.EndScrollView();
            }

            private void DrawBranchRow(BranchChoice choice, int rowIndex)
            {
                var selected = string.Equals(_selectedBranch, choice.Name, StringComparison.Ordinal);
                EditorGUILayout.BeginHorizontal(GetBranchRowStyle(selected, rowIndex), GUILayout.Height(BranchRowHeight));
                if (GUILayout.Button(choice.Name, selected ? _branchSelectedNameStyle : _branchNameStyle, GUILayout.Height(24)))
                {
                    _selectedBranch = choice.Name;
                    _manualBranch = string.Empty;
                    _useManualBranch = false;
                }

                DrawBranchMetadataColumns(choice);
                EditorGUILayout.EndHorizontal();
            }

            private void DrawNewBranchPanel()
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(_manualLabel, EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Push/Upload will create or update this GitHub branch from the current saved project state.", MessageType.Info);

                GUI.SetNextControlName("DreamScriptsGitHubNewBranch");
                _manualBranch = EditorGUILayout.TextField(_manualBranch ?? string.Empty, GUILayout.Height(24));

                if (string.IsNullOrWhiteSpace(_manualBranch))
                {
                    EditorGUILayout.HelpBox("Example: feature/new-level-art", MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox("This typed branch is the upload target. The selected branch list is ignored.", MessageType.Info);
                }

                EditorGUILayout.EndVertical();
            }

            private void DrawFooter()
            {
                EditorGUILayout.Space(8);
                var canConfirm = TryGetCandidate(out var branch, out var error);
                if (canConfirm)
                {
                    var source = _allowManualBranch && _useManualBranch ? "New branch: " : "Selected branch: ";
                    EditorGUILayout.HelpBox(source + branch, MessageType.Info);
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
                    var buttonLabel = _allowManualBranch && _useManualBranch
                        ? "Create + " + _actionLabel
                        : _actionLabel;
                    if (GUILayout.Button(buttonLabel, GUILayout.Width(140), GUILayout.Height(30)))
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
                       choice.LastUpdatedLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       choice.LastAuthorLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       choice.StatusLabel.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private bool TryGetCandidate(out string branch, out string error)
            {
                var raw = _allowManualBranch && _useManualBranch ? _manualBranch : _selectedBranch;
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

        private static void DrawBranchColumnsHeader()
        {
            EnsureBranchStyles();
            EditorGUILayout.BeginHorizontal(_branchHeaderStyle, GUILayout.Height(28));
            EditorGUILayout.LabelField("Branch", _branchHeaderLabelStyle);
            GUILayout.Label("Last update", _branchHeaderLabelStyle, GUILayout.Width(BranchLastUpdateColumnWidth));
            GUILayout.Label("Who", _branchHeaderLabelStyle, GUILayout.Width(BranchAuthorColumnWidth));
            GUILayout.Label("Status", _branchHeaderLabelStyle, GUILayout.Width(BranchStatusColumnWidth));
            EditorGUILayout.EndHorizontal();
        }

        private static GUIStyle GetBranchRowStyle(bool selected, int rowIndex)
        {
            EnsureBranchStyles();
            if (selected)
            {
                return _branchSelectedRowStyle;
            }

            return rowIndex % 2 == 0 ? _branchEvenRowStyle : _branchOddRowStyle;
        }

        private static void DrawBranchMetadataColumns(BranchChoice choice)
        {
            GUILayout.Label(choice.LastUpdatedLabel, _branchMetaStyle, GUILayout.Width(BranchLastUpdateColumnWidth), GUILayout.Height(24));
            GUILayout.Label(choice.LastAuthorLabel, _branchMetaStyle, GUILayout.Width(BranchAuthorColumnWidth), GUILayout.Height(24));
            GUILayout.Label(choice.StatusLabel, GetBranchStatusStyle(choice), GUILayout.Width(BranchStatusColumnWidth), GUILayout.Height(22));
        }

        private static GUIStyle GetBranchStatusStyle(BranchChoice choice)
        {
            EnsureBranchStyles();
            if (choice.IsCurrent && choice.ExistsRemote)
            {
                return _statusCurrentRemoteStyle;
            }

            if (choice.IsCurrent)
            {
                return _statusCurrentLocalStyle;
            }

            if (choice.ExistsLocal && choice.ExistsRemote)
            {
                return _statusLocalRemoteStyle;
            }

            return choice.ExistsRemote ? _statusRemoteOnlyStyle : _statusLocalOnlyStyle;
        }

        private static void EnsureBranchStyles()
        {
            var proSkin = EditorGUIUtility.isProSkin;
            if (_branchStylesInitialized && _branchStylesProSkin == proSkin)
            {
                return;
            }

            _branchStylesInitialized = true;
            _branchStylesProSkin = proSkin;

            var headerBackground = proSkin ? new Color(0.15f, 0.18f, 0.22f) : new Color(0.78f, 0.84f, 0.91f);
            var evenBackground = proSkin ? new Color(0.20f, 0.20f, 0.20f) : new Color(0.93f, 0.95f, 0.97f);
            var oddBackground = proSkin ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.88f, 0.91f, 0.94f);
            var selectedBackground = proSkin ? new Color(0.12f, 0.30f, 0.50f) : new Color(0.54f, 0.72f, 0.95f);
            var headerText = proSkin ? new Color(0.82f, 0.88f, 0.94f) : new Color(0.12f, 0.17f, 0.24f);
            var text = proSkin ? new Color(0.88f, 0.90f, 0.92f) : new Color(0.10f, 0.13f, 0.18f);
            var mutedText = proSkin ? new Color(0.72f, 0.76f, 0.80f) : new Color(0.28f, 0.34f, 0.42f);
            var selectedText = Color.white;

            _branchHeaderStyle = MakeBranchBoxStyle(headerBackground, new RectOffset(8, 8, 4, 4), new RectOffset(0, 0, 6, 2));
            _branchEvenRowStyle = MakeBranchBoxStyle(evenBackground, new RectOffset(8, 8, 3, 3), new RectOffset(0, 0, 1, 1));
            _branchOddRowStyle = MakeBranchBoxStyle(oddBackground, new RectOffset(8, 8, 3, 3), new RectOffset(0, 0, 1, 1));
            _branchSelectedRowStyle = MakeBranchBoxStyle(selectedBackground, new RectOffset(8, 8, 3, 3), new RectOffset(0, 0, 1, 1));

            _branchHeaderLabelStyle = MakeBranchLabelStyle(headerText, FontStyle.Bold, TextAnchor.MiddleLeft);
            _branchNameStyle = MakeBranchButtonStyle(text, FontStyle.Bold);
            _branchSelectedNameStyle = MakeBranchButtonStyle(selectedText, FontStyle.Bold);
            _branchMetaStyle = MakeBranchLabelStyle(mutedText, FontStyle.Normal, TextAnchor.MiddleLeft);

            _statusCurrentRemoteStyle = MakeStatusStyle(new Color(0.16f, 0.44f, 0.75f));
            _statusCurrentLocalStyle = MakeStatusStyle(new Color(0.75f, 0.47f, 0.13f));
            _statusLocalRemoteStyle = MakeStatusStyle(new Color(0.18f, 0.50f, 0.28f));
            _statusRemoteOnlyStyle = MakeStatusStyle(new Color(0.25f, 0.35f, 0.47f));
            _statusLocalOnlyStyle = MakeStatusStyle(new Color(0.43f, 0.43f, 0.45f));
        }

        private static GUIStyle MakeBranchBoxStyle(Color background, RectOffset padding, RectOffset margin)
        {
            var style = new GUIStyle(GUIStyle.none)
            {
                padding = padding,
                margin = margin
            };
            style.normal.background = MakeTexture(background);
            return style;
        }

        private static GUIStyle MakeBranchButtonStyle(Color textColor, FontStyle fontStyle)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis,
                fontStyle = fontStyle,
                padding = new RectOffset(0, 6, 0, 0)
            };
            style.normal.textColor = textColor;
            style.hover.textColor = textColor;
            style.active.textColor = textColor;
            style.focused.textColor = textColor;
            return style;
        }

        private static GUIStyle MakeBranchLabelStyle(Color textColor, FontStyle fontStyle, TextAnchor alignment)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = alignment,
                clipping = TextClipping.Ellipsis,
                fontStyle = fontStyle,
                padding = new RectOffset(0, 4, 0, 0)
            };
            style.normal.textColor = textColor;
            return style;
        }

        private static GUIStyle MakeStatusStyle(Color background)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 0, 1)
            };
            style.normal.background = MakeTexture(background);
            style.normal.textColor = Color.white;
            return style;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
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
