using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Audio;

/// <summary>
/// A tap-sequence puzzle. Each stone has its own required tap count.
/// On failure you can choose to reset ALL stones or keep already-dropped
/// stones in place (only the shake + fail sound plays).
/// </summary>
public class AnimatedInteractable : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector entry — one row per stone
    // -------------------------------------------------------------------------
    [System.Serializable]
    public class StoneEntry
    {
        [Tooltip("The stone / island Transform to drop")]
        public Transform stone;

        [Tooltip("How many taps are required to drop this stone")]
        [Min(1)] public int requiredTaps = 1;
    }

    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------
    [Header("Input")]
    [SerializeField] private InputActionReference activateAction;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera puzzleCamera;

    [Header("Stones (assign in order)")]
    [SerializeField] private StoneEntry[] stones = new StoneEntry[3];

    [Header("Reference Y (the lowered / dropped Y position)")]
    [SerializeField] private float loweredYPosition = -14f;

    [Header("Fail Behaviour")]
    [Tooltip(
        "If TRUE  → all stones fly back to their starting Y on failure (classic full reset).\n" +
        "If FALSE → already-dropped stones stay down; only the shake + fail sound trigger.")]
    [SerializeField] private bool resetAllStonesOnFail = true;

    [Header("Timing")]
    [Tooltip("Max time between taps within the same group")]
    [SerializeField] private float tapWindowDuration = 1.2f;
    [Tooltip("Mandatory pause after a correct group before the next group is accepted")]
    [SerializeField] private float groupPauseDuration = 0.8f;
    [Tooltip("How fast stones move up / down")]
    [SerializeField] private float moveSpeed = 3f;
    [Tooltip("Delay between the last correct tap and the stone actually dropping")]
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

    [Header("Fail Shake")]
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeMagnitude = 0.08f;
    [SerializeField] private float shakeSpeed = 40f;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------
    private float[]     originalYPositions;
    private bool[]      stoneDropped;          // tracks which stones are already down
    private Coroutine[] moveCoroutines;

    private int   currentStep     = 0;
    private int   currentTapCount = 0;
    private float tapTimer        = 0f;

    private bool  waitingForPause = false;
    private float pauseTimer      = 0f;

    private bool  puzzleSolved    = false;

    private float idleTimer       = 0f;
    private bool  idleTimerActive = false;

    private SpriteOutline spriteOutline;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------
    void Start()
    {
        ConfigureBellAudioSource();
        ConfigureFailAudioSource();
        spriteOutline = GetComponent<SpriteOutline>();

        // Cache original Y positions and allocate helpers
        originalYPositions = new float[stones.Length];
        stoneDropped       = new bool[stones.Length];
        moveCoroutines     = new Coroutine[stones.Length];

        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i]?.stone != null)
                originalYPositions[i] = stones[i].stone.position.y;
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

        // Camera priority follows outline visibility
        if (puzzleCamera != null && spriteOutline != null)
            puzzleCamera.Priority = spriteOutline.currentOutlineSize > 0f ? 5 : 0;

        // ── Mandatory pause between groups ──────────────────────────────────
        if (waitingForPause)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= groupPauseDuration)
            {
                waitingForPause = false;
                pauseTimer      = 0f;
                currentStep++;
                currentTapCount = 0;
                tapTimer        = 0f;
if (currentStep >= stones.Length)
{
    puzzleSolved    = true;
    idleTimerActive = false;
    if (puzzleCamera != null) puzzleCamera.Priority = 0;
    if (spriteOutline != null) spriteOutline.LockOutlineOff(); // lock outline off
    Debug.Log("Puzzle SOLVED!");
    return;
}

                idleTimer       = 0f;
                idleTimerActive = true;
            }
            return; // no input accepted during pause
        }

        // ── Idle reset (player does nothing between groups) ──────────────────
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

        // ── Tap-window expiry within a group ────────────────────────────────
        if (currentTapCount > 0)
        {
            idleTimer  = 0f;
            tapTimer  += Time.deltaTime;
            if (tapTimer > tapWindowDuration)
            {
                Debug.Log($"Tap timeout at step {currentStep}. Resetting.");
                ResetPuzzle();
            }
        }
    }

    // -------------------------------------------------------------------------
    //  Input
    // -------------------------------------------------------------------------
    private void OnActivatePressed(InputAction.CallbackContext _)
    {
        if (puzzleSolved) return;
        if (spriteOutline == null || spriteOutline.currentOutlineSize <= 0f) return;

        // Camera priority follows outline
        if (puzzleCamera != null)
            puzzleCamera.Priority = spriteOutline.currentOutlineSize > 0f ? 5 : 0;

        // Tapping during mandatory pause = fail
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

        int required = stones[currentStep].requiredTaps;
        Debug.Log($"Step {currentStep}: tap {currentTapCount}/{required}");

        if (currentTapCount > required)
        {
            Debug.Log($"Too many taps at step {currentStep}. Resetting.");
            ResetPuzzle();
        }
        else if (currentTapCount == required)
        {
            // Lock input — mandatory pause starts immediately
            waitingForPause = true;
            pauseTimer      = 0f;
            currentTapCount = 0;
            tapTimer        = 0f;

            StartCoroutine(DelayedDrop(currentStep));
        }
    }

    // -------------------------------------------------------------------------
    //  Stone movement
    // -------------------------------------------------------------------------
    private IEnumerator DelayedDrop(int stepIndex)
    {
        yield return new WaitForSeconds(dropDelay);
        DropStone(stepIndex);
    }

    private void DropStone(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= stones.Length) return;
        if (stones[stepIndex]?.stone == null) return;

        stoneDropped[stepIndex] = true;

        if (moveCoroutines[stepIndex] != null)
            StopCoroutine(moveCoroutines[stepIndex]);

        moveCoroutines[stepIndex] = StartCoroutine(
            MoveStoneToY(stones[stepIndex].stone, loweredYPosition));
    }

    private IEnumerator MoveStoneToY(Transform stone, float targetY)
    {
        while (!Mathf.Approximately(stone.position.y, targetY))
        {
            float newY = Mathf.MoveTowards(stone.position.y, targetY, moveSpeed * Time.deltaTime);
            stone.position = new Vector3(stone.position.x, newY, stone.position.z);
            yield return null;
        }
        stone.position = new Vector3(stone.position.x, targetY, stone.position.z);
    }

    // -------------------------------------------------------------------------
    //  Fail / reset
    // -------------------------------------------------------------------------
    private void ResetPuzzle()
    {
        StopAllCoroutines();
        PlayFailSound();
        StartCoroutine(ShakeObject());

        if (resetAllStonesOnFail)
        {
            // ── Full reset: every stone flies back up ────────────────────────
            for (int i = 0; i < stones.Length; i++)
            {
                stoneDropped[i] = false;
                if (stones[i]?.stone != null)
                    moveCoroutines[i] = StartCoroutine(
                        MoveStoneToY(stones[i].stone, originalYPositions[i]));
            }

            currentStep = 0;
        }
        else
        {
            // ── Partial reset: dropped stones stay, sequence restarts from
            //    the current step (re-tap the same stone again) ───────────────
            // Stones that are already down stay down — no movement needed.
            // We only reset the tap counter so the player tries again.
        }

        currentTapCount = 0;
        tapTimer        = 0f;
        waitingForPause = false;
        pauseTimer      = 0f;
        idleTimer       = 0f;
        idleTimerActive = false;
    }

    private IEnumerator ShakeObject()
    {
        Vector3 origin  = transform.position;
        float   elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float offset = Mathf.Sin(elapsed * shakeSpeed) * shakeMagnitude;
            transform.position = origin + new Vector3(offset, 0f, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = origin;
    }

    // -------------------------------------------------------------------------
    //  Audio
    // -------------------------------------------------------------------------
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
        bellAudioSource.loop        = false;
        bellAudioSource.spatialBlend        = bellSoundSpatialBlend;
        bellAudioSource.outputAudioMixerGroup = bellSoundOutput;
    }

    private void ConfigureFailAudioSource()
    {
        if (failAudioSource == null)
        {
            foreach (var src in GetComponents<AudioSource>())
            {
                if (src != bellAudioSource) { failAudioSource = src; break; }
            }
        }
        if (failAudioSource == null)
            failAudioSource = gameObject.AddComponent<AudioSource>();

        failAudioSource.playOnAwake = false;
        failAudioSource.loop        = false;
        failAudioSource.spatialBlend        = failSoundSpatialBlend;
        failAudioSource.outputAudioMixerGroup = failSoundOutput;
    }
}