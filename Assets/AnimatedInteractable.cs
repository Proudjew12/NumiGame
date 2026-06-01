using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class AnimatedInteractable : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference activateAction;

    [Header("Islands (assign in order: 1, 2, 3, 4, 5)")]
    [SerializeField] private Transform island1;
    [SerializeField] private Transform island2;
    [SerializeField] private Transform island4;
    [SerializeField] private Transform island5;

    [Header("Reference Y (Island 3's lowered Y position)")]
    [SerializeField] private float loweredYPosition = -14f; // Set this to island3's Y in Inspector

    [Header("Timing")]
    [Tooltip("Max time between taps within the same group")]
    [SerializeField] private float tapWindowDuration = 1.2f;
    [Tooltip("Time to wait after a group is completed before accepting next group")]
    [SerializeField] private float groupPauseDuration = 0.8f;
    [Tooltip("How fast islands move up/down")]
    [SerializeField] private float moveSpeed = 3f;

    // The puzzle sequence: how many taps per step, and which island to drop
    // Step 0: 4 taps → island1 drops
    // Step 1: 2 taps → island2 drops
    // Step 2: 3 taps → island4 drops
    // Step 3: 1 tap  → island5 drops
    private readonly int[] requiredTaps = { 4, 2, 3, 1 };
    private Transform[] stepIslands; // filled in Start()

    private int currentStep = 0;
    private int currentTapCount = 0;
    private float tapTimer = 0f;
    private bool waitingForPause = false;   // between tap groups
    private float pauseTimer = 0f;
    private bool puzzleSolved = false;
    private bool isAcceptingInput = true;

    // Store each island's original Y so we can reset them
    private float[] originalYPositions;

    private SpriteOutline spriteOutline;

    void Start()
    {
        spriteOutline = GetComponent<SpriteOutline>();

        stepIslands = new Transform[] { island1, island2, island4, island5 };

        // Cache original Y positions
        originalYPositions = new float[stepIslands.Length];
        for (int i = 0; i < stepIslands.Length; i++)
        {
            if (stepIslands[i] != null)
                originalYPositions[i] = stepIslands[i].position.y;
        }
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
        if (puzzleSolved || !isAcceptingInput) return;

        // Waiting for the pause between groups
        if (waitingForPause)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= groupPauseDuration)
            {
                waitingForPause = false;
                pauseTimer = 0f;
                // Move to next step
                currentStep++;
                currentTapCount = 0;
                tapTimer = 0f;

                if (currentStep >= requiredTaps.Length)
                {
                    // All steps completed!
                    puzzleSolved = true;
                    Debug.Log("Puzzle SOLVED!");
                    return;
                }
            }
            return; // Don't run tap timer while in pause
        }

        // Run tap expiry timer only after the first tap of a group
        if (currentTapCount > 0)
        {
            tapTimer += Time.deltaTime;
            if (tapTimer > tapWindowDuration)
            {
                // Timed out — wrong number of taps or player stopped mid-group
                Debug.Log($"Tap timeout at step {currentStep}. Resetting.");
                ResetPuzzle();
            }
        }
    }

    private void OnActivatePressed(InputAction.CallbackContext _)
    {
        if (puzzleSolved) return;
        if (spriteOutline == null || spriteOutline.currentOutlineSize <= 0f) return;
        if (!isAcceptingInput) return;

        // Tapping during the mandatory pause between groups = immediate fail
        if (waitingForPause)
        {
            Debug.Log("Tapped during pause — too eager! Resetting.");
            ResetPuzzle();
            return;
        }

        currentTapCount++;
        tapTimer = 0f;

        Debug.Log($"Step {currentStep}: tap {currentTapCount}/{requiredTaps[currentStep]}");

        if (currentTapCount > requiredTaps[currentStep])
        {
            // Pressed too many times — fail immediately
            Debug.Log($"Too many taps at step {currentStep}. Resetting.");
            ResetPuzzle();
        }
        else if (currentTapCount == requiredTaps[currentStep])
        {
            // Exactly right — drop the island and start the mandatory pause
            DropIsland(currentStep);
            waitingForPause = true;
            pauseTimer = 0f;
            currentTapCount = 0;
            tapTimer = 0f;
        }
        // Less than required — keep waiting for more taps
    }

    private void DropIsland(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= stepIslands.Length) return;
        Transform island = stepIslands[stepIndex];
        if (island == null) return;

        StopAllCoroutinesOnIsland(island);
        StartCoroutine(MoveIslandTo(island, loweredYPosition));
        Debug.Log($"Island for step {stepIndex} dropped to Y={loweredYPosition}");
    }

    private void ResetPuzzle()
    {
        // Stop any ongoing movement coroutines and raise all dropped islands back up
        StopAllCoroutines();

        currentStep = 0;
        currentTapCount = 0;
        tapTimer = 0f;
        waitingForPause = false;
        pauseTimer = 0f;

        for (int i = 0; i < stepIslands.Length; i++)
        {
            if (stepIslands[i] != null)
                StartCoroutine(MoveIslandTo(stepIslands[i], originalYPositions[i]));
        }
    }

    private IEnumerator MoveIslandTo(Transform island, float targetY)
    {
        while (!Mathf.Approximately(island.position.y, targetY))
        {
            float newY = Mathf.MoveTowards(island.position.y, targetY, moveSpeed * Time.deltaTime);
            island.position = new Vector3(island.position.x, newY, island.position.z);
            yield return null;
        }
        // Snap to exact position
        island.position = new Vector3(island.position.x, targetY, island.position.z);
    }

    // Helper: we can't stop coroutines per-island easily, 
    // so we track them with a dictionary if needed in the future.
    // For now StopAllCoroutines() in ResetPuzzle covers it.
    private void StopAllCoroutinesOnIsland(Transform island)
    {
        // Placeholder — StopAllCoroutines() is called in ResetPuzzle anyway.
        // If you need finer control per island, switch to Coroutine handles.
    }
}