using UnityEngine;

namespace NumiDream.StageOne
{
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer2D : MonoBehaviour
    {
        [Header("--------- Parallax ---------")]
        [Header("+Settings+")]
        [Space(4)]
        [InspectorName("Enabled")]
        [SerializeField] private bool enableParallax;
        [Space(4)]
        [InspectorName("Target")]
        [SerializeField] private Transform followTarget;
        [Space(4)]
        [InspectorName("Multiplier")]
        [SerializeField] private Vector2 parallaxMultiplier = new Vector2(0.2f, 0.08f);

        private Vector3 _startPosition;
        private Vector3 _targetStartPosition;

        public Transform FollowTarget
        {
            get => followTarget;
            set
            {
                followTarget = value;
                CacheStartPositions();
            }
        }

        private void Awake()
        {
            CacheStartPositions();
        }

        private void LateUpdate()
        {
            if (!enableParallax || followTarget == null)
            {
                return;
            }

            var delta = followTarget.position - _targetStartPosition;
            transform.position = new Vector3(
                _startPosition.x + delta.x * parallaxMultiplier.x,
                _startPosition.y + delta.y * parallaxMultiplier.y,
                _startPosition.z);
        }

        private void CacheStartPositions()
        {
            _startPosition = transform.position;
            _targetStartPosition = followTarget != null ? followTarget.position : Vector3.zero;
        }
    }
}
