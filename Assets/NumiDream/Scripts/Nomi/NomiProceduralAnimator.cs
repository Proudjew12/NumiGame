using UnityEngine;

namespace NumiDream.Nomi
{
    [DisallowMultipleComponent]
    public sealed class NomiProceduralAnimator : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private Rigidbody2D body;
        [Space(4)]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Space(4)]
        [SerializeField] private Transform groundCheck;
        [Space(4)]
        [SerializeField] private LayerMask groundLayers = 1;
        [Space(4)]
        [InspectorName("Check Radius")]
        [SerializeField] private float groundCheckRadius = 0.18f;

        [Space(10)]
        [Header("--------- Jump ---------")]
        [Header("+Pose Sprites+")]
        [Space(4)]
        [InspectorName("Use Pose Sprites")]
        [SerializeField] private bool useJumpPoseSprites = true;
        [Space(4)]
        [InspectorName("Takeoff Sprite")]
        [SerializeField] private Sprite takeoffSprite;
        [Space(4)]
        [InspectorName("Rise Sprite")]
        [SerializeField] private Sprite riseSprite;
        [Space(4)]
        [InspectorName("Apex Sprite")]
        [SerializeField] private Sprite apexSprite;
        [Space(4)]
        [InspectorName("Fall Sprite")]
        [SerializeField] private Sprite fallSprite;
        [Space(4)]
        [InspectorName("Land Sprite")]
        [SerializeField] private Sprite landSprite;
        [Space(4)]
        [InspectorName("Rise Threshold")]
        [SerializeField] private float riseVelocityThreshold = 1.2f;
        [Space(4)]
        [InspectorName("Fall Threshold")]
        [SerializeField] private float fallVelocityThreshold = -1.2f;
        [Space(4)]
        [InspectorName("Takeoff Time")]
        [SerializeField] private float takeoffPoseDuration = 0.08f;
        [Space(4)]
        [InspectorName("Landing Time")]
        [SerializeField] private float landingPoseDuration = 0.12f;

        [Space(10)]
        [Header("--------- Procedural Motion ---------")]
        [Header("+Motion+")]
        [Space(4)]
        [InspectorName("Use Motion")]
        [SerializeField] private bool useProceduralMotion = true;
        [Space(4)]
        [InspectorName("Takeoff Scale")]
        [SerializeField] private Vector2 takeoffScale = new Vector2(1.08f, 0.92f);
        [Space(4)]
        [InspectorName("Rise Scale")]
        [SerializeField] private Vector2 riseScale = new Vector2(0.94f, 1.08f);
        [Space(4)]
        [InspectorName("Apex Scale")]
        [SerializeField] private Vector2 apexScale = new Vector2(1f, 1.02f);
        [Space(4)]
        [InspectorName("Fall Scale")]
        [SerializeField] private Vector2 fallScale = new Vector2(1.03f, 0.98f);
        [Space(4)]
        [InspectorName("Land Scale")]
        [SerializeField] private Vector2 landScale = Vector2.one;
        [Space(4)]
        [InspectorName("Max Tilt")]
        [SerializeField] private float maxTiltDegrees = 6f;
        [Space(4)]
        [InspectorName("Tilt Velocity")]
        [SerializeField] private float velocityForMaxTilt = 7f;
        [Space(4)]
        [InspectorName("Air Lift")]
        [SerializeField] private float airLiftOffset = 0.04f;
        [Space(4)]
        [InspectorName("Scale Sharpness")]
        [SerializeField] private float scaleSharpness = 18f;
        [Space(4)]
        [InspectorName("Rotation Sharpness")]
        [SerializeField] private float rotationSharpness = 16f;

        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _isGrounded;
        private bool _wasGrounded;
        private float _takeoffPoseTime;
        private float _landingPoseTime;

        private void Reset()
        {
            FindReferences();
        }

        private void Awake()
        {
            FindReferences();
            _baseLocalPosition = transform.localPosition;
            _baseLocalScale = transform.localScale;
        }

        private void LateUpdate()
        {
            if (body == null)
            {
                return;
            }

            UpdateGrounded();
            UpdatePoseTimers();

            if (useJumpPoseSprites && spriteRenderer != null)
            {
                ApplyJumpPoseSprite();
            }

            if (useProceduralMotion)
            {
                ApplyProceduralMotion();
            }

            _wasGrounded = _isGrounded;
        }

        private void OnDisable()
        {
            transform.localPosition = _baseLocalPosition;
            transform.localScale = _baseLocalScale;
            transform.localRotation = Quaternion.identity;
        }

        public void ResetVisualState()
        {
            _isGrounded = true;
            _wasGrounded = true;
            _takeoffPoseTime = 0f;
            _landingPoseTime = 0f;

            transform.localPosition = _baseLocalPosition;
            transform.localScale = _baseLocalScale;
            transform.localRotation = Quaternion.identity;
        }

        private void UpdateGrounded()
        {
            _isGrounded = false;
            if (groundCheck == null)
            {
                return;
            }

            var hits = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayers);
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                if (body != null && hit.transform.IsChildOf(body.transform))
                {
                    continue;
                }

                _isGrounded = true;
                return;
            }
        }

        private void UpdatePoseTimers()
        {
            if (_wasGrounded && !_isGrounded)
            {
                _takeoffPoseTime = takeoffPoseDuration;
            }

            if (!_wasGrounded && _isGrounded)
            {
                _landingPoseTime = landingPoseDuration;
            }

            _takeoffPoseTime = Mathf.Max(0f, _takeoffPoseTime - Time.deltaTime);
            _landingPoseTime = Mathf.Max(0f, _landingPoseTime - Time.deltaTime);
        }

        private void ApplyJumpPoseSprite()
        {
            var sprite = ChooseJumpPoseSprite();
            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }
        }

        private Sprite ChooseJumpPoseSprite()
        {
            var velocityY = body.linearVelocity.y;

            if (_landingPoseTime > 0f)
            {
                return landSprite;
            }

            if (_isGrounded)
            {
                return velocityY > riseVelocityThreshold ? takeoffSprite : null;
            }

            if (_takeoffPoseTime > 0f)
            {
                return takeoffSprite;
            }

            if (velocityY > riseVelocityThreshold)
            {
                return riseSprite;
            }

            if (velocityY < fallVelocityThreshold)
            {
                return fallSprite;
            }

            return apexSprite;
        }

        private void ApplyProceduralMotion()
        {
            var targetScale = GetTargetScale();
            var targetTilt = GetTargetTilt();
            var targetOffset = GetTargetOffset();
            var scaleT = 1f - Mathf.Exp(-scaleSharpness * Time.deltaTime);
            var rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);

            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, scaleT);
            transform.localRotation = Quaternion.Lerp(
                transform.localRotation,
                Quaternion.Euler(0f, 0f, targetTilt),
                rotationT);
            transform.localPosition = Vector3.Lerp(transform.localPosition, _baseLocalPosition + targetOffset, scaleT);
        }

        private Vector3 GetTargetScale()
        {
            var multiplier = Vector2.one;
            var velocityY = body.linearVelocity.y;

            if (_landingPoseTime > 0f)
            {
                multiplier = landScale;
            }
            else if (_isGrounded)
            {
                multiplier = velocityY > riseVelocityThreshold ? takeoffScale : Vector2.one;
            }
            else if (_takeoffPoseTime > 0f)
            {
                multiplier = takeoffScale;
            }
            else if (velocityY > riseVelocityThreshold)
            {
                multiplier = riseScale;
            }
            else if (velocityY < fallVelocityThreshold)
            {
                multiplier = fallScale;
            }
            else
            {
                multiplier = apexScale;
            }

            return new Vector3(
                _baseLocalScale.x * multiplier.x,
                _baseLocalScale.y * multiplier.y,
                _baseLocalScale.z);
        }

        private float GetTargetTilt()
        {
            if (_isGrounded && _landingPoseTime <= 0f)
            {
                return 0f;
            }

            var horizontal = Mathf.Clamp(body.linearVelocity.x / Mathf.Max(0.01f, velocityForMaxTilt), -1f, 1f);
            return -horizontal * maxTiltDegrees;
        }

        private Vector3 GetTargetOffset()
        {
            if (_isGrounded)
            {
                return Vector3.zero;
            }

            return new Vector3(0f, airLiftOffset, 0f);
        }

        private void FindReferences()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (body == null)
            {
                body = GetComponentInParent<Rigidbody2D>();
            }

            if (groundCheck == null && body != null)
            {
                var foundGroundCheck = body.transform.Find("GroundCheck");
                if (foundGroundCheck != null)
                {
                    groundCheck = foundGroundCheck;
                }
            }
        }
    }
}
