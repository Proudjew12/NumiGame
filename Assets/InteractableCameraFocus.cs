using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections.Generic;
using NumiDream.Input;

public class InteractableCameraFocus : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 5f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    private bool isFocusingObject = false;
    private Transform playerFollowTarget;
    private InteractableManipulator currentManipulator;
    private int lastInteractFrame = -1;

    private readonly HashSet<SpriteOutline> activeOutlines = new();

    public Transform detectionPivot;

    private void Start()
    {
        if (playerCamera != null)
            playerFollowTarget = playerCamera.Follow;
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.started += OnInteractPressed;
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.started -= OnInteractPressed;
    }

    private void Update()
    {
        UpdateOutlines();
        if (NumiInput.WasInteractPressed())
        {
            TryToggleFocus();
        }
    }

    // Single overlap call — updates outlines and caches results for focus use
    private void UpdateOutlines()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPivot.position, detectionRadius, ~0);

        HashSet<SpriteOutline> inRangeOutlines = new();

        foreach (var hit in hits)
        {
            Transform interactableTransform = FindInteractableInParents(hit.transform);
            if (interactableTransform == null) continue;

            SpriteOutline outline = interactableTransform.GetComponentInChildren<SpriteOutline>();
            if (outline == null) continue;

            inRangeOutlines.Add(outline);
        }

        foreach (var outline in inRangeOutlines)
            if (!activeOutlines.Contains(outline))
                outline.SetInRange(true);

        foreach (var outline in activeOutlines)
            if (!inRangeOutlines.Contains(outline))
                outline.SetInRange(false);

        activeOutlines.Clear();
        foreach (var o in inRangeOutlines)
            activeOutlines.Add(o);
    }

    private void OnInteractPressed(InputAction.CallbackContext _)
    {
        TryToggleFocus();
    }

    private void TryToggleFocus()
    {
        if (lastInteractFrame == Time.frameCount)
        {
            return;
        }

        lastInteractFrame = Time.frameCount;

        if (isFocusingObject)
            ReturnCameraToPlayer();
        else
            TryFocusClosestInteractable();
    }

    private void TryFocusClosestInteractable()
    {
        GameObject closest = GetClosestInteractable();
        if (closest == null) return;

        // Check self and children for the manipulator
        InteractableManipulator manipulator = closest.GetComponentInChildren<InteractableManipulator>();
        if (manipulator == null) return;

        playerCamera.Follow = manipulator.transform;
        isFocusingObject = true;

        currentManipulator = manipulator;
        currentManipulator.OnFocused();
    }

    private void ReturnCameraToPlayer()
    {
        playerCamera.Follow = playerFollowTarget;
        isFocusingObject = false;

        currentManipulator?.OnFocusReleased();
        currentManipulator = null;
    }

    private GameObject GetClosestInteractable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPivot.position, detectionRadius, ~0);

        GameObject closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Transform interactableTransform = FindInteractableInParents(hit.transform);
            if (interactableTransform == null) continue;

            // Must have a manipulator on itself or any child
            InteractableManipulator manipulator =
                interactableTransform.GetComponentInChildren<InteractableManipulator>();
            if (manipulator == null) continue;

            // Outline must be visible (size > 0)
            SpriteOutline outline = interactableTransform.GetComponentInChildren<SpriteOutline>();
            if (outline == null || outline.currentOutlineSize <= 0f) continue;

            float dist = Vector2.Distance(detectionPivot.position, interactableTransform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = interactableTransform.gameObject;
            }
        }

        return closest;
    }

    private Transform FindInteractableInParents(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Interactable"))
                return t;
            t = t.parent;
        }
        return null;
    }

    private void OnDrawGizmosSelected()
    {
        bool nearInteractable = GetClosestInteractable() != null;
        Gizmos.color = nearInteractable
            ? new Color(0f, 0.4f, 1f, 0.6f)
            : new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(detectionPivot.position, detectionRadius);
    }
}
