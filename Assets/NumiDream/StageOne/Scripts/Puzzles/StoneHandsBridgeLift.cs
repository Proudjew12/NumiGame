using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class StoneHandsBridgeLift : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Header("+Scene Objects+")]
        [Space(4)]
        [InspectorName("Bridge")]
        [SerializeField] private Transform stoneHandsBridge;
        [Space(4)]
        [InspectorName("Bell Rope")]
        [SerializeField] private Transform bellRope;
        [Space(4)]
        [InspectorName("Player Ground")]
        [SerializeField] private Transform playerGroundCheck;
        [Space(4)]
        [InspectorName("Bridge Collider")]
        [SerializeField] private Collider2D bridgeCollider;
        [Space(4)]
        [InspectorName("Bridge Renderer")]
        [SerializeField] private Renderer bridgeRenderer;

        [Space(10)]
        [Header("--------- Input ---------")]
        [Header("+Activation+")]
        [Space(4)]
        [InspectorName("Require Range")]
        [SerializeField] private bool requirePlayerInRange;
        [Space(4)]
        [SerializeField] private Transform player;
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Distance")]
        [SerializeField] private float activationDistance = 5f;

        [Space(10)]
        [Header("--------- Motion ---------")]
        [Header("+Bridge Lift+")]
        [Space(4)]
        [InspectorName("Rise Speed")]
        [SerializeField] private float bridgeRiseSpeed = 0.85f;
        [Space(4)]
        [InspectorName("Rope Drop")]
        [SerializeField] private float ropeDropPerBridgeUnit = 0.75f;
        [Space(4)]
        [InspectorName("Stop Offset")]
        [SerializeField] private float stopBelowGroundCheck = 0.03f;
        [Space(4)]
        [InspectorName("Fallback Rise")]
        [SerializeField] private float fallbackMaxBridgeRise = 6f;

        private Vector3 _bridgeStartPosition;
        private Vector3 _ropeStartPosition;
        private float _bridgeRaisedDistance;
        private bool _completed;
        private bool _hasCachedStartPose;

        private void Reset()
        {
            bellRope = transform;
            FindSceneReferences();
        }

        private void Awake()
        {
            if (bellRope == null)
            {
                bellRope = transform;
            }

            FindSceneReferences();
            CacheStartPose();

            if (HasReachedStop())
            {
                _completed = true;
            }
        }

        private void Update()
        {
            if (_completed || !IsLiftInputHeld() || !CanPlayerInteract())
            {
                return;
            }

            CacheStartPose();

            var step = GetAllowedRiseStep(bridgeRiseSpeed * Time.deltaTime);
            if (step <= 0f)
            {
                CompleteLift();
                return;
            }

            MoveTransform(stoneHandsBridge, Vector3.up * step);
            MoveTransform(bellRope, Vector3.down * (step * ropeDropPerBridgeUnit));
            _bridgeRaisedDistance += step;

            if (HasReachedStop())
            {
                CompleteLift();
            }
        }

        [ContextMenu("Reset Stone Hands Lift")]
        public void ResetLift()
        {
            CacheStartPose();

            if (stoneHandsBridge != null)
            {
                stoneHandsBridge.position = _bridgeStartPosition;
            }

            if (bellRope != null)
            {
                bellRope.position = _ropeStartPosition;
            }

            _bridgeRaisedDistance = 0f;
            _completed = false;
        }

        private void CompleteLift()
        {
            _completed = true;
        }

        private float GetAllowedRiseStep(float requestedStep)
        {
            requestedStep = Mathf.Max(0f, requestedStep);

            if (playerGroundCheck != null)
            {
                var targetTopY = playerGroundCheck.position.y - stopBelowGroundCheck;
                var remaining = targetTopY - GetBridgeTopY();
                return Mathf.Clamp(requestedStep, 0f, Mathf.Max(0f, remaining));
            }

            var fallbackRemaining = fallbackMaxBridgeRise - _bridgeRaisedDistance;
            return Mathf.Clamp(requestedStep, 0f, Mathf.Max(0f, fallbackRemaining));
        }

        private bool HasReachedStop()
        {
            if (playerGroundCheck != null)
            {
                return GetBridgeTopY() >= playerGroundCheck.position.y - stopBelowGroundCheck;
            }

            return _bridgeRaisedDistance >= fallbackMaxBridgeRise;
        }

        private float GetBridgeTopY()
        {
            if (bridgeCollider != null && bridgeCollider.enabled)
            {
                return bridgeCollider.bounds.max.y;
            }

            if (bridgeRenderer != null && bridgeRenderer.enabled)
            {
                return bridgeRenderer.bounds.max.y;
            }

            return stoneHandsBridge != null ? stoneHandsBridge.position.y : float.PositiveInfinity;
        }

        private bool CanPlayerInteract()
        {
            if (!requirePlayerInRange)
            {
                return true;
            }

            if (player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            return player != null && Vector2.Distance(player.position, transform.position) <= activationDistance;
        }

        private void FindSceneReferences()
        {
            if (stoneHandsBridge == null)
            {
                var bridgeObject = GameObject.Find("StoneHandsBridge_Final");
                if (bridgeObject != null)
                {
                    stoneHandsBridge = bridgeObject.transform;
                }
            }

            if (stoneHandsBridge != null)
            {
                if (bridgeCollider == null)
                {
                    bridgeCollider = stoneHandsBridge.GetComponent<Collider2D>();
                }

                if (bridgeRenderer == null)
                {
                    bridgeRenderer = stoneHandsBridge.GetComponent<Renderer>();
                }
            }

            if (player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            if (playerGroundCheck == null && player != null)
            {
                var groundCheck = player.Find("Player-GroundCheck") ?? player.Find("GroundCheck");
                if (groundCheck != null)
                {
                    playerGroundCheck = groundCheck;
                }
            }
        }

        private void CacheStartPose()
        {
            if (_hasCachedStartPose)
            {
                return;
            }

            if (stoneHandsBridge != null)
            {
                _bridgeStartPosition = stoneHandsBridge.position;
            }

            if (bellRope != null)
            {
                _ropeStartPosition = bellRope.position;
            }

            _hasCachedStartPose = true;
        }

        private static void MoveTransform(Transform target, Vector3 worldDelta)
        {
            if (target != null)
            {
                target.position += worldDelta;
            }
        }

        private static bool IsLiftInputHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.isPressed)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.T);
#else
            return false;
#endif
        }

        private void OnDrawGizmosSelected()
        {
            if (requirePlayerInRange)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
                Gizmos.DrawWireSphere(transform.position, activationDistance);
            }

            if (playerGroundCheck == null)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 1f, 0.45f, 0.75f);
            var from = new Vector3(transform.position.x - 1f, playerGroundCheck.position.y - stopBelowGroundCheck, transform.position.z);
            var to = new Vector3(transform.position.x + 1f, playerGroundCheck.position.y - stopBelowGroundCheck, transform.position.z);
            Gizmos.DrawLine(from, to);
        }
    }
}
