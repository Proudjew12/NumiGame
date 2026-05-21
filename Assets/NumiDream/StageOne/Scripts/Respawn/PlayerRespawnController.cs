using System.Collections;
using NumiDream.Nomi;
using UnityEngine;

namespace NumiDream.StageOne.Respawn
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [Header("--------- Respawn ---------")]
        [Header("+Position+")]
        [Space(4)]
        [InspectorName("Current Point")]
        [SerializeField] private RespawnPoint currentRespawnPoint;
        [Space(4)]
        [InspectorName("Fall Y")]
        [SerializeField] private float fallYThreshold = -8f;
        [Space(4)]
        [InspectorName("Offset")]
        [SerializeField] private Vector2 respawnOffset = new Vector2(0f, 0.35f);
        [Space(4)]
        [InspectorName("Keep Z")]
        [SerializeField] private bool keepCurrentZ = true;

        [Space(10)]
        [Header("--------- Blink ---------")]
        [Header("+Timing+")]
        [Space(4)]
        [InspectorName("Duration")]
        [SerializeField] private float blinkDuration = 2f;
        [Space(4)]
        [InspectorName("Interval")]
        [SerializeField] private float blinkInterval = 0.12f;

        [Space(10)]
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private Rigidbody2D body;
        [Space(4)]
        [InspectorName("Blink Renderers")]
        [SerializeField] private SpriteRenderer[] renderersToBlink;
        [Space(4)]
        [InspectorName("Movement")]
        [SerializeField] private NomiPlayerMovement playerMovement;
        [Space(4)]
        [InspectorName("Animator Driver")]
        [SerializeField] private NomiAnimatorDriver animatorDriver;
        [Space(4)]
        [InspectorName("Procedural Animators")]
        [SerializeField] private NomiProceduralAnimator[] proceduralAnimators;

        private Vector3 _fallbackRespawnPosition;
        private bool _isRespawning;

        private void Reset()
        {
            FindReferences();
        }

        private void Awake()
        {
            FindReferences();
            _fallbackRespawnPosition = transform.position;
        }

        private void Update()
        {
            if (_isRespawning || transform.position.y > fallYThreshold)
            {
                return;
            }

            StartCoroutine(RespawnRoutine());
        }

        public void SetRespawnPoint(RespawnPoint respawnPoint)
        {
            if (respawnPoint == null)
            {
                return;
            }

            currentRespawnPoint = respawnPoint;
            _fallbackRespawnPosition = respawnPoint.SpawnPosition;
        }

        [ContextMenu("Respawn Now")]
        public void RespawnNow()
        {
            if (_isRespawning)
            {
                return;
            }

            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            _isRespawning = true;
            FindBlinkRenderersIfNeeded();
            var originalRendererStates = CaptureRendererStates();

            MoveToRespawnPoint();
            StopPlayerVelocity();
            ResetAnimationState();

            yield return BlinkRoutine(originalRendererStates);

            RestoreRenderers(originalRendererStates);
            ResetAnimationState();
            _isRespawning = false;
        }

        private void MoveToRespawnPoint()
        {
            var targetPosition = currentRespawnPoint != null
                ? currentRespawnPoint.SpawnPosition
                : _fallbackRespawnPosition;

            targetPosition += (Vector3)respawnOffset;

            if (keepCurrentZ)
            {
                targetPosition.z = transform.position.z;
            }

            transform.position = targetPosition;
        }

        private void StopPlayerVelocity()
        {
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private IEnumerator BlinkRoutine(bool[] originalRendererStates)
        {
            var elapsed = 0f;
            var visible = true;

            while (elapsed < blinkDuration)
            {
                visible = !visible;
                SetBlinkVisible(visible, originalRendererStates);

                var waitTime = Mathf.Max(0.03f, blinkInterval);
                elapsed += waitTime;
                yield return new WaitForSeconds(waitTime);
            }
        }

        private bool[] CaptureRendererStates()
        {
            if (renderersToBlink == null)
            {
                return System.Array.Empty<bool>();
            }

            var states = new bool[renderersToBlink.Length];
            for (var i = 0; i < renderersToBlink.Length; i++)
            {
                states[i] = renderersToBlink[i] != null && renderersToBlink[i].enabled;
            }

            return states;
        }

        private void SetBlinkVisible(bool visible, bool[] originalRendererStates)
        {
            if (renderersToBlink == null)
            {
                return;
            }

            for (var i = 0; i < renderersToBlink.Length; i++)
            {
                var spriteRenderer = renderersToBlink[i];
                if (spriteRenderer != null)
                {
                    var originallyEnabled = i >= originalRendererStates.Length || originalRendererStates[i];
                    spriteRenderer.enabled = originallyEnabled && visible;
                }
            }
        }

        private void RestoreRenderers(bool[] originalRendererStates)
        {
            if (renderersToBlink == null)
            {
                return;
            }

            for (var i = 0; i < renderersToBlink.Length; i++)
            {
                if (renderersToBlink[i] != null && i < originalRendererStates.Length)
                {
                    renderersToBlink[i].enabled = originalRendererStates[i];
                }
            }
        }

        private void ResetAnimationState()
        {
            if (playerMovement != null)
            {
                playerMovement.ResetAfterRespawn();
            }

            if (animatorDriver != null)
            {
                animatorDriver.ResetAfterRespawn();
            }

            if (proceduralAnimators == null)
            {
                return;
            }

            for (var i = 0; i < proceduralAnimators.Length; i++)
            {
                if (proceduralAnimators[i] != null)
                {
                    proceduralAnimators[i].ResetVisualState();
                }
            }
        }

        private void FindReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (animatorDriver == null)
            {
                animatorDriver = GetComponentInChildren<NomiAnimatorDriver>(true);
            }

            if (playerMovement == null)
            {
                playerMovement = GetComponent<NomiPlayerMovement>();
            }

            if (proceduralAnimators == null || proceduralAnimators.Length == 0)
            {
                proceduralAnimators = GetComponentsInChildren<NomiProceduralAnimator>(true);
            }

            FindBlinkRenderersIfNeeded();
        }

        private void FindBlinkRenderersIfNeeded()
        {
            if (renderersToBlink != null && renderersToBlink.Length > 0)
            {
                return;
            }

            renderersToBlink = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);
            Gizmos.DrawLine(new Vector3(transform.position.x - 2f, fallYThreshold, transform.position.z),
                new Vector3(transform.position.x + 2f, fallYThreshold, transform.position.z));
        }
    }
}
