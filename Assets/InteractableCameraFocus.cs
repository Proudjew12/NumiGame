using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections.Generic;

public class InteractableCameraFocus : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRadius = 5f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference interactAction;

    // State
    private bool isFocusingObject = false;
    private Transform playerFollowTarget;
    private InteractableManipulator currentManipulator; // track focused object

    // Track which outlines are currently highlighted so we can turn them off
    private readonly HashSet<SpriteOutline> activeOutlines = new();

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
    }

    private void UpdateOutlines()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, ~0);

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
        {
            if (!activeOutlines.Contains(outline))
                outline.SetInRange(true);
        }

        foreach (var outline in activeOutlines)
        {
            if (!inRangeOutlines.Contains(outline))
                outline.SetInRange(false);
        }

        activeOutlines.Clear();
        foreach (var o in inRangeOutlines)
            activeOutlines.Add(o);
    }

    private void OnInteractPressed(InputAction.CallbackContext _)
    {
        if (isFocusingObject)
            ReturnCameraToPlayer();
        else
            TryFocusClosestInteractable();
    }

    private void TryFocusClosestInteractable()
    {
        GameObject closest = GetClosestInteractable();
        if (closest == null) return;

        playerCamera.Follow = closest.transform;
        isFocusingObject = true;

        // Enable gravity on focus
        currentManipulator = closest.GetComponent<InteractableManipulator>();
        currentManipulator?.OnFocused();
    }

    private void ReturnCameraToPlayer()
    {
        playerCamera.Follow = playerFollowTarget;
        isFocusingObject = false;

        // Disable gravity on release
        currentManipulator?.OnFocusReleased();
        currentManipulator = null;
    }

    private GameObject GetClosestInteractable()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius, ~0);

        GameObject closest = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            Transform interactableTransform = FindInteractableInParents(hit.transform);
            if (interactableTransform == null) continue;

            float dist = Vector2.Distance(transform.position, interactableTransform.position);
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
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}