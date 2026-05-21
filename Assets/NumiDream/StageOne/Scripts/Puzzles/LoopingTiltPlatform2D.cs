using UnityEngine;

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class LoopingTiltPlatform2D : MonoBehaviour
    {
        [Header("--------- Rotation ---------")]
        [Header("+Loop+")]
        [Space(4)]
        [InspectorName("Left Angle")]
        [SerializeField] private float leftAngle = -45f;
        [Space(4)]
        [InspectorName("Right Angle")]
        [SerializeField] private float rightAngle = 45f;
        [Space(4)]
        [InspectorName("Cycle Time")]
        [SerializeField] private float cycleDuration = 2.5f;
        [Space(4)]
        [InspectorName("Phase Offset")]
        [SerializeField] private float phaseOffset;
        [Space(4)]
        [InspectorName("Use Start Angle")]
        [SerializeField] private bool useLocalStartAngle = true;

        [Space(10)]
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private Rigidbody2D body;

        private float _baseAngle;

        private void Reset()
        {
            FindReferences();
            ConfigureBody();
        }

        private void Awake()
        {
            FindReferences();
            ConfigureBody();
            _baseAngle = useLocalStartAngle ? transform.eulerAngles.z : 0f;
        }

        private void FixedUpdate()
        {
            var duration = Mathf.Max(0.1f, cycleDuration);
            var centerAngle = (leftAngle + rightAngle) * 0.5f;
            var amplitude = Mathf.Abs(rightAngle - leftAngle) * 0.5f;
            var phase = ((Time.time / duration) + phaseOffset) * Mathf.PI * 2f;
            var targetAngle = _baseAngle + centerAngle + Mathf.Sin(phase) * amplitude;

            body.MoveRotation(targetAngle);
        }

        private void FindReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                return;
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }
}
