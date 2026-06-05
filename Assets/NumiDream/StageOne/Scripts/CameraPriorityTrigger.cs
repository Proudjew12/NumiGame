using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

namespace NumiDream.StageOne
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CameraPriorityTrigger : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Space(4)]
        [InspectorName("Virtual Camera")]
        [SerializeField] private CinemachineCamera virtualCamera;

        [Space(10)]
        [Header("--------- Settings ---------")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Active Priority")]
        [SerializeField] private int activePriority = 2;
        [Space(4)]
        [InspectorName("Inactive Priority")]
        [SerializeField] private int inactivePriority = 0;
        [Space(4)]
        [InspectorName("Reset Delay")]
        [SerializeField] private float resetDelay = 1.5f;

        private Coroutine _resetRoutine;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;

            if (virtualCamera == null)
            {
                virtualCamera = GetComponent<CinemachineCamera>();
            }

            if (virtualCamera != null)
            {
                virtualCamera.Priority = inactivePriority;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (virtualCamera == null) return;

            if (_resetRoutine != null)
            {
                StopCoroutine(_resetRoutine);
                _resetRoutine = null;
            }

            virtualCamera.Priority = activePriority;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag(playerTag)) return;
            if (virtualCamera == null) return;

            if (_resetRoutine != null) StopCoroutine(_resetRoutine);
            _resetRoutine = StartCoroutine(ResetPriorityAfterDelay());
        }

        private IEnumerator ResetPriorityAfterDelay()
        {
            yield return new WaitForSeconds(resetDelay);

            if (virtualCamera != null)
            {
                virtualCamera.Priority = inactivePriority;
            }

            _resetRoutine = null;
        }
    }
}