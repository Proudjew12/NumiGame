using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class RightStickSpinInput : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference rightStickAction;

    [Header("Spin Settings")]
    [SerializeField] private float stickDeadzone = 0.3f;
    [SerializeField] private float minSpinArcDegrees = 45f;
    [SerializeField] private bool clockwiseOnly = true;

    public UnityEvent<float> onSpinDelta; // broadcasts degrees spun each frame

    private float previousAngle = 0f;
    private bool wasActiveLastFrame = false;
    private float currentArcDegrees = 0f;
    private float lastDeltaSign = 0f;
    private bool arcIsValid = false;

    private void OnEnable()
    {
        if (rightStickAction != null)
            rightStickAction.action.Enable();
    }

    private void OnDisable()
    {
        if (rightStickAction != null)
            rightStickAction.action.Disable();
    }

    void Update()
    {
        if (rightStickAction == null) return;

        Vector2 stickValue = rightStickAction.action.ReadValue<Vector2>();
        float magnitude = stickValue.magnitude;

        if (magnitude < stickDeadzone)
        {
            wasActiveLastFrame = false;
            currentArcDegrees = 0f;
            lastDeltaSign = 0f;
            arcIsValid = false;
            return;
        }

        float currentAngle = Mathf.Atan2(stickValue.y, stickValue.x) * Mathf.Rad2Deg;

        if (!wasActiveLastFrame)
        {
            previousAngle = currentAngle;
            wasActiveLastFrame = true;
            return;
        }

        float delta = Mathf.DeltaAngle(previousAngle, currentAngle);
        previousAngle = currentAngle;

        if (Mathf.Abs(delta) < 0.01f) return;

        float deltaSign = Mathf.Sign(delta);

        if (lastDeltaSign != 0f && deltaSign != lastDeltaSign)
        {
            currentArcDegrees = 0f;
            arcIsValid = false;
        }

        lastDeltaSign = deltaSign;
        currentArcDegrees += Mathf.Abs(delta);

        if (currentArcDegrees >= minSpinArcDegrees)
            arcIsValid = true;

        if (!arcIsValid) return;
        if (clockwiseOnly && delta > 0f) return;

        onSpinDelta.Invoke(Mathf.Abs(delta));
    }
}