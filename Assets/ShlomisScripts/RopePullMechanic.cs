using UnityEngine;
using Unity.Cinemachine;

public class RopePullMechanic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RightStickSpinInput spinInput;
    [SerializeField] private SpriteOutline ropeOutline;
    [SerializeField] private Transform ropeTransform;
    [SerializeField] private Transform handsGround;
    [SerializeField] private CinemachineCamera ropeCamera; // Assign in Inspector

    [Header("Targets")]
    [SerializeField] private float ropeTargetY = 3.13f;
    [SerializeField] private float handsTargetY = -5.16f;

    [Header("Pull Settings")]
    [SerializeField] private float totalDegreesRequired = 1080f;
    [SerializeField] private float moveSpeed = 5f;

    private float accumulatedDegrees = 0f;
    private Vector3 ropeStartPos;
    private Vector3 handsStartPos;
    private Vector3 ropeCurrentTarget;
    private Vector3 handsCurrentTarget;
    private bool mechanicComplete = false;
    private bool cameraActivated = false; // Track state to avoid setting priority every frame

    void Start()
    {
        ropeStartPos = ropeTransform.localPosition;
        handsStartPos = handsGround.localPosition;
        ropeCurrentTarget = ropeStartPos;
        handsCurrentTarget = handsStartPos;

        if (spinInput != null)
            spinInput.onSpinDelta.AddListener(OnSpinDelta);
    }

    private void OnDestroy()
    {
        if (spinInput != null)
            spinInput.onSpinDelta.RemoveListener(OnSpinDelta);
    }

    void Update()
    {
        if (mechanicComplete) return;

        ropeTransform.localPosition = Vector3.Lerp(
            ropeTransform.localPosition, ropeCurrentTarget, Time.deltaTime * moveSpeed);

        handsGround.localPosition = Vector3.Lerp(
            handsGround.localPosition, handsCurrentTarget, Time.deltaTime * moveSpeed);

        HandleCameraPriority();
    }

    private void HandleCameraPriority()
    {
        if (ropeCamera == null || ropeOutline == null) return;

        bool shouldActivate = ropeOutline.currentOutlineSize > 0f;

        if (shouldActivate && !cameraActivated)
        {
            ropeCamera.Priority = 5;
            cameraActivated = true;
        }
        else if (!shouldActivate && cameraActivated)
        {
            ropeCamera.Priority = 0;
            cameraActivated = false;
        }
    }

    public void OnSpinDelta(float degrees)
    {
        if (mechanicComplete) return;
        if (ropeOutline == null || ropeOutline.currentOutlineSize <= 0f) return;

        accumulatedDegrees = Mathf.Min(accumulatedDegrees + degrees, totalDegreesRequired);
        float progress = accumulatedDegrees / totalDegreesRequired;

        ropeCurrentTarget = new Vector3(ropeStartPos.x,
            Mathf.Lerp(ropeStartPos.y, ropeTargetY, progress), ropeStartPos.z);

        handsCurrentTarget = new Vector3(handsStartPos.x,
            Mathf.Lerp(handsStartPos.y, handsTargetY, progress), handsStartPos.z);

       if (accumulatedDegrees >= totalDegreesRequired)
{
    mechanicComplete = true;
    
    if (ropeCamera != null)
    {
        ropeCamera.Priority = 0;
        cameraActivated = false;
    }
    
    Debug.Log("[RopePullMechanic] Puzzle complete!");
}
    }

    public void ResetMechanic()
    {
        accumulatedDegrees = 0f;
        mechanicComplete = false;
        ropeCurrentTarget = ropeStartPos;
        handsCurrentTarget = handsStartPos;
        ropeTransform.localPosition = ropeStartPos;
        handsGround.localPosition = handsStartPos;

        if (ropeCamera != null)
        {
            ropeCamera.Priority = 0;
            cameraActivated = false;
        }
    }
}