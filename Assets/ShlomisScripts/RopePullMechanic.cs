using UnityEngine;
using Unity.Cinemachine;

public class RopePullMechanic : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RightStickSpinInput spinInput;
    [SerializeField] private SpriteOutline ropeOutline;
    [SerializeField] private CinemachineCamera ropeCamera;

    [Header("Object A")]
    [SerializeField] private Transform objectA;
    [SerializeField] private float objectATargetY = 3.13f;
    [SerializeField] private float objectAMoveSpeed = 5f;
    [SerializeField] private bool moveObjectA = true;

    [Header("Object B")]
    [SerializeField] private Transform objectB;
    [SerializeField] private float objectBTargetY = -5.16f;
    [SerializeField] private float objectBMoveSpeed = 5f;
    [SerializeField] private bool moveObjectB = true;

    [Header("Pull Settings")]
    [SerializeField] private float totalDegreesRequired = 1080f;

    private float accumulatedDegrees = 0f;
    private Vector3 objectAStartPos;
    private Vector3 objectBStartPos;
    private Vector3 objectACurrentTarget;
    private Vector3 objectBCurrentTarget;
    private bool mechanicComplete = false;
    private bool cameraActivated = false;

    void Start()
    {
        if (objectA != null)
        {
            objectAStartPos = objectA.localPosition;
            objectACurrentTarget = objectAStartPos;
        }

        if (objectB != null)
        {
            objectBStartPos = objectB.localPosition;
            objectBCurrentTarget = objectBStartPos;
        }

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

        if (moveObjectA && objectA != null)
            objectA.localPosition = Vector3.Lerp(
                objectA.localPosition, objectACurrentTarget, Time.deltaTime * objectAMoveSpeed);

        if (moveObjectB && objectB != null)
            objectB.localPosition = Vector3.Lerp(
                objectB.localPosition, objectBCurrentTarget, Time.deltaTime * objectBMoveSpeed);

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

        if (moveObjectA && objectA != null)
            objectACurrentTarget = new Vector3(objectAStartPos.x,
                Mathf.Lerp(objectAStartPos.y, objectATargetY, progress), objectAStartPos.z);

        if (moveObjectB && objectB != null)
            objectBCurrentTarget = new Vector3(objectBStartPos.x,
                Mathf.Lerp(objectBStartPos.y, objectBTargetY, progress), objectBStartPos.z);

        if (accumulatedDegrees >= totalDegreesRequired)
        {
            mechanicComplete = true;

            if (ropeCamera != null)
            {
                ropeCamera.Priority = 0;
                cameraActivated = false;
            }

            if (ropeOutline != null) ropeOutline.LockOutlineOff();

            Debug.Log("[RopePullMechanic] Puzzle complete!");
        }
    }

    public void ResetMechanic()
    {
        accumulatedDegrees = 0f;
        mechanicComplete = false;

        if (objectA != null)
        {
            objectACurrentTarget = objectAStartPos;
            objectA.localPosition = objectAStartPos;
        }

        if (objectB != null)
        {
            objectBCurrentTarget = objectBStartPos;
            objectB.localPosition = objectBStartPos;
        }

        if (ropeCamera != null)
        {
            ropeCamera.Priority = 0;
            cameraActivated = false;
        }
    }
}