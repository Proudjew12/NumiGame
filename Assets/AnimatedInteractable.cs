using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Audio;

public class AnimatedInteractable : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference activateAction;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera puzzleCamera;

    [Header("Islands (assign in order: 1, 2, 3, 4, 5)")]
    [SerializeField] private Transform island1;
    [SerializeField] private Transform island2;
    [SerializeField] private Transform island4;
    [SerializeField] private Transform island5;

    [Header("Reference Y (Island 3's lowered Y position)")]
    [SerializeField] private float loweredYPosition = -14f;

    [Header("Timing")]
    [Tooltip("Max time between taps within the same group")]
    [SerializeField] private float tapWindowDuration = 1.2f;
    [Tooltip("Mandatory pause after a correct group before the next group is accepted")]
    [SerializeField] private float groupPauseDuration = 0.8f;
    [Tooltip("How fast islands move up/down")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Delay between last correct tap and the island actually dropping")]
    [SerializeField] private float dropDelay = 0.6f;
    [Tooltip("How long the player can idle between groups before the puzzle resets")]
    [SerializeField] private float idleResetDuration = 4f;

    [Header("Bell Audio")]
    [SerializeField] private AudioClip[] bellSounds;
    [SerializeField, Range(0f, 1f)] private float bellSoundVolume = 1f;
    [SerializeField] private AudioMixerGroup bellSoundOutput;
    [SerializeField] private AudioSource bellAudioSource;
    [SerializeField, Range(0f, 1f)] private float bellSoundSpatialBlend = 0.15f;

    [Header("Fail Audio")]
    [SerializeField] private AudioClip[] failSounds;
    [SerializeField, Range(0f, 1f)] private float failSoundVolume = 1f;
    [SerializeField] private AudioMixerGroup failSoundOutput;
    [SerializeField] private AudioSource failAudioSource;
    [SerializeField, Range(0f, 1f)] private float failSoundSpatialBlend = 0.15f;

    // Step 0: 4 taps → island1 drops
    // Step 1: 2 taps → island2 drops
    // Step 2: 3 taps → island4 drops
    // Step 3: 1 tap  → island5 drops
    private readonly int[] requiredTaps = { 4, 2, 3, 1 };
    private Transform[] stepIslands;

    private int currentStep = 0;
    private int currentTapCount = 0;
    private float tapTimer = 0f;
    private bool waitingForPause = false;
    private float pauseTimer = 0f;
    private bool puzzleSolved = false;

    private float idleTimer = 0f;
    private bool idleTimerActive = false;

    private float[] originalYPositions;
    private Coroutine[] moveCoroutines;

    private SpriteOutline spriteOutline;

    void Start()
    {
        ConfigureBellAudioSource();
        ConfigureFailAudioSource();
        spriteOutline = GetComponent<SpriteOutline>();
        stepIslands = new Transform[] { island1, island2, island4, island5 };

        originalYPositions = new float[stepIslands.Length];
        moveCoroutines = new Coroutine[stepIslands.Length];

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
        if (puzzleSolved) return;

        // Camera priority follows outline
        if (puzzleCamera != null && spriteOutline != null)
            puzzleCamera.Priority = spriteOutline.currentOutlineSize > 0f ? 5 : 0;

        // --- Pause window between groups ---
        if (waitingForPause)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= groupPauseDuration)
            {
                waitingForPause = false;
                pauseTimer = 0f;
                currentStep++;
                currentTapCount = 0;
                tapTimer = 0f;

                if (currentStep >= requiredTaps.Length)
                {
                    puzzleSolved = true;
                    idleTimerActive = false;
                    if (puzzleCamera != null) puzzleCamera.Priority = 0;
                    Debug.Log("Puzzle SOLVED!");
                    return;
                }

                idleTimer = 0f;
                idleTimerActive = true;
            }
            return; // no input accepted during pause
        }

        // --- Idle reset (doing nothing between groups) ---
        if (idleTimerActive && currentTapCount == 0)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= idleResetDuration)
            {
                Debug.Log("Player went idle — resetting puzzle.");
                ResetPuzzle();
                return;
            }
        }

        // --- Tap expiry within a group ---
        if (currentTapCount > 0)
        {
            idleTimer = 0f;
            tapTimer += Time.deltaTime;
            if (tapTimer > tapWindowDuration)
            {
                Debug.Log($"Tap timeout at step {currentStep}. Resetting.");
                ResetPuzzle();
            }
        }
    }

    private void OnActivatePressed(InputAction.CallbackContext _)
    {
        if (puzzleSolved) return;

        // Camera priority follows outline
        if (puzzleCamera != null && spriteOutline != null)
            puzzleCamera.Priority = spriteOutline.currentOutlineSize > 0f ? 5 : 0;
        if (spriteOutline == null || spriteOutline.currentOutlineSize <= 0f) return;

        // Tapping during the mandatory pause = fail
        if (waitingForPause)
        {
            PlayBellSound();
            Debug.Log("Tapped during pause — resetting.");
            ResetPuzzle();
            return;
        }

        PlayBellSound();
        currentTapCount++;
        tapTimer = 0f;

        Debug.Log($"Step {currentStep}: tap {currentTapCount}/{requiredTaps[currentStep]}");

        if (currentTapCount > requiredTaps[currentStep])
        {
            Debug.Log($"Too many taps at step {currentStep}. Resetting.");
            ResetPuzzle();
        }
        else if (currentTapCount == requiredTaps[currentStep])
        {
            // Start the mandatory pause immediately — input is blocked from this point
            // The island drop fires after dropDelay but input state is already advancing
            waitingForPause = true;
            pauseTimer = 0f;
            currentTapCount = 0;
            tapTimer = 0f;

            StartCoroutine(DelayedDrop(currentStep));
        }
    }

    private void PlayBellSound()
    {
        if (bellSounds == null || bellSounds.Length == 0) return;

        ConfigureBellAudioSource();
        if (bellAudioSource == null) return;

        var clip = bellSounds[Random.Range(0, bellSounds.Length)];
        if (clip == null) return;

        bellAudioSource.pitch = 1f;
        bellAudioSource.PlayOneShot(clip, bellSoundVolume);
    }

    private void PlayFailSound()
    {
        if (failSounds == null || failSounds.Length == 0) return;

        ConfigureFailAudioSource();
        if (failAudioSource == null) return;

        var clip = failSounds[Random.Range(0, failSounds.Length)];
        if (clip == null) return;

        failAudioSource.pitch = 1f;
        failAudioSource.PlayOneShot(clip, failSoundVolume);
    }

    private void ConfigureBellAudioSource()
    {
        if (bellAudioSource == null)
            bellAudioSource = GetComponent<AudioSource>();

        if (bellAudioSource == null)
            bellAudioSource = gameObject.AddComponent<AudioSource>();

        bellAudioSource.playOnAwake = false;
        bellAudioSource.loop = false;
        bellAudioSource.spatialBlend = bellSoundSpatialBlend;
        bellAudioSource.outputAudioMixerGroup = bellSoundOutput;
    }

    private void ConfigureFailAudioSource()
    {
        if (failAudioSource == null)
        {
            // Try to find a second AudioSource on the object; if none, add one
            var sources = GetComponents<AudioSource>();
            foreach (var src in sources)
            {
                if (src != bellAudioSource)
                {
                    failAudioSource = src;
                    break;
                }
            }
        }

        if (failAudioSource == null)
            failAudioSource = gameObject.AddComponent<AudioSource>();

        failAudioSource.playOnAwake = false;
        failAudioSource.loop = false;
        failAudioSource.spatialBlend = failSoundSpatialBlend;
        failAudioSource.outputAudioMixerGroup = failSoundOutput;
    }

    private IEnumerator DelayedDrop(int stepIndex)
    {
        yield return new WaitForSeconds(dropDelay);
        DropIsland(stepIndex);
    }

    private void DropIsland(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= stepIslands.Length) return;
        if (stepIslands[stepIndex] == null) return;

        if (moveCoroutines[stepIndex] != null)
            StopCoroutine(moveCoroutines[stepIndex]);

        moveCoroutines[stepIndex] = StartCoroutine(MoveIslandTo(stepIslands[stepIndex], loweredYPosition));
    }

    private void ResetPuzzle()
    {
        StopAllCoroutines();
        PlayFailSound();
        StartCoroutine(ShakeObject());

        for (int i = 0; i < stepIslands.Length; i++)
        {
            if (stepIslands[i] != null)
                moveCoroutines[i] = StartCoroutine(MoveIslandTo(stepIslands[i], originalYPositions[i]));
        }

        currentStep = 0;
        currentTapCount = 0;
        tapTimer = 0f;
        waitingForPause = false;
        pauseTimer = 0f;
        idleTimer = 0f;
        idleTimerActive = false;
    }

    [Header("Fail Shake")]
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeMagnitude = 0.08f;
    [SerializeField] private float shakeSpeed = 40f;

    private IEnumerator ShakeObject()
    {
        Vector3 origin = transform.position;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float offset = Mathf.Sin(elapsed * shakeSpeed) * shakeMagnitude;
            transform.position = origin + new Vector3(offset, 0f, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = origin;
    }

    private IEnumerator MoveIslandTo(Transform island, float targetY)
    {
        while (!Mathf.Approximately(island.position.y, targetY))
        {
            float newY = Mathf.MoveTowards(island.position.y, targetY, moveSpeed * Time.deltaTime);
            island.position = new Vector3(island.position.x, newY, island.position.z);
            yield return null;
        }
        island.position = new Vector3(island.position.x, targetY, island.position.z);
    }
}