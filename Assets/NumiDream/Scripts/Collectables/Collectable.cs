using System.Collections;
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

        [Header("+Water Orb+")]
        [Space(4)]
        [InspectorName("Water Orb")]
        [SerializeField] private WaterOrbUIController waterOrb;
        [Space(4)]
        [InspectorName("Fill Amount Per Collect")]
        [SerializeField] private float fillAmountPerCollect = 0.25f;
        [Space(4)]
        [InspectorName("Fill Animation Duration")]
        [SerializeField] private float fillDuration = 0.5f;

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
                return;

            _collected = true;

            if (waterOrb != null)
            {
                float target = Mathf.Clamp01(waterOrb.fillAmount + fillAmountPerCollect);
                waterOrb.StartCoroutine(waterOrb.AnimateFillTo(target, fillDuration));
            }

            if (destroyOnCollect)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}