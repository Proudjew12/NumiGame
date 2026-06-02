using UnityEngine;
using UnityEngine.InputSystem;

public class RopePullMechanic : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference activateAction;

    [Header("References")]
    [SerializeField] private SpriteOutline ropeOutline;
    [SerializeField] private Transform ropeTransform;
    [SerializeField] private Transform handsGround;

    [Header("Targets")]
    [SerializeField] private float ropeTargetY = 3.13f;
    [SerializeField] private float handsTargetY = -5.16f;

    [Header("Pull Settings")]
    [Tooltip("How many button presses to fully complete the mechanic")]
    [SerializeField] private int totalPresses = 25;
    [Tooltip("How smoothly the objects move toward their targets after each press")]
    [SerializeField] private float moveSpeed = 5f;

    private int pressCount = 0;
    private Vector3 ropeStartPos;
    private Vector3 handsStartPos;
    private Vector3 ropeCurrentTarget;
    private Vector3 handsCurrentTarget;
    private bool mechanicComplete = false;

    void Start()
    {
        ropeStartPos = ropeTransform.localPosition;
        handsStartPos = handsGround.localPosition;

        ropeCurrentTarget = ropeStartPos;
        handsCurrentTarget = handsStartPos;
    }

    private void OnEnable()
    {
        if (activateAction != null)
            activateAction.action.started += OnActivatePressed;
    }

    private void OnDisable()
    {
        if (activateAction != null)
            activateAction.action.started -= OnActivatePressed;
    }

    void Update()
    {
        if (mechanicComplete) return;

        ropeTransform.localPosition = Vector3.Lerp(
            ropeTransform.localPosition,
            ropeCurrentTarget,
            Time.deltaTime * moveSpeed
        );

        handsGround.localPosition = Vector3.Lerp(
            handsGround.localPosition,
            handsCurrentTarget,
            Time.deltaTime * moveSpeed
        );
    }

    private void OnActivatePressed(InputAction.CallbackContext _)
    {
        if (mechanicComplete) return;
        if (ropeOutline == null || ropeOutline.currentOutlineSize <= 0f) return;

        pressCount++;

        float progress = Mathf.Clamp01((float)pressCount / totalPresses);

        float newRopeY = Mathf.Lerp(ropeStartPos.y, ropeTargetY, progress);
        ropeCurrentTarget = new Vector3(ropeStartPos.x, newRopeY, ropeStartPos.z);

        float newHandsY = Mathf.Lerp(handsStartPos.y, handsTargetY, progress);
        handsCurrentTarget = new Vector3(handsStartPos.x, newHandsY, handsStartPos.z);

        if (pressCount >= totalPresses)
        {
            mechanicComplete = true;
            Debug.Log("[RopePullMechanic] Puzzle complete! Hands have risen fully.");
        }
    }

    public void ResetMechanic()
    {
        pressCount = 0;
        mechanicComplete = false;
        ropeCurrentTarget = ropeStartPos;
        handsCurrentTarget = handsStartPos;
        ropeTransform.localPosition = ropeStartPos;
        handsGround.localPosition = handsStartPos;
    }
}