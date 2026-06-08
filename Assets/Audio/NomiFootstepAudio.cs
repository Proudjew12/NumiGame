using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public sealed class NomiFootstepAudio : MonoBehaviour
{
    [System.Serializable]
    private sealed class SurfaceClips
    {
        [SerializeField] private Collider2D surfaceCollider;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private AudioMixerGroup output;

        public bool Matches(NomiMovment movement, Collider2D candidate)
        {
            if (surfaceCollider == null)
            {
                return false;
            }

            if (candidate == surfaceCollider)
            {
                return true;
            }

            return movement != null && movement.HasGroundColliderForAudio(surfaceCollider);
        }

        public AudioClip[] Clips => clips;
        public AudioMixerGroup Output => output;
    }

    [Header("--------- Footsteps ---------")]
    [Header("+Timing+")]
    [Space(4)]
    [InspectorName("Step Interval")]
    [SerializeField] private float stepInterval = 0.48f;
    [Space(4)]
    [InspectorName("Min Speed")]
    [SerializeField] private float minSpeed = 0.2f;
    [Space(4)]
    [InspectorName("Play First Immediately")]
    [SerializeField] private bool playFirstImmediately = true;
    [Space(4)]
    [InspectorName("Min Event Spacing")]
    [SerializeField] private float minEventSpacing = 0.1f;
    [Space(4)]
    [InspectorName("Suppress Walk After Force")]
    [SerializeField] private float suppressWalkAfterForce = 0.32f;

    [Header("+Sound+")]
    [Space(4)]
    [InspectorName("Clips")]
    [SerializeField] private AudioClip[] clips;
    [Space(4)]
    [InspectorName("Surface Overrides")]
    [SerializeField] private SurfaceClips[] surfaceOverrides;
    [Space(4)]
    [InspectorName("Volume")]
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [Space(4)]
    [InspectorName("Pitch Random")]
    [SerializeField] private Vector2 pitchRandom = new Vector2(0.96f, 1.04f);
    [Space(4)]
    [InspectorName("Spatial Blend")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.15f;
    [Space(4)]
    [InspectorName("Default Output")]
    [SerializeField] private AudioMixerGroup output;

    [Header("+Jump+")]
    [Space(4)]
    [InspectorName("Jump Clips")]
    [SerializeField] private AudioClip[] jumpClips;
    [Space(4)]
    [InspectorName("Jump Volume")]
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
    [Space(4)]
    [InspectorName("Jump Pitch Random")]
    [SerializeField] private Vector2 jumpPitchRandom = new Vector2(0.98f, 1.02f);
    [Space(4)]
    [InspectorName("Jump Output")]
    [SerializeField] private AudioMixerGroup jumpOutput;

    [Space(10)]
    [Header("--------- References ---------")]
    [Header("+Components+")]
    [Space(4)]
    [SerializeField] private NomiMovment movement;
    [Space(4)]
    [SerializeField] private AudioSource audioSource;
    [Space(4)]
    [SerializeField] private AudioSource jumpAudioSource;

    private Collider2D _currentGroundCollider;
    private float _stepTimer;
    private float _lastFootstepTime = -999f;
    private float _suppressWalkFootstepsUntil;
    private int _lastClipIndex = -1;
    private int _lastJumpClipIndex = -1;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
        ConfigureAudioSource();
        ConfigureJumpAudioSource();
        _stepTimer = playFirstImmediately ? 0f : stepInterval;
    }

    private void Update()
    {
    }

    private bool ShouldPlayFootsteps()
    {
        if (movement == null || GetCurrentClips().Length == 0)
        {
            return false;
        }

        return movement.IsPlayerControlledForAudio &&
               movement.IsGroundedForAudio &&
               movement.HorizontalSpeedForAudio >= minSpeed &&
               Mathf.Abs(movement.HorizontalInputForAudio) > 0.01f;
    }

    private bool PlayFootstep()
    {
        return PlayFootstep(false);
    }

    private bool PlayFootstep(bool useLastGroundFallback)
    {
        if (Time.time - _lastFootstepTime < minEventSpacing)
        {
            return false;
        }

        UpdateCurrentGroundCollider(useLastGroundFallback);

        var surfaceOverride = GetCurrentSurfaceOverride(useLastGroundFallback);
        var clipSet = surfaceOverride != null
            ? surfaceOverride.Clips ?? System.Array.Empty<AudioClip>()
            : clips ?? System.Array.Empty<AudioClip>();
        var clip = GetRandomClip(clipSet);
        if (clip == null || audioSource == null)
        {
            return false;
        }

        audioSource.outputAudioMixerGroup = surfaceOverride != null && surfaceOverride.Output != null
            ? surfaceOverride.Output
            : output;
        audioSource.pitch = Random.Range(pitchRandom.x, pitchRandom.y);
        audioSource.PlayOneShot(clip, volume);
        _lastFootstepTime = Time.time;
        return true;
    }

    private AudioClip[] GetCurrentClips()
    {
        var surfaceOverride = GetCurrentSurfaceOverride();
        return surfaceOverride != null
            ? surfaceOverride.Clips ?? System.Array.Empty<AudioClip>()
            : clips ?? System.Array.Empty<AudioClip>();
    }

    private AudioClip GetRandomClip(AudioClip[] clipSet)
    {
        return GetRandomClip(clipSet, ref _lastClipIndex);
    }

    private AudioClip GetRandomClip(AudioClip[] clipSet, ref int lastClipIndex)
    {
        if (clipSet == null || clipSet.Length == 0)
        {
            return null;
        }

        if (clipSet.Length == 1)
        {
            lastClipIndex = 0;
            return clipSet[0];
        }

        var clipIndex = Random.Range(0, clipSet.Length);
        if (clipIndex == lastClipIndex)
        {
            clipIndex = (clipIndex + 1) % clipSet.Length;
        }

        lastClipIndex = clipIndex;
        return clipSet[clipIndex];
    }

    private void PlayJumpSound()
    {
        var clip = GetRandomClip(jumpClips, ref _lastJumpClipIndex);
        if (clip == null)
        {
            return;
        }

        ConfigureJumpAudioSource();
        if (jumpAudioSource == null)
        {
            return;
        }

        jumpAudioSource.outputAudioMixerGroup = jumpOutput != null ? jumpOutput : output;
        jumpAudioSource.pitch = Random.Range(jumpPitchRandom.x, jumpPitchRandom.y);
        jumpAudioSource.PlayOneShot(clip, jumpVolume);
    }

    private void UpdateCurrentGroundCollider()
    {
        UpdateCurrentGroundCollider(false);
    }

    private void UpdateCurrentGroundCollider(bool useLastGroundFallback)
    {
        _currentGroundCollider = null;

        if (movement == null)
        {
            return;
        }

        _currentGroundCollider = movement.GroundColliderForAudio;
        if (_currentGroundCollider == null && useLastGroundFallback)
        {
            _currentGroundCollider = movement.LastGroundColliderForAudio;
        }
    }

    private SurfaceClips GetCurrentSurfaceOverride()
    {
        UpdateCurrentGroundCollider();
        return GetCurrentSurfaceOverrideFromCurrentGround();
    }

    private SurfaceClips GetCurrentSurfaceOverride(bool useLastGroundFallback)
    {
        UpdateCurrentGroundCollider(useLastGroundFallback);
        return GetCurrentSurfaceOverrideFromCurrentGround();
    }

    private SurfaceClips GetCurrentSurfaceOverrideFromCurrentGround()
    {

        if (surfaceOverrides == null)
        {
            return null;
        }

        for (var i = 0; i < surfaceOverrides.Length; i++)
        {
            var surfaceOverride = surfaceOverrides[i];
            if (surfaceOverride != null && surfaceOverride.Matches(movement, _currentGroundCollider))
            {
                return surfaceOverride;
            }
        }

        return null;
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = spatialBlend;
    }

    private void ConfigureJumpAudioSource()
    {
        if (jumpAudioSource == null)
        {
            jumpAudioSource = gameObject.AddComponent<AudioSource>();
        }

        jumpAudioSource.playOnAwake = false;
        jumpAudioSource.loop = false;
        jumpAudioSource.spatialBlend = spatialBlend;
    }

    private void FindReferences()
    {
        if (movement == null)
        {
            movement = GetComponent<NomiMovment>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnValidate()
    {
        stepInterval = Mathf.Max(0.01f, stepInterval);
        minSpeed = Mathf.Max(0f, minSpeed);
        minEventSpacing = Mathf.Max(0f, minEventSpacing);
        suppressWalkAfterForce = Mathf.Max(0f, suppressWalkAfterForce);
        jumpVolume = Mathf.Clamp01(jumpVolume);

        if (pitchRandom.x <= 0f)
        {
            pitchRandom.x = 0.01f;
        }

        if (pitchRandom.y < pitchRandom.x)
        {
            pitchRandom.y = pitchRandom.x;
        }

        if (jumpPitchRandom.x <= 0f)
        {
            jumpPitchRandom.x = 0.01f;
        }

        if (jumpPitchRandom.y < jumpPitchRandom.x)
        {
            jumpPitchRandom.y = jumpPitchRandom.x;
        }
    }

    public void PlayFootstepFromAnimation()
    {
        ConfigureAudioSource();

        if (Time.time < _suppressWalkFootstepsUntil)
        {
            return;
        }

        if (!ShouldPlayFootsteps())
        {
            return;
        }

        PlayFootstep();
    }

    public void PlayFootstepFromAnimationForce()
    {
        ConfigureAudioSource();

        if (movement == null || GetCurrentClips().Length == 0)
        {
            return;
        }

        if (PlayFootstep(true))
        {
            _suppressWalkFootstepsUntil = Time.time + suppressWalkAfterForce;
        }
    }

    public void PlayJumpFromInput()
    {
        PlayJumpSound();
    }

    public void footseps_animation()
    {
        PlayFootstepFromAnimation();
    }
}
