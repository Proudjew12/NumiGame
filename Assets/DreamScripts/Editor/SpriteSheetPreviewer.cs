using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal sealed class SpriteSheetPreviewer : EditorWindow
    {
        private const string MenuPath = "DreamScripts/Sprite Sheet Preview";
        private const int RootMenuPriority = 60080;

        private Texture2D _spriteSheet;
        private Vector2 _scroll;
        private Color _backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        private bool _isPlaying = true;
        private bool _fitToWindow = true;
        private float _zoom = 1f;
        private float _fps = 8f;
        private int _frameWidth = 828;
        private int _frameHeight = 1910;
        private int _columns = 6;
        private int _frameCount = 6;
        private int _currentFrame;
        private double _lastFrameTime;
        private bool _showGenerator = true;

        static SpriteSheetPreviewer()
        {
            DreamScriptRegistry.Register("Sprite Sheet Preview", OpenWindow, priority: 80);
        }

        [MenuItem(MenuPath, false, RootMenuPriority)]
        private static void OpenWindow()
        {
            var window = GetWindow<SpriteSheetPreviewer>("Sprite Sheet Preview");
            window.minSize = new Vector2(420f, 360f);
            window.TryUseSelection();
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            TryUseSelection();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!_isPlaying || _spriteSheet == null || _frameCount <= 1 || _fps <= 0f)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var frameDuration = 1.0 / _fps;
            if (now - _lastFrameTime < frameDuration)
            {
                return;
            }

            _lastFrameTime = now;
            _currentFrame = (_currentFrame + 1) % Mathf.Max(1, _frameCount);
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawSettings();
            DrawGenerator();
            DrawPreview();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(_isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    _isPlaying = !_isPlaying;
                    _lastFrameTime = EditorApplication.timeSinceStartup;
                }

                if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    _currentFrame = 0;
                    _lastFrameTime = EditorApplication.timeSinceStartup;
                    Repaint();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                {
                    TryUseSelection(force: true);
                }
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(8f);

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                _spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", _spriteSheet, typeof(Texture2D), false);
                if (change.changed)
                {
                    ApplyDetectedLayout();
                    _currentFrame = 0;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Detect", GUILayout.Width(110f)))
                {
                    ApplyDetectedLayout(showWarning: true);
                }

                _fitToWindow = EditorGUILayout.ToggleLeft("Fit", _fitToWindow, GUILayout.Width(48f));
                using (new EditorGUI.DisabledScope(_fitToWindow))
                {
                    _zoom = EditorGUILayout.Slider("Zoom", _zoom, 0.1f, 4f);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _frameWidth = Mathf.Max(1, EditorGUILayout.IntField("Frame W", _frameWidth));
                _frameHeight = Mathf.Max(1, EditorGUILayout.IntField("Frame H", _frameHeight));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _columns = Mathf.Max(1, EditorGUILayout.IntField("Columns", _columns));
                _frameCount = Mathf.Max(1, EditorGUILayout.IntField("Frames", _frameCount));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _fps = Mathf.Max(1f, EditorGUILayout.FloatField("FPS", _fps));
                _backgroundColor = EditorGUILayout.ColorField("Background", _backgroundColor);
            }

            _currentFrame = Mathf.Clamp(
                EditorGUILayout.IntSlider("Frame", _currentFrame + 1, 1, Mathf.Max(1, _frameCount)) - 1,
                0,
                Mathf.Max(0, _frameCount - 1));
        }

        private void DrawGenerator()
        {
            EditorGUILayout.Space(6f);
            _showGenerator = EditorGUILayout.Foldout(_showGenerator, "Animation Generator", true);
            if (!_showGenerator)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Output", GetOutputRootForCurrentSheet());
                EditorGUILayout.LabelField("Animator Params", "Speed float, Jump trigger");

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_spriteSheet == null || !IsLayoutValid()))
                    {
                        if (GUILayout.Button("Create Clip From Current Sheet"))
                        {
                            CreateCurrentSheetClip();
                        }
                    }

                    if (GUILayout.Button("Create Nomi Animator"))
                    {
                        CreateNomiAnimatorSet();
                    }
                }
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.Space(8f);

            var previewRect = GUILayoutUtility.GetRect(1f, 100000f, 1f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(previewRect, _backgroundColor);

            if (_spriteSheet == null)
            {
                DrawCenteredMessage(previewRect, "Select a sprite sheet texture.");
                return;
            }

            if (!IsLayoutValid())
            {
                DrawCenteredMessage(previewRect, "Frame layout does not fit inside the selected texture.");
                return;
            }

            var frameRect = GetFramePixelRect(_currentFrame);
            var uvRect = new Rect(
                frameRect.x / _spriteSheet.width,
                frameRect.y / _spriteSheet.height,
                frameRect.width / _spriteSheet.width,
                frameRect.height / _spriteSheet.height);

            var targetSize = CalculatePreviewSize(previewRect, frameRect.size);
            var contentRect = new Rect(0f, 0f, targetSize.x, targetSize.y);

            if (_fitToWindow)
            {
                contentRect.center = previewRect.center;
                GUI.DrawTextureWithTexCoords(contentRect, _spriteSheet, uvRect, true);
                DrawFrameLabel(previewRect);
                return;
            }

            var viewRect = new Rect(previewRect.x, previewRect.y, previewRect.width, previewRect.height);
            var scrollContentRect = new Rect(0f, 0f, Mathf.Max(targetSize.x, viewRect.width - 18f), Mathf.Max(targetSize.y, viewRect.height - 18f));

            _scroll = GUI.BeginScrollView(viewRect, _scroll, scrollContentRect);
            contentRect.center = scrollContentRect.center;
            GUI.DrawTextureWithTexCoords(contentRect, _spriteSheet, uvRect, true);
            GUI.EndScrollView();

            DrawFrameLabel(previewRect);
        }

        private Vector2 CalculatePreviewSize(Rect previewRect, Vector2 frameSize)
        {
            if (!_fitToWindow)
            {
                return frameSize * _zoom;
            }

            var scale = Mathf.Min(previewRect.width / frameSize.x, previewRect.height / frameSize.y) * 0.92f;
            scale = Mathf.Clamp(scale, 0.02f, 1f);
            return frameSize * scale;
        }

        private Rect GetFramePixelRect(int frameIndex)
        {
            var column = frameIndex % _columns;
            var rowFromTop = frameIndex / _columns;
            return new Rect(
                column * _frameWidth,
                _spriteSheet.height - ((rowFromTop + 1) * _frameHeight),
                _frameWidth,
                _frameHeight);
        }

        private bool IsLayoutValid()
        {
            if (_spriteSheet == null || _frameWidth <= 0 || _frameHeight <= 0 || _columns <= 0 || _frameCount <= 0)
            {
                return false;
            }

            var rows = Mathf.CeilToInt(_frameCount / (float)_columns);
            return _columns * _frameWidth <= _spriteSheet.width && rows * _frameHeight <= _spriteSheet.height;
        }

        private void DrawFrameLabel(Rect previewRect)
        {
            var labelRect = new Rect(previewRect.x + 10f, previewRect.y + 10f, 220f, 22f);
            EditorGUI.DrawRect(labelRect, new Color(0f, 0f, 0f, 0.45f));
            GUI.Label(labelRect, "  Frame " + (_currentFrame + 1) + " / " + _frameCount, EditorStyles.whiteLabel);
        }

        private static void DrawCenteredMessage(Rect rect, string message)
        {
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, message, style);
        }

        private void TryUseSelection(bool force = false)
        {
            if (!force && _spriteSheet != null)
            {
                return;
            }

            if (Selection.activeObject is Texture2D texture)
            {
                _spriteSheet = texture;
                ApplyDetectedLayout();
                _currentFrame = 0;
            }
        }

        private void ApplyDetectedLayout(bool showWarning = false)
        {
            if (_spriteSheet == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(_spriteSheet);
            if (TryGetKnownLayout(path, out var layout))
            {
                _frameWidth = layout.FrameWidth;
                _frameHeight = layout.FrameHeight;
                _columns = layout.Columns;
                _frameCount = layout.FrameCount;
                _fps = layout.Fps;
                return;
            }

            if (showWarning)
            {
                EditorUtility.DisplayDialog(
                    "Sprite Sheet Preview",
                    "No known layout for this sheet. Enter frame width, height, columns, and frame count manually.",
                    "OK");
            }
        }

        private static bool TryGetKnownLayout(string path, out SpriteSheetLayout layout)
        {
            switch (path)
            {
                case "Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/Nomi-Idle-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout("Nomi-Idle", 828, 1910, 6, 6, 6f, true, new Vector2(0.5f, 0f));
                    return true;
                case "Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/Nomi-Walk-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout("Nomi-Walk", 916, 2495, 5, 5, 8f, true, new Vector2(0.5f, 0f));
                    return true;
                case "Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/Nomi-Jump-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout("Nomi-Jump", 1068, 2203, 5, 9, 8f, false, new Vector2(0.5f, 0f));
                    return true;
                case "Assets/NumiDream/StageOne/Art/Collectibles/Memory-Fragment/Memory-Fragment-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout("Memory-Fragment", 1200, 1388, 5, 20, 10f, true, new Vector2(0.5f, 0.5f));
                    return true;
                default:
                    layout = default;
                    return false;
            }
        }

        private void CreateCurrentSheetClip()
        {
            if (_spriteSheet == null || !IsLayoutValid())
            {
                return;
            }

            var sourcePath = AssetDatabase.GetAssetPath(_spriteSheet);
            if (!TryGetKnownLayout(sourcePath, out var layout))
            {
                var clipName = Path.GetFileNameWithoutExtension(sourcePath);
                layout = new SpriteSheetLayout(clipName, _frameWidth, _frameHeight, _columns, _frameCount, _fps, true, new Vector2(0.5f, 0.5f));
            }

            var clip = GenerateClipFromSheet(sourcePath, layout, GetOutputRootForSheet(sourcePath));

            if (clip != null)
            {
                EditorGUIUtility.PingObject(clip);
                EditorUtility.DisplayDialog("Sprite Sheet Preview", "Created animation clip:\n" + AssetDatabase.GetAssetPath(clip), "OK");
            }
        }

        private static void CreateNomiAnimatorSet()
        {
            var outputRoot = GetNomiOutputRoot();
            var idle = GenerateKnownNomiClip("Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/Nomi-Idle-Sprite-Sheet.png", outputRoot);
            var walk = GenerateKnownNomiClip("Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/Nomi-Walk-Sprite-Sheet.png", outputRoot);
            var jump = GenerateKnownNomiClip("Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/Nomi-Jump-Sprite-Sheet.png", outputRoot);

            if (idle == null || walk == null || jump == null)
            {
                EditorUtility.DisplayDialog("Sprite Sheet Preview", "Could not generate all Nomi clips. Check that the three Nomi sprite sheets exist.", "OK");
                return;
            }

            var controller = CreateNomiAnimatorController(idle, walk, jump, outputRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(controller);

            EditorUtility.DisplayDialog(
                "Sprite Sheet Preview",
                "Created Nomi clips and animator:\n" + AssetDatabase.GetAssetPath(controller),
                "OK");
        }

        private static AnimationClip GenerateKnownNomiClip(string sheetPath, string outputRoot)
        {
            return TryGetKnownLayout(sheetPath, out var layout)
                ? GenerateClipFromSheet(sheetPath, layout, outputRoot)
                : null;
        }

        private static AnimationClip GenerateClipFromSheet(string sheetPath, SpriteSheetLayout layout, string outputRoot)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            if (sheet == null)
            {
                return null;
            }

            var framesFolder = outputRoot + "/Frames/" + layout.Name;
            var clipsFolder = outputRoot + "/Clips";
            EnsureAssetFolder(framesFolder);
            EnsureAssetFolder(clipsFolder);

            var readableSheet = CreateReadableCopy(sheet);
            var sprites = new List<Sprite>(layout.FrameCount);

            for (var index = 0; index < layout.FrameCount; index++)
            {
                var frameRect = GetFramePixelRect(sheet, layout, index);
                var frameTexture = new Texture2D(layout.FrameWidth, layout.FrameHeight, TextureFormat.RGBA32, false);
                frameTexture.SetPixels(readableSheet.GetPixels(
                    Mathf.RoundToInt(frameRect.x),
                    Mathf.RoundToInt(frameRect.y),
                    Mathf.RoundToInt(frameRect.width),
                    Mathf.RoundToInt(frameRect.height)));
                frameTexture.Apply();

                var framePath = framesFolder + "/" + layout.Name + "-Frame-" + (index + 1).ToString("00") + ".png";
                File.WriteAllBytes(framePath, frameTexture.EncodeToPNG());
                DestroyImmediate(frameTexture);
            }

            DestroyImmediate(readableSheet);
            AssetDatabase.Refresh();

            for (var index = 0; index < layout.FrameCount; index++)
            {
                var framePath = framesFolder + "/" + layout.Name + "-Frame-" + (index + 1).ToString("00") + ".png";
                ConfigureFrameTexture(framePath, layout.Pivot);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                if (sprite != null)
                {
                    sprites.Add(sprite);
                }
            }

            if (sprites.Count == 0)
            {
                return null;
            }

            var clipPath = clipsFolder + "/" + layout.Name + ".anim";
            if (File.Exists(clipPath))
            {
                AssetDatabase.DeleteAsset(clipPath);
            }

            var clip = new AnimationClip
            {
                frameRate = layout.Fps
            };

            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (var index = 0; index < sprites.Count; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = index / layout.Fps,
                    value = sprites[index]
                };
            }

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = layout.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        private static AnimatorController CreateNomiAnimatorController(AnimationClip idle, AnimationClip walk, AnimationClip jump, string outputRoot)
        {
            var controllersFolder = outputRoot + "/Controllers";
            EnsureAssetFolder(controllersFolder);

            var controllerPath = controllersFolder + "/NomiAnimator.controller";
            if (File.Exists(controllerPath))
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var stateMachine = controller.layers[0].stateMachine;
            var idleState = stateMachine.AddState("Idle", new Vector3(260f, 120f, 0f));
            var walkState = stateMachine.AddState("Walk", new Vector3(520f, 120f, 0f));
            var jumpState = stateMachine.AddState("Jump", new Vector3(390f, 300f, 0f));

            idleState.motion = idle;
            walkState.motion = walk;
            jumpState.motion = jump;
            stateMachine.defaultState = idleState;

            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0.08f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0.08f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var anyToJump = stateMachine.AddAnyStateTransition(jumpState);
            anyToJump.hasExitTime = false;
            anyToJump.duration = 0.04f;
            anyToJump.canTransitionToSelf = false;
            anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            var jumpToIdle = jumpState.AddTransition(idleState);
            jumpToIdle.hasExitTime = true;
            jumpToIdle.exitTime = 0.95f;
            jumpToIdle.duration = 0.08f;
            jumpToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var jumpToWalk = jumpState.AddTransition(walkState);
            jumpToWalk.hasExitTime = true;
            jumpToWalk.exitTime = 0.95f;
            jumpToWalk.duration = 0.08f;
            jumpToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            return controller;
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            var previous = RenderTexture.active;
            var renderTexture = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
            return readable;
        }

        private static Rect GetFramePixelRect(Texture2D sheet, SpriteSheetLayout layout, int frameIndex)
        {
            var column = frameIndex % layout.Columns;
            var rowFromTop = frameIndex / layout.Columns;
            return new Rect(
                column * layout.FrameWidth,
                sheet.height - ((rowFromTop + 1) * layout.FrameHeight),
                layout.FrameWidth,
                layout.FrameHeight);
        }

        private static void ConfigureFrameTexture(string framePath, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = pivot;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetFolder);
            Directory.CreateDirectory(fullPath);
        }

        private static string GetNomiOutputRoot()
        {
            return "Assets/NumiDream/Animations/Nomi";
        }

        private string GetOutputRootForCurrentSheet()
        {
            return _spriteSheet == null
                ? GetNomiOutputRoot()
                : GetOutputRootForSheet(AssetDatabase.GetAssetPath(_spriteSheet));
        }

        private static string GetOutputRootForSheet(string sheetPath)
        {
            return sheetPath.StartsWith("Assets/NumiDream/StageOne/Art/Collectibles/", System.StringComparison.Ordinal)
                ? "Assets/NumiDream/StageOne/Animations/Collectibles"
                : GetNomiOutputRoot();
        }

        private readonly struct SpriteSheetLayout
        {
            public SpriteSheetLayout(string name, int frameWidth, int frameHeight, int columns, int frameCount, float fps, bool loop, Vector2 pivot)
            {
                Name = name;
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                Columns = columns;
                FrameCount = frameCount;
                Fps = fps;
                Loop = loop;
                Pivot = pivot;
            }

            public string Name { get; }
            public int FrameWidth { get; }
            public int FrameHeight { get; }
            public int Columns { get; }
            public int FrameCount { get; }
            public float Fps { get; }
            public bool Loop { get; }
            public Vector2 Pivot { get; }
        }
    }
}
