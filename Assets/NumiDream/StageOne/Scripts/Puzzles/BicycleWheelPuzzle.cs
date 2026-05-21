using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BicycleWheelPuzzle : MonoBehaviour
    {
        private enum WheelState
        {
            WaitingForPlayer,
            Moving,
            AutoFalling,
            RollingBack,
            LockedAtFinal
        }

        [Header("--------- Player ---------")]
        [Header("+Activation+")]
        [Space(4)]
        [SerializeField] private Transform player;
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Require Range")]
        [SerializeField] private bool requirePlayerInRange = true;
        [Space(4)]
        [InspectorName("Distance")]
        [SerializeField] private float activationDistance = 16f;
        [Space(4)]
        [InspectorName("Side Dead Zone")]
        [SerializeField] private float playerSideDeadZone = 0.25f;

        [Space(10)]
        [Header("--------- Wheel Path ---------")]
        [Header("+Waypoints+")]
        [Space(4)]
        [InspectorName("Waypoints")]
        [SerializeField] private Transform[] pathWaypoints = Array.Empty<Transform>();
        [Space(4)]
        [InspectorName("Fallback Destination")]
        [SerializeField] private Transform destinationPoint;

        [Header("+Movement+")]
        [Space(4)]
        [InspectorName("Move Speed")]
        [SerializeField] private float moveSpeed = 3.55f;
        [Space(4)]
        [InspectorName("Move Accel")]
        [SerializeField] private float moveAcceleration = 15f;
        [Space(4)]
        [InspectorName("Move Decel")]
        [SerializeField] private float moveDeceleration = 16f;
        [Space(4)]
        [InspectorName("Lock At Final")]
        [SerializeField] private bool lockAtFinalWaypoint = true;
        [Space(4)]
        [InspectorName("No Rollback After WP")]
        [SerializeField] private int noRollbackAfterWaypoint = 3;

        [Header("+Edge Rollback+")]
        [Space(4)]
        [InspectorName("Rollback When Released")]
        [SerializeField] private bool rollbackWhenInputReleased = true;
        [Space(4)]
        [InspectorName("Rollback Delay")]
        [SerializeField] private float rollbackStartDelay = 0.2f;
        [Space(4)]
        [InspectorName("Rollback Speed")]
        [SerializeField] private float rollbackSpeed = 3f;
        [Space(4)]
        [InspectorName("Safe Middle WPs")]
        [SerializeField] private bool intermediateWaypointsAreSafeStops;

        [Header("+Natural Fall+")]
        [Space(4)]
        [InspectorName("Arc From WP")]
        [SerializeField] private int arcFromWaypoint = 3;
        [Space(4)]
        [InspectorName("Arc Height")]
        [SerializeField] private float arcHeight = 2.15f;
        [Space(4)]
        [InspectorName("Second Arc Height")]
        [SerializeField] private float secondArcHeight = 1.25f;
        [Space(4)]
        [InspectorName("Auto Fall From WP")]
        [SerializeField] private int autoFallFromWaypoint = 3;
        [Space(4)]
        [InspectorName("Auto Fall To WP")]
        [SerializeField] private int autoFallToWaypoint = 4;
        [Space(4)]
        [InspectorName("Second Fall From WP")]
        [SerializeField] private int secondAutoFallFromWaypoint = 5;
        [Space(4)]
        [InspectorName("Second Fall To WP")]
        [SerializeField] private int secondAutoFallToWaypoint = 6;
        [Space(4)]
        [InspectorName("Auto Fall Start")]
        [SerializeField] private float autoFallStartProgress = 0.08f;
        [Space(4)]
        [InspectorName("Fall Start Speed")]
        [SerializeField] private float autoFallStartSpeedMultiplier = 1.45f;
        [Space(4)]
        [InspectorName("Fall Max Speed")]
        [SerializeField] private float autoFallMaxSpeedMultiplier = 2.8f;
        [Space(4)]
        [InspectorName("Fall Accel")]
        [SerializeField] private float autoFallAcceleration = 18f;
        [Space(4)]
        [InspectorName("Fall Curve")]
        [SerializeField] private float autoFallCurvePower = 2.25f;
        [Space(4)]
        [InspectorName("Fall X Ease")]
        [SerializeField] private float autoFallHorizontalEase = 0.35f;

        [Header("+Spin+")]
        [Space(4)]
        [InspectorName("Physical Spin")]
        [SerializeField] private bool usePhysicalSpinFromRadius = true;
        [Space(4)]
        [InspectorName("Spin Per Unit")]
        [SerializeField] private float spinDegreesPerUnit = 11f;
        [Space(4)]
        [InspectorName("Invert Spin")]
        [SerializeField] private bool invertSpin;

        [Space(10)]
        [Header("--------- Player Carry ---------")]
        [Header("+Push+")]
        [Space(4)]
        [InspectorName("Carry On Contact")]
        [SerializeField] private bool carryPlayerOnContact = true;
        [Space(4)]
        [InspectorName("Push Speed")]
        [SerializeField] private float ridePushSpeed = 1.6f;
        [Space(4)]
        [InspectorName("Push Force")]
        [SerializeField] private float ridePushForce = 6f;
        [Space(4)]
        [InspectorName("Unstick Bad Contact")]
        [SerializeField] private bool unstickPlayerOnInvalidContact = true;
        [Space(4)]
        [InspectorName("Unstick Speed")]
        [SerializeField] private float invalidContactEscapeSpeed = 3.5f;
        [Space(4)]
        [InspectorName("Unstick Force")]
        [SerializeField] private float invalidContactEscapeForce = 16f;
        [Space(4)]
        [InspectorName("Unstick Nudge")]
        [SerializeField] private float invalidContactPositionNudge = 0.04f;

        [Space(10)]
        [Header("--------- Final Wheel Ride ---------")]
        [Header("+Player Spin+")]
        [Space(4)]
        [InspectorName("Enable Final Ride")]
        [SerializeField] private bool enableFinalRideMotion = true;
        [Space(4)]
        [InspectorName("Input Dead Zone")]
        [SerializeField] private float finalRideInputDeadZone = 0.08f;
        [Space(4)]
        [InspectorName("Player Push Speed")]
        [SerializeField] private float finalRidePlayerPushSpeed = 1.65f;
        [Space(4)]
        [InspectorName("Player Push Force")]
        [SerializeField] private float finalRidePlayerPushForce = 7.5f;
        [Space(4)]
        [InspectorName("Spin Speed")]
        [SerializeField] private float finalRideSpinDegreesPerSecond = 300f;
        [Space(4)]
        [InspectorName("Spin Accel")]
        [SerializeField] private float finalRideSpinAcceleration = 720f;
        [Space(4)]
        [InspectorName("Top Contact")]
        [SerializeField] private float finalRideTopContactRatio = 0.25f;
        [Header("+Hamster Mode+")]
        [Space(4)]
        [InspectorName("Hamster Mode")]
        [SerializeField] private bool finalRideHamsterMode = true;
        [Space(4)]
        [InspectorName("Center Strength")]
        [SerializeField] private float finalRideCenteringStrength = 80f;
        [Space(4)]
        [InspectorName("Max Center Speed")]
        [SerializeField] private float finalRideMaxCenteringSpeed = 12f;
        [Space(4)]
        [InspectorName("Release Up Speed")]
        [SerializeField] private float finalRideReleaseUpVelocity = 0.45f;
        [Space(4)]
        [InspectorName("Release Time")]
        [SerializeField] private float finalRideJumpReleaseTime = 0.55f;
        [Space(4)]
        [InspectorName("Max Drift Fix")]
        [SerializeField] private float finalRideMaxDriftCorrection = 0.095f;

        [Space(10)]
        [Header("--------- Physics ---------")]
        [Header("+Setup+")]
        [Space(4)]
        [InspectorName("Configure On Awake")]
        [SerializeField] private bool configureRigidbodyOnAwake = true;
        [Space(4)]
        [InspectorName("Trigger Before Final")]
        [SerializeField] private bool wheelIsTriggerUntilFinal = true;
        [Space(4)]
        [InspectorName("Circle Radius")]
        [SerializeField] private float wheelColliderRadius = 5.25f;

        private readonly List<Vector3> _pathLocalPositions = new List<Vector3>();
        private readonly List<float> _pathDistances = new List<float>();
        private Rigidbody2D _body;
        private WheelState _state;
        private Vector3 _startLocalPosition;
        private Quaternion _startLocalRotation;
        private float _pathLength;
        private float _pathDistance;
        private float _rotationDegrees;
        private float _idleTime;
        private float _rollbackTargetDistance = -1f;
        private float _currentPathSpeed;
        private int _lastMotionDirection;
        private int _lastSpinDirection;
        private float _finalRideSpinVelocity;
        private float _finalRideContactTimer;
        private float _finalRideReleaseTimer;
        private int _finalRideDirection;
        private bool _hasCachedStartPose;

        public bool IsLockedAtFinal => _state == WheelState.LockedAtFinal;
        public float PathProgress => _pathLength <= 0.0001f ? 0f : Mathf.Clamp01(_pathDistance / _pathLength);

        private void Reset()
        {
            SetupPhysicsDefaults();
            CacheStartPose(force: true);
            RebuildPath();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();

            if (configureRigidbodyOnAwake)
            {
                SetupPhysicsDefaults();
            }

            FindPlayerIfNeeded();
            CacheStartPose(force: true);
            RebuildPath();
            ApplyPose(usePhysicsMove: false);
            UpdateWheelColliderTriggerState();
        }

        private void FixedUpdate()
        {
            CacheStartPose(force: false);
            RebuildPath();
            UpdateWheelColliderTriggerState();

            if (_state == WheelState.LockedAtFinal || _pathLength <= 0.0001f)
            {
                _currentPathSpeed = Mathf.MoveTowards(_currentPathSpeed, 0f, moveDeceleration * Time.fixedDeltaTime);
                _lastMotionDirection = 0;
                UpdateFinalRideMotion(Time.fixedDeltaTime);
                StopBodyVelocity();
                return;
            }

            if (TryGetAutoFall(out var autoFallSpeed, out var autoFallTargetDistance))
            {
                _idleTime = 0f;
                _rollbackTargetDistance = autoFallTargetDistance;
                _state = WheelState.AutoFalling;
                _currentPathSpeed = Mathf.MoveTowards(
                    _currentPathSpeed,
                    autoFallSpeed,
                    autoFallAcceleration * Time.fixedDeltaTime);
                StepAlongPath(1, GetAutoFallSpinDirection(), _currentPathSpeed, Time.fixedDeltaTime, hasRollbackTarget: true);
                return;
            }

            var manualSpinDirection = GetManualSpinDirection();
            if (manualSpinDirection != 0)
            {
                _idleTime = 0f;
                _rollbackTargetDistance = -1f;
                _lastSpinDirection = manualSpinDirection;
                _state = WheelState.Moving;
                _currentPathSpeed = Mathf.MoveTowards(
                    _currentPathSpeed,
                    moveSpeed,
                    moveAcceleration * Time.fixedDeltaTime);
                StepAlongPath(1, manualSpinDirection, _currentPathSpeed, Time.fixedDeltaTime, hasRollbackTarget: false);
                return;
            }

            _idleTime += Time.fixedDeltaTime;

            if (ShouldRollBack())
            {
                if (_rollbackTargetDistance < -0.5f)
                {
                    _rollbackTargetDistance = GetPreviousWaypointDistance(_pathDistance);
                }

                _state = WheelState.RollingBack;
                _currentPathSpeed = Mathf.MoveTowards(
                    _currentPathSpeed,
                    rollbackSpeed,
                    moveAcceleration * Time.fixedDeltaTime);
                StepAlongPath(-1, -1, _currentPathSpeed, Time.fixedDeltaTime, hasRollbackTarget: true);
                return;
            }

            _currentPathSpeed = Mathf.MoveTowards(_currentPathSpeed, 0f, moveDeceleration * Time.fixedDeltaTime);
            _lastMotionDirection = 0;
            StopBodyVelocity();
        }

        public void ReleaseToAutoFall()
        {
            if (_state == WheelState.LockedAtFinal || _pathLength <= 0.0001f || _pathDistance <= 0.0001f)
            {
                return;
            }

            var noRollbackDistance = GetNoRollbackDistance();
            if (noRollbackDistance > 0f && _pathDistance >= noRollbackDistance - 0.001f)
            {
                return;
            }

            _idleTime = rollbackStartDelay;
            _rollbackTargetDistance = GetPreviousWaypointDistance(_pathDistance);
            _state = WheelState.RollingBack;
        }

        public bool OwnsCollider(Collider2D candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.GetComponentInParent<BicycleWheelPuzzle>() == this)
            {
                return true;
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody2D>();
            }

            return candidate.attachedRigidbody != null && candidate.attachedRigidbody == _body;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleWheelCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            HandleWheelCollision(collision);
        }

        private void HandleWheelCollision(Collision2D collision)
        {
            var playerBody = collision.rigidbody;
            if (playerBody == null || !IsPlayerCollision(collision, playerBody))
            {
                return;
            }

            if (_state == WheelState.LockedAtFinal)
            {
                RegisterFinalRideContact(collision, playerBody);
                return;
            }

            if (!carryPlayerOnContact || _lastMotionDirection == 0)
            {
                return;
            }

            if (!ShouldCarryPlayerFromContact(collision, playerBody))
            {
                ResolveInvalidPlayerContact(playerBody);
                return;
            }

            var direction = Mathf.Sign(_lastMotionDirection);
            var velocity = playerBody.linearVelocity;
            if (ridePushSpeed > 0f)
            {
                velocity.x = direction > 0f
                    ? Mathf.Max(velocity.x, ridePushSpeed)
                    : Mathf.Min(velocity.x, -ridePushSpeed);
                playerBody.linearVelocity = velocity;
            }

            if (ridePushForce > 0f)
            {
                playerBody.AddForce(Vector2.right * (direction * ridePushForce), ForceMode2D.Force);
            }
        }

        private bool ShouldCarryPlayerFromContact(Collision2D collision, Rigidbody2D playerBody)
        {
            if (playerBody == null || collision == null)
            {
                return false;
            }

            var playerCollider = GetPlayerCollider(collision, playerBody);
            if (playerCollider == null)
            {
                return false;
            }

            var wheelCenter = _body != null ? _body.position : (Vector2)transform.position;
            var playerBounds = playerCollider.bounds;
            var lowerSideContactLimit = wheelCenter.y - Mathf.Max(0.25f, wheelColliderRadius * 0.72f);

            if (playerBounds.max.y < lowerSideContactLimit)
            {
                return false;
            }

            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (IsCarryContactOnWheelSide(wheelCenter, contact.point, lowerSideContactLimit))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCarryContactOnWheelSide(Vector2 wheelCenter, Vector2 contactPoint, float lowerSideContactLimit)
        {
            var fromWheelCenter = contactPoint - wheelCenter;
            var isSideContact = Mathf.Abs(fromWheelCenter.x) >= Mathf.Abs(fromWheelCenter.y) * 0.45f;
            var isAboveUnderbelly = contactPoint.y >= lowerSideContactLimit;

            return isSideContact && isAboveUnderbelly;
        }

        private void RegisterFinalRideContact(Collision2D collision, Rigidbody2D playerBody)
        {
            if (!enableFinalRideMotion || !IsFinalRideTopContact(collision, playerBody))
            {
                return;
            }

            var moveDirection = GetFinalRideMoveDirection(playerBody);
            _finalRideDirection = moveDirection;
            _finalRideContactTimer = 0.14f;

            if (finalRideHamsterMode)
            {
                if (ShouldReleaseFinalRide(playerBody))
                {
                    _finalRideContactTimer = 0f;
                    _finalRideDirection = 0;
                    return;
                }

                ApplyFinalRideHamsterAnchor(playerBody);
            }
            else
            {
                ApplyFinalRidePlayerPush(playerBody, moveDirection);
            }
        }

        private bool IsFinalRideTopContact(Collision2D collision, Rigidbody2D playerBody)
        {
            var playerCollider = GetPlayerCollider(collision, playerBody);
            if (playerCollider == null)
            {
                return false;
            }

            var wheelCenter = _body != null ? _body.position : (Vector2)transform.position;
            if (playerCollider.bounds.center.y <= wheelCenter.y)
            {
                return false;
            }

            var topContactLimit = wheelCenter.y + Mathf.Max(0.25f, wheelColliderRadius * finalRideTopContactRatio);
            if (collision.contactCount == 0)
            {
                return playerCollider.bounds.min.y >= topContactLimit;
            }

            for (var i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).point.y >= topContactLimit)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateFinalRideMotion(float deltaTime)
        {
            if (!enableFinalRideMotion)
            {
                _finalRideSpinVelocity = 0f;
                _finalRideReleaseTimer = 0f;
                ApplyPose(usePhysicsMove: true);
                return;
            }

            _finalRideReleaseTimer = Mathf.Max(0f, _finalRideReleaseTimer - Mathf.Max(0f, deltaTime));
            _finalRideContactTimer = Mathf.Max(0f, _finalRideContactTimer - Mathf.Max(0f, deltaTime));

            var hasRideInput = _finalRideContactTimer > 0f && _finalRideDirection != 0;
            var spinSign = finalRideHamsterMode
                ? (invertSpin ? -1f : 1f)
                : (invertSpin ? 1f : -1f);
            var targetSpinVelocity = hasRideInput
                ? _finalRideDirection * spinSign * finalRideSpinDegreesPerSecond
                : 0f;
            _finalRideSpinVelocity = Mathf.MoveTowards(
                _finalRideSpinVelocity,
                targetSpinVelocity,
                finalRideSpinAcceleration * deltaTime);

            if (Mathf.Abs(_finalRideSpinVelocity) > 0.001f)
            {
                _rotationDegrees += _finalRideSpinVelocity * deltaTime;
                _rotationDegrees = Mathf.Repeat(_rotationDegrees, 360f);
            }

            ApplyPose(usePhysicsMove: true);
        }

        private int GetFinalRideMoveDirection(Rigidbody2D playerBody)
        {
            var inputDirection = GetHorizontalInputDirection();
            if (inputDirection != 0)
            {
                return inputDirection;
            }

            if (playerBody == null || Mathf.Abs(playerBody.linearVelocity.x) <= finalRideInputDeadZone)
            {
                return 0;
            }

            return playerBody.linearVelocity.x > 0f ? 1 : -1;
        }

        private void ApplyFinalRidePlayerPush(Rigidbody2D playerBody, int direction)
        {
            if (playerBody == null || direction == 0)
            {
                return;
            }

            var signedDirection = direction > 0 ? 1f : -1f;
            var velocity = playerBody.linearVelocity;

            if (finalRidePlayerPushSpeed > 0f)
            {
                velocity.x = signedDirection > 0f
                    ? Mathf.Max(velocity.x, finalRidePlayerPushSpeed)
                    : Mathf.Min(velocity.x, -finalRidePlayerPushSpeed);
                playerBody.linearVelocity = velocity;
            }

            if (finalRidePlayerPushForce > 0f)
            {
                playerBody.AddForce(Vector2.right * (signedDirection * finalRidePlayerPushForce), ForceMode2D.Force);
            }
        }

        private bool ShouldReleaseFinalRide(Rigidbody2D playerBody)
        {
            if (_finalRideReleaseTimer > 0f)
            {
                return true;
            }

            if (!IsJumpInputHeld() && (playerBody == null || playerBody.linearVelocity.y <= finalRideReleaseUpVelocity))
            {
                return false;
            }

            _finalRideReleaseTimer = finalRideJumpReleaseTime;
            return true;
        }

        private void ApplyFinalRideHamsterAnchor(Rigidbody2D playerBody)
        {
            if (playerBody == null)
            {
                return;
            }

            var wheelCenter = _body != null ? _body.position : (Vector2)transform.position;
            var xError = wheelCenter.x - playerBody.position.x;
            var targetVelocityX = Mathf.Clamp(
                xError * finalRideCenteringStrength,
                -finalRideMaxCenteringSpeed,
                finalRideMaxCenteringSpeed);

            var velocity = playerBody.linearVelocity;
            velocity.x = targetVelocityX;
            playerBody.linearVelocity = velocity;

            if (finalRideMaxDriftCorrection <= 0f || Mathf.Abs(xError) <= 0.001f)
            {
                return;
            }

            var correction = Mathf.Clamp(xError, -finalRideMaxDriftCorrection, finalRideMaxDriftCorrection);
            playerBody.position += Vector2.right * correction;
            Physics2D.SyncTransforms();
        }

        private void ResolveInvalidPlayerContact(Rigidbody2D playerBody)
        {
            if (!unstickPlayerOnInvalidContact || playerBody == null)
            {
                return;
            }

            var wheelCenter = _body != null ? _body.position : (Vector2)transform.position;
            var awayDirection = Mathf.Sign(playerBody.position.x - wheelCenter.x);
            if (Mathf.Abs(awayDirection) < 0.01f)
            {
                awayDirection = _lastMotionDirection < 0 ? 1f : -1f;
            }

            var velocity = playerBody.linearVelocity;
            if (invalidContactEscapeSpeed > 0f)
            {
                velocity.x = awayDirection > 0f
                    ? Mathf.Max(velocity.x, invalidContactEscapeSpeed)
                    : Mathf.Min(velocity.x, -invalidContactEscapeSpeed);
                velocity.y = Mathf.Max(velocity.y, -0.25f);
                playerBody.linearVelocity = velocity;
            }

            if (invalidContactEscapeForce > 0f)
            {
                playerBody.AddForce(Vector2.right * (awayDirection * invalidContactEscapeForce), ForceMode2D.Force);
            }

            if (invalidContactPositionNudge > 0f)
            {
                playerBody.position += Vector2.right * (awayDirection * invalidContactPositionNudge);
                Physics2D.SyncTransforms();
            }
        }

        private Collider2D GetPlayerCollider(Collision2D collision, Rigidbody2D playerBody)
        {
            if (collision.collider != null && collision.collider.attachedRigidbody == playerBody)
            {
                return collision.collider;
            }

            if (collision.otherCollider != null && collision.otherCollider.attachedRigidbody == playerBody)
            {
                return collision.otherCollider;
            }

            if (collision.collider != null && IsPlayerObject(playerBody.gameObject, collision.collider.gameObject))
            {
                return collision.collider;
            }

            if (collision.otherCollider != null && IsPlayerObject(playerBody.gameObject, collision.otherCollider.gameObject))
            {
                return collision.otherCollider;
            }

            return null;
        }

        [ContextMenu("Reset Wheel Puzzle")]
        public void ResetWheelPuzzle()
        {
            CacheStartPose(force: true);
            RebuildPath();
            _state = WheelState.WaitingForPlayer;
            _pathDistance = 0f;
            _rotationDegrees = 0f;
            _idleTime = 0f;
            _rollbackTargetDistance = -1f;
            _currentPathSpeed = 0f;
            _lastMotionDirection = 0;
            _lastSpinDirection = 0;
            _finalRideSpinVelocity = 0f;
            _finalRideContactTimer = 0f;
            _finalRideReleaseTimer = 0f;
            _finalRideDirection = 0;
            ApplyPose(usePhysicsMove: false);
            UpdateWheelColliderTriggerState();
            StopBodyVelocity();
        }

        [ContextMenu("Set Current Position As Start")]
        public void SetCurrentPositionAsStart()
        {
            _startLocalPosition = transform.localPosition;
            _startLocalRotation = transform.localRotation;
            _hasCachedStartPose = true;
            RebuildPath();
        }

        private void StepAlongPath(int pathDirection, int spinDirection, float speed, float deltaTime, bool hasRollbackTarget)
        {
            pathDirection = pathDirection < 0 ? -1 : 1;
            spinDirection = spinDirection < 0 ? -1 : 1;
            speed = Mathf.Max(0f, speed);

            var previousDistance = _pathDistance;
            var targetDistance = hasRollbackTarget ? Mathf.Max(0f, _rollbackTargetDistance) : (pathDirection > 0 ? _pathLength : 0f);
            var nextDistance = _pathDistance + pathDirection * speed * Mathf.Max(0f, deltaTime);

            if (pathDirection > 0)
            {
                nextDistance = Mathf.Min(nextDistance, targetDistance);
            }
            else
            {
                nextDistance = Mathf.Max(nextDistance, targetDistance);
            }

            _pathDistance = Mathf.Clamp(nextDistance, 0f, _pathLength);

            var movedDistance = _pathDistance - previousDistance;
            if (Mathf.Abs(movedDistance) > 0.0001f)
            {
                var spinSign = invertSpin ? -1f : 1f;
                _rotationDegrees += spinDirection * spinSign * Mathf.Abs(movedDistance) * GetEffectiveSpinDegreesPerUnit();
                _rotationDegrees = Mathf.Repeat(_rotationDegrees, 360f);
                _lastMotionDirection = movedDistance > 0f ? 1 : -1;
            }
            else
            {
                _lastMotionDirection = 0;
            }

            if (lockAtFinalWaypoint && _pathDistance >= _pathLength - 0.001f)
            {
                _pathDistance = _pathLength;
                _state = WheelState.LockedAtFinal;
                _rollbackTargetDistance = -1f;
                _currentPathSpeed = 0f;
                _finalRideSpinVelocity = 0f;
                _finalRideContactTimer = 0f;
                _finalRideReleaseTimer = 0f;
                _finalRideDirection = 0;
                UpdateWheelColliderTriggerState();
            }
            else if (hasRollbackTarget && HasReachedTarget(pathDirection, targetDistance))
            {
                _pathDistance = targetDistance;
                _state = WheelState.WaitingForPlayer;
                _rollbackTargetDistance = -1f;
                _currentPathSpeed = 0f;
                _idleTime = 0f;
            }

            ApplyPose(usePhysicsMove: true);
        }

        private bool HasReachedTarget(int pathDirection, float targetDistance)
        {
            return pathDirection > 0
                ? _pathDistance >= targetDistance - 0.001f
                : _pathDistance <= targetDistance + 0.001f;
        }

        private bool TryGetAutoFall(out float speed, out float targetDistance)
        {
            speed = 0f;
            targetDistance = 0f;

            return TryGetAutoFallSegment(autoFallFromWaypoint, autoFallToWaypoint, out speed, out targetDistance) ||
                   TryGetAutoFallSegment(secondAutoFallFromWaypoint, secondAutoFallToWaypoint, out speed, out targetDistance);
        }

        private bool TryGetAutoFallSegment(int fromWaypoint, int toWaypoint, out float speed, out float targetDistance)
        {
            speed = 0f;
            targetDistance = 0f;

            if (!TryGetWaypointDistance(fromWaypoint, out var startDistance) ||
                !TryGetWaypointDistance(toWaypoint, out var endDistance) ||
                endDistance <= startDistance + 0.001f)
            {
                return false;
            }

            if (_pathDistance < startDistance || _pathDistance >= endDistance - 0.001f)
            {
                return false;
            }

            var segmentProgress = Mathf.InverseLerp(startDistance, endDistance, _pathDistance);
            if (_state != WheelState.AutoFalling && segmentProgress < autoFallStartProgress)
            {
                return false;
            }

            var fallProgress = Mathf.InverseLerp(autoFallStartProgress, 1f, segmentProgress);
            fallProgress = Mathf.SmoothStep(0f, 1f, fallProgress);
            var speedMultiplier = Mathf.Lerp(autoFallStartSpeedMultiplier, autoFallMaxSpeedMultiplier, fallProgress);

            speed = moveSpeed * speedMultiplier;
            targetDistance = endDistance;
            return true;
        }

        private int GetAutoFallSpinDirection()
        {
            if (_lastSpinDirection != 0)
            {
                return _lastSpinDirection;
            }

            var manualSpinDirection = GetManualSpinDirection();
            return manualSpinDirection != 0 ? manualSpinDirection : 1;
        }

        private bool TryGetWaypointDistance(int waypointNumber, out float distance)
        {
            distance = 0f;

            if (waypointNumber <= 0 || _pathDistances.Count == 0)
            {
                return false;
            }

            var pathPointIndex = Mathf.Clamp(waypointNumber, 0, _pathDistances.Count - 1);
            distance = _pathDistances[pathPointIndex];
            return true;
        }

        private int GetManualSpinDirection()
        {
            if (!IsSpinInputHeld() || !CanPlayerInteract())
            {
                return 0;
            }

            if (player == null)
            {
                return 0;
            }

            var deltaX = player.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= playerSideDeadZone)
            {
                return 0;
            }

            return deltaX > 0f ? 1 : -1;
        }

        private bool ShouldRollBack()
        {
            if (!rollbackWhenInputReleased || _idleTime < rollbackStartDelay)
            {
                return false;
            }

            if (_state == WheelState.LockedAtFinal || _pathLength <= 0.0001f)
            {
                return false;
            }

            if (_pathDistance <= 0.0001f || _pathDistance >= _pathLength - 0.001f)
            {
                return false;
            }

            var noRollbackDistance = GetNoRollbackDistance();
            if (noRollbackDistance > 0f && _pathDistance >= noRollbackDistance - 0.001f)
            {
                return false;
            }

            if (intermediateWaypointsAreSafeStops && IsAtIntermediateWaypoint(_pathDistance))
            {
                return false;
            }

            return true;
        }

        private float GetPreviousWaypointDistance(float currentDistance)
        {
            if (_pathDistances.Count == 0)
            {
                return 0f;
            }

            const float epsilon = 0.03f;
            var noRollbackDistance = GetNoRollbackDistance();
            var minimumDistance = currentDistance >= noRollbackDistance - epsilon ? noRollbackDistance : 0f;

            for (var i = _pathDistances.Count - 1; i >= 0; i--)
            {
                var waypointDistance = _pathDistances[i];
                if (waypointDistance < currentDistance - epsilon)
                {
                    return Mathf.Max(minimumDistance, waypointDistance);
                }

                if (!intermediateWaypointsAreSafeStops && Mathf.Abs(waypointDistance - currentDistance) <= epsilon && i > 0)
                {
                    return Mathf.Max(minimumDistance, _pathDistances[i - 1]);
                }
            }

            return minimumDistance;
        }

        private float GetNoRollbackDistance()
        {
            if (noRollbackAfterWaypoint <= 0 || _pathDistances.Count == 0)
            {
                return 0f;
            }

            var pathPointIndex = Mathf.Clamp(noRollbackAfterWaypoint, 0, _pathDistances.Count - 1);
            return _pathDistances[pathPointIndex];
        }

        private bool IsAtIntermediateWaypoint(float currentDistance)
        {
            const float epsilon = 0.035f;

            for (var i = 1; i < _pathDistances.Count - 1; i++)
            {
                if (Mathf.Abs(_pathDistances[i] - currentDistance) <= epsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildPath()
        {
            _pathLocalPositions.Clear();
            _pathDistances.Clear();

            _pathLocalPositions.Add(_startLocalPosition);
            _pathDistances.Add(0f);

            if (pathWaypoints != null)
            {
                for (var i = 0; i < pathWaypoints.Length; i++)
                {
                    if (pathWaypoints[i] == null)
                    {
                        continue;
                    }

                    _pathLocalPositions.Add(ToLocalPathPosition(pathWaypoints[i].position));
                }
            }

            if (_pathLocalPositions.Count == 1 && destinationPoint != null)
            {
                _pathLocalPositions.Add(ToLocalPathPosition(destinationPoint.position));
            }

            _pathLength = 0f;
            for (var i = 1; i < _pathLocalPositions.Count; i++)
            {
                _pathLength += Vector3.Distance(_pathLocalPositions[i - 1], _pathLocalPositions[i]);
                _pathDistances.Add(_pathLength);
            }

            _pathDistance = Mathf.Clamp(_pathDistance, 0f, _pathLength);
        }

        private Vector3 GetPathLocalPosition(float distance)
        {
            if (_pathLocalPositions.Count == 0)
            {
                return transform.localPosition;
            }

            if (_pathLocalPositions.Count == 1 || distance <= 0f)
            {
                return _pathLocalPositions[0];
            }

            if (distance >= _pathLength)
            {
                return _pathLocalPositions[_pathLocalPositions.Count - 1];
            }

            var traveled = 0f;
            for (var i = 1; i < _pathLocalPositions.Count; i++)
            {
                var previous = _pathLocalPositions[i - 1];
                var current = _pathLocalPositions[i];
                var segmentLength = Vector3.Distance(previous, current);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                if (traveled + segmentLength >= distance)
                {
                    var t = (distance - traveled) / segmentLength;
                    return GetSegmentPosition(previous, current, i, t);
                }

                traveled += segmentLength;
            }

            return _pathLocalPositions[_pathLocalPositions.Count - 1];
        }

        private Vector3 GetSegmentPosition(Vector3 previous, Vector3 current, int segmentEndPathIndex, float t)
        {
            t = Mathf.Clamp01(t);

            if (IsAutoFallSegment(segmentEndPathIndex))
            {
                return GetAutoFallSegmentPosition(previous, current, t);
            }

            if (!IsArcedSegment(segmentEndPathIndex))
            {
                return Vector3.Lerp(previous, current, t);
            }

            var arcSegmentHeight = GetArcHeight(segmentEndPathIndex);
            var easedT = Mathf.Lerp(t, SmootherStep(t), 0.45f);
            var position = Vector3.Lerp(previous, current, easedT);
            position.y += Mathf.Sin(easedT * Mathf.PI) * arcSegmentHeight;
            return position;
        }

        private Vector3 GetAutoFallSegmentPosition(Vector3 previous, Vector3 current, float t)
        {
            var horizontalT = Mathf.Lerp(t, SmootherStep(t), autoFallHorizontalEase);
            var fallT = Mathf.Pow(t, Mathf.Max(0.25f, autoFallCurvePower));

            return new Vector3(
                Mathf.Lerp(previous.x, current.x, horizontalT),
                Mathf.Lerp(previous.y, current.y, fallT),
                Mathf.Lerp(previous.z, current.z, horizontalT));
        }

        private bool IsAutoFallSegment(int segmentEndPathIndex)
        {
            var segmentStartPathIndex = segmentEndPathIndex - 1;
            return (autoFallFromWaypoint > 0 && segmentStartPathIndex == autoFallFromWaypoint) ||
                   (secondAutoFallFromWaypoint > 0 && segmentStartPathIndex == secondAutoFallFromWaypoint);
        }

        private bool IsArcedSegment(int segmentEndPathIndex)
        {
            return GetArcHeight(segmentEndPathIndex) > 0f;
        }

        private float GetArcHeight(int segmentEndPathIndex)
        {
            if (arcFromWaypoint <= 0 || arcHeight <= 0f)
            {
                if (secondAutoFallFromWaypoint <= 0 || secondArcHeight <= 0f)
                {
                    return 0f;
                }
            }

            var segmentStartPathIndex = segmentEndPathIndex - 1;
            if (arcFromWaypoint > 0 && arcHeight > 0f && segmentStartPathIndex == arcFromWaypoint)
            {
                return arcHeight;
            }

            if (secondAutoFallFromWaypoint > 0 && secondArcHeight > 0f && segmentStartPathIndex == secondAutoFallFromWaypoint)
            {
                return secondArcHeight;
            }

            return 0f;
        }

        private void ApplyPose(bool usePhysicsMove)
        {
            var localPosition = GetPathLocalPosition(_pathDistance);
            var localRotation = _startLocalRotation * Quaternion.Euler(0f, 0f, _rotationDegrees);

            transform.localPosition = localPosition;
            transform.localRotation = localRotation;

            if (usePhysicsMove && _body != null && _body.bodyType == RigidbodyType2D.Kinematic && Application.isPlaying)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.position = transform.position;
                _body.rotation = transform.eulerAngles.z;
                Physics2D.SyncTransforms();
                return;
            }
        }

        private void SetupPhysicsDefaults()
        {
            var rigidbody2D = GetComponent<Rigidbody2D>();
            if (rigidbody2D == null)
            {
                rigidbody2D = gameObject.AddComponent<Rigidbody2D>();
            }

            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = GetComponent<CircleCollider2D>();
            if (circle == null)
            {
                circle = gameObject.AddComponent<CircleCollider2D>();
            }

            circle.offset = Vector2.zero;
            circle.radius = Mathf.Max(0.05f, wheelColliderRadius);

            UpdateWheelColliderTriggerState();
        }

        private void UpdateWheelColliderTriggerState()
        {
            var shouldBeTrigger = wheelIsTriggerUntilFinal && _state != WheelState.LockedAtFinal;

            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                if (collider != null)
                {
                    collider.isTrigger = shouldBeTrigger;
                }
            }
        }

        private float GetEffectiveSpinDegreesPerUnit()
        {
            if (!usePhysicalSpinFromRadius)
            {
                return spinDegreesPerUnit;
            }

            var radius = Mathf.Max(0.05f, wheelColliderRadius);
            return 360f / (2f * Mathf.PI * radius);
        }

        private void CacheStartPose(bool force)
        {
            if (_hasCachedStartPose && !force)
            {
                return;
            }

            _startLocalPosition = transform.localPosition;
            _startLocalRotation = transform.localRotation;
            _hasCachedStartPose = true;
        }

        private Vector3 ToLocalPathPosition(Vector3 worldPosition)
        {
            return transform.parent != null
                ? transform.parent.InverseTransformPoint(worldPosition)
                : worldPosition;
        }

        private bool CanPlayerInteract()
        {
            if (!requirePlayerInRange)
            {
                return true;
            }

            FindPlayerIfNeeded();
            if (player == null)
            {
                return false;
            }

            return Vector2.Distance(player.position, transform.position) <= activationDistance;
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null)
            {
                return;
            }

            var playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        private bool IsPlayerObject(GameObject bodyObject, GameObject colliderObject)
        {
            return IsTaggedPlayer(bodyObject) ||
                   IsTaggedPlayer(colliderObject) ||
                   (bodyObject != null && IsTaggedPlayer(bodyObject.transform.root.gameObject)) ||
                   (colliderObject != null && IsTaggedPlayer(colliderObject.transform.root.gameObject));
        }

        private bool IsPlayerCollision(Collision2D collision, Rigidbody2D playerBody)
        {
            if (collision == null || playerBody == null)
            {
                return false;
            }

            var colliderObject = collision.collider != null ? collision.collider.gameObject : null;
            var otherColliderObject = collision.otherCollider != null ? collision.otherCollider.gameObject : null;

            return IsPlayerObject(playerBody.gameObject, colliderObject) ||
                   IsPlayerObject(playerBody.gameObject, otherColliderObject);
        }

        private bool IsTaggedPlayer(GameObject candidate)
        {
            return candidate != null && candidate.CompareTag(playerTag);
        }

        private void StopBodyVelocity()
        {
            if (_body == null)
            {
                return;
            }

            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }

        private static bool IsSpinInputHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.isPressed)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.T);
#else
            return false;
#endif
        }

        private static bool IsJumpInputHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.isPressed || keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetButton("Jump") ||
                   Input.GetKey(KeyCode.Space) ||
                   Input.GetKey(KeyCode.W) ||
                   Input.GetKey(KeyCode.UpArrow);
#else
            return false;
#endif
        }

        private static int GetHorizontalInputDirection()
        {
            var direction = 0;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    direction--;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    direction++;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            var axis = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(axis) > 0.01f)
            {
                direction += axis > 0f ? 1 : -1;
            }
            else
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                {
                    direction--;
                }

                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                {
                    direction++;
                }
            }
#endif

            return direction > 0 ? 1 : direction < 0 ? -1 : 0;
        }

        private void OnValidate()
        {
            activationDistance = Mathf.Max(0f, activationDistance);
            playerSideDeadZone = Mathf.Max(0.01f, playerSideDeadZone);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            moveAcceleration = Mathf.Max(0.01f, moveAcceleration);
            moveDeceleration = Mathf.Max(0.01f, moveDeceleration);
            noRollbackAfterWaypoint = Mathf.Max(0, noRollbackAfterWaypoint);
            rollbackStartDelay = Mathf.Max(0f, rollbackStartDelay);
            rollbackSpeed = Mathf.Max(0f, rollbackSpeed);
            arcFromWaypoint = Mathf.Max(0, arcFromWaypoint);
            arcHeight = Mathf.Max(0f, arcHeight);
            secondArcHeight = Mathf.Max(0f, secondArcHeight);
            autoFallFromWaypoint = Mathf.Max(0, autoFallFromWaypoint);
            autoFallToWaypoint = Mathf.Max(0, autoFallToWaypoint);
            secondAutoFallFromWaypoint = Mathf.Max(0, secondAutoFallFromWaypoint);
            secondAutoFallToWaypoint = Mathf.Max(0, secondAutoFallToWaypoint);
            autoFallStartProgress = Mathf.Clamp01(autoFallStartProgress);
            autoFallStartSpeedMultiplier = Mathf.Max(0f, autoFallStartSpeedMultiplier);
            autoFallMaxSpeedMultiplier = Mathf.Max(autoFallStartSpeedMultiplier, autoFallMaxSpeedMultiplier);
            autoFallAcceleration = Mathf.Max(0.01f, autoFallAcceleration);
            autoFallCurvePower = Mathf.Max(0.25f, autoFallCurvePower);
            autoFallHorizontalEase = Mathf.Clamp01(autoFallHorizontalEase);
            spinDegreesPerUnit = Mathf.Max(0f, spinDegreesPerUnit);
            ridePushSpeed = Mathf.Max(0f, ridePushSpeed);
            ridePushForce = Mathf.Max(0f, ridePushForce);
            invalidContactEscapeSpeed = Mathf.Max(0f, invalidContactEscapeSpeed);
            invalidContactEscapeForce = Mathf.Max(0f, invalidContactEscapeForce);
            invalidContactPositionNudge = Mathf.Max(0f, invalidContactPositionNudge);
            finalRideInputDeadZone = Mathf.Max(0f, finalRideInputDeadZone);
            finalRidePlayerPushSpeed = Mathf.Max(0f, finalRidePlayerPushSpeed);
            finalRidePlayerPushForce = Mathf.Max(0f, finalRidePlayerPushForce);
            finalRideSpinDegreesPerSecond = Mathf.Max(0f, finalRideSpinDegreesPerSecond);
            finalRideSpinAcceleration = Mathf.Max(0f, finalRideSpinAcceleration);
            finalRideTopContactRatio = Mathf.Clamp01(finalRideTopContactRatio);
            finalRideCenteringStrength = Mathf.Max(0f, finalRideCenteringStrength);
            finalRideMaxCenteringSpeed = Mathf.Max(0f, finalRideMaxCenteringSpeed);
            finalRideReleaseUpVelocity = Mathf.Max(0f, finalRideReleaseUpVelocity);
            finalRideJumpReleaseTime = Mathf.Max(0f, finalRideJumpReleaseTime);
            finalRideMaxDriftCorrection = Mathf.Max(0f, finalRideMaxDriftCorrection);
            wheelColliderRadius = Mathf.Max(0.05f, wheelColliderRadius);
        }

        private static float SmootherStep(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position, activationDistance);

            DrawPathGizmos();
        }

        private void DrawPathGizmos()
        {
            var previous = transform.position;
            var hasWaypoint = false;

            Gizmos.color = new Color(0.25f, 1f, 0.45f, 0.85f);

            if (pathWaypoints != null)
            {
                for (var i = 0; i < pathWaypoints.Length; i++)
                {
                    var waypoint = pathWaypoints[i];
                    if (waypoint == null)
                    {
                        continue;
                    }

                    hasWaypoint = true;
                    DrawPathSegmentGizmo(previous, waypoint.position, i + 1);
                    Gizmos.DrawWireSphere(waypoint.position, 0.22f);
                    previous = waypoint.position;
                }
            }

            if (hasWaypoint || destinationPoint == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(destinationPoint.position, 0.25f);
            DrawPathSegmentGizmo(transform.position, destinationPoint.position, 1);
        }

        private void DrawPathSegmentGizmo(Vector3 previous, Vector3 current, int segmentEndPathIndex)
        {
            if (!IsArcedSegment(segmentEndPathIndex))
            {
                Gizmos.DrawLine(previous, current);
                return;
            }

            var last = previous;
            const int samples = 16;
            for (var i = 1; i <= samples; i++)
            {
                var t = i / (float)samples;
                var next = GetSegmentPosition(previous, current, segmentEndPathIndex, t);
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }
    }
}
