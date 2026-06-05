using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;

namespace NumiDream.Tests.EditMode
{
    public sealed class GitHubToolsTests
    {
        private const string GitHubToolsTypeName = "DreamScripts.EditorTools.GitHubTools, Assembly-CSharp-Editor";
        private const string RegistryTypeName = "DreamScripts.EditorTools.DreamScriptRegistry, Assembly-CSharp-Editor";

        private readonly List<string> _tempRoots = new List<string>();
        private Type _gitHubToolsType;

        [SetUp]
        public void SetUp()
        {
            _gitHubToolsType = Type.GetType(GitHubToolsTypeName, throwOnError: true);
            RuntimeHelpers.RunClassConstructor(_gitHubToolsType.TypeHandle);
        }

        [TearDown]
        public void TearDown()
        {
            var overrideProperty = _gitHubToolsType?.GetProperty(
                "TestProjectRootOverride",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            overrideProperty?.SetValue(null, null);

            foreach (var root in _tempRoots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    Directory.Delete(root, recursive: true);
                }
                catch
                {
                    // Temp cleanup is best-effort; a git process can briefly hold a lock on Windows.
                }
            }

            _tempRoots.Clear();
        }

        [Test]
        public void DreamToolbarRegistersAllGitHubActions()
        {
            var registryType = Type.GetType(RegistryTypeName, throwOnError: true);
            var getActions = registryType.GetMethod("GetActions", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var actions = (System.Collections.IEnumerable)getActions.Invoke(null, Array.Empty<object>());
            var paths = actions
                .Cast<object>()
                .Select(action => (string)action.GetType().GetProperty("Path").GetValue(action))
                .ToArray();

            Assert.That(paths, Does.Contain("GitHub/Push/Upload"));
            Assert.That(paths, Does.Contain("GitHub/Pull/Import"));
            Assert.That(paths, Does.Contain("GitHub/Commit/Write Commit"));
            Assert.That(paths, Does.Contain("GitHub/History/Branch History"));
            Assert.That(paths, Does.Contain("GitHub/Merge/Branch Into Current"));
            Assert.That(paths, Does.Contain("GitHub/Merge/Branches Into Main"));
            Assert.That(paths, Does.Contain("GitHub/Repo/Status"));
            Assert.That(paths, Does.Contain("GitHub/Repo/SetRepo"));
            Assert.That(paths, Does.Contain("GitHub/Repo/EnterRepo"));
            Assert.That(paths, Does.Contain("GitHub/Repo/Refresh Branches"));
        }

        [Test]
        public void UnityMenuItemsUseSafeGitHubPaths()
        {
            var menuPaths = _gitHubToolsType
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), inherit: false))
                .Select(attribute => (string)attribute.GetType().GetField("menuItem").GetValue(attribute))
                .ToArray();

            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Push/Upload"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Pull/Import"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Repo/SetRepo"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Repo/EnterRepo"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Repo/Refresh Branches"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Commit/Write Commit"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/History/Branch History"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Merge/Branch Into Current"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/GitHub/Merge/Branches Into Main"));
        }

        [Test]
        public void RepoUrlsAndBranchNamesAreNormalizedAndValidated()
        {
            Assert.That(Invoke<string>("TestToGitHubBrowserUrl", "git@github.com:team/game.git"), Is.EqualTo("https://github.com/team/game"));
            Assert.That(Invoke<string>("TestToGitHubBrowserUrl", "ssh://git@github.com/team/game.git"), Is.EqualTo("https://github.com/team/game"));
            Assert.That(Invoke<string>("TestToGitHubBrowserUrl", "https://github.com/team/game.git"), Is.EqualTo("https://github.com/team/game"));
            Assert.That(Invoke<string>("TestToGitHubBrowserUrl", "https://example.com/team/game.git"), Is.Empty);

            Assert.That(Invoke<string>("TestNormalizeBranchInput", "refs/remotes/origin/sound/scene-one"), Is.EqualTo("sound/scene-one"));
            Assert.That(Invoke<string>("TestNormalizeBranchInput", "origin/unity/scene-one"), Is.EqualTo("unity/scene-one"));

            var validArgs = new object[] { "feature/scene-one", null };
            Assert.That((bool)Invoke("TestIsValidBranchName", validArgs), Is.True);
            Assert.That((string)validArgs[1], Is.Empty);

            var invalidArgs = new object[] { "bad branch name", null };
            Assert.That((bool)Invoke("TestIsValidBranchName", invalidArgs), Is.False);
            Assert.That((string)invalidArgs[1], Does.Contain("character"));
        }

        [Test]
        public void CommitWritesUserMessageAndStagesAllChanges()
        {
            var repo = CreateRepoWithOrigin();
            File.WriteAllText(Path.Combine(repo.WorkTree, "scene-one.txt"), "local work\n");

            var args = new object[] { repo.WorkTree, "Manual scene one commit", null };
            Assert.That((bool)Invoke("TestCommitPendingChanges", args), Is.True, "Commit helper should succeed.");

            Assert.That((string)args[2], Does.Contain("scene-one.txt"));
            Assert.That(Git(repo.WorkTree, "log -1 --pretty=%s").Output.Trim(), Is.EqualTo("Manual scene one commit"));
            Assert.That(Git(repo.WorkTree, "status --porcelain").Output.Trim(), Is.Empty);
        }

        [Test]
        public void PushUploadPushesCurrentHeadToSelectedBranch()
        {
            var repo = CreateRepoWithOrigin();
            File.WriteAllText(Path.Combine(repo.WorkTree, "upload.txt"), "upload change\n");
            Git(repo.WorkTree, "add -A").AssertSuccess();
            Git(repo.WorkTree, "commit -m " + Quote("Upload commit")).AssertSuccess();

            var args = new object[] { repo.WorkTree, "feature/upload", "main", false, null };
            Assert.That((bool)Invoke("TestPushToRemoteBranch", args), Is.True, (string)args[4]);

            var localHead = Git(repo.WorkTree, "rev-parse HEAD").Output.Trim();
            var remoteHead = Git(repo.WorkTree, "ls-remote origin refs/heads/feature/upload").Output.Split('\t')[0].Trim();
            Assert.That(remoteHead, Is.EqualTo(localHead));
        }

        [Test]
        public void PullImportCreatesBackupAndResetsToRemoteBranch()
        {
            var repo = CreateRepoWithOrigin();
            var originalHead = Git(repo.WorkTree, "rev-parse HEAD").Output.Trim();
            CreateAndPushBranch(repo, "sound/scene-one", "scene-one-sound.txt", "sound work\n", "Sound work");
            Git(repo.WorkTree, "checkout main").AssertSuccess();

            var args = new object[] { repo.WorkTree, "sound/scene-one", null, null };
            Assert.That((bool)Invoke("TestImportFromBranchNoDialogs", args), Is.True, (string)args[3]);

            var backupBranch = (string)args[2];
            Assert.That(backupBranch, Does.StartWith("backup/test-import-main-"));
            Assert.That(Git(repo.WorkTree, "rev-parse " + Quote(backupBranch)).Output.Trim(), Is.EqualTo(originalHead));
            Assert.That(File.ReadAllText(Path.Combine(repo.WorkTree, "scene-one-sound.txt")), Is.EqualTo("sound work\n"));
            Assert.That(Git(repo.WorkTree, "rev-parse HEAD").Output.Trim(), Is.EqualTo(Git(repo.WorkTree, "rev-parse origin/sound/scene-one").Output.Trim()));
        }

        [Test]
        public void PullImportRejectsBranchWithoutRestoredDreamScripts()
        {
            var repo = CreateRepoWithOrigin(includeRestoredDreamScripts: false);
            CreateAndPushBranch(repo, "old/sound-upload", "old-sound.txt", "old branch\n", "Old sound upload");
            Git(repo.WorkTree, "checkout main").AssertSuccess();

            var guardArgs = new object[] { repo.WorkTree, "origin/old/sound-upload", null };
            Assert.That((bool)Invoke("TestRemoteKeepsRestoredDreamScripts", guardArgs), Is.False);
            Assert.That((string)guardArgs[2], Does.Contain("Assets/DreamScripts/Editor/GitHubTools.cs is missing"));

            var importArgs = new object[] { repo.WorkTree, "old/sound-upload", null, null };
            Assert.That((bool)Invoke("TestImportFromBranchNoDialogs", importArgs), Is.False);
            Assert.That((string)importArgs[3], Does.Contain("GitHubTools.cs is missing"));
            Assert.That(File.Exists(Path.Combine(repo.WorkTree, "old-sound.txt")), Is.False);
        }

        [Test]
        public void HistoryAndRepoStatusIncludeTimeBranchAndRemoteInformation()
        {
            var repo = CreateRepoWithOrigin();
            CreateAndPushBranch(repo, "history/scene-one", "history.txt", "history\n", "History commit");

            var history = Invoke<string>("TestBranchHistory", repo.WorkTree, "history/scene-one");
            Assert.That(history, Does.Contain("History commit"));
            Assert.That(history, Does.Contain("Test User"));
            Assert.That(history, Does.Contain("|"));

            var status = Invoke<string>("TestRepoStatus", repo.WorkTree);
            Assert.That(status, Does.Contain("## main"));
            Assert.That(status, Does.Contain("origin"));
        }

        [Test]
        public void BranchPickerRowsIncludeLastUpdateDateAndAuthor()
        {
            var repo = CreateRepoWithOrigin();
            CreateAndPushBranch(repo, "history/scene-one", "history-row.txt", "history row\n", "History row");

            var rows = Invoke<string[]>("TestBranchChoiceRows", repo.WorkTree, "main", false);
            var row = rows.FirstOrDefault(value => value.StartsWith("history/scene-one|", StringComparison.Ordinal));

            Assert.That(row, Is.Not.Null);
            Assert.That(row, Does.Contain(((char)0x200E).ToString()));
            Assert.That(row, Does.Match(@"\d{1,2}/\d{1,2}/\d{4}"));
            Assert.That(row, Does.Contain("|Test User|"));
            Assert.That(row, Does.Contain("local + GitHub"));
        }

        [Test]
        public void BranchPickerUsesBatchedMetadataAndDoesNotFetchBeforeOpening()
        {
            var source = File.ReadAllText("Assets/DreamScripts/Editor/GitHubTools.cs");

            Assert.That(source, Does.Contain("for-each-ref --format="));
            Assert.That(source, Does.Contain("%(committerdate:unix)"));
            Assert.That(source, Does.Contain("ReadBranchRefs"));
            Assert.That(source, Does.Not.Contain("ReadBranchLastCommit"));
            Assert.That(source, Does.Not.Contain("RefreshOriginBranchList"));
        }

        [Test]
        public void MergeBranchIntoCurrentCreatesBackupAndMergesBranch()
        {
            var repo = CreateRepoWithOrigin();
            CreateAndPushBranch(repo, "unity/scene-one", "unity-art.txt", "unity work\n", "Unity work");
            Git(repo.WorkTree, "checkout main").AssertSuccess();

            var args = new object[] { repo.WorkTree, "unity/scene-one", null, null };
            Assert.That((bool)Invoke("TestMergeBranchIntoCurrentNoDialogs", args), Is.True, (string)args[3]);

            var backupBranch = (string)args[2];
            Assert.That(backupBranch, Does.StartWith("backup/test-merge-current-main-"));
            Assert.That(File.ReadAllText(Path.Combine(repo.WorkTree, "unity-art.txt")), Is.EqualTo("unity work\n"));
            Assert.That(Git(repo.WorkTree, "show-ref --verify --quiet " + Quote("refs/heads/" + backupBranch)).ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void MergeBranchesIntoMainMergesAllBranchesAndPushesMain()
        {
            var repo = CreateRepoWithOrigin();
            CreateAndPushBranch(repo, "unity/scene-one", "unity-art.txt", "unity work\n", "Unity work");
            CreateAndPushBranch(repo, "sound/scene-one", "sound-bank.txt", "sound work\n", "Sound work");
            Git(repo.WorkTree, "checkout main").AssertSuccess();

            var args = new object[] { repo.WorkTree, new[] { "unity/scene-one", "sound/scene-one" }, null, null };
            Assert.That((bool)Invoke("TestMergeBranchesIntoMainNoDialogs", args), Is.True, (string)args[3]);

            Assert.That((string)args[2], Does.StartWith("backup/test-main-before-merge-main-"));
            Git(repo.WorkTree, "fetch origin main").AssertSuccess();
            Assert.That(Git(repo.WorkTree, "show origin/main:unity-art.txt").Output, Is.EqualTo("unity work\n"));
            Assert.That(Git(repo.WorkTree, "show origin/main:sound-bank.txt").Output, Is.EqualTo("sound work\n"));
        }

        [Test]
        public void MergeBranchesIntoMainConflictRestoresMainAndDoesNotPush()
        {
            var repo = CreateRepoWithOrigin();
            File.WriteAllText(Path.Combine(repo.WorkTree, "shared-scene.txt"), "base\n");
            Git(repo.WorkTree, "add -A").AssertSuccess();
            Git(repo.WorkTree, "commit -m " + Quote("Base shared file")).AssertSuccess();
            Git(repo.WorkTree, "push origin main").AssertSuccess();

            CreateAndPushBranch(repo, "unity/conflict", "shared-scene.txt", "unity\n", "Unity conflict");
            CreateAndPushBranch(repo, "sound/conflict", "shared-scene.txt", "sound\n", "Sound conflict");
            Git(repo.WorkTree, "checkout main").AssertSuccess();

            var args = new object[] { repo.WorkTree, new[] { "unity/conflict", "sound/conflict" }, null, null };
            Assert.That((bool)Invoke("TestMergeBranchesIntoMainNoDialogs", args), Is.False, "Conflicting merge should fail safely.");

            Assert.That(File.ReadAllText(Path.Combine(repo.WorkTree, "shared-scene.txt")), Is.EqualTo("base\n"));
            Git(repo.WorkTree, "fetch origin main").AssertSuccess();
            Assert.That(Git(repo.WorkTree, "show origin/main:shared-scene.txt").Output, Is.EqualTo("base\n"));
            Assert.That((string)args[2], Does.StartWith("backup/test-main-before-merge-main-"));
        }

        private TempRepo CreateRepoWithOrigin(bool includeRestoredDreamScripts = true)
        {
            var root = Path.Combine(Path.GetTempPath(), "NumiDreamGitHubToolsTests-" + Guid.NewGuid().ToString("N"));
            var workTree = Path.Combine(root, "work");
            var origin = Path.Combine(root, "origin.git");
            Directory.CreateDirectory(workTree);
            Directory.CreateDirectory(origin);
            _tempRoots.Add(root);

            Git(origin, "init --bare").AssertSuccess();
            Git(workTree, "init").AssertSuccess();
            Git(workTree, "checkout -B main").AssertSuccess();
            Git(workTree, "config user.email tests@example.com").AssertSuccess();
            Git(workTree, "config user.name " + Quote("Test User")).AssertSuccess();
            File.WriteAllText(Path.Combine(workTree, "README.md"), "initial\n");
            if (includeRestoredDreamScripts)
            {
                WriteRestoredDreamScriptsMarkers(workTree);
            }

            Git(workTree, "add -A").AssertSuccess();
            Git(workTree, "commit -m " + Quote("Initial commit")).AssertSuccess();
            Git(workTree, "remote add origin " + Quote(origin)).AssertSuccess();
            Git(workTree, "push -u origin main").AssertSuccess();

            return new TempRepo(root, workTree, origin);
        }

        private static void WriteRestoredDreamScriptsMarkers(string workTree)
        {
            var gitHubToolsPath = Path.Combine(workTree, "Assets", "DreamScripts", "Editor", "GitHubTools.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(gitHubToolsPath));
            File.WriteAllText(
                gitHubToolsPath,
                "GitHub/Push/Upload\nGitHub/Pull/Import\nGitHub/Repo/SetRepo\nGitHub/Merge/Branches Into Main\n");

            var manifestPath = Path.Combine(workTree, "Packages", "manifest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            File.WriteAllText(
                manifestPath,
                "{\"dependencies\":{\"com.coplaydev.unity-mcp\":\"https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v9.7.1\"}}\n");

            var testsPath = Path.Combine(workTree, "Assets", "NumiDream", "Tests", "EditMode", "GitHubToolsTests.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(testsPath));
            File.WriteAllText(
                testsPath,
                "MergeBranchesIntoMainConflictRestoresMainAndDoesNotPush\nPullImportRejectsBranchWithoutRestoredDreamScripts\n");
        }

        private static void CreateAndPushBranch(TempRepo repo, string branch, string fileName, string content, string message)
        {
            Git(repo.WorkTree, "checkout -B " + Quote(branch) + " main").AssertSuccess();
            File.WriteAllText(Path.Combine(repo.WorkTree, fileName), content);
            Git(repo.WorkTree, "add -A").AssertSuccess();
            Git(repo.WorkTree, "commit -m " + Quote(message)).AssertSuccess();
            Git(repo.WorkTree, "push -u origin " + Quote(branch)).AssertSuccess();
            Git(repo.WorkTree, "checkout main").AssertSuccess();
        }

        private static GitProcessResult Git(string workingDirectory, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new GitProcessResult(process.ExitCode, output, error);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private object Invoke(string methodName, params object[] args)
        {
            return Invoke(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, args);
        }

        private object Invoke(string methodName, BindingFlags flags, params object[] args)
        {
            var method = _gitHubToolsType.GetMethod(methodName, flags);
            Assert.That(method, Is.Not.Null, "Missing GitHubTools method: " + methodName);
            return method.Invoke(null, args);
        }

        private T Invoke<T>(string methodName, params object[] args)
        {
            return (T)Invoke(methodName, args);
        }

        private readonly struct TempRepo
        {
            public TempRepo(string root, string workTree, string origin)
            {
                Root = root;
                WorkTree = workTree;
                Origin = origin;
            }

            public string Root { get; }
            public string WorkTree { get; }
            public string Origin { get; }
        }

        private readonly struct GitProcessResult
        {
            public GitProcessResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public int ExitCode { get; }
            public string Output { get; }
            public string Error { get; }

            public void AssertSuccess()
            {
                Assert.That(ExitCode, Is.EqualTo(0), Output + "\n" + Error);
            }
        }
    }
}
