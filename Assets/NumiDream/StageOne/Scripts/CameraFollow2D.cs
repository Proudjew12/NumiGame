using UnityEngine;

namespace NumiDream.StageOne
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private Transform target;
        [Space(4)]
        [InspectorName("Auto Find Player")]
        [SerializeField] private bool autoFindPlayerTarget = true;
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Snap On Start")]
        [SerializeField] private bool snapToTargetOnStart = true;

        [Space(10)]
        [Header("--------- Follow ---------")]
        [Header("+Motion+")]
        [Space(4)]
        [SerializeField] private Vector2 offset = new Vector2(0f, 1.05f);
        [Space(4)]
        [InspectorName("Z Position")]
        [SerializeField] private float zPosition = -20f;
        [Space(4)]
        [InspectorName("Smooth Time")]
        [SerializeField] private float smoothTime = 0.28f;

        [Space(10)]
        [Header("--------- Bounds ---------")]
        [Header("+One-Sided+")]
        [Space(4)]
        [InspectorName("Clamp Left X")]
        [SerializeField] private bool clampLeftX;
        [Space(4)]
        [InspectorName("Left X Limit")]
        [SerializeField] private float leftXLimit;
        [Space(4)]
        [InspectorName("Clamp Bottom Y")]
        [SerializeField] private bool clampBottomY;
        [Space(4)]
        [InspectorName("Bottom Y Limit")]
        [SerializeField] private float bottomYLimit;
        [Space(4)]
        [InspectorName("Clamp Top Y")]
        [SerializeField] private bool clampTopY;
        [Space(4)]
        [InspectorName("Top Y Limit")]
        [SerializeField] private float topYLimit;
        [Header("+Full Bounds+")]
        [Space(4)]
        [InspectorName("Use Bounds")]
        [SerializeField] private bool useBounds;
        [Space(4)]
        [InspectorName("Min")]
        [SerializeField] private Vector2 minBounds = new Vector2(-999f, -999f);
        [Space(4)]
        [InspectorName("Max")]
        [SerializeField] private Vector2 maxBounds = new Vector2(999f, 999f);

        private Vector3 _velocity;
        private Vector2 _runtimeOffset;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public Vector2 Offset => offset;
        public Vector2 RuntimeOffset => _runtimeOffset;

        public void SetRuntimeOffset(Vector2 value)
        {
            _runtimeOffset = value;
        }

        public void ClearRuntimeOffset()
        {
            _runtimeOffset = Vector2.zero;
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = GetWantedPosition();
            _velocity = Vector3.zero;
        }

        private void Awake()
        {
            ResolvePlayerTarget();
        }

        private void Start()
        {
            ResolvePlayerTarget();

            if (snapToTargetOnStart)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            ResolvePlayerTarget();

            if (target == null)
            {
                return;
            }

            var wanted = GetWantedPosition();

            var next = smoothTime <= 0f
                ? wanted
                : Vector3.SmoothDamp(transform.position, wanted, ref _velocity, smoothTime);

            transform.position = next;
        }

        private Vector3 GetWantedPosition()
        {
            var totalOffset = offset + _runtimeOffset;
            var wanted = new Vector3(target.position.x + totalOffset.x, target.position.y + totalOffset.y, zPosition);

            if (useBounds)
            {
                wanted.x = Mathf.Clamp(wanted.x, minBounds.x, maxBounds.x);
                wanted.y = Mathf.Clamp(wanted.y, minBounds.y, maxBounds.y);
                return wanted;
            }

            if (clampLeftX && wanted.x < leftXLimit)
            {
                wanted.x = leftXLimit;
            }

            if (clampBottomY && wanted.y < bottomYLimit)
            {
                wanted.y = bottomYLimit;
            }

            if (clampTopY && wanted.y > topYLimit)
            {
                wanted.y = topYLimit;
            }

            return wanted;
        }

        private void ResolvePlayerTarget()
        {
            if (!autoFindPlayerTarget)
            {
                return;
            }

            if (target != null && target.CompareTag(playerTag))
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                target = player.transform;
            }
        }
    }
}
