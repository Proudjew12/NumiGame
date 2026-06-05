using System;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace NumiDream.Tests.EditMode
{
    public sealed class McpForUnityPackageTests
    {
        private const string PackageName = "com.coplaydev.unity-mcp";
        private const string ExpectedVersion = "9.7.1";
        private const string EditorWindowTypeName =
            "MCPForUnity.Editor.Windows.MCPForUnityEditorWindow, MCPForUnity.Editor";

        [Test]
        public void PackageIsInstalledAtExpectedVersion()
        {
            var packageInfo = PackageInfo.FindForPackageName(PackageName);

            Assert.That(packageInfo, Is.Not.Null, $"{PackageName} should be installed.");
            Assert.That(packageInfo.name, Is.EqualTo(PackageName));
            Assert.That(packageInfo.version, Is.EqualTo(ExpectedVersion));
            Assert.That(packageInfo.source, Is.EqualTo(PackageSource.Git));
        }

        [Test]
        public void EditorWindowTypeIsLoadable()
        {
            var editorWindowType = Type.GetType(EditorWindowTypeName);

            Assert.That(editorWindowType, Is.Not.Null, "MCP for Unity editor assembly should be loaded.");
            Assert.That(typeof(EditorWindow).IsAssignableFrom(editorWindowType), Is.True);
        }
    }
}
