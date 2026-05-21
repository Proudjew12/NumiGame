using UnityEngine;

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BicycleWheelFallTrigger : MonoBehaviour
    {
        [Header("--------- Fall Trigger ---------")]
        [Header("+Wheel+")]
        [Space(4)]
        [SerializeField] private BicycleWheelPuzzle wheel;
        [Space(4)]
        [InspectorName("Trigger Once")]
        [SerializeField] private bool triggerOnce = true;

        private bool _triggered;

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void Awake()
        {
            EnsureTriggerCollider();

            if (wheel == null)
            {
                wheel = FindFirstObjectByType<BicycleWheelPuzzle>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryReleaseWheel(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryReleaseWheel(other);
        }

        private void TryReleaseWheel(Collider2D other)
        {
            if (triggerOnce && _triggered)
            {
                return;
            }

            if (wheel == null || !wheel.OwnsCollider(other))
            {
                return;
            }

            _triggered = true;
            wheel.ReleaseToAutoFall();
        }

        private void EnsureTriggerCollider()
        {
            foreach (var triggerCollider in GetComponents<Collider2D>())
            {
                triggerCollider.isTrigger = true;
            }
        }
    }
}
