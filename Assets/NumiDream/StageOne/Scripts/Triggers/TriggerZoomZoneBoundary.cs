using System.Collections;
using System.Collections.Generic;
using NumiDream.Nomi;
using UnityEngine;

namespace NumiDream.StageOne.Triggers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class TriggerZoomZoneBoundary : MonoBehaviour
    {
        private static readonly Dictionary<string, List<TriggerZoomZoneBoundary>> Zones = new Dictionary<string, List<TriggerZoomZoneBoundary>>();
        private static readonly Dictionary<Camera, TriggerZoomZoneBoundary> ActiveZoomOwners = new Dictionary<Camera, TriggerZoomZoneBoundary>();
        private static readonly HashSet<string> PlayedOneShotZones = new HashSet<string>();

        [Header("--------- Zone ---------")]
        [Header("+Detection+")]
        [Space(4)]
        [InspectorName("Zone Name")]
        [SerializeField] private string zoneName = "ZoomZone";
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";

        [Space(10)]
        [Header("--------- Camera ---------")]
        [Header("+Zoom+")]
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
        [InspectorName("Zone Size")]
        [SerializeField] private float zoneOrthographicSize = 6.4f;
        [Space(4)]
        [InspectorName("Zoom Time")]
        [SerializeField] private float zoomDuration = 1.35f;
        [Space(4)]
        [InspectorName("Keep Bottom")]
        [SerializeField] private bool keepBottomClamped = true;
        [Space(4)]
        [InspectorName("Focus Root")]
        [SerializeField] private Transform focusRoot;
        [Space(4)]
        [InspectorName("Focus Padding")]
        [SerializeField] private float focusPadding = 1.15f;
        [Space(4)]
        [InspectorName("Focus X")]
        [SerializeField] private bool focusHorizontal;
        [Space(4)]
        [InspectorName("Focus Y")]
        [SerializeField] private bool focusVertical;

        [Space(10)]
        [Header("--------- One Shot ---------")]
        [Header("+Cinematic+")]
        [Space(4)]
        [InspectorName("One Shot")]
        [SerializeField] private bool oneShotCinematic;
        [Space(4)]
        [InspectorName("Hold Time")]
        [SerializeField] private float oneShotHoldDuration = 1.25f;
        [Space(4)]
        [InspectorName("Return Time")]
        [SerializeField] private float oneShotReturnDuration = 2.35f;

        [Space(10)]
        [Header("--------- Detection ---------")]
        [Header("+Padding+")]
        [Space(4)]
        [InspectorName("Inside Padding")]
        [SerializeField] private float insidePadding = 0.05f;

        private Coroutine _zoomRoutine;
        private Transform _trackedPlayer;
        private float _activeTargetSize = -1f;
        private float _oneShotReturnSize;
        private Vector2 _oneShotReturnOffset;
        private bool _oneShotPlaying;
        private bool _playerFrozenForOneShot;
        private NomiPlayerMovement _frozenMovement;
        private Rigidbody2D _frozenBody;
        private RigidbodyConstraints2D _previousConstraints;
        private float _previousGravityScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Zones.Clear();
            ActiveZoomOwners.Clear();
            PlayedOneShotZones.Clear();
        }

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider2D>();
            triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            FindCameraReferences();

            if (targetCamera != null && normalOrthographicSize <= 0f)
            {
                normalOrthographicSize = targetCamera.orthographicSize;
            }
        }

        private void OnEnable()
        {
            if (!Zones.TryGetValue(zoneName, out var zoneBoundaries))
            {
                zoneBoundaries = new List<TriggerZoomZoneBoundary>();
                Zones.Add(zoneName, zoneBoundaries);
            }

            if (!zoneBoundaries.Contains(this))
            {
                zoneBoundaries.Add(this);
            }
        }

        private void OnDisable()
        {
            UnfreezePlayerForOneShot();

            if (!Zones.TryGetValue(zoneName, out var zoneBoundaries))
            {
                return;
            }

            zoneBoundaries.Remove(this);

            if (zoneBoundaries.Count == 0)
            {
                Zones.Remove(zoneName);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            EvaluatePlayerPosition(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            EvaluatePlayerPosition(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            EvaluatePlayerPosition(other);
        }

        private void Update()
        {
            if (oneShotCinematic)
            {
                return;
            }

            if (_trackedPlayer == null)
            {
                return;
            }

            UpdateZoomForTrackedPlayer();
        }

        private void EvaluatePlayerPosition(Collider2D other)
        {
            if (!other.CompareTag(playerTag))
            {
                return;
            }

            _trackedPlayer = other.transform;

            if (oneShotCinematic)
            {
                StartOneShotCinematic();
                return;
            }

            UpdateZoomForTrackedPlayer();
        }

        private void StartOneShotCinematic()
        {
            if (_oneShotPlaying || PlayedOneShotZones.Contains(zoneName))
            {
                return;
            }

            FindCameraReferences();

            if (targetCamera == null)
            {
                Debug.LogWarning("[TriggerZoomZoneBoundary] No target camera found.", this);
                return;
            }

            PlayedOneShotZones.Add(zoneName);
            _oneShotReturnSize = targetCamera.orthographicSize;
            _oneShotReturnOffset = cameraFollow != null ? cameraFollow.RuntimeOffset : Vector2.zero;

            if (_zoomRoutine != null)
            {
                CancelZoomRoutine();
            }

            if (ActiveZoomOwners.TryGetValue(targetCamera, out var currentOwner) &&
                currentOwner != null &&
                currentOwner != this &&
                currentOwner._zoomRoutine != null)
            {
                currentOwner.CancelZoomRoutine();
            }

            ActiveZoomOwners[targetCamera] = this;
            _oneShotPlaying = true;
            FreezePlayerForOneShot();
            SetOneShotZoneCollidersEnabled(false);
            _zoomRoutine = StartCoroutine(OneShotCinematicRoutine());
        }

        private void UpdateZoomForTrackedPlayer()
        {
            if (_trackedPlayer == null)
            {
                return;
            }

            FindCameraReferences();

            if (targetCamera == null)
            {
                Debug.LogWarning("[TriggerZoomZoneBoundary] No target camera found.", this);
                return;
            }

            var playerX = _trackedPlayer.position.x;
            var shouldZoomOut = IsInsideZone(playerX);
            var targetSize = shouldZoomOut ? zoneOrthographicSize : normalOrthographicSize;

            StartZoom(targetSize);
        }

        private bool IsInsideZone(float playerX)
        {
            if (!Zones.TryGetValue(zoneName, out var zoneBoundaries) || zoneBoundaries.Count < 2)
            {
                return false;
            }

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;

            foreach (var boundary in zoneBoundaries)
            {
                if (boundary == null)
                {
                    continue;
                }

                var x = boundary.transform.position.x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }

            if (float.IsInfinity(minX) || Mathf.Approximately(minX, maxX))
            {
                return false;
            }

            return playerX >= minX - insidePadding && playerX <= maxX + insidePadding;
        }

        private void StartZoom(float targetSize)
        {
            if (Mathf.Approximately(targetCamera.orthographicSize, targetSize))
            {
                ApplyCameraRuntimeOffset(targetSize);

                if (Mathf.Approximately(targetSize, normalOrthographicSize))
                {
                    _trackedPlayer = null;
                }

                return;
            }

            if (_zoomRoutine != null && Mathf.Approximately(_activeTargetSize, targetSize))
            {
                return;
            }

            if (_zoomRoutine != null)
            {
                CancelZoomRoutine();
            }

            if (ActiveZoomOwners.TryGetValue(targetCamera, out var currentOwner) &&
                currentOwner != null &&
                currentOwner != this &&
                currentOwner._zoomRoutine != null)
            {
                if (Mathf.Approximately(currentOwner._activeTargetSize, targetSize))
                {
                    return;
                }

                currentOwner.CancelZoomRoutine();
            }

            ActiveZoomOwners[targetCamera] = this;
            _activeTargetSize = targetSize;
            _zoomRoutine = StartCoroutine(ZoomRoutine(targetSize));
        }

        private IEnumerator ZoomRoutine(float targetSize)
        {
            var startSize = targetCamera.orthographicSize;
            var startRuntimeOffset = cameraFollow != null ? cameraFollow.RuntimeOffset : Vector2.zero;
            var elapsed = 0f;

            while (elapsed < zoomDuration)
            {
                elapsed += Time.deltaTime;
                var t = zoomDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / zoomDuration);
                t = t * t * (3f - 2f * t);

                var currentSize = Mathf.Lerp(startSize, targetSize, t);
                targetCamera.orthographicSize = currentSize;

                if (cameraFollow != null)
                {
                    var targetRuntimeOffset = GetTargetRuntimeOffset(targetSize);
                    cameraFollow.SetRuntimeOffset(Vector2.Lerp(startRuntimeOffset, targetRuntimeOffset, t));
                    cameraFollow.SnapToTarget();
                }

                yield return null;
            }

            targetCamera.orthographicSize = targetSize;
            ApplyCameraRuntimeOffset(targetSize);
            _zoomRoutine = null;

            if (Mathf.Approximately(targetSize, normalOrthographicSize))
            {
                _trackedPlayer = null;
            }

            if (ActiveZoomOwners.TryGetValue(targetCamera, out var owner) && owner == this)
            {
                ActiveZoomOwners.Remove(targetCamera);
            }
        }

        private void CancelZoomRoutine()
        {
            if (_zoomRoutine != null)
            {
                StopCoroutine(_zoomRoutine);
                _zoomRoutine = null;
            }

            if (_oneShotPlaying)
            {
                RestoreOneShotCameraFollow();
                UnfreezePlayerForOneShot();
                _oneShotPlaying = false;
                _trackedPlayer = null;
            }

            if (targetCamera != null &&
                ActiveZoomOwners.TryGetValue(targetCamera, out var owner) &&
                owner == this)
            {
                ActiveZoomOwners.Remove(targetCamera);
            }
        }

        private IEnumerator OneShotCinematicRoutine()
        {
            yield return AnimateCameraView(
                zoneOrthographicSize,
                GetTargetRuntimeOffset(zoneOrthographicSize),
                Mathf.Max(0.01f, zoomDuration));

            if (oneShotHoldDuration > 0f)
            {
                yield return new WaitForSeconds(oneShotHoldDuration);
            }

            yield return AnimateCameraView(
                GetOneShotReturnSize(),
                _oneShotReturnOffset,
                Mathf.Max(0.01f, oneShotReturnDuration));

            CompleteOneShotCinematic();
        }

        private float GetOneShotReturnSize()
        {
            return _oneShotReturnSize > 0.01f ? _oneShotReturnSize : normalOrthographicSize;
        }

        private void RestoreOneShotCameraFollow()
        {
            FindCameraReferences();

            if (targetCamera != null)
            {
                targetCamera.orthographicSize = GetOneShotReturnSize();
            }

            if (cameraFollow == null)
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                cameraFollow.Target = player.transform;
            }

            if (_oneShotReturnOffset == Vector2.zero)
            {
                cameraFollow.ClearRuntimeOffset();
            }
            else
            {
                cameraFollow.SetRuntimeOffset(_oneShotReturnOffset);
            }

            cameraFollow.SnapToTarget();
        }

        private void CompleteOneShotCinematic()
        {
            RestoreOneShotCameraFollow();
            UnfreezePlayerForOneShot();

            _trackedPlayer = null;
            _oneShotPlaying = false;
            _zoomRoutine = null;
            _activeTargetSize = GetOneShotReturnSize();

            if (ActiveZoomOwners.TryGetValue(targetCamera, out var owner) && owner == this)
            {
                ActiveZoomOwners.Remove(targetCamera);
            }
        }

        private void FreezePlayerForOneShot()
        {
            if (_playerFrozenForOneShot)
            {
                return;
            }

            _frozenMovement = _trackedPlayer != null
                ? _trackedPlayer.GetComponentInParent<NomiPlayerMovement>()
                : null;

            if (_frozenMovement == null)
            {
                var player = GameObject.FindGameObjectWithTag(playerTag);
                _frozenMovement = player != null ? player.GetComponent<NomiPlayerMovement>() : null;
            }

            _frozenBody = _frozenMovement != null
                ? _frozenMovement.GetComponent<Rigidbody2D>()
                : (_trackedPlayer != null ? _trackedPlayer.GetComponentInParent<Rigidbody2D>() : null);

            if (_frozenMovement == null && _frozenBody == null)
            {
                Debug.LogWarning("[TriggerZoomZoneBoundary] Could not find player movement or Rigidbody2D to freeze.", this);
                return;
            }

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

            _playerFrozenForOneShot = true;
        }

        private void UnfreezePlayerForOneShot()
        {
            if (!_playerFrozenForOneShot)
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
            _frozenMovement = null;
            _frozenBody = null;
            _playerFrozenForOneShot = false;
        }

        private void SetOneShotZoneCollidersEnabled(bool enabled)
        {
            if (!Zones.TryGetValue(zoneName, out var zoneBoundaries))
            {
                return;
            }

            foreach (var boundary in zoneBoundaries)
            {
                if (boundary == null || !boundary.oneShotCinematic)
                {
                    continue;
                }

                var triggerCollider = boundary.GetComponent<Collider2D>();
                if (triggerCollider != null)
                {
                    triggerCollider.enabled = enabled;
                }
            }
        }

        private IEnumerator AnimateCameraView(float targetSize, Vector2 targetRuntimeOffset, float duration)
        {
            var startSize = targetCamera.orthographicSize;
            var startRuntimeOffset = cameraFollow != null ? cameraFollow.RuntimeOffset : Vector2.zero;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = SmoothCinematic(t);

                targetCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);

                if (cameraFollow != null)
                {
                    cameraFollow.SetRuntimeOffset(Vector2.Lerp(startRuntimeOffset, targetRuntimeOffset, t));
                    cameraFollow.SnapToTarget();
                }

                yield return null;
            }

            targetCamera.orthographicSize = targetSize;

            if (cameraFollow != null)
            {
                if (targetRuntimeOffset == Vector2.zero)
                {
                    cameraFollow.ClearRuntimeOffset();
                    cameraFollow.SnapToTarget();
                }
                else
                {
                    cameraFollow.SetRuntimeOffset(targetRuntimeOffset);
                    cameraFollow.SnapToTarget();
                }
            }
        }

        private void ApplyCameraRuntimeOffset(float currentSize)
        {
            if (cameraFollow == null)
            {
                return;
            }

            var runtimeOffset = GetTargetRuntimeOffset(currentSize);

            if (runtimeOffset == Vector2.zero)
            {
                cameraFollow.ClearRuntimeOffset();
                return;
            }

            cameraFollow.SetRuntimeOffset(runtimeOffset);
        }

        private Vector2 GetTargetRuntimeOffset(float targetSize)
        {
            var currentSize = targetSize;

            if (currentSize <= normalOrthographicSize + 0.01f)
            {
                return Vector2.zero;
            }

            var runtimeOffset = Vector2.zero;

            if (_trackedPlayer != null && cameraFollow != null && TryGetFocusCenter(out var focusCenter))
            {
                if (focusHorizontal)
                {
                    runtimeOffset.x = focusCenter.x - _trackedPlayer.position.x - cameraFollow.Offset.x;
                }

                if (focusVertical)
                {
                    runtimeOffset.y = focusCenter.y - _trackedPlayer.position.y - cameraFollow.Offset.y;
                }
            }

            if (keepBottomClamped)
            {
                var zoomDelta = Mathf.Max(0f, currentSize - normalOrthographicSize);
                runtimeOffset.y = Mathf.Max(runtimeOffset.y, zoomDelta);
            }

            return runtimeOffset;
        }

        private bool TryGetFocusCenter(out Vector3 focusCenter)
        {
            focusCenter = default;

            if (focusRoot != null && TryGetFocusBounds(out var focusBounds))
            {
                focusCenter = focusBounds.center;
                return true;
            }

            return TryGetZoneCenter(out focusCenter);
        }

        private bool TryGetFocusBounds(out Bounds bounds)
        {
            bounds = default;

            if (focusRoot == null)
            {
                return false;
            }

            var renderers = focusRoot.GetComponentsInChildren<SpriteRenderer>(false);
            var hasBounds = false;

            foreach (var spriteRenderer in renderers)
            {
                if (spriteRenderer == null || !spriteRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = spriteRenderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(spriteRenderer.bounds);
            }

            if (!hasBounds || focusPadding <= 0f)
            {
                return hasBounds;
            }

            bounds.Expand(focusPadding);
            return true;
        }

        private bool TryGetZoneCenter(out Vector3 center)
        {
            center = default;

            if (!Zones.TryGetValue(zoneName, out var zoneBoundaries) || zoneBoundaries.Count < 2)
            {
                return false;
            }

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var yTotal = 0f;
            var validCount = 0;

            foreach (var boundary in zoneBoundaries)
            {
                if (boundary == null)
                {
                    continue;
                }

                var boundaryPosition = boundary.transform.position;
                minX = Mathf.Min(minX, boundaryPosition.x);
                maxX = Mathf.Max(maxX, boundaryPosition.x);
                yTotal += boundaryPosition.y;
                validCount++;
            }

            if (validCount < 2 || float.IsInfinity(minX) || Mathf.Approximately(minX, maxX))
            {
                return false;
            }

            center = new Vector3((minX + maxX) * 0.5f, yTotal / validCount, 0f);
            return true;
        }

        private void FindCameraReferences()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (cameraFollow == null && targetCamera != null)
            {
                cameraFollow = targetCamera.GetComponent<CameraFollow2D>();
            }
        }

        private void OnValidate()
        {
            zoomDuration = Mathf.Max(0.01f, zoomDuration);
            oneShotHoldDuration = Mathf.Max(0f, oneShotHoldDuration);
            oneShotReturnDuration = Mathf.Max(0.01f, oneShotReturnDuration);
            focusPadding = Mathf.Max(0f, focusPadding);
            insidePadding = Mathf.Max(0f, insidePadding);
        }

        private static float SmoothCinematic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (6f * value - 15f) + 10f);
        }
    }
}
