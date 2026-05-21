using UnityEngine;

namespace NumiDream.Collectables
{
    [DisallowMultipleComponent]
    public sealed class Collectable : MonoBehaviour
    {
        [Header("--------- Collectable ---------")]
        [Header("+Trigger+")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Destroy On Collect")]
        [SerializeField] private bool destroyOnCollect = true;

        private bool _collected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryCollect(collision.gameObject);
        }

        private void TryCollect(GameObject other)
        {
            if (_collected || !other.CompareTag(playerTag))
            {
                return;
            }

            _collected = true;

            if (destroyOnCollect)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
