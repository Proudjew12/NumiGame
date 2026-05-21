using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NumiDream.StageOne.Triggers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CameraZoomMarkerTrigger : MonoBehaviour
    {
        private static readonly Dictionary<Camera, CameraZoomMarkerTrigger> ActiveOwners = new Dictionary<Camera, CameraZoomMarkerTrigger>();

        public enum ZoomAction
        {
            ZoomOut,
            RestoreNormal
        }

        [Header("--------- Marker ---------")]
        [Space(4)]
        [SerializeField] private ZoomAction action = ZoomAction.ZoomOut;
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";

        [Space(10)]
        [Header("--------- Camera ---------")]
        [Space(4)]
        [InspectorName("Target Camera")]
        [SerializeField] private Camera targetCamera;
        [Space(4)]
        [InspectorName("Camera Follow")]
        [SerializeField] private CameraFollow2D cameraFollow;
        [Space(4)]
        [InspectorName("Normal Size")]
        [SerializeField] private float normalOrthographicSize = 4.23f;
        [Space(4)]
        [InspectorName("Zoomed Size")]
        [SerializeField] private float zoomedOrthographicSize = 8.46f;
        [Space(4)]
        [InspectorName("Zoom Time")]
        [SerializeField] private float zoomDuration = 1.35f;
        [Space(4)]
        [InspectorName("Snap Follow")]
        [SerializeField] private bool snapFollowDuringZoom = true;
        [Space(4)]
        [InspectorName("Clear Offset On Restore")]
        [SerializeField] private bool clearRuntimeOffsetOnRestore = true;

        private Coroutine _zoomRoutine;
        private float _activeTargetSize = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActiveOwners.Clear();
        }

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void Awake()
        {
            EnsureTriggerCollider();
            FindCameraReferences();
        }

        private void OnDisable()
        {
            CancelZoomRoutine();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other))
            {
                return;
            }

            FindCameraReferences();

            if (targetCamera == null)
            {
                Debug.LogWarning("[CameraZoomMarkerTrigger] No target camera found.", this);
                return;
            }

            var targetSize = action == ZoomAction.ZoomOut
                ? zoomedOrthographicSize
                : normalOrthographicSize;

            StartZoom(targetSize);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (other.CompareTag(playerTag))
            {
                return true;
            }

            var attachedBody = other.attachedRigidbody;
            if (attachedBody != null && attachedBody.CompareTag(playerTag))
            {
                return true;
            }

            return other.GetComponentInParent<Transform>() != null &&
                   other.transform.root.CompareTag(playerTag);
        }

        private void StartZoom(float targetSize)
        {
            if (_zoomRoutine != null && Mathf.Approximately(_activeTargetSize, targetSize))
            {
                return;
            }

            if (_zoomRoutine != null)
            {
                CancelZoomRoutine();
            }

            if (ActiveOwners.TryGetValue(targetCamera, out var currentOwner) &&
                currentOwner != null &&
                currentOwner != this)
            {
                currentOwner.CancelZoomRoutine();
            }

            ActiveOwners[targetCamera] = this;
            _activeTargetSize = targetSize;
            _zoomRoutine = StartCoroutine(ZoomRoutine(targetSize));
        }

        private IEnumerator ZoomRoutine(float targetSize)
        {
            var startSize = targetCamera.orthographicSize;
            var elapsed = 0f;

            while (elapsed < zoomDuration)
            {
                elapsed += Time.deltaTime;
                var t = zoomDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / zoomDuration);
                t = t * t * (3f - 2f * t);

                targetCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, t);

                if (snapFollowDuringZoom && cameraFollow != null)
                {
                    cameraFollow.SnapToTarget();
                }

                yield return null;
            }

            targetCamera.orthographicSize = targetSize;

            if (Mathf.Approximately(targetSize, normalOrthographicSize) &&
                clearRuntimeOffsetOnRestore &&
                cameraFollow != null)
            {
                cameraFollow.ClearRuntimeOffset();
                cameraFollow.SnapToTarget();
            }

            _zoomRoutine = null;

            if (ActiveOwners.TryGetValue(targetCamera, out var owner) && owner == this)
            {
                ActiveOwners.Remove(targetCamera);
            }
        }

        private void CancelZoomRoutine()
        {
            if (_zoomRoutine != null)
            {
                StopCoroutine(_zoomRoutine);
                _zoomRoutine = null;
            }

            if (targetCamera != null &&
                ActiveOwners.TryGetValue(targetCamera, out var owner) &&
                owner == this)
            {
                ActiveOwners.Remove(targetCamera);
            }
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

        private void EnsureTriggerCollider()
        {
            var triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            normalOrthographicSize = Mathf.Max(0.01f, normalOrthographicSize);
            zoomedOrthographicSize = Mathf.Max(normalOrthographicSize, zoomedOrthographicSize);
            zoomDuration = Mathf.Max(0.01f, zoomDuration);
            EnsureTriggerCollider();
        }
    }
}
