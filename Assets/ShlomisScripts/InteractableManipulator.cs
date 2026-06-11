using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;

public class InteractableManipulator : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private CinemachineCamera playerCamera;

    [Header("Focus Collider Trigger")]
    [SerializeField] private Collider2D targetCollider;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private InputActionReference moveAction;

    [Header("Player Carry")]
    [Tooltip("When enabled, a player standing on this object will move with it.")]
    [SerializeField] private bool moveStandingPlayerWithObject = false;
    [Tooltip("Temporarily enables Rigidbody interpolation while this object is controlled.")]
    [SerializeField] private bool smoothRigidbodyInterpolation = true;
    [Tooltip("Extra downward ray distance to keep the player attached when the platform moves down quickly.")]
    [SerializeField] private float carryHoverTolerance = 0.35f;
    [Tooltip("How long to keep carrying the player after contact flickers for one or two physics frames.")]
    [SerializeField] private float carryStickTime = 0.15f;
    [Tooltip("Layers that block the platform and carried player while they move together.")]
    [SerializeField] private LayerMask carriedPairCollisionMask = ~0;
    [Tooltip("Small space kept before blocking colliders so the carried pair does not jitter against walls.")]
    [SerializeField] private float collisionSkinWidth = 0.02f;

    [Header("Scale")]
    [SerializeField] private float scaleSpeed = 1f;
    [SerializeField] private float minScale = 0.2f;
    [SerializeField] private float maxScale = 5f;
    [SerializeField] private InputActionReference scaleUpAction;
    [SerializeField] private InputActionReference scaleDownAction;

    [Header("Snap Back")]
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float snapSpeed = 8f;
    [SerializeField] private bool instantSnap = false;

    [Header("Snap Back Scale")]
    [Tooltip("The scale the object returns to after snapping back.")]
    [SerializeField] private float snapBackScale = 1f;

    public Transform originPoint;

    private bool _isSnappingBack = false;
    private Rigidbody2D _rb;
    private Collider2D[] _movementColliders;
    private bool _wasFocused = false;
    private bool _wasCarryingPlayer = false;
    private float _carryUntilTime = 0f;
    private Vector2 _carryOffset;
    private RigidbodyInterpolation2D _originalInterpolation;

    private readonly RaycastHit2D[] _carryRayHits = new RaycastHit2D[4];
    private readonly RaycastHit2D[] _movementCastHits = new RaycastHit2D[12];

    private bool IsControlled => playerCamera != null && playerCamera.Follow == transform;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _movementColliders = GetComponentsInChildren<Collider2D>(true);

        if (_rb != null)
            _originalInterpolation = _rb.interpolation;
    }

    private void OnValidate()
    {
        carryHoverTolerance = Mathf.Max(0f, carryHoverTolerance);
        carryStickTime = Mathf.Max(0f, carryStickTime);
        collisionSkinWidth = Mathf.Max(0f, collisionSkinWidth);
    }

    public void OnFocused()
    {
        if (_rb == null) return;

        if (smoothRigidbodyInterpolation)
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        _rb.gravityScale = 0f;
        _rb.linearVelocity = Vector2.zero;
    }

    public void OnFocusReleased()
    {
        ClearCarryState();

        if (_rb == null) return;
        _rb.interpolation = _originalInterpolation;
        _rb.gravityScale = 1f;
        _rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        HandleFocusColliderToggle();

        if (originPoint != null && !_isSnappingBack)
        {
            float distance = Vector3.Distance(GetObjectPosition(), originPoint.position);

            if (distance > maxDistance)
            {
                _isSnappingBack = true;
                if (_rb != null)
                {
                    _rb.gravityScale = 0f;
                    _rb.linearVelocity = Vector2.zero;
                }
            }
        }

        if (_isSnappingBack || !IsControlled) return;

        float scaleDelta = 0f;

        if (scaleUpAction != null && scaleUpAction.action.IsPressed())
            scaleDelta += scaleSpeed * Time.deltaTime;

        if (scaleDownAction != null && scaleDownAction.action.IsPressed())
            scaleDelta -= scaleSpeed * Time.deltaTime;

        if (scaleDelta != 0f)
        {
            float next = Mathf.Clamp(transform.localScale.x + scaleDelta, minScale, maxScale);
            transform.localScale = new Vector3(next, next, next);
        }
    }

    private void FixedUpdate()
    {
        if (_isSnappingBack)
        {
            SnapBack(Time.fixedDeltaTime);
            return;
        }

        if (!IsControlled) return;

        Vector2 input = moveAction != null
            ? moveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        MoveObjectTo(GetObjectPosition() + (Vector3)(input * moveSpeed * Time.fixedDeltaTime));
    }

    private void HandleFocusColliderToggle()
    {
        if (targetCollider == null) return;

        bool isFocusedNow = IsControlled;

        if (isFocusedNow && !_wasFocused)
            targetCollider.enabled = false;
        else if (!isFocusedNow && _wasFocused)
            targetCollider.enabled = true;

        _wasFocused = isFocusedNow;
    }

    private void SnapBack(float deltaTime)
    {
        if (originPoint == null)
        {
            _isSnappingBack = false;
            return;
        }

        if (instantSnap)
        {
            ApplySnapResult();
            return;
        }

        Vector3 nextPosition = Vector3.Lerp(
            GetObjectPosition(),
            originPoint.position,
            snapSpeed * deltaTime
        );

        MoveObjectTo(nextPosition);

        if (Vector3.Distance(nextPosition, originPoint.position) < 0.01f)
        {
            ApplySnapResult();
            StartCoroutine(FlashInstantSnap());
        }
    }

    private void ApplySnapResult()
    {
        MoveObjectTo(originPoint.position);
        transform.localScale = Vector3.one * snapBackScale;
        transform.rotation = Quaternion.identity;
        _isSnappingBack = false;

        if (_rb != null && !IsControlled)
        {
            _rb.gravityScale = 1f;
            _rb.linearVelocity = Vector2.zero;
        }
    }

    private IEnumerator FlashInstantSnap()
    {
        instantSnap = true;
        yield return new WaitForEndOfFrame();
        instantSnap = false;
    }

    private void MoveObjectTo(Vector3 targetPosition)
    {
        Vector3 currentPosition = GetObjectPosition();
        targetPosition.z = currentPosition.z;

        NomiMovment playerToCarry = GetPlayerToCarry();
        Vector3 requestedDelta = targetPosition - currentPosition;
        Vector3 allowedDelta = ClampDeltaForSolidCollisions(requestedDelta, playerToCarry);
        Vector3 finalPosition = currentPosition + allowedDelta;

        if (allowedDelta.sqrMagnitude > Mathf.Epsilon)
        {
            if (_rb != null)
                _rb.MovePosition((Vector2)finalPosition);
            else
                transform.position = finalPosition;
        }

        if (playerToCarry != null)
            MoveStandingPlayer(playerToCarry, finalPosition);
    }

    private Vector3 GetObjectPosition()
    {
        Vector3 position = transform.position;

        if (_rb != null)
        {
            position.x = _rb.position.x;
            position.y = _rb.position.y;
        }

        return position;
    }

    private NomiMovment GetPlayerToCarry()
    {
        if (!moveStandingPlayerWithObject)
        {
            ClearCarryState();
            return null;
        }

        NomiMovment playerMovement = NomiMovment.instance;
        if (playerMovement == null || playerMovement.player == null || _movementColliders == null)
        {
            ClearCarryState();
            return null;
        }

        if (IsPlayerStandingOnThisObject(playerMovement))
        {
            if (!_wasCarryingPlayer)
                _carryOffset = playerMovement.player.position - (Vector2)GetObjectPosition();

            _wasCarryingPlayer = true;
            _carryUntilTime = Time.time + Mathf.Max(0f, carryStickTime);
            return playerMovement;
        }

        if (_wasCarryingPlayer && Time.time <= _carryUntilTime)
            return playerMovement;

        ClearCarryState();
        return null;
    }

    private bool IsPlayerStandingOnThisObject(NomiMovment playerMovement)
    {
        foreach (Collider2D currentCollider in _movementColliders)
        {
            if (currentCollider == null || currentCollider.isTrigger) continue;

            if (playerMovement.IsStandingOn(currentCollider)) return true;
            if (IsPlayerHoveringAbove(playerMovement, currentCollider)) return true;
        }

        return false;
    }

    private Vector3 ClampDeltaForSolidCollisions(Vector3 delta, NomiMovment playerToCarry)
    {
        float distance = delta.magnitude;
        if (distance <= Mathf.Epsilon) return Vector3.zero;

        Vector2 direction = (Vector2)(delta / distance);
        float allowedDistance = distance;

        CastSolidColliders(_movementColliders, direction, distance, playerToCarry, ref allowedDistance);

        if (playerToCarry != null)
            CastSolidColliders(playerToCarry.SolidColliders, direction, distance, playerToCarry, ref allowedDistance);

        return (Vector3)(direction * allowedDistance);
    }

    private void CastSolidColliders(
        Collider2D[] colliders,
        Vector2 direction,
        float distance,
        NomiMovment playerToCarry,
        ref float allowedDistance)
    {
        if (colliders == null || allowedDistance <= 0f) return;

        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(carriedPairCollisionMask);

        foreach (Collider2D currentCollider in colliders)
        {
            if (currentCollider == null || currentCollider.isTrigger) continue;

            int hitCount = currentCollider.Cast(direction, filter, _movementCastHits, distance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = _movementCastHits[i];
                if (ShouldIgnoreSolidHit(hit.collider, playerToCarry)) continue;

                float hitDistance = Mathf.Max(0f, hit.distance - collisionSkinWidth);
                allowedDistance = Mathf.Min(allowedDistance, hitDistance);
            }
        }
    }

    private bool ShouldIgnoreSolidHit(Collider2D hitCollider, NomiMovment playerToCarry)
    {
        if (hitCollider == null || hitCollider.isTrigger) return true;
        if (ContainsCollider(_movementColliders, hitCollider)) return true;
        if (playerToCarry != null && ContainsCollider(playerToCarry.SolidColliders, hitCollider)) return true;

        return false;
    }

    private static bool ContainsCollider(Collider2D[] colliders, Collider2D target)
    {
        if (colliders == null || target == null) return false;

        foreach (Collider2D currentCollider in colliders)
            if (currentCollider == target) return true;

        return false;
    }

    private void MoveStandingPlayer(NomiMovment playerMovement, Vector3 objectPosition)
    {
        Vector2 targetPosition = (Vector2)objectPosition + _carryOffset;
        playerMovement.MoveWithPlatform(targetPosition, Time.fixedDeltaTime);
    }

    private void ClearCarryState()
    {
        bool wasCarryingPlayer = _wasCarryingPlayer;

        _wasCarryingPlayer = false;
        _carryUntilTime = 0f;

        if (wasCarryingPlayer)
            NomiMovment.instance?.ClearPlatformCarry();
    }

    private bool IsPlayerHoveringAbove(NomiMovment playerMovement, Collider2D target)
    {
        if (playerMovement.GroundCheck == null) return false;

        int hitCount = Physics2D.RaycastNonAlloc(
            playerMovement.GroundCheck.position,
            Vector2.down,
            _carryRayHits,
            playerMovement.GroundCheckRadius + Mathf.Max(0f, carryHoverTolerance),
            playerMovement.GroundLayer);

        for (int i = 0; i < hitCount; i++)
            if (_carryRayHits[i].collider == target) return true;

        return false;
    }
}
