using UnityEngine;
using UnityEngine.Audio;

namespace NumiDream.Nomi
{
    [DisallowMultipleComponent]
    public sealed class NomiFootstepAudio : MonoBehaviour
    {
        [Header("--------- Footsteps ---------")]
        [Header("+Timing+")]
        [Space(4)]
        [InspectorName("Step Interval")]
        [SerializeField] private float stepInterval = 0.55f;
        [Space(4)]
        [InspectorName("Min Speed")]
        [SerializeField] private float minimumHorizontalSpeed = 0.2f;
        [Space(4)]
        [InspectorName("Play First Immediately")]
        [SerializeField] private bool playFirstStepImmediately = true;

        [Header("+Sound+")]
        [Space(4)]
        [InspectorName("Clips")]
        [SerializeField] private AudioClip[] footstepClips = System.Array.Empty<AudioClip>();
        [Space(4)]
        [InspectorName("Volume")]
        [SerializeField] private float volume = 1f;
        [Space(4)]
        [InspectorName("Pitch Random")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f);
        [Space(4)]
        [InspectorName("Spatial Blend")]
        [SerializeField] private float spatialBlend = 0.15f;
        [Space(4)]
        [InspectorName("Output")]
        [SerializeField] private AudioMixerGroup output;

        [Space(10)]
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private NomiPlayerMovement movement;
        [Space(4)]
        [SerializeField] private AudioSource audioSource;

        private float _stepTimer;
        private int _lastClipIndex = -1;
        private bool _wasPlayingFootsteps;

        private void Reset()
        {
            FindReferences();
        }

        private void Awake()
        {
            FindReferences();
            ConfigureAudioSource();
            _stepTimer = playFirstStepImmediately ? 0f : stepInterval;
        }

        private void Update()
        {
            if (!ShouldPlayFootsteps())
            {
                _wasPlayingFootsteps = false;
                _stepTimer = Mathf.Min(_stepTimer, stepInterval);
                return;
            }

            if (!_wasPlayingFootsteps && playFirstStepImmediately)
            {
                _stepTimer = Mathf.Min(_stepTimer, stepInterval * 0.5f);
            }

            _wasPlayingFootsteps = true;
            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f)
            {
                return;
            }

            PlayFootstep();
            _stepTimer = stepInterval;
        }

        private bool ShouldPlayFootsteps()
        {
            if (movement == null || audioSource == null || footstepClips == null || footstepClips.Length == 0)
            {
                return false;
            }

            return movement.IsGrounded &&
                   movement.IsMovementAudioAllowed &&
                   Mathf.Abs(movement.MoveInput) > 0.01f &&
                   movement.HorizontalSpeed >= minimumHorizontalSpeed;
        }

        private void PlayFootstep()
        {
            var clip = GetFootstepClip();
            if (clip == null)
            {
                return;
            }

            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, volume);
        }

        private AudioClip GetFootstepClip()
        {
            if (footstepClips.Length == 1)
            {
                _lastClipIndex = 0;
                return footstepClips[0];
            }

            var index = Random.Range(0, footstepClips.Length);
            if (index == _lastClipIndex)
            {
                index = (index + 1) % footstepClips.Length;
            }

            _lastClipIndex = index;
            return footstepClips[index];
        }

        private void FindReferences()
        {
            if (movement == null)
            {
                movement = GetComponent<NomiPlayerMovement>();
            }

            if (movement == null)
            {
                movement = GetComponentInParent<NomiPlayerMovement>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = Mathf.Clamp01(spatialBlend);
            audioSource.outputAudioMixerGroup = output;
        }

        private void OnValidate()
        {
            stepInterval = Mathf.Max(0.05f, stepInterval);
            minimumHorizontalSpeed = Mathf.Max(0f, minimumHorizontalSpeed);
            volume = Mathf.Clamp01(volume);
            spatialBlend = Mathf.Clamp01(spatialBlend);

            if (pitchRange.x <= 0f)
            {
                pitchRange.x = 0.01f;
            }

            if (pitchRange.y < pitchRange.x)
            {
                pitchRange.y = pitchRange.x;
            }
        }
    }
}
