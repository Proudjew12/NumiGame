using System.Collections;
using UnityEngine;
using NumiDream.Nomi;

namespace NumiDream.StageOne.Triggers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CrumblingHandRecoveryTrigger : MonoBehaviour
    {
        [Header("--------- Trigger ---------")]
        [Header("+Detection+")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Trigger Once")]
        [SerializeField] private bool triggerOnce = true;

        [Space(10)]
        [Header("--------- Bridge ---------")]
        [Header("+Recovery+")]
        [Space(4)]
        [InspectorName("Bridge Collapse")]
        [SerializeField] private CrumblingHandBridgeCollapse bridgeCollapse;
        [Space(4)]
        [InspectorName("Recovery Delay")]
        [SerializeField] private float recoveryDelay = 3f;
        [Space(4)]
        [InspectorName("Disable Collapse")]
        [SerializeField] private bool disableFutureCollapse = true;
        [Space(4)]
        [InspectorName("Pieces To Rebuild")]
        [SerializeField] private string[] piecesToRebuild;
        [Space(4)]
        [InspectorName("Keep Missing")]
        [SerializeField]
        private string[] piecesToKeepMissing =
        {
            "CrumblingHand-Piece-06",
            "CrumblingHand-Piece-10",
            "CrumblingHand-Piece-14",
            "CrumblingHand-Piece-23"
        };

        [Space(10)]
        [Header("--------- Rebuild Animation ---------")]
        [Header("+Motion+")]
        [Space(4)]
        [InspectorName("Animate Rebuild")]
        [SerializeField] private bool animateRebuild = true;
        [Space(4)]
        [InspectorName("Stagger Delay")]
        [SerializeField] private float rebuildStaggerDelay = 0.07f;
        [Space(4)]
        [InspectorName("Piece Time")]
        [SerializeField] private float rebuildPieceDuration = 0.72f;
        [Space(4)]
        [InspectorName("Start Drop")]
        [SerializeField] private float rebuildStartDrop = 1.45f;
        [Space(4)]
        [InspectorName("Side Drift")]
        [SerializeField] private float rebuildSideDrift = 0.26f;
        [Space(4)]
        [InspectorName("Arc Height")]
        [SerializeField] private float rebuildArcHeight = 0.18f;
        [Space(4)]
        [InspectorName("Settle Time")]
        [SerializeField] private float rebuildSettleDuration = 0.16f;

        [Space(10)]
        [Header("--------- Player Freeze ---------")]
        [Header("+Control+")]
        [Space(4)]
        [InspectorName("Freeze Player")]
        [SerializeField] private bool freezePlayerDuringRecovery = true;

        [Space(10)]
        [Header("--------- Collider Swap ---------")]
        [Header("+Recovery+")]
        [Space(4)]
        [InspectorName("Starting Collider")]
        [SerializeField] private Collider2D startingBridgeCollider;
        [Space(4)]
        [InspectorName("Recovered Collider")]
        [SerializeField] private Collider2D recoveredBridgeCollider;
        [Space(4)]
        [InspectorName("Swap On Recovery")]
        [SerializeField] private bool swapBridgeColliderOnRecovery = true;

        [Space(10)]
        [Header("--------- Reveal ---------")]
        [Header("+Objects+")]
        [Space(4)]
        [InspectorName("Objects To Reveal")]
        [SerializeField] private GameObject[] objectsToRevealAfterRecovery;
        [Space(4)]
        [InspectorName("Fade Time")]
        [SerializeField] private float revealFadeDuration = 1.25f;

        [Space(10)]
        [Header("--------- Camera Preview ---------")]
        [Header("+View+")]
        [Space(4)]
        [InspectorName("Target Camera")]
        [SerializeField] private Camera targetCamera;
        [Space(4)]
        [InspectorName("Camera Follow")]
        [SerializeField] private CameraFollow2D cameraFollow;
        [Space(4)]
        [InspectorName("Normal Size")]
        [SerializeField] private float normalOrthographicSize = 4.25f;
        [Space(4)]
        [InspectorName("Preview Size")]
        [SerializeField] private float previewOrthographicSize = 6.8f;
        [Space(4)]
        [InspectorName("Auto Fit Preview")]
        [SerializeField] private bool autoFitPreviewToBridge = true;
        [Space(4)]
        [InspectorName("Preview Padding")]
        [SerializeField] private float previewPadding = 1.25f;
        [Space(4)]
        [InspectorName("Include Player")]
        [SerializeField] private bool includePlayerInPreview = true;
        [Space(4)]
        [InspectorName("Focus Y")]
        [SerializeField] private bool focusBridgeVertically = true;
        [Space(4)]
        [InspectorName("Move To Bridge")]
        [SerializeField] private float moveToBridgeDuration = 2f;
        [Space(4)]
        [InspectorName("Return To Player")]
        [SerializeField] private float returnToPlayerDuration = 1.7f;
        [Space(4)]
        [InspectorName("Preview Hold")]
        [SerializeField] private float previewHoldDuration = 2f;
        [Space(4)]
        [InspectorName("Center Bridge")]
        [SerializeField] private bool centerOnRecoveredBridge = true;
        [Space(4)]
        [InspectorName("Keep Bottom")]
        [SerializeField] private bool keepBottomClamped = true;

        private Coroutine _routine;
        private bool _hasTriggered;
        private Transform _lastPlayer;
        private bool _playerFrozen;
        private Rigidbody2D _frozenBody;
        private RigidbodyConstraints2D _previousConstraints;
        private float _previousGravityScale;
        private NomiPlayerMovement _frozenMovement;

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider2D>();
            triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            FindReferences();
        }

        private void OnDisable()
        {
            UnfreezePlayer();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggerOnce && _hasTriggered)
            {
                return;
            }

            if (!other.CompareTag(playerTag))
            {
                return;
            }

            _hasTriggered = true;
            _lastPlayer = other.transform;

            if (_routine != null)
            {
                StopCoroutine(_routine);
            }

            _routine = StartCoroutine(RecoverRoutine());
        }

        private IEnumerator RecoverRoutine()
        {
            FindReferences();

            if (bridgeCollapse == null)
            {
                Debug.LogWarning("[CrumblingHandRecoveryTrigger] No CrumblingHandBridgeCollapse assigned.", this);
                yield break;
            }

            FreezePlayer();
            SwapBridgeCollider();

            if (recoveryDelay > 0f)
            {
                yield return new WaitForSeconds(recoveryDelay);
            }

            Bounds previewBounds;
            var hasPreviewBounds = TryGetPreviewBounds(out previewBounds);
            var previewSize = GetPreviewCameraSize(previewBounds, hasPreviewBounds);

            if (targetCamera != null)
            {
                yield return MoveCameraView(
                    previewSize,
                    GetBridgeFocusOffset(previewSize, previewBounds, hasPreviewBounds),
                    moveToBridgeDuration);
            }

            if (animateRebuild && HasPiecesToRebuild())
            {
                yield return bridgeCollapse.RebuildPiecesAnimated(
                    piecesToRebuild,
                    disableFutureCollapse,
                    rebuildStaggerDelay,
                    rebuildPieceDuration,
                    rebuildStartDrop,
                    rebuildSideDrift,
                    rebuildArcHeight,
                    rebuildSettleDuration);
            }
            else
            {
                bridgeCollapse.RecoverBridge(piecesToKeepMissing, disableFutureCollapse);
            }

            yield return RevealObjects();

            if (previewHoldDuration > 0f)
            {
                yield return new WaitForSeconds(previewHoldDuration);
            }

            if (targetCamera != null)
            {
                yield return MoveCameraView(normalOrthographicSize, Vector2.zero, returnToPlayerDuration);
            }

            UnfreezePlayer();
            _routine = null;
        }

        private bool HasPiecesToRebuild()
        {
            if (piecesToRebuild == null)
            {
                return false;
            }

            for (var i = 0; i < piecesToRebuild.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(piecesToRebuild[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void SwapBridgeCollider()
        {
            if (!swapBridgeColliderOnRecovery)
            {
                return;
            }

            if (startingBridgeCollider != null)
            {
                startingBridgeCollider.enabled = false;
            }

            if (recoveredBridgeCollider != null)
            {
                recoveredBridgeCollider.gameObject.SetActive(true);
                recoveredBridgeCollider.enabled = true;
            }
        }

        private IEnumerator RevealObjects()
        {
            if (objectsToRevealAfterRecovery == null)
            {
                yield break;
            }

            foreach (var objectToReveal in objectsToRevealAfterRecovery)
            {
                if (objectToReveal == null)
                {
                    continue;
                }

                yield return FadeInObject(objectToReveal);
            }
        }

        private IEnumerator FadeInObject(GameObject objectToReveal)
        {
            var renderers = objectToReveal.GetComponentsInChildren<SpriteRenderer>(true);
            var originalColors = new Color[renderers.Length];

            for (var i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].color;
                var transparent = originalColors[i];
                transparent.a = 0f;
                renderers[i].color = transparent;
            }

            objectToReveal.SetActive(true);

            if (revealFadeDuration <= 0f || renderers.Length == 0)
            {
                for (var i = 0; i < renderers.Length; i++)
                {
                    renderers[i].color = originalColors[i];
                }

                yield break;
            }

            var elapsed = 0f;

            while (elapsed < revealFadeDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / revealFadeDuration);
                t = t * t * (3f - 2f * t);

                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    var color = originalColors[i];
                    color.a = Mathf.Lerp(0f, originalColors[i].a, t);
                    renderers[i].color = color;
                }

                yield return null;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = originalColors[i];
                }
            }
        }

        private IEnumerator MoveCameraView(float targetSize, Vector2 targetRuntimeOffset, float duration)
        {
            var startSize = targetCamera.orthographicSize;
            var startOffset = cameraFollow != null ? cameraFollow.RuntimeOffset : Vector2.zero;
            var elapsed = 0f;

            if (duration <= 0f)
            {
                targetCamera.orthographicSize = targetSize;

                if (cameraFollow != null)
                {
                    cameraFollow.SetRuntimeOffset(targetRuntimeOffset);
                    cameraFollow.SnapToTarget();
                }

                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);

                var currentSize = Mathf.Lerp(startSize, targetSize, t);
                targetCamera.orthographicSize = currentSize;

                if (cameraFollow != null)
                {
                    cameraFollow.SetRuntimeOffset(Vector2.Lerp(startOffset, targetRuntimeOffset, t));
                    cameraFollow.SnapToTarget();
                }

                yield return null;
            }

            targetCamera.orthographicSize = targetSize;

            if (cameraFollow != null)
            {
                cameraFollow.SetRuntimeOffset(targetRuntimeOffset);
                cameraFollow.SnapToTarget();
            }
        }

        private float GetPreviewCameraSize(Bounds previewBounds, bool hasPreviewBounds)
        {
            if (!autoFitPreviewToBridge || !hasPreviewBounds || targetCamera == null)
            {
                return previewOrthographicSize;
            }

            var paddedSize = previewBounds.size;
            var padding = Mathf.Max(0f, previewPadding);
            paddedSize.x += padding * 2f;
            paddedSize.y += padding * 2f;

            var aspect = Mathf.Max(0.01f, targetCamera.aspect);
            var sizeForWidth = paddedSize.x / (aspect * 2f);
            var sizeForHeight = paddedSize.y * 0.5f;
            return Mathf.Max(previewOrthographicSize, sizeForWidth, sizeForHeight);
        }

        private bool TryGetPreviewBounds(out Bounds previewBounds)
        {
            previewBounds = default;
            var hasBounds = false;

            if (bridgeCollapse != null &&
                (bridgeCollapse.TryGetPieceBounds(piecesToRebuild, out var bridgeBounds) ||
                 bridgeCollapse.TryGetRecoverablePieceBounds(piecesToKeepMissing, out bridgeBounds) ||
                 bridgeCollapse.TryGetVisiblePieceBounds(out bridgeBounds)))
            {
                previewBounds = bridgeBounds;
                hasBounds = true;
            }

            if (objectsToRevealAfterRecovery != null)
            {
                for (var i = 0; i < objectsToRevealAfterRecovery.Length; i++)
                {
                    EncapsulateObjectBounds(objectsToRevealAfterRecovery[i], ref previewBounds, ref hasBounds);
                }
            }

            if (includePlayerInPreview && _lastPlayer != null)
            {
                var playerBounds = new Bounds(_lastPlayer.position, Vector3.one * 0.25f);
                EncapsulateBounds(playerBounds, ref previewBounds, ref hasBounds);
            }

            return hasBounds;
        }

        private static void EncapsulateObjectBounds(GameObject target, ref Bounds previewBounds, ref bool hasBounds)
        {
            if (target == null)
            {
                return;
            }

            var renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled)
                {
                    continue;
                }

                EncapsulateBounds(renderers[i].bounds, ref previewBounds, ref hasBounds);
            }

            var colliders = target.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || !colliders[i].enabled)
                {
                    continue;
                }

                EncapsulateBounds(colliders[i].bounds, ref previewBounds, ref hasBounds);
            }
        }

        private static void EncapsulateBounds(Bounds bounds, ref Bounds previewBounds, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                previewBounds = bounds;
                hasBounds = true;
                return;
            }

            previewBounds.Encapsulate(bounds);
        }

        private Vector2 GetBridgeFocusOffset(float cameraSize, Bounds previewBounds, bool hasPreviewBounds)
        {
            var offset = Vector2.zero;
            var followOffset = cameraFollow != null ? cameraFollow.Offset : Vector2.zero;

            if (centerOnRecoveredBridge &&
                _lastPlayer != null &&
                hasPreviewBounds)
            {
                offset.x = previewBounds.center.x - _lastPlayer.position.x - followOffset.x;

                if (focusBridgeVertically)
                {
                    offset.y = previewBounds.center.y - _lastPlayer.position.y - followOffset.y;
                }
            }

            if (keepBottomClamped && !focusBridgeVertically)
            {
                offset.y = Mathf.Max(0f, cameraSize - normalOrthographicSize);
            }

            return offset;
        }

        private void FreezePlayer()
        {
            if (!freezePlayerDuringRecovery || _playerFrozen || _lastPlayer == null)
            {
                return;
            }

            _frozenMovement = _lastPlayer.GetComponentInParent<NomiPlayerMovement>();
            if (_frozenMovement == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag(playerTag);
                _frozenMovement = playerObject != null ? playerObject.GetComponent<NomiPlayerMovement>() : null;
            }

            _frozenBody = _frozenMovement != null
                ? _frozenMovement.GetComponent<Rigidbody2D>()
                : _lastPlayer.GetComponentInParent<Rigidbody2D>();

            _frozenMovement?.SetExternalMovementLock(true);

            if (_frozenBody != null)
            {
                _previousConstraints = _frozenBody.constraints;
                _previousGravityScale = _frozenBody.gravityScale;
                _frozenBody.linearVelocity = Vector2.zero;
                _frozenBody.angularVelocity = 0f;
                _frozenBody.gravityScale = 0f;
                _frozenBody.constraints = _previousConstraints |
                                          RigidbodyConstraints2D.FreezePositionX |
                                          RigidbodyConstraints2D.FreezePositionY |
                                          RigidbodyConstraints2D.FreezeRotation;
            }

            _playerFrozen = true;
        }

        private void UnfreezePlayer()
        {
            if (!_playerFrozen)
            {
                return;
            }

            if (_frozenBody != null)
            {
                _frozenBody.constraints = _previousConstraints;
                _frozenBody.gravityScale = _previousGravityScale;
                _frozenBody.linearVelocity = Vector2.zero;
                _frozenBody.angularVelocity = 0f;
            }

            _frozenMovement?.SetExternalMovementLock(false);
            _frozenBody = null;
            _frozenMovement = null;
            _playerFrozen = false;
        }

        private void FindReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (cameraFollow == null && targetCamera != null)
            {
                cameraFollow = targetCamera.GetComponent<CameraFollow2D>();
            }

            if (bridgeCollapse == null)
            {
#if UNITY_2023_1_OR_NEWER
                bridgeCollapse = FindFirstObjectByType<CrumblingHandBridgeCollapse>();
#else
                bridgeCollapse = FindObjectOfType<CrumblingHandBridgeCollapse>();
#endif
            }
        }
    }
}
