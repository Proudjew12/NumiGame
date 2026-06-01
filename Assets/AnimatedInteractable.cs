// AnimatedInteractable.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimatedInteractable : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string animationTriggerName = "Play";

    [Header("Input")]
    [SerializeField] private InputActionReference activateAction;

    private SpriteOutline spriteOutline;

    void Start()
    {
        animator.speed = 0f;
        spriteOutline = GetComponent<SpriteOutline>();
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

    private void OnActivatePressed(InputAction.CallbackContext _)
    {
        if (spriteOutline == null || spriteOutline.currentOutlineSize <= 0f) return;

        if (animator != null)
        {
            animator.speed = 1f;
            Debug.Log("Animation started");
        }
    }
}