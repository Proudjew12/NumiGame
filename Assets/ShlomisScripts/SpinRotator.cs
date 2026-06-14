using UnityEngine;

/// <summary>
/// Rotates this GameObject in response to spin input from RightStickSpinInput.
/// Wire up RightStickSpinInput.onSpinDelta to SpinRotator.OnSpinDelta in the Inspector,
/// or call OnSpinDelta(degrees) from any other source.
/// </summary>
public class SpinRotator : MonoBehaviour
{
    [Header("Rotation Axis")]
    [Tooltip("Local axis to rotate around. Defaults to the Z axis (2D-style spin).")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [Header("Direction")]
    [Tooltip("If true, positive spin delta rotates clockwise (negative Z). If false, counter-clockwise.")]
    [SerializeField] private bool invertDirection = false;

    [Header("Speed Multiplier")]
    [Tooltip("Scales the incoming spin degrees. 1 = 1:1 with stick motion, >1 = faster, <1 = slower.")]
    [SerializeField] private float speedMultiplier = 1f;

    [Header("Smoothing (optional)")]
    [Tooltip("When > 0, the rotation is smoothed toward the target. 0 = instant.")]
    [SerializeField] private float smoothTime = 0f;

    [Header("Outline Gate")]
    [Tooltip("Drag the object that has SpriteOutline on it. If left empty, this object and its children are searched automatically.")]
    [SerializeField] private SpriteOutline outlineSource;

    // Accumulated target angle (degrees) driven by input
    private float _targetAngle = 0f;
    // Current smoothed angle
    private float _currentAngle = 0f;
    private float _angularVelocity = 0f;

    private void Start()
    {
        // Auto-find SpriteOutline on this object or any child if not assigned
        if (outlineSource == null)
            outlineSource = GetComponentInChildren<SpriteOutline>(includeInactive: true);
    }

    private bool IsOutlineActive()
    {
        return outlineSource != null && outlineSource.currentOutlineSize > 0f;
    }

    private void Update()
    {
        if (smoothTime <= 0f)
        {
            // Instant: apply any accumulated delta directly via Transform
            // (accumulated via OnSpinDelta during this frame or last frame)
            return;
        }

        // Smooth toward target
        _currentAngle = Mathf.SmoothDampAngle(
            _currentAngle,
            _targetAngle,
            ref _angularVelocity,
            smoothTime
        );

        ApplyAngle(_currentAngle);
    }

    /// <summary>
    /// Call this from RightStickSpinInput.onSpinDelta (wire up in Inspector).
    /// degrees: absolute degrees spun this frame (always positive from RightStickSpinInput).
    /// </summary>
    public void OnSpinDelta(float degrees)
    {
        if (!IsOutlineActive()) return;

        float sign = invertDirection ? 1f : -1f;
        float delta = degrees * speedMultiplier * sign;

        if (smoothTime <= 0f)
        {
            // Instant rotation — apply immediately
            transform.Rotate(rotationAxis, delta, Space.Self);
        }
        else
        {
            // Accumulate for smooth damping in Update
            _targetAngle += delta;
            _currentAngle += delta * 0.01f; // nudge so SmoothDamp has something to chase
        }
    }

    /// <summary>
    /// Instantly snaps the object back to its original local rotation.
    /// </summary>
    public void ResetRotation()
    {
        transform.localRotation = Quaternion.identity;
        _targetAngle = 0f;
        _currentAngle = 0f;
        _angularVelocity = 0f;
    }

    private void ApplyAngle(float angle)
    {
        // Reconstruct the full rotation from accumulated angle
        transform.localRotation = Quaternion.AngleAxis(angle, rotationAxis);
    }
}