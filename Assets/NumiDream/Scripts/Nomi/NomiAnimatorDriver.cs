using UnityEngine;

namespace NumiDream.Nomi
{
    public sealed class NomiAnimatorDriver : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private Animator animator;
        [Space(4)]
        [SerializeField] private Rigidbody2D body;

        [Space(10)]
        [Header("--------- Speed ---------")]
        [Header("+Runtime+")]
        [Space(4)]
        [InspectorName("Use Body Speed")]
        [SerializeField] private bool setSpeedFromRigidbody = true;
        [Space(4)]
        [InspectorName("Speed Scale")]
        [SerializeField] private float velocityScale = 1f;

        [Space(10)]
        [Header("--------- Animator ---------")]
        [Header("+Parameters+")]
        [Space(4)]
        [InspectorName("Speed")]
        [SerializeField] private string speedParameter = "Speed";
        [Space(4)]
        [InspectorName("Jump")]
        [SerializeField] private string jumpParameter = "Jump";
        [Space(4)]
        [InspectorName("Land")]
        [SerializeField] private string landParameter = "Land";
        [Space(4)]
        [InspectorName("Grounded")]
        [SerializeField] private string groundedParameter = "Grounded";
        [Space(4)]
        [InspectorName("Vertical Velocity")]
        [SerializeField] private string verticalVelocityParameter = "VerticalVelocity";

        [Header("+State Names+")]
        [Space(4)]
        [InspectorName("Idle")]
        [SerializeField] private string idleStateName = "Idle";
        [Space(4)]
        [InspectorName("Jump")]
        [SerializeField] private string jumpStateName = "Jump";
        [Space(4)]
        [InspectorName("Land")]
        [SerializeField] private string landStateName = "Land";

        private int _speedHash;
        private int _jumpHash;
        private int _landHash;
        private int _groundedHash;
        private int _verticalVelocityHash;

        private void Awake()
        {
            FindReferences();
            CacheParameterHashes();
        }

        private void Update()
        {
            if (animator == null)
            {
                FindReferences();
            }

            if (body == null)
            {
                FindReferences();
            }

            if (!setSpeedFromRigidbody || animator == null || body == null)
            {
                return;
            }

            animator.SetFloat(_speedHash, Mathf.Abs(body.linearVelocity.x) * velocityScale);
        }

        private void CacheParameterHashes()
        {
            _speedHash = Animator.StringToHash(speedParameter);
            _jumpHash = Animator.StringToHash(jumpParameter);
            _landHash = Animator.StringToHash(landParameter);
            _groundedHash = Animator.StringToHash(groundedParameter);
            _verticalVelocityHash = Animator.StringToHash(verticalVelocityParameter);
        }

        private void FindReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body == null)
            {
                body = GetComponentInParent<Rigidbody2D>();
            }
        }

        public void SetSpeed(float speed)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(_speedHash, Mathf.Abs(speed));
        }

        public void TriggerJump()
        {
            if (animator == null)
            {
                return;
            }

            animator.speed = 1f;
            animator.ResetTrigger(_landHash);
            animator.ResetTrigger(_jumpHash);

            if (!string.IsNullOrWhiteSpace(jumpStateName))
            {
                animator.Play(jumpStateName, 0, 0f);
                animator.Update(0f);
                return;
            }

            animator.SetTrigger(_jumpHash);
        }

        public void TriggerLand()
        {
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(_jumpHash);
            animator.ResetTrigger(_landHash);

            if (!string.IsNullOrWhiteSpace(landStateName))
            {
                animator.Play(landStateName, 0, 0f);
                animator.Update(0f);
                return;
            }

            animator.SetTrigger(_landHash);
        }

        public void SetGrounded(bool grounded)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetBool(_groundedHash, grounded);
        }

        public void SetVerticalVelocity(float velocity)
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(_verticalVelocityHash, velocity);
        }

        public void PausePlayback()
        {
            if (animator == null)
            {
                return;
            }

            animator.speed = 0f;
        }

        public void ResumePlayback()
        {
            if (animator == null)
            {
                return;
            }

            animator.speed = 1f;
        }

        public void ResetAfterRespawn()
        {
            FindReferences();
            CacheParameterHashes();

            if (animator == null)
            {
                return;
            }

            animator.speed = 1f;
            animator.ResetTrigger(_jumpHash);
            animator.ResetTrigger(_landHash);
            animator.SetFloat(_speedHash, 0f);
            animator.SetBool(_groundedHash, true);
            animator.SetFloat(_verticalVelocityHash, 0f);

            if (!string.IsNullOrWhiteSpace(idleStateName))
            {
                animator.Play(idleStateName, 0, 0f);
            }

            animator.Update(0f);
        }
    }
}
