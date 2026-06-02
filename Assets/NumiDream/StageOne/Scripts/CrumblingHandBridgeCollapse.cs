using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NumiDream.StageOne
{
    [DisallowMultipleComponent]
    public sealed class CrumblingHandBridgeCollapse : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Header("+Scene Objects+")]
        [Space(4)]
        [SerializeField] private Transform player;
        [Space(4)]
        [InspectorName("Piece Root")]
        [SerializeField] private Transform pieceRoot;
        [Space(4)]
        [InspectorName("Piece Prefix")]
        [SerializeField] private string pieceNamePrefix = "CrumblingHand-Piece-";

        [Space(10)]
        [Header("--------- Collapse ---------")]
        [Header("+Timing+")]
        [Space(4)]
        [InspectorName("Passed X Offset")]
        [SerializeField] private float passedXOffset = 0.2f;
        [Space(4)]
        [InspectorName("Shake Time")]
        [SerializeField] private float shakeDuration = 1.39f;
        [Space(4)]
        [InspectorName("Fall Time")]
        [SerializeField] private float fallDuration = 1.63f;
        [Space(4)]
        [InspectorName("Deactivate Below Y")]
        [SerializeField] private float deactivateBelowY = -18f;

        [Header("+Motion+")]
        [Space(4)]
        [InspectorName("Shake Amount")]
        [SerializeField] private float shakeAmount = 0.045f;
        [Space(4)]
        [InspectorName("Shake Rotation")]
        [SerializeField] private float shakeRotation = 1.6f;
        [Space(4)]
        [InspectorName("Fall Start Speed")]
        [SerializeField] private float fallStartSpeed = 3.38f;
        [Space(4)]
        [InspectorName("Fall Gravity")]
        [SerializeField] private float fallGravity = 18.23f;
        [Space(4)]
        [InspectorName("Side Drift")]
        [SerializeField] private float fallSideDrift = 0.61f;
        [Space(4)]
        [InspectorName("Rotation Speed")]
        [SerializeField] private float fallRotationSpeed = 148.5f;
        [Space(4)]
        [InspectorName("Fade While Falling")]
        [SerializeField] private bool fadeWhileFalling = true;

        [Space(10)]
        [Header("--------- Reset On Respawn ---------")]
        [Space(4)]
        [InspectorName("Auto Reset On Enable")]
        [Tooltip("If true, the bridge fully resets every time this GameObject is re-enabled (e.g. when your respawn system re-enables the area).")]
        [SerializeField] private bool autoResetOnEnable = false;

        [Space(10)]
        [Header("--------- Auto Rebuild ---------")]
        [Header("+Each piece rebuilds automatically after falling+")]
        [Space(4)]
        [InspectorName("Auto Rebuild")]
        [Tooltip("Each piece will automatically animate back into place after a delay once it has finished falling.")]
        [SerializeField] private bool autoRebuildAfterFall = true;
        [Space(4)]
        [InspectorName("Rebuild Delay (s)")]
        [Tooltip("Seconds to wait after a piece finishes falling before it starts rebuilding.")]
        [SerializeField] private float autoRebuildDelay = 10f;
        [Space(4)]
        [InspectorName("Rebuild Duration")]
        [SerializeField] private float autoRebuildDuration = 0.9f;
        [Space(4)]
        [InspectorName("Rebuild Start Drop")]
        [SerializeField] private float autoRebuildStartDrop = 1.2f;
        [Space(4)]
        [InspectorName("Rebuild Side Drift")]
        [SerializeField] private float autoRebuildSideDrift = 0.4f;
        [Space(4)]
        [InspectorName("Rebuild Arc Height")]
        [SerializeField] private float autoRebuildArcHeight = 0.5f;
        [Space(4)]
        [InspectorName("Rebuild Settle Duration")]
        [SerializeField] private float autoRebuildSettleDuration = 0.35f;

        [Space(10)]
        [Header("--------- Collision ---------")]
        [Header("+Walk Surface+")]
        [Space(4)]
        [InspectorName("Shared Surface")]
        [SerializeField] private EdgeCollider2D sharedWalkSurface;
        [Space(4)]
        [InspectorName("Piece Supports")]
        [SerializeField] private bool segmentWalkSurfaceByPiece = true;
        [Space(4)]
        [InspectorName("Piece Padding")]
        [SerializeField] private float walkSurfacePiecePadding = 0.2f;
        [Space(4)]
        [InspectorName("Fallback Width")]
        [SerializeField] private float fallbackSupportHalfWidth = 0.18f;
        [Space(4)]
        [InspectorName("Release Delay")]
        [SerializeField] private float supportReleaseDelay = 0.12f;
        [Space(4)]
        [InspectorName("Release Drop")]
        [SerializeField] private float supportReleaseDropDistance = 0.18f;

        private readonly List<PieceState> _pieces = new List<PieceState>();
        private bool _collapseEnabled = true;
        private GameObject _generatedWalkSurfaceRoot;

        private void Awake()
        {
            if (pieceRoot == null)
            {
                pieceRoot = transform;
            }

            if (player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            CachePieces();
            ConfigureSegmentedWalkSurface();
        }

        private void OnEnable()
        {
            EnsurePiecesCached();
            ConfigureSegmentedWalkSurface();

            // If autoResetOnEnable is true, fully restore the bridge every time
            // the GameObject is re-enabled (e.g. after a respawn that toggles this object).
            if (autoResetOnEnable)
            {
                ResetBridge();
            }
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            EnsurePiecesCached();

            if (!_collapseEnabled)
            {
                return;
            }

            var playerX = player.position.x;

            foreach (var piece in _pieces)
            {
                if (piece.HasStarted || piece.Transform == null || !piece.Transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (playerX >= piece.TriggerWorldX + passedXOffset)
                {
                    piece.HasStarted = true;
                    piece.Routine = StartCoroutine(CrumblePiece(piece));
                }
            }
        }

        // -----------------------------------------------------------------------
        //  RESET — call this from your respawn / checkpoint system to fully
        //  restore every piece so the player can attempt the bridge again.
        //
        //  Example from a respawn manager:
        //      bridge.ResetBridge();
        //
        //  Or set "Auto Reset On Enable" in the Inspector and just disable/enable
        //  this GameObject as part of your respawn flow.
        // -----------------------------------------------------------------------
        public void ResetBridge()
        {
            EnsurePiecesCached();

            _collapseEnabled = true;

            foreach (var piece in _pieces)
            {
                if (piece.Transform == null)
                {
                    continue;
                }

                // Stop any in-progress shake/fall coroutine
                if (piece.Routine != null)
                {
                    StopCoroutine(piece.Routine);
                    piece.Routine = null;
                }

                // Restore transform and colour
                RestorePieceTransform(piece);

                // Re-activate the piece GameObject
                piece.Transform.gameObject.SetActive(true);

                // Re-enable walk-surface support collider
                SetPieceSupportEnabled(piece, true);

                // Allow the piece to trigger collapse again
                piece.HasStarted = false;
            }
        }

        public void RecoverBridge(IEnumerable<string> pieceNamesToKeepMissing, bool disableFutureCollapse)
        {
            EnsurePiecesCached();

            var missingPieces = new HashSet<string>(System.StringComparer.Ordinal);

            if (pieceNamesToKeepMissing != null)
            {
                foreach (var pieceName in pieceNamesToKeepMissing)
                {
                    if (!string.IsNullOrWhiteSpace(pieceName))
                    {
                        missingPieces.Add(pieceName.Trim());
                    }
                }
            }

            _collapseEnabled = !disableFutureCollapse;

            foreach (var piece in _pieces)
            {
                if (piece.Transform == null)
                {
                    continue;
                }

                if (piece.Routine != null)
                {
                    StopCoroutine(piece.Routine);
                    piece.Routine = null;
                }

                var shouldStayMissing = missingPieces.Contains(piece.Transform.name);

                piece.Transform.localPosition = piece.StartLocalPosition;
                piece.Transform.localRotation = piece.StartLocalRotation;
                piece.Transform.localScale = piece.StartLocalScale;

                if (piece.SpriteRenderer != null)
                {
                    piece.SpriteRenderer.color = piece.StartColor;
                }

                piece.HasStarted = disableFutureCollapse;
                piece.Transform.gameObject.SetActive(!shouldStayMissing);
                SetPieceSupportEnabled(piece, !shouldStayMissing);
            }
        }

        public IEnumerator RebuildPiecesAnimated(
            IEnumerable<string> pieceNamesToRebuild,
            bool disableFutureCollapse,
            float staggerDelay,
            float rebuildDuration,
            float rebuildStartDrop,
            float rebuildSideDrift,
            float rebuildArcHeight,
            float rebuildSettleDuration)
        {
            EnsurePiecesCached();

            var rebuildNames = BuildNameSet(pieceNamesToRebuild);

            if (rebuildNames.Count == 0)
            {
                yield break;
            }

            _collapseEnabled = !disableFutureCollapse;

            var rebuildPieces = new List<PieceState>();
            foreach (var piece in _pieces)
            {
                if (piece.Transform == null || !rebuildNames.Contains(piece.Transform.name))
                {
                    continue;
                }

                if (piece.Routine != null)
                {
                    StopCoroutine(piece.Routine);
                    piece.Routine = null;
                }

                piece.HasStarted = true;
                SetPieceSupportEnabled(piece, false);
                rebuildPieces.Add(piece);
            }

            if (rebuildPieces.Count == 0)
            {
                yield break;
            }

            staggerDelay = Mathf.Max(0f, staggerDelay);
            rebuildDuration = Mathf.Max(0.01f, rebuildDuration);
            rebuildStartDrop = Mathf.Max(0f, rebuildStartDrop);
            rebuildSideDrift = Mathf.Max(0f, rebuildSideDrift);
            rebuildArcHeight = Mathf.Max(0f, rebuildArcHeight);
            rebuildSettleDuration = Mathf.Max(0f, rebuildSettleDuration);

            var totalDuration = 0f;
            for (var i = 0; i < rebuildPieces.Count; i++)
            {
                var delay = i * staggerDelay;
                totalDuration = Mathf.Max(totalDuration, delay + rebuildDuration + rebuildSettleDuration);
                rebuildPieces[i].Routine = StartCoroutine(RebuildPiece(
                    rebuildPieces[i],
                    delay,
                    rebuildDuration,
                    rebuildStartDrop,
                    rebuildSideDrift,
                    rebuildArcHeight,
                    rebuildSettleDuration,
                    disableFutureCollapse));
            }

            yield return new WaitForSeconds(totalDuration);

            foreach (var piece in rebuildPieces)
            {
                if (piece.Transform == null)
                {
                    continue;
                }

                RestorePieceTransform(piece);
                piece.HasStarted = disableFutureCollapse;
                piece.Routine = null;
                piece.Transform.gameObject.SetActive(true);
                SetPieceSupportEnabled(piece, true);
            }
        }

        public IEnumerator CollapsePiecesSequentially(
            IEnumerable<string> pieceNamesToCollapse,
            float initialDelay,
            float staggerDelay,
            bool includeInactivePieces)
        {
            EnsurePiecesCached();

            var collapseNames = BuildNameSet(pieceNamesToCollapse);

            if (collapseNames.Count == 0)
            {
                yield break;
            }

            var collapsePieces = new List<PieceState>();
            foreach (var piece in _pieces)
            {
                if (piece.Transform == null || !collapseNames.Contains(piece.Transform.name))
                {
                    continue;
                }

                if (!includeInactivePieces && !piece.Transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (piece.Routine != null)
                {
                    StopCoroutine(piece.Routine);
                    piece.Routine = null;
                }

                piece.HasStarted = true;
                collapsePieces.Add(piece);
            }

            if (collapsePieces.Count == 0)
            {
                yield break;
            }

            initialDelay = Mathf.Max(0f, initialDelay);
            staggerDelay = Mathf.Max(0f, staggerDelay);

            var totalDuration = initialDelay + ((collapsePieces.Count - 1) * staggerDelay) + shakeDuration + fallDuration;
            for (var i = 0; i < collapsePieces.Count; i++)
            {
                var delay = initialDelay + (i * staggerDelay);
                collapsePieces[i].Routine = StartCoroutine(CrumblePieceAfterDelay(collapsePieces[i], delay, includeInactivePieces));
            }

            yield return new WaitForSeconds(totalDuration);
        }

        public bool TryGetVisiblePieceBounds(out Bounds bounds)
        {
            EnsurePiecesCached();

            bounds = default;
            var hasBounds = false;

            foreach (var piece in _pieces)
            {
                if (piece.Transform == null ||
                    piece.SpriteRenderer == null ||
                    !piece.Transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = piece.SpriteRenderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(piece.SpriteRenderer.bounds);
            }

            return hasBounds;
        }

        public bool TryGetRecoverablePieceBounds(IEnumerable<string> pieceNamesToKeepMissing, out Bounds bounds)
        {
            EnsurePiecesCached();

            bounds = default;
            var hasBounds = false;
            var missingPieces = new HashSet<string>(System.StringComparer.Ordinal);

            if (pieceNamesToKeepMissing != null)
            {
                foreach (var pieceName in pieceNamesToKeepMissing)
                {
                    if (!string.IsNullOrWhiteSpace(pieceName))
                    {
                        missingPieces.Add(pieceName.Trim());
                    }
                }
            }

            foreach (var piece in _pieces)
            {
                if (piece.Transform == null || missingPieces.Contains(piece.Transform.name))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = piece.StartBounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(piece.StartBounds);
            }

            return hasBounds;
        }

        public bool TryGetPieceBounds(IEnumerable<string> pieceNames, out Bounds bounds)
        {
            EnsurePiecesCached();

            bounds = default;
            var hasBounds = false;
            var names = BuildNameSet(pieceNames);

            if (names.Count == 0)
            {
                return false;
            }

            foreach (var piece in _pieces)
            {
                if (piece.Transform == null || !names.Contains(piece.Transform.name))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = piece.StartBounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(piece.StartBounds);
            }

            return hasBounds;
        }

        private void CachePieces()
        {
            _pieces.Clear();

            if (pieceRoot == null)
            {
                return;
            }

            foreach (Transform child in pieceRoot)
            {
                if (!IsHandPieceName(child.name))
                {
                    continue;
                }

                var spriteRenderer = child.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    continue;
                }

                _pieces.Add(new PieceState(child, spriteRenderer));
            }

            _pieces.Sort((left, right) => left.TriggerWorldX.CompareTo(right.TriggerWorldX));
        }

        private void EnsurePiecesCached()
        {
            if (pieceRoot == null)
            {
                pieceRoot = transform;
            }

            if (_pieces.Count == 0)
            {
                CachePieces();
            }
        }

        private bool IsHandPieceName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return false;
            }

            return (!string.IsNullOrEmpty(pieceNamePrefix) &&
                    objectName.StartsWith(pieceNamePrefix, System.StringComparison.Ordinal)) ||
                   objectName.StartsWith("CrumblingHand-Piece-", System.StringComparison.Ordinal) ||
                   objectName.StartsWith("CrumblingHandPiece_", System.StringComparison.Ordinal);
        }

        private IEnumerator CrumblePiece(PieceState piece)
        {
            var elapsed = 0f;

            while (elapsed < shakeDuration && piece.Transform != null)
            {
                elapsed += Time.deltaTime;
                var shake = Random.insideUnitCircle * shakeAmount;
                piece.Transform.localPosition = piece.StartLocalPosition + new Vector3(shake.x, shake.y, 0f);

                var shakeAngle = Random.Range(-shakeRotation, shakeRotation);
                piece.Transform.localRotation = piece.StartLocalRotation * Quaternion.Euler(0f, 0f, shakeAngle);

                yield return null;
            }

            if (piece.Transform == null)
            {
                yield break;
            }

            piece.Transform.localPosition = piece.StartLocalPosition;
            piece.Transform.localRotation = piece.StartLocalRotation;

            yield return FallPiece(piece);
        }

        private IEnumerator CrumblePieceAfterDelay(PieceState piece, float delay, bool includeInactivePieces)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (piece.Transform == null)
            {
                yield break;
            }

            if (!piece.Transform.gameObject.activeInHierarchy)
            {
                if (!includeInactivePieces)
                {
                    piece.Routine = null;
                    yield break;
                }

                RestorePieceTransform(piece);
                piece.Transform.gameObject.SetActive(true);
                SetPieceSupportEnabled(piece, true);
            }

            yield return CrumblePiece(piece);
        }

        private IEnumerator FallPiece(PieceState piece)
        {
            var elapsed = 0f;
            var verticalSpeed = fallStartSpeed;
            var drift = Random.Range(-fallSideDrift, fallSideDrift);
            var rotationDirection = Random.value < 0.5f ? -1f : 1f;
            var startColor = piece.SpriteRenderer.color;
            var startY = piece.Transform.position.y;
            var supportReleased = false;
            var releaseDelay = Mathf.Max(0f, supportReleaseDelay);
            var releaseDrop = Mathf.Max(0f, supportReleaseDropDistance);

            while (elapsed < fallDuration && piece.Transform != null)
            {
                elapsed += Time.deltaTime;
                verticalSpeed += fallGravity * Time.deltaTime;

                var position = piece.Transform.position;
                position.x += drift * Time.deltaTime;
                position.y -= verticalSpeed * Time.deltaTime;
                piece.Transform.position = position;
                piece.Transform.Rotate(0f, 0f, rotationDirection * fallRotationSpeed * Time.deltaTime);

                if (!supportReleased &&
                    elapsed >= releaseDelay &&
                    startY - piece.Transform.position.y >= releaseDrop)
                {
                    SetPieceSupportEnabled(piece, false);
                    supportReleased = true;
                }

                if (fadeWhileFalling && piece.SpriteRenderer != null)
                {
                    var alpha = Mathf.Lerp(startColor.a, 0f, Mathf.Clamp01(elapsed / fallDuration));
                    piece.SpriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                }

                if (piece.Transform.position.y <= deactivateBelowY)
                {
                    break;
                }

                yield return null;
            }

            if (piece.Transform != null)
            {
                SetPieceSupportEnabled(piece, false);
                piece.Transform.gameObject.SetActive(false);
            }

            piece.Routine = null;

            if (autoRebuildAfterFall && piece.Transform != null)
            {
                piece.Routine = StartCoroutine(AutoRebuildPiece(piece));
            }
        }

        private IEnumerator AutoRebuildPiece(PieceState piece)
        {
            // Wait the minimum delay first
            yield return new WaitForSeconds(Mathf.Max(0f, autoRebuildDelay));

            if (piece.Transform == null)
            {
                piece.Routine = null;
                yield break;
            }

            // Then wait until the player is behind (to the left of) this piece.
            // This way the piece quietly rebuilds while the player isn't watching.
            if (player != null)
            {
                while (player.position.x >= piece.TriggerWorldX)
                {
                    yield return null;
                }
            }

            if (piece.Transform == null)
            {
                piece.Routine = null;
                yield break;
            }

            // Reuse the existing RebuildPiece coroutine with our auto-rebuild settings.
            // disableFutureCollapse = false so HasStarted resets and the piece can fall again.
            yield return RebuildPiece(
                piece,
                delay: 0f,
                rebuildDuration: autoRebuildDuration,
                rebuildStartDrop: autoRebuildStartDrop,
                rebuildSideDrift: autoRebuildSideDrift,
                rebuildArcHeight: autoRebuildArcHeight,
                rebuildSettleDuration: autoRebuildSettleDuration,
                disableFutureCollapse: false);

            piece.Routine = null;
        }

        private IEnumerator RebuildPiece(
            PieceState piece,
            float delay,
            float rebuildDuration,
            float rebuildStartDrop,
            float rebuildSideDrift,
            float rebuildArcHeight,
            float rebuildSettleDuration,
            bool disableFutureCollapse)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (piece.Transform == null)
            {
                yield break;
            }

            var sideOffset = Mathf.Lerp(-rebuildSideDrift, rebuildSideDrift, piece.Seed01(73.6f));
            var startPosition = piece.StartLocalPosition + new Vector3(sideOffset, -rebuildStartDrop, 0f);
            var startRotation = piece.StartLocalRotation *
                                Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 8f, piece.Seed01(91.2f)));

            piece.Transform.localPosition = startPosition;
            piece.Transform.localRotation = startRotation;
            piece.Transform.localScale = piece.StartLocalScale;

            if (piece.SpriteRenderer != null)
            {
                var transparent = piece.StartColor;
                transparent.a = 0f;
                piece.SpriteRenderer.color = transparent;
            }

            piece.Transform.gameObject.SetActive(true);

            var elapsed = 0f;
            while (elapsed < rebuildDuration && piece.Transform != null)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / rebuildDuration);
                var rise = EaseOutCubic(normalized);
                var alphaT = Smooth01(Mathf.Clamp01(normalized * 1.15f));
                var position = Vector3.LerpUnclamped(startPosition, piece.StartLocalPosition, rise);
                position.y += Mathf.Sin(normalized * Mathf.PI) * rebuildArcHeight;
                position.x += Mathf.Sin((normalized * Mathf.PI * 2f) + piece.Seed) * 0.035f * (1f - normalized);

                var settleAngle = Mathf.Sin(normalized * Mathf.PI * 2.5f + piece.Seed * 0.1f) * 2.25f * (1f - normalized);
                var targetRotation = piece.StartLocalRotation * Quaternion.Euler(0f, 0f, settleAngle);

                piece.Transform.localPosition = position;
                piece.Transform.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, Smooth01(normalized));

                if (piece.SpriteRenderer != null)
                {
                    var color = piece.StartColor;
                    color.a = Mathf.Lerp(0f, piece.StartColor.a, alphaT);
                    piece.SpriteRenderer.color = color;
                }

                yield return null;
            }

            if (rebuildSettleDuration > 0f && piece.Transform != null)
            {
                var settleElapsed = 0f;
                while (settleElapsed < rebuildSettleDuration && piece.Transform != null)
                {
                    settleElapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(settleElapsed / rebuildSettleDuration);
                    var damp = 1f - t;
                    var offset = Mathf.Sin(t * Mathf.PI * 2f) * 0.025f * damp;
                    var angle = Mathf.Sin(t * Mathf.PI * 2f + piece.Seed) * 1.15f * damp;

                    piece.Transform.localPosition = piece.StartLocalPosition + Vector3.up * offset;
                    piece.Transform.localRotation = piece.StartLocalRotation * Quaternion.Euler(0f, 0f, angle);
                    yield return null;
                }
            }

            if (piece.Transform != null)
            {
                RestorePieceTransform(piece);
                piece.Transform.gameObject.SetActive(true);
                SetPieceSupportEnabled(piece, true);
            }

            piece.HasStarted = disableFutureCollapse;
            piece.Routine = null;
        }

        private void RestorePieceTransform(PieceState piece)
        {
            piece.Transform.localPosition = piece.StartLocalPosition;
            piece.Transform.localRotation = piece.StartLocalRotation;
            piece.Transform.localScale = piece.StartLocalScale;

            if (piece.SpriteRenderer != null)
            {
                piece.SpriteRenderer.color = piece.StartColor;
            }
        }

        private void ConfigureSegmentedWalkSurface()
        {
            if (!segmentWalkSurfaceByPiece || !Application.isPlaying)
            {
                return;
            }

            EnsurePiecesCached();
            FindSharedWalkSurface();

            if (sharedWalkSurface == null || _generatedWalkSurfaceRoot != null || _pieces.Count == 0)
            {
                return;
            }

            var sourcePoints = sharedWalkSurface.points;
            if (sourcePoints == null || sourcePoints.Length < 2)
            {
                return;
            }

            _generatedWalkSurfaceRoot = new GameObject("Generated-CrumblingHand-WalkSurface");
            _generatedWalkSurfaceRoot.layer = sharedWalkSurface.gameObject.layer;
            _generatedWalkSurfaceRoot.tag = sharedWalkSurface.gameObject.tag;
            _generatedWalkSurfaceRoot.transform.SetParent(sharedWalkSurface.transform.parent, false);
            _generatedWalkSurfaceRoot.transform.localPosition = sharedWalkSurface.transform.localPosition;
            _generatedWalkSurfaceRoot.transform.localRotation = sharedWalkSurface.transform.localRotation;
            _generatedWalkSurfaceRoot.transform.localScale = sharedWalkSurface.transform.localScale;

            var supportCount = 0;
            for (var i = 0; i < sourcePoints.Length - 1; i++)
            {
                var segmentMidpoint = Vector2.Lerp(sourcePoints[i], sourcePoints[i + 1], 0.5f);
                var segmentWorldMidpoint = sharedWalkSurface.transform.TransformPoint(segmentMidpoint);
                var pieceIndex = FindSupportPieceIndex(segmentWorldMidpoint.x);
                if (pieceIndex < 0)
                {
                    continue;
                }

                var support = CreateSupportCollider(
                    _pieces[pieceIndex],
                    new[] { sourcePoints[i], sourcePoints[i + 1] },
                    i + 1);
                if (support != null)
                {
                    _pieces[pieceIndex].SupportColliders.Add(support);
                    supportCount++;
                }
            }

            if (supportCount > 0)
            {
                sharedWalkSurface.enabled = false;
            }
            else
            {
                Destroy(_generatedWalkSurfaceRoot);
                _generatedWalkSurfaceRoot = null;
            }
        }

        private void FindSharedWalkSurface()
        {
            if (sharedWalkSurface != null)
            {
                return;
            }

            var edgeColliders = GetComponentsInChildren<EdgeCollider2D>(true);
            for (var i = 0; i < edgeColliders.Length; i++)
            {
                if (edgeColliders[i] != null && edgeColliders[i].name.Contains("WalkSurface"))
                {
                    sharedWalkSurface = edgeColliders[i];
                    return;
                }
            }

            for (var i = 0; i < edgeColliders.Length; i++)
            {
                if (edgeColliders[i] != null && edgeColliders[i].CompareTag("Ground"))
                {
                    sharedWalkSurface = edgeColliders[i];
                    return;
                }
            }
        }

        private int FindSupportPieceIndex(float worldX)
        {
            var directIndex = FindNearestPieceIndex(worldX, requireWithinBounds: true);
            return directIndex >= 0 ? directIndex : FindNearestPieceIndex(worldX, requireWithinBounds: false);
        }

        private int FindNearestPieceIndex(float worldX)
        {
            return FindNearestPieceIndex(worldX, requireWithinBounds: true);
        }

        private int FindNearestPieceIndex(float worldX, bool requireWithinBounds)
        {
            var bestIndex = -1;
            var bestDistance = float.PositiveInfinity;
            var padding = Mathf.Max(0f, walkSurfacePiecePadding);

            for (var i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                var minX = piece.StartBounds.min.x - padding;
                var maxX = piece.StartBounds.max.x + padding;
                if (requireWithinBounds && (worldX < minX || worldX > maxX))
                {
                    continue;
                }

                var distance = Mathf.Abs(worldX - piece.StartBounds.center.x);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private EdgeCollider2D CreateSupportCollider(PieceState piece, Vector2[] points, int segmentIndex)
        {
            if (_generatedWalkSurfaceRoot == null)
            {
                return null;
            }

            var supportObject = new GameObject("Support-" + piece.Transform.name + "-" + segmentIndex.ToString("00"));
            supportObject.layer = sharedWalkSurface.gameObject.layer;
            supportObject.tag = sharedWalkSurface.gameObject.tag;
            supportObject.transform.SetParent(_generatedWalkSurfaceRoot.transform, false);

            var supportCollider = supportObject.AddComponent<EdgeCollider2D>();
            supportCollider.edgeRadius = sharedWalkSurface.edgeRadius;
            supportCollider.isTrigger = false;

            if (points == null || points.Length == 0)
            {
                Destroy(supportObject);
                return null;
            }

            if (points.Length == 1)
            {
                var halfWidth = Mathf.Max(0.01f, fallbackSupportHalfWidth);
                var point = points[0];
                supportCollider.points = new[]
                {
                    new Vector2(point.x - halfWidth, point.y),
                    new Vector2(point.x + halfWidth, point.y)
                };
            }
            else
            {
                supportCollider.points = points;
            }

            return supportCollider;
        }

        private static void SetPieceSupportEnabled(PieceState piece, bool enabled)
        {
            if (piece == null || piece.SupportColliders.Count == 0)
            {
                return;
            }

            for (var i = 0; i < piece.SupportColliders.Count; i++)
            {
                var support = piece.SupportColliders[i];
                if (support != null)
                {
                    support.enabled = enabled;
                }
            }
        }

        private static HashSet<string> BuildNameSet(IEnumerable<string> pieceNames)
        {
            var names = new HashSet<string>(System.StringComparer.Ordinal);

            if (pieceNames == null)
            {
                return names;
            }

            foreach (var pieceName in pieceNames)
            {
                if (!string.IsNullOrWhiteSpace(pieceName))
                {
                    var trimmedName = pieceName.Trim();
                    names.Add(trimmedName);
                    names.Add(NormalizePieceName(trimmedName));
                }
            }

            return names;
        }

        private static string NormalizePieceName(string pieceName)
        {
            const string oldPrefix = "CrumblingHandPiece_";
            if (string.IsNullOrEmpty(pieceName) || !pieceName.StartsWith(oldPrefix, System.StringComparison.Ordinal))
            {
                return pieceName;
            }

            var numberPart = pieceName.Substring(oldPrefix.Length);
            var separatorIndex = numberPart.IndexOf('_');
            if (separatorIndex >= 0)
            {
                numberPart = numberPart.Substring(0, separatorIndex);
            }

            if (!int.TryParse(numberPart, out var pieceNumber))
            {
                return pieceName;
            }

            return $"CrumblingHand-Piece-{pieceNumber:00}";
        }

        private sealed class PieceState
        {
            public PieceState(Transform transform, SpriteRenderer spriteRenderer)
            {
                Transform = transform;
                SpriteRenderer = spriteRenderer;
                TriggerWorldX = transform.position.x;
                StartLocalPosition = transform.localPosition;
                StartLocalRotation = transform.localRotation;
                StartLocalScale = transform.localScale;
                StartColor = spriteRenderer.color;
                StartBounds = spriteRenderer.bounds;
                Seed = Mathf.Abs((transform.position.x * 12.9898f) + (transform.position.y * 78.233f));
            }

            public Transform Transform { get; }
            public SpriteRenderer SpriteRenderer { get; }
            public float TriggerWorldX { get; }
            public Vector3 StartLocalPosition { get; }
            public Quaternion StartLocalRotation { get; }
            public Vector3 StartLocalScale { get; }
            public Color StartColor { get; }
            public Bounds StartBounds { get; }
            public List<Collider2D> SupportColliders { get; } = new List<Collider2D>();
            public float Seed { get; }
            public bool HasStarted { get; set; }
            public Coroutine Routine { get; set; }

            public float Seed01(float offset)
            {
                return Mathf.Repeat(Mathf.Sin(Seed + offset) * 43758.5453f, 1f);
            }
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutCubic(float value)
        {
            value = 1f - Mathf.Clamp01(value);
            return 1f - value * value * value;
        }
    }
}