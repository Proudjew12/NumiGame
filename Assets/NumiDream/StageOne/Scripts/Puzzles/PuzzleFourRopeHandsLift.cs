using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleFourRopeHandsLift : MonoBehaviour
    {
        [Header("--------- Player ---------")]
        [Header("+Activation+")]
        [Space(4)]
        [SerializeField] private Transform player;
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Require Range")]
        [SerializeField] private bool requirePlayerInRange = true;
        [Space(4)]
        [InspectorName("Distance")]
        [SerializeField] private float activationDistance = 6f;

        [Space(10)]
        [Header("--------- Lift ---------")]
        [Header("+Targets+")]
        [Space(4)]
        [InspectorName("Rope")]
        [SerializeField] private Transform rope;
        [Space(4)]
        [InspectorName("Rope Target Y (final)")]
        [SerializeField] private float ropeTargetWorldY = 5.08f;
        [Space(4)]
        [InspectorName("Hands Ground")]
        [SerializeField] private Transform handsUpGround;
        [Space(4)]
        [InspectorName("Hands Target Y (final)")]
        [SerializeField] private float handsTargetWorldY = -5.16f;

        [Space(10)]
        [Header("--------- Tap Motion ---------")]
        [Header("+Feel+")]
        [Space(4)]
        [InspectorName("Step Size (units per tap)")]
        [SerializeField] private float stepSize = 0.25f;
        [Space(4)]
        [InspectorName("Step Duration (sec)")]
        [SerializeField] private float stepDuration = 0.15f;

        private Coroutine _moveRoutine;
        private bool _completed;

        // Tracks where each object currently is (or is animating toward)
        private float _ropeCurrentY;
        private float _handsCurrentY;

        public bool IsCompleted => _completed;

        private void Reset()
        {
            rope = transform;
        }

        private void Awake()
        {
            if (rope == null)
                rope = transform;

            FindPlayerIfNeeded();
            RefreshCompletionState();

            // Initialise current Y trackers from actual world positions
            _ropeCurrentY  = rope  != null ? rope.position.y          : ropeTargetWorldY;
            _handsCurrentY = handsUpGround != null ? handsUpGround.position.y : handsTargetWorldY;
        }

        private void Update()
        {
            if (_completed || !WasLiftPressed() || !CanPlayerInteract())
                return;

            TriggerTap();
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        [ContextMenu("Trigger One Tap")]
        public void TriggerTap()
        {
            if (_completed)
                return;

            // Calculate the next Y values clamped to their targets
            float nextRopeY  = Mathf.Min(_ropeCurrentY  + stepSize, ropeTargetWorldY);
            float nextHandsY = Mathf.Max(_handsCurrentY - stepSize, handsTargetWorldY);

            // Snap current trackers so a new tap mid-animation starts from here
            _ropeCurrentY  = nextRopeY;
            _handsCurrentY = nextHandsY;

            // Restart the step animation toward the new targets
            if (_moveRoutine != null)
                StopCoroutine(_moveRoutine);

            _moveRoutine = StartCoroutine(StepRoutine(nextRopeY, nextHandsY));
        }

        [ContextMenu("Complete Puzzle Four Lift Instantly")]
        public void CompleteLiftNow()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            SetFinalPositions();
            _completed = true;
        }

        // ──────────────────────────────────────────────
        //  Coroutines
        // ──────────────────────────────────────────────

        private IEnumerator StepRoutine(float targetRopeY, float targetHandsY)
        {
            var ropeStart  = rope           != null ? rope.position           : Vector3.zero;
            var handsStart = handsUpGround  != null ? handsUpGround.position  : Vector3.zero;

            var ropeTarget  = GetTargetPosition(ropeStart,  targetRopeY);
            var handsTarget = GetTargetPosition(handsStart, targetHandsY);

            float elapsed  = 0f;
            float duration = Mathf.Max(0.01f, stepDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t); // smoothstep

                MoveTo(rope,          Vector3.LerpUnclamped(ropeStart,  ropeTarget,  t));
                MoveTo(handsUpGround, Vector3.LerpUnclamped(handsStart, handsTarget, t));

                yield return null;
            }

            // Snap to exact targets
            MoveTo(rope,          ropeTarget);
            MoveTo(handsUpGround, handsTarget);

            _moveRoutine = null;

            // Check if fully done
            if (Mathf.Approximately(targetRopeY, ropeTargetWorldY) &&
                Mathf.Approximately(targetHandsY, handsTargetWorldY))
            {
                _completed = true;
            }
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        private void SetFinalPositions()
        {
            if (rope != null)
                MoveTo(rope, GetTargetPosition(rope.position, ropeTargetWorldY));

            if (handsUpGround != null)
                MoveTo(handsUpGround, GetTargetPosition(handsUpGround.position, handsTargetWorldY));

            _ropeCurrentY  = ropeTargetWorldY;
            _handsCurrentY = handsTargetWorldY;
        }

        private static Vector3 GetTargetPosition(Vector3 current, float targetY)
        {
            current.y = targetY;
            return current;
        }

        private static void MoveTo(Transform target, Vector3 position)
        {
            if (target != null)
                target.position = position;
        }

        private void RefreshCompletionState()
        {
            var ropeComplete  = rope          == null || Mathf.Approximately(rope.position.y,          ropeTargetWorldY);
            var handsComplete = handsUpGround == null || Mathf.Approximately(handsUpGround.position.y, handsTargetWorldY);
            _completed = ropeComplete && handsComplete;
        }

        private bool CanPlayerInteract()
        {
            if (!requirePlayerInRange)
                return true;

            FindPlayerIfNeeded();
            return player != null && Vector2.Distance(player.position, transform.position) <= activationDistance;
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null)
                return;

            var playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
                player = playerObject.transform;
        }

        private static bool WasLiftPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.tKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        // ──────────────────────────────────────────────
        //  Editor
        // ──────────────────────────────────────────────

        private void OnValidate()
        {
            activationDistance = Mathf.Max(0f,    activationDistance);
            stepDuration       = Mathf.Max(0.01f, stepDuration);
            stepSize           = Mathf.Max(0.01f, stepSize);
        }

        private void OnDrawGizmosSelected()
        {
            if (requirePlayerInRange)
            {
                Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.65f);
                Gizmos.DrawWireSphere(transform.position, activationDistance);
            }

            Gizmos.color = new Color(0.45f, 1f, 0.45f, 0.75f);
            DrawTargetLine(rope,          ropeTargetWorldY);
            DrawTargetLine(handsUpGround, handsTargetWorldY);
        }

        private static void DrawTargetLine(Transform target, float targetY)
        {
            if (target == null)
                return;

            var targetPosition   = target.position;
            targetPosition.y     = targetY;
            Gizmos.DrawLine(target.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.2f);
        }
    }
}