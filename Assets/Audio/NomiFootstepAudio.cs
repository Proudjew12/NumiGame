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

    [Space(10)]
    [Header("--------- References ---------")]
    [Header("+Components+")]
    [Space(4)]
    [SerializeField] private NomiMovment movement;
    [Space(4)]
    [SerializeField] private AudioSource audioSource;

    private Collider2D _currentGroundCollider;
    private float _stepTimer;
    private int _lastClipIndex = -1;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
        ConfigureAudioSource();
        _stepTimer = playFirstImmediately ? 0f : stepInterval;
    }

    private void Update()
    {
        ConfigureAudioSource();

        if (!ShouldPlayFootsteps())
        {
            _stepTimer = playFirstImmediately ? 0f : stepInterval;
            return;
        }

        _stepTimer -= Time.deltaTime;
        if (_stepTimer > 0f)
        {
            return;
        }

        PlayFootstep();
        _stepTimer = Mathf.Max(0.01f, stepInterval);
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

    private void PlayFootstep()
    {
        UpdateCurrentGroundCollider();

        var surfaceOverride = GetCurrentSurfaceOverride();
        var clipSet = surfaceOverride != null
            ? surfaceOverride.Clips ?? System.Array.Empty<AudioClip>()
            : clips ?? System.Array.Empty<AudioClip>();
        var clip = GetRandomClip(clipSet);
        if (clip == null || audioSource == null)
        {
            return;
        }

        audioSource.outputAudioMixerGroup = surfaceOverride != null && surfaceOverride.Output != null
            ? surfaceOverride.Output
            : output;
        audioSource.pitch = Random.Range(pitchRandom.x, pitchRandom.y);
        audioSource.PlayOneShot(clip, volume);
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
        if (clipSet == null || clipSet.Length == 0)
        {
            return null;
        }

        if (clipSet.Length == 1)
        {
            _lastClipIndex = 0;
            return clipSet[0];
        }

        var clipIndex = Random.Range(0, clipSet.Length);
        if (clipIndex == _lastClipIndex)
        {
            clipIndex = (clipIndex + 1) % clipSet.Length;
        }

        _lastClipIndex = clipIndex;
        return clipSet[clipIndex];
    }

    private void UpdateCurrentGroundCollider()
    {
        _currentGroundCollider = null;

        if (movement == null)
        {
            return;
        }

        _currentGroundCollider = movement.GroundColliderForAudio;
    }

    private SurfaceClips GetCurrentSurfaceOverride()
    {
        UpdateCurrentGroundCollider();

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

        if (pitchRandom.x <= 0f)
        {
            pitchRandom.x = 0.01f;
        }

        if (pitchRandom.y < pitchRandom.x)
        {
            pitchRandom.y = pitchRandom.x;
        }
    }
}
