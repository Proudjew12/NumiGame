using UnityEngine;
using NumiDream.Input;

namespace NumiDream.Nomi
{
    [SelectionBase]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class NomiPlayerMovement : MonoBehaviour
    {
        [Header("--------- Movement ---------")]
        [Space(4)]
        [InspectorName("Speed")]
        [SerializeField] private float moveSpeed = 4.5f;
        [Space(4)]
        [InspectorName("Air Control")]
        [SerializeField] private bool allowAirControl = true;
        [Space(4)]
        [InspectorName("Lock Z")]
        [SerializeField] private bool lockZPosition = true;

        [Space(10)]
        [Header("--------- Jump ---------")]
        [Header("+Jump Timing+")]
        [Space(4)]
        [InspectorName("Takeoff Delay")]
        [SerializeField] private float jumpTakeoffDelay = 1f;
        [Space(4)]
        [InspectorName("Air Freeze Delay")]
        [SerializeField] private float jumpAirFreezeDelay;
        [Space(4)]
        [InspectorName("Cooldown")]
        [SerializeField] private float jumpCooldown = 0.15f;
        [Space(4)]
        [InspectorName("Input Buffer")]
        [SerializeField] private float jumpInputBufferTime = 0.18f;

        [Header("+Jump Power+")]
        [Space(4)]
        [InspectorName("Velocity")]
        [SerializeField] private float jumpVelocity = 8.5f;
        [Space(4)]
        [InspectorName("Forward Speed")]
        [SerializeField] private float jumpForwardVelocity = 5.2f;
        [Space(4)]
        [InspectorName("Forward Time")]
        [SerializeField] private float jumpForwardBoostDuration = 0.35f;

        [Header("+Jump Feel+")]
        [Space(4)]
        [InspectorName("Takeoff Lock")]
        [SerializeField] private bool lockMovementDuringTakeoff = true;
        [Space(4)]
        [InspectorName("Pause In Air")]
        [SerializeField] private bool pauseJumpAnimationInAir;
        [Space(4)]
        [InspectorName("Rise Gravity")]
        [SerializeField] private float riseGravityMultiplier = 0.9f;
        [Space(4)]
        [InspectorName("Fall Gravity")]
        [SerializeField] private float fallGravityMultiplier = 1.8f;
        [Space(4)]
        [InspectorName("Short Hop Gravity")]
        [SerializeField] private float shortHopGravityMultiplier = 2.6f;
        [Space(4)]
        [InspectorName("Max Fall Speed")]
        [SerializeField] private float maxFallSpeed = 18f;

        [Space(10)]
        [Header("--------- Landing ---------")]
        [Header("+Landing Feel+")]
        [Space(4)]
        [InspectorName("Landing Lock")]
        [SerializeField] private bool lockMovementDuringLanding = true;
        [Space(4)]
        [InspectorName("Lock Time")]
        [SerializeField] private float landingMovementLockDuration = 0.6f;

        [Space(10)]
        [Header("--------- Ground Check ---------")]
        [Space(4)]
        [InspectorName("Check Transform")]
        [SerializeField] private Transform groundCheck;
        [Space(4)]
        [InspectorName("Check Radius")]
        [SerializeField] private float groundCheckRadius = 0.18f;
        [Space(4)]
        [InspectorName("Layers")]
        [SerializeField] private LayerMask groundLayers = 1;
        [Space(4)]
        [InspectorName("Contact Fallback")]
        [SerializeField] private bool useContactGroundFallback = true;
        [Space(4)]
        [InspectorName("Min Normal Y")]
        [SerializeField] private float minimumGroundNormalY = 0.45f;
        [Space(4)]
        [InspectorName("Grace Time")]
        [SerializeField] private float groundedGraceTime = 0.08f;
        [Space(4)]
        [InspectorName("Probe Distance")]
        [SerializeField] private float groundProbeDistance = 0.12f;

        [Space(10)]
        [Header("--------- References ---------")]
        [Space(4)]
        [SerializeField] private Rigidbody2D body;
        [Space(4)]
        [SerializeField] private NomiAnimatorDriver animatorDriver;
        [Space(4)]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private float _moveInput;
        private bool _jumpQueued;
        private bool _jumpTakeoffQueued;
        private bool _jumpAnimationActive;
        private bool _jumpAirFrozen;
        private bool _isGrounded;
        private bool _wasGrounded;
        private float _lockedZ;
        private float _jumpTakeoffTime;
        private float _jumpAirFreezeTime;
        private float _nextJumpTime;
        private float _lastJumpPressedTime = float.NegativeInfinity;
        private float _lastMoveDirection = 1f;
        private float _jumpForwardDirection;
        private float _jumpForwardBoostEndTime;
        private float _landingMovementLockEndTime;
        private float _lastGroundedTime = float.NegativeInfinity;
        private float _defaultGravityScale = 1f;
        private bool _jumpHeld;
        private bool _jumpReleased;
        private bool _externalMovementLocked;
        private Collider2D[] _bodyColliders;
        private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[16];
        private readonly RaycastHit2D[] _groundProbeHits = new RaycastHit2D[12];

        private void Reset()
        {
            FindReferences();
        }

        private void Awake()
        {
            FindReferences();
            _lockedZ = transform.position.z;
            _defaultGravityScale = body != null ? body.gravityScale : 1f;
            _lastMoveDirection = spriteRenderer != null && !spriteRenderer.flipX ? -1f : 1f;
        }

        private void Update()
        {
            if (_externalMovementLocked)
            {
                ClearInputState();
                return;
            }

            ReadMovementInput();

            if (_moveInput < -0.01f)
            {
                _lastMoveDirection = -1f;
                SetFacingLeft(false);
            }
            else if (_moveInput > 0.01f)
            {
                _lastMoveDirection = 1f;
                SetFacingLeft(true);
            }
        }

        private void FixedUpdate()
        {
            UpdateGrounded();

            if (_externalMovementLocked)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                animatorDriver?.SetSpeed(0f);
                animatorDriver?.SetGrounded(_isGrounded);
                animatorDriver?.SetVerticalVelocity(0f);
                ClearInputState();
                _wasGrounded = _isGrounded;
                return;
            }

            Vector2 velocity = body.linearVelocity;
            var takeoffMovementLocked = _jumpTakeoffQueued && lockMovementDuringTakeoff;
            var landingMovementLocked = IsLandingMovementLocked();
            var movementLocked = takeoffMovementLocked || landingMovementLocked;

            if (_isGrounded || allowAirControl)
            {
                velocity.x = movementLocked ? 0f : _moveInput * moveSpeed;
            }

            if (!_externalMovementLocked && HasBufferedJumpInput() && _isGrounded && !_jumpTakeoffQueued && !landingMovementLocked && Time.time >= _nextJumpTime)
            {
                _jumpTakeoffQueued = true;
                _jumpAnimationActive = true;
                _jumpAirFrozen = false;
                _nextJumpTime = Time.time + jumpCooldown;
                ConsumeBufferedJumpInput();
                _jumpTakeoffTime = Time.time + jumpTakeoffDelay;
                _jumpAirFreezeTime = Time.time + jumpAirFreezeDelay;
                _jumpForwardDirection = Mathf.Abs(_moveInput) > 0.01f ? Mathf.Sign(_moveInput) : 0f;
                if (lockMovementDuringTakeoff)
                {
                    velocity.x = 0f;
                }

                animatorDriver?.ResumePlayback();
                animatorDriver?.TriggerJump();
            }

            if (!_externalMovementLocked && _jumpTakeoffQueued && Time.time >= _jumpTakeoffTime)
            {
                if (_isGrounded)
                {
                    velocity.y = jumpVelocity;
                    velocity.x = _jumpForwardDirection * jumpForwardVelocity;
                    _jumpForwardBoostEndTime = Mathf.Abs(_jumpForwardDirection) > 0.01f
                        ? Time.time + jumpForwardBoostDuration
                        : 0f;
                }

                _jumpTakeoffQueued = false;
            }

            if (!_externalMovementLocked && !_isGrounded && Time.time < _jumpForwardBoostEndTime)
            {
                velocity.x = _jumpForwardDirection * jumpForwardVelocity;
            }

            if (pauseJumpAnimationInAir && jumpAirFreezeDelay > 0f && _jumpAnimationActive && !_jumpAirFrozen && !_isGrounded && Time.time >= _jumpAirFreezeTime)
            {
                _jumpAirFrozen = true;
                animatorDriver?.PausePlayback();
            }

            if (_jumpAnimationActive && !_wasGrounded && _isGrounded)
            {
                _jumpAnimationActive = false;
                _jumpAirFrozen = false;
                _landingMovementLockEndTime = Time.time + landingMovementLockDuration;
                if (lockMovementDuringLanding)
                {
                    velocity.x = 0f;
                }

                animatorDriver?.ResumePlayback();
                animatorDriver?.TriggerLand();
            }

            ApplyJumpGravity(ref velocity);

            body.linearVelocity = velocity;
            animatorDriver?.SetGrounded(_isGrounded);
            animatorDriver?.SetVerticalVelocity(body.linearVelocity.y);
            _jumpQueued = false;
            _jumpReleased = false;
            _wasGrounded = _isGrounded;
        }

        private void LateUpdate()
        {
            if (!lockZPosition)
            {
                return;
            }

            Vector3 position = transform.position;
            if (!Mathf.Approximately(position.z, _lockedZ))
            {
                position.z = _lockedZ;
                transform.position = position;
            }
        }

        public void ResetAfterRespawn()
        {
            FindReferences();

            _externalMovementLocked = false;
            _moveInput = 0f;
            _jumpQueued = false;
            _jumpTakeoffQueued = false;
            _jumpAnimationActive = false;
            _jumpAirFrozen = false;
            _jumpTakeoffTime = 0f;
            _jumpAirFreezeTime = 0f;
            _lastJumpPressedTime = float.NegativeInfinity;
            _jumpForwardBoostEndTime = 0f;
            _landingMovementLockEndTime = 0f;
            _jumpHeld = false;
            _jumpReleased = false;
            _isGrounded = true;
            _wasGrounded = true;
            _lastGroundedTime = Time.time;

            if (body != null)
            {
                body.gravityScale = _defaultGravityScale;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            animatorDriver?.ResumePlayback();
            animatorDriver?.SetSpeed(0f);
            animatorDriver?.SetGrounded(true);
            animatorDriver?.SetVerticalVelocity(0f);
        }

        public void SetExternalMovementLock(bool locked)
        {
            _externalMovementLocked = locked;

            if (!locked)
            {
                return;
            }

            ClearInputState();
            _jumpTakeoffQueued = false;
            _jumpAnimationActive = false;
            _jumpAirFrozen = false;
            _lastJumpPressedTime = float.NegativeInfinity;
            _jumpForwardBoostEndTime = 0f;
            _landingMovementLockEndTime = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            animatorDriver?.ResumePlayback();
            animatorDriver?.SetSpeed(0f);
            animatorDriver?.SetVerticalVelocity(0f);
        }

        private void ReadMovementInput()
        {
            float horizontal = NumiInput.ReadHorizontal();
            bool jumpPressed = NumiInput.WasJumpPressed();
            bool jumpHeld = NumiInput.IsJumpHeld();
            bool jumpReleased = !jumpHeld && NumiInput.WasJumpReleased();

            _moveInput = Mathf.Clamp(horizontal, -1f, 1f);
            _jumpHeld = jumpHeld;

            if (jumpPressed)
            {
                _jumpQueued = true;
                _lastJumpPressedTime = Time.time;
            }

            if (jumpReleased)
            {
                _jumpReleased = true;
            }
        }

        private void ClearInputState()
        {
            _moveInput = 0f;
            _jumpQueued = false;
            _lastJumpPressedTime = float.NegativeInfinity;
            _jumpHeld = false;
            _jumpReleased = false;
        }

        private bool HasBufferedJumpInput()
        {
            return _jumpQueued || Time.time - _lastJumpPressedTime <= jumpInputBufferTime;
        }

        private void ConsumeBufferedJumpInput()
        {
            _jumpQueued = false;
            _lastJumpPressedTime = float.NegativeInfinity;
        }

        private void ApplyJumpGravity(ref Vector2 velocity)
        {
            if (body == null)
            {
                return;
            }

            if (_isGrounded || _jumpTakeoffQueued)
            {
                body.gravityScale = _defaultGravityScale;
                return;
            }

            var gravityMultiplier = riseGravityMultiplier;
            if (velocity.y < -0.01f)
            {
                gravityMultiplier = fallGravityMultiplier;
            }
            else if (velocity.y > 0.01f && (!_jumpHeld || _jumpReleased))
            {
                gravityMultiplier = shortHopGravityMultiplier;
            }

            body.gravityScale = _defaultGravityScale * Mathf.Max(0.01f, gravityMultiplier);

            if (maxFallSpeed > 0f && velocity.y < -maxFallSpeed)
            {
                velocity.y = -maxFallSpeed;
            }
        }

        private bool IsLandingMovementLocked()
        {
            return lockMovementDuringLanding && Time.time < _landingMovementLockEndTime;
        }

        private void UpdateGrounded()
        {
            var grounded = false;

            if (groundCheck != null)
            {
                grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayers) != null;
            }

            if (!grounded && useContactGroundFallback)
            {
                grounded = HasGroundContact();
            }

            if (!grounded && groundProbeDistance > 0f)
            {
                grounded = HasGroundProbeHit();
            }

            if (grounded)
            {
                _lastGroundedTime = Time.time;
            }

            _isGrounded = grounded || Time.time - _lastGroundedTime <= groundedGraceTime;
        }

        private bool HasGroundContact()
        {
            if (_bodyColliders == null || _bodyColliders.Length == 0)
            {
                _bodyColliders = GetComponentsInChildren<Collider2D>();
            }

            for (var colliderIndex = 0; colliderIndex < _bodyColliders.Length; colliderIndex++)
            {
                var bodyCollider = _bodyColliders[colliderIndex];
                if (bodyCollider == null || !bodyCollider.enabled || bodyCollider.isTrigger)
                {
                    continue;
                }

                var contactCount = bodyCollider.GetContacts(_groundContacts);
                for (var contactIndex = 0; contactIndex < contactCount; contactIndex++)
                {
                    var contact = _groundContacts[contactIndex];
                    if (contact.collider == null || contact.collider.isTrigger)
                    {
                        continue;
                    }

                    if (contact.collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    var contactIsBelowPlayer = contact.point.y <= bodyCollider.bounds.center.y;
                    var upwardContact = contact.normal.y >= minimumGroundNormalY;
                    var downwardContactBelowPlayer = contactIsBelowPlayer && -contact.normal.y >= minimumGroundNormalY;

                    if (upwardContact || downwardContactBelowPlayer)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasGroundProbeHit()
        {
            if (_bodyColliders == null || _bodyColliders.Length == 0)
            {
                _bodyColliders = GetComponentsInChildren<Collider2D>();
            }

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundLayers,
                useTriggers = false
            };

            for (var colliderIndex = 0; colliderIndex < _bodyColliders.Length; colliderIndex++)
            {
                var bodyCollider = _bodyColliders[colliderIndex];
                if (bodyCollider == null || !bodyCollider.enabled || bodyCollider.isTrigger)
                {
                    continue;
                }

                var hitCount = bodyCollider.Cast(Vector2.down, filter, _groundProbeHits, groundProbeDistance);
                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    var hit = _groundProbeHits[hitIndex];
                    if (hit.collider == null || hit.collider.isTrigger)
                    {
                        continue;
                    }

                    if (hit.collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (hit.normal.y >= minimumGroundNormalY)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void SetFacingLeft(bool facingLeft)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingLeft;
            }
        }

        private void FindReferences()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (groundCheck == null)
            {
                Transform foundGroundCheck = transform.Find("GroundCheck");
                if (foundGroundCheck != null)
                {
                    groundCheck = foundGroundCheck;
                }
            }

            if (animatorDriver == null)
            {
                animatorDriver = GetComponentInChildren<NomiAnimatorDriver>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            _bodyColliders = GetComponentsInChildren<Collider2D>();
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        private void OnValidate()
        {
            jumpInputBufferTime = Mathf.Max(0f, jumpInputBufferTime);
            groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
            groundedGraceTime = Mathf.Max(0f, groundedGraceTime);
            jumpCooldown = Mathf.Max(0f, jumpCooldown);
        }
    }
}
