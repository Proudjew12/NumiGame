using System.Collections;
using UnityEngine;
using NumiDream.Input;

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
        [InspectorName("Rope Y")]
        [SerializeField] private float ropeTargetWorldY = 5.08f;
        [Space(4)]
        [InspectorName("Hands Ground")]
        [SerializeField] private Transform handsUpGround;
        [Space(4)]
        [InspectorName("Hands Y")]
        [SerializeField] private float handsTargetWorldY = -5.16f;

        [Space(10)]
        [Header("--------- Motion ---------")]
        [Header("+Feel+")]
        [Space(4)]
        [InspectorName("Move Time")]
        [SerializeField] private float moveDuration = 1.25f;

        private Coroutine _moveRoutine;
        private bool _completed;

        public bool IsCompleted => _completed;

        private void Reset()
        {
            rope = transform;
        }

        private void Awake()
        {
            if (rope == null)
            {
                rope = transform;
            }

            FindPlayerIfNeeded();
            RefreshCompletionState();
        }

        private void Update()
        {
            if (_completed || _moveRoutine != null || !WasLiftPressed() || !CanPlayerInteract())
            {
                return;
            }

            TriggerLift();
        }

        [ContextMenu("Trigger Puzzle Four Lift")]
        public void TriggerLift()
        {
            if (_completed || _moveRoutine != null)
            {
                return;
            }

            _moveRoutine = StartCoroutine(MoveRoutine());
        }

        [ContextMenu("Complete Puzzle Four Lift")]
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

        private IEnumerator MoveRoutine()
        {
            var ropeStart = rope != null ? rope.position : Vector3.zero;
            var handsStart = handsUpGround != null ? handsUpGround.position : Vector3.zero;
            var ropeTarget = GetTargetPosition(ropeStart, ropeTargetWorldY);
            var handsTarget = GetTargetPosition(handsStart, handsTargetWorldY);
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, moveDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);

                MoveTo(rope, Vector3.LerpUnclamped(ropeStart, ropeTarget, t));
                MoveTo(handsUpGround, Vector3.LerpUnclamped(handsStart, handsTarget, t));

                yield return null;
            }

            SetFinalPositions();
            _completed = true;
            _moveRoutine = null;
        }

        private void SetFinalPositions()
        {
            if (rope != null)
            {
                MoveTo(rope, GetTargetPosition(rope.position, ropeTargetWorldY));
            }

            if (handsUpGround != null)
            {
                MoveTo(handsUpGround, GetTargetPosition(handsUpGround.position, handsTargetWorldY));
            }
        }

        private static Vector3 GetTargetPosition(Vector3 current, float targetY)
        {
            current.y = targetY;
            return current;
        }

        private static void MoveTo(Transform target, Vector3 position)
        {
            if (target != null)
            {
                target.position = position;
            }
        }

        private void RefreshCompletionState()
        {
            var ropeComplete = rope == null || Mathf.Approximately(rope.position.y, ropeTargetWorldY);
            var handsComplete = handsUpGround == null || Mathf.Approximately(handsUpGround.position.y, handsTargetWorldY);
            _completed = ropeComplete && handsComplete;
        }

        private bool CanPlayerInteract()
        {
            if (!requirePlayerInRange)
            {
                return true;
            }

            FindPlayerIfNeeded();
            return player != null && Vector2.Distance(player.position, transform.position) <= activationDistance;
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null)
            {
                return;
            }

            var playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        private static bool WasLiftPressed()
        {
            return NumiInput.WasPuzzleActionPressed();
        }

        private void OnValidate()
        {
            activationDistance = Mathf.Max(0f, activationDistance);
            moveDuration = Mathf.Max(0.01f, moveDuration);
        }

        private void OnDrawGizmosSelected()
        {
            if (requirePlayerInRange)
            {
                Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.65f);
                Gizmos.DrawWireSphere(transform.position, activationDistance);
            }

            Gizmos.color = new Color(0.45f, 1f, 0.45f, 0.75f);
            DrawTargetLine(rope, ropeTargetWorldY);
            DrawTargetLine(handsUpGround, handsTargetWorldY);
        }

        private static void DrawTargetLine(Transform target, float targetY)
        {
            if (target == null)
            {
                return;
            }

            var targetPosition = target.position;
            targetPosition.y = targetY;
            Gizmos.DrawLine(target.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.2f);
        }
    }
}
