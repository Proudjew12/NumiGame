using System;
using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class PuzzleThreeBellIslands : MonoBehaviour
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
        [Header("--------- Bell ---------")]
        [Header("+Feedback+")]
        [Space(4)]
        [InspectorName("Bell Visual")]
        [SerializeField] private Transform bellVisual;
        [Space(4)]
        [InspectorName("Ring Angle")]
        [SerializeField] private float ringAngle = 7f;
        [Space(4)]
        [InspectorName("Ring Time")]
        [SerializeField] private float ringDuration = 0.28f;

        [Space(10)]
        [Header("--------- Islands ---------")]
        [Header("+Sequence+")]
        [Space(4)]
        [InspectorName("Islands")]
        [SerializeField] private Transform[] islands = Array.Empty<Transform>();
        [Space(4)]
        [InspectorName("Target Y")]
        [SerializeField] private float targetWorldY = -5.85f;
        [Space(4)]
        [InspectorName("Move Time")]
        [SerializeField] private float moveDuration = 0.85f;

        private Vector3[] _startPositions = Array.Empty<Vector3>();
        private Coroutine _moveRoutine;
        private Coroutine _ringRoutine;
        private int _nextIslandIndex;
        private bool _completed;

        public int NextIslandIndex => _nextIslandIndex;
        public bool IsCompleted => _completed;

        private void Reset()
        {
            bellVisual = transform;
        }

        private void Awake()
        {
            if (bellVisual == null)
            {
                bellVisual = transform;
            }

            FindPlayerIfNeeded();
            CacheStartPositions();
            RefreshCompletionState();
        }

        private void Update()
        {
            if (_completed || _moveRoutine != null || !WasBellPressed() || !CanPlayerInteract())
            {
                return;
            }

            ActivateNextIsland();
        }

        [ContextMenu("Reset Puzzle Three Islands")]
        public void ResetPuzzle()
        {
            CacheStartPositions();

            for (var i = 0; i < islands.Length && i < _startPositions.Length; i++)
            {
                if (islands[i] != null)
                {
                    islands[i].position = _startPositions[i];
                }
            }

            _nextIslandIndex = 0;
            _completed = false;
        }

        private void ActivateNextIsland()
        {
            var islandIndex = GetNextUnfinishedIslandIndex();
            if (islandIndex < 0)
            {
                CompletePuzzle();
                return;
            }

            RingBell();
            _moveRoutine = StartCoroutine(MoveIslandRoutine(islandIndex));
        }

        private int GetNextUnfinishedIslandIndex()
        {
            for (var i = Mathf.Max(0, _nextIslandIndex); i < islands.Length; i++)
            {
                if (islands[i] == null)
                {
                    continue;
                }

                if (!Mathf.Approximately(islands[i].position.y, targetWorldY))
                {
                    return i;
                }

                _nextIslandIndex = i + 1;
            }

            return -1;
        }

        private IEnumerator MoveIslandRoutine(int islandIndex)
        {
            var island = islands[islandIndex];
            var start = island.position;
            var target = GetTargetPosition(island);
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, moveDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                island.position = Vector3.LerpUnclamped(start, target, t);
                yield return null;
            }

            FinishIslandMove(islandIndex);
            _moveRoutine = null;
        }

        private void FinishIslandMove(int islandIndex)
        {
            if (islandIndex < 0 || islandIndex >= islands.Length || islands[islandIndex] == null)
            {
                return;
            }

            islands[islandIndex].position = GetTargetPosition(islands[islandIndex]);
            _nextIslandIndex = Mathf.Max(_nextIslandIndex, islandIndex + 1);

            if (GetNextUnfinishedIslandIndex() < 0)
            {
                CompletePuzzle();
            }
        }

        private Vector3 GetTargetPosition(Transform island)
        {
            var position = island.position;
            position.y = targetWorldY;
            return position;
        }

        private void RingBell()
        {
            if (bellVisual == null || ringAngle <= 0f || ringDuration <= 0f)
            {
                return;
            }

            if (_ringRoutine != null)
            {
                StopCoroutine(_ringRoutine);
            }

            _ringRoutine = StartCoroutine(RingBellRoutine());
        }

        private IEnumerator RingBellRoutine()
        {
            var startRotation = bellVisual.localRotation;
            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, ringDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var wobble = Mathf.Sin(t * Mathf.PI * 2f) * ringAngle * (1f - t);
                bellVisual.localRotation = startRotation * Quaternion.Euler(0f, 0f, wobble);
                yield return null;
            }

            bellVisual.localRotation = startRotation;
            _ringRoutine = null;
        }

        private void CompletePuzzle()
        {
            _completed = true;
            _moveRoutine = null;
        }

        private void RefreshCompletionState()
        {
            _nextIslandIndex = 0;

            for (var i = 0; i < islands.Length; i++)
            {
                if (islands[i] == null)
                {
                    continue;
                }

                if (!Mathf.Approximately(islands[i].position.y, targetWorldY))
                {
                    _nextIslandIndex = i;
                    _completed = false;
                    return;
                }

                _nextIslandIndex = i + 1;
            }

            _completed = islands.Length > 0;
        }

        private void CacheStartPositions()
        {
            _startPositions = new Vector3[islands.Length];

            for (var i = 0; i < islands.Length; i++)
            {
                _startPositions[i] = islands[i] != null ? islands[i].position : Vector3.zero;
            }
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

        private static bool WasBellPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.T);
#else
            return false;
#endif
        }

        private void OnValidate()
        {
            activationDistance = Mathf.Max(0f, activationDistance);
            ringAngle = Mathf.Max(0f, ringAngle);
            ringDuration = Mathf.Max(0f, ringDuration);
            moveDuration = Mathf.Max(0.01f, moveDuration);
        }

        private void OnDrawGizmosSelected()
        {
            if (requirePlayerInRange)
            {
                Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.65f);
                Gizmos.DrawWireSphere(transform.position, activationDistance);
            }

            if (islands == null)
            {
                return;
            }

            Gizmos.color = new Color(0.45f, 1f, 0.45f, 0.7f);
            foreach (var island in islands)
            {
                if (island == null)
                {
                    continue;
                }

                var target = GetTargetPosition(island);
                Gizmos.DrawLine(island.position, target);
                Gizmos.DrawWireSphere(target, 0.18f);
            }
        }
    }
}
