using UnityEngine;

namespace NumiDream.StageOne.Respawn
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class RespawnPoint : MonoBehaviour
    {
        [Header("--------- Respawn Point ---------")]
        [Header("+Trigger+")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Use Once")]
        [SerializeField] private bool useOnlyOnce;

        private bool _used;

        public Vector3 SpawnPosition => transform.position;

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryRegister(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryRegister(other);
        }

        private void TryRegister(Collider2D other)
        {
            if (useOnlyOnce && _used)
            {
                return;
            }

            if (!other.CompareTag(playerTag))
            {
                return;
            }

            var respawnController = other.GetComponentInParent<PlayerRespawnController>();
            if (respawnController == null)
            {
                return;
            }

            _used = true;
            respawnController.SetRespawnPoint(this);
        }

        private void EnsureTriggerCollider()
        {
            foreach (var triggerCollider in GetComponents<Collider2D>())
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.75f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.down * 0.65f, transform.position + Vector3.up * 0.65f);
        }
    }
}
